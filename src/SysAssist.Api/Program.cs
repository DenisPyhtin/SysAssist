using System.Security.Claims;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SysAssist.Api;
using SysAssist.Application;
using SysAssist.Application.Api;
using SysAssist.Application.Auth;
using SysAssist.Application.Common;
using SysAssist.Application.Security;
using SysAssist.Contracts.Api;
using SysAssist.Contracts.Auth;
using SysAssist.Contracts.Health;
using SysAssist.Infrastructure;
using SysAssist.Infrastructure.Auth;
using SysAssist.Infrastructure.Data;

const string AuthRateLimitPolicy = "auth";
const string WebhookRateLimitPolicy = "webhook";
const string WriteRateLimitPolicy = "write";

DotEnv.LoadFromNearestFile(".env", ".env.local");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long?>("Security:MaxRequestBodyBytes") ?? 1_048_576;
});

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();
});

var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
ValidateSecurityConfiguration(builder.Configuration, builder.Environment);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireOperatorOrHigher", policy => policy.RequireRole("Admin", "Operator", "Engineer", "SeniorAdmin"));
    options.AddPolicy("RequireEngineerOrHigher", policy => policy.RequireRole("Admin", "Engineer", "SeniorAdmin"));
    options.AddPolicy("RequireSeniorAdmin", policy => policy.RequireRole("Admin", "SeniorAdmin"));
    options.AddPolicy("RequireAuditorOrAdmin", policy => policy.RequireRole("Admin", "Auditor"));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SysAssist API",
        Version = "v1",
        Description = "Enterprise event coordination API."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste a JWT from POST /api/auth/login. Example: Bearer eyJhbGciOi...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimitPolicy, context => FixedWindowPolicy(
        ClientPartitionKey(context),
        builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10),
        TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Auth:WindowMinutes", 1))));
    options.AddPolicy(WebhookRateLimitPolicy, context => FixedWindowPolicy(
        ClientPartitionKey(context),
        builder.Configuration.GetValue("RateLimiting:Webhooks:PermitLimit", 240),
        TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Webhooks:WindowMinutes", 1))));
    options.AddPolicy(WriteRateLimitPolicy, context => FixedWindowPolicy(
        UserOrClientPartitionKey(context),
        builder.Configuration.GetValue("RateLimiting:Writes:PermitLimit", 90),
        TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Writes:WindowMinutes", 1))));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if ((app.Environment.IsDevelopment() || builder.Configuration.GetValue("SysAssist:ApplyMigrationsOnStartup", false))
    && !builder.Configuration.GetValue("SysAssist:UseDemoData", false))
{
    await app.Services.InitializeSysAssistDatabaseAsync();
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value)
        ? value.ToString()
        : Guid.CreateVersion7().ToString();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString();
        var isBadRequest = exception is BadHttpRequestException;
        try
        {
            var apiService = context.RequestServices.GetRequiredService<ISysAssistApiService>();
            await apiService.WriteSystemLogAsync(isBadRequest ? "Warning" : "Error", "Api", exception.Message, correlationId, $$"""{"type":"{{exception.GetType().Name}}"}""", context.RequestAborted);
        }
        catch
        {
            // Last-resort exception handler must not fail while writing the error response.
        }

        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.StatusCode = isBadRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            error = isBadRequest ? "Invalid request body." : "Unhandled server error.",
            correlationId
        });
    }
});

app.UseSerilogRequestLogging();
UseSecurityHeaders(app);
app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (!RequiresValidLicense(context.Request.Path))
    {
        await next();
        return;
    }

    var license = context.RequestServices.GetRequiredService<ILicenseService>().GetStatus();
    if (license.IsValid)
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
    await context.Response.WriteAsJsonAsync(new
    {
        error = "SysAssist license is invalid.",
        license.Status,
        license.Message,
        license.Fingerprint
    });
});

app.MapGet("/health", (IHostEnvironment environment, IClock clock) =>
    new HealthCheckResponse("Healthy", "SysAssist.Api", environment.EnvironmentName, clock.UtcNow))
    .WithName("GetHealth")
    .WithOpenApi();
app.MapGet("/health/live", (IHostEnvironment environment, IClock clock) =>
    Results.Ok(new HealthCheckResponse("Healthy", "SysAssist.Api", environment.EnvironmentName, clock.UtcNow)))
    .WithName("GetLiveness")
    .WithOpenApi();
app.MapGet("/health/ready", async (HttpContext context, IConfiguration configuration, IHostEnvironment environment, ILicenseService licenseService, IClock clock, CancellationToken cancellationToken) =>
    await BuildReadinessResponseAsync(context, configuration, environment, licenseService, clock, cancellationToken))
    .WithName("GetReadiness")
    .WithOpenApi();

var api = app.MapGroup("/api").WithOpenApi();

api.MapGet("/license", (ILicenseService licenseService) => Results.Ok(licenseService.GetStatus()))
    .RequireAuthorization()
    .WithTags("License");
api.MapGet("/production-readiness", async (HttpContext context, IConfiguration configuration, IHostEnvironment environment, ISysAssistApiService service, ILicenseService licenseService, IClock clock, CancellationToken cancellationToken) =>
    Results.Ok(await BuildProductionReadinessAsync(context, configuration, environment, service, licenseService, clock, cancellationToken)))
    .RequireAuthorization("RequireSeniorAdmin")
    .WithTags("Production");

var auth = api.MapGroup("/auth").WithTags("Auth");
auth.MapPost("/login", async (LoginRequest request, HttpContext context, IAuthService authService, ISysAssistApiService apiService, CancellationToken cancellationToken) =>
    {
        if (ValidateLoginRequest(request) is { } validation)
        {
            await WriteLoginSecurityLogAsync(apiService, request.Login, false, "ValidationFailed", context, cancellationToken);
            return Results.BadRequest(validation);
        }

        var response = await authService.LoginAsync(request, cancellationToken);
        await WriteLoginSecurityLogAsync(apiService, request.Login, response is not null, response is null ? "InvalidCredentials" : "Success", context, cancellationToken);
        return response is null ? Results.Unauthorized() : Results.Ok(response);
    })
    .AllowAnonymous()
    .RequireRateLimiting(AuthRateLimitPolicy);
auth.MapGet("/me", async (ClaimsPrincipal user, IAuthService authService, CancellationToken cancellationToken) =>
    await authService.GetCurrentUserAsync(user, cancellationToken) is { } response
        ? Results.Ok(response)
        : Results.Unauthorized())
    .RequireAuthorization();

api.MapGet("/dashboard", async (ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetDashboardAsync(cancellationToken)))
    .RequireAuthorization("RequireOperatorOrHigher")
    .WithTags("Dashboard");
api.MapGet("/dashboard/summary", async (ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetDashboardAsync(cancellationToken)))
    .RequireAuthorization("RequireOperatorOrHigher")
    .WithTags("Dashboard");

var events = api.MapGroup("/events").RequireAuthorization("RequireOperatorOrHigher").WithTags("Events");
events.MapGet("/", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListEventsAsync(cancellationToken)));
events.MapGet("/{id:guid}", async (Guid id, ISysAssistApiService service, CancellationToken cancellationToken) =>
    await service.GetEventAsync(id, cancellationToken) is { } item ? Results.Ok(item) : Results.NotFound());
events.MapPost("/{id:guid}/reprocess", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ReprocessEventAsync(id, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireEngineerOrHigher");

var approvals = api.MapGroup("/approvals").RequireAuthorization("RequireSeniorAdmin").WithTags("Approvals");
approvals.MapGet("/", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListApprovalsAsync(cancellationToken)));
approvals.MapGet("/{id:guid}", async (Guid id, ISysAssistApiService service, CancellationToken cancellationToken) =>
    await service.GetApprovalAsync(id, cancellationToken) is { } item ? Results.Ok(item) : Results.NotFound());
approvals.MapPost("/{id:guid}/approve", async (Guid id, DecisionRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.DecideApprovalAsync(id, approve: true, Actor(user), UserId(user), request.Comment, CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);
approvals.MapPost("/{id:guid}/reject", async (Guid id, DecisionRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.DecideApprovalAsync(id, approve: false, Actor(user), UserId(user), request.Comment, CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);

var modules = api.MapGroup("/modules").RequireAuthorization("RequireEngineerOrHigher").WithTags("Modules");
modules.MapGet("/", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListModulesAsync(cancellationToken)));
modules.MapGet("/{id:guid}", async (Guid id, ISysAssistApiService service, CancellationToken cancellationToken) =>
    await service.GetModuleAsync(id, cancellationToken) is { } item ? Results.Ok(item) : Results.NotFound());
modules.MapPost("/{id:guid}/enable", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.SetModuleEnabledAsync(id, enabled: true, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireAdmin");
modules.MapPost("/{id:guid}/disable", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.SetModuleEnabledAsync(id, enabled: false, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireAdmin");
modules.MapGet("/{id:guid}/settings", async (Guid id, ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.GetModuleSettingsAsync(id, cancellationToken)));
modules.MapPut("/{id:guid}/settings", async (Guid id, UpdateModuleSettingsRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    ValidateModuleSettingsRequest(request) is { } validation
        ? Results.BadRequest(validation)
        : Results.Ok(await service.UpdateModuleSettingsAsync(id, request, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);
modules.MapPut("/{id:guid}/settings/{key}/secret", async (Guid id, string key, ReplaceSecretRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    ValidateReplaceSecretRequest(key, request) is { } validation
        ? Results.BadRequest(validation)
        : Results.Ok(await service.ReplaceModuleSecretAsync(id, key, request, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);
modules.MapPost("/{id:guid}/test-connection", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.TestModuleConnectionAsync(id, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);
modules.MapPost("/{id:guid}/health", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.CheckModuleHealthAsync(id, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);
modules.MapPost("/{id:guid}/fetch-events", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.FetchModuleEventsAsync(id, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireOperatorOrHigher")
    .RequireRateLimiting(WriteRateLimitPolicy);
modules.MapGet("/{id:guid}/actions", async (Guid id, ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.GetModuleActionsAsync(id, cancellationToken)));
modules.MapPut("/{id:guid}/actions/{actionId:guid}/enable", async (Guid id, Guid actionId, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.SetModuleActionEnabledAsync(id, actionId, enabled: true, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireAdmin");
modules.MapPut("/{id:guid}/actions/{actionId:guid}/disable", async (Guid id, Guid actionId, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.SetModuleActionEnabledAsync(id, actionId, enabled: false, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireAdmin");
modules.MapPost("/{id:guid}/reset-demo-mode", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ResetModuleDemoModeAsync(id, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireAdmin")
    .RequireRateLimiting(WriteRateLimitPolicy);

var customModules = api.MapGroup("/custom-modules").RequireAuthorization("RequireEngineerOrHigher").WithTags("Custom Modules");
customModules.MapGet("/", async (IWebHostEnvironment environment, CancellationToken cancellationToken) =>
    Results.Ok(await ReadCustomModulesAsync(environment, cancellationToken)));
customModules.MapPost("/", async (CustomModuleManifestDto manifest, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    var validation = ValidateCustomModule(manifest);
    if (validation is not null)
    {
        return Results.BadRequest(new OperationResultDto(false, validation, null));
    }

    var items = await ReadCustomModulesAsync(environment, cancellationToken);
    items.RemoveAll(item => item.Key.Equals(manifest.Key, StringComparison.OrdinalIgnoreCase));
    items.Insert(0, manifest with
    {
        Enabled = manifest.Enabled ?? true,
        Permissions = manifest.Permissions ?? [],
        Tags = manifest.Tags ?? [],
        Settings = manifest.Settings ?? [],
        Actions = manifest.Actions ?? []
    });
    await WriteCustomModulesAsync(environment, items, cancellationToken);
    return Results.Ok(new OperationResultDto(true, "Custom module manifest uploaded.", null));
})
    .RequireRateLimiting(WriteRateLimitPolicy);
customModules.MapDelete("/{key}", async (string key, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    var items = await ReadCustomModulesAsync(environment, cancellationToken);
    var removed = items.RemoveAll(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    await WriteCustomModulesAsync(environment, items, cancellationToken);
    return Results.Ok(new OperationResultDto(removed > 0, removed > 0 ? "Custom module removed." : "Custom module not found.", null));
})
    .RequireRateLimiting(WriteRateLimitPolicy);
customModules.MapPost("/{key}/enable", async (string key, IWebHostEnvironment environment, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
{
    var items = await ReadCustomModulesAsync(environment, cancellationToken);
    var index = items.FindIndex(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (index < 0)
    {
        return Results.NotFound(new OperationResultDto(false, "Custom module not found.", CorrelationId(context)));
    }

    items[index] = items[index] with { Enabled = true };
    await WriteCustomModulesAsync(environment, items, cancellationToken);
    await service.WriteSystemLogAsync("Information", "CustomModules", $"Custom module {items[index].Name} enabled.", CorrelationId(context), $$"""{"module":"{{items[index].Key}}","operation":"enable"}""", cancellationToken);
    return Results.Ok(new OperationResultDto(true, "Custom module enabled.", CorrelationId(context)));
})
    .RequireRateLimiting(WriteRateLimitPolicy);
customModules.MapPost("/{key}/disable", async (string key, IWebHostEnvironment environment, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
{
    var items = await ReadCustomModulesAsync(environment, cancellationToken);
    var index = items.FindIndex(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (index < 0)
    {
        return Results.NotFound(new OperationResultDto(false, "Custom module not found.", CorrelationId(context)));
    }

    items[index] = items[index] with { Enabled = false };
    await WriteCustomModulesAsync(environment, items, cancellationToken);
    await service.WriteSystemLogAsync("Information", "CustomModules", $"Custom module {items[index].Name} disabled.", CorrelationId(context), $$"""{"module":"{{items[index].Key}}","operation":"disable"}""", cancellationToken);
    return Results.Ok(new OperationResultDto(true, "Custom module disabled.", CorrelationId(context)));
})
    .RequireRateLimiting(WriteRateLimitPolicy);
customModules.MapPost("/{key}/test-connection", async (string key, IWebHostEnvironment environment, IHttpClientFactory httpClientFactory, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
{
    var module = (await ReadCustomModulesAsync(environment, cancellationToken)).SingleOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (module is null)
    {
        return Results.NotFound(new OperationResultDto(false, "Custom module not found.", CorrelationId(context)));
    }
    if (module.Enabled == false)
    {
        return Results.Ok(new OperationResultDto(false, "Custom module is disabled.", CorrelationId(context)));
    }

    var result = await CallCustomModuleHealthAsync(module, httpClientFactory.CreateClient("SysAssistIntegrations"), cancellationToken);
    await service.WriteSystemLogAsync(result.Success ? "Information" : "Warning", "CustomModules", result.Message, CorrelationId(context), $$"""{"module":"{{module.Key}}","operation":"health"}""", cancellationToken);
    return Results.Ok(result with { CorrelationId = CorrelationId(context) });
})
    .RequireRateLimiting(WriteRateLimitPolicy);
customModules.MapPost("/{key}/fetch-events", async (string key, IWebHostEnvironment environment, IHttpClientFactory httpClientFactory, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
{
    var module = (await ReadCustomModulesAsync(environment, cancellationToken)).SingleOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (module is null)
    {
        return Results.NotFound(new CustomModuleEventsResultDto(false, "Custom module not found.", []));
    }
    if (module.Enabled == false)
    {
        return Results.Ok(new CustomModuleEventsResultDto(false, "Custom module is disabled.", []));
    }

    var result = await CallCustomModuleEventsAsync(module, httpClientFactory.CreateClient("SysAssistIntegrations"), cancellationToken);
    await service.WriteSystemLogAsync(result.Success ? "Information" : "Warning", "CustomModules", result.Message, CorrelationId(context), $$"""{"module":"{{module.Key}}","operation":"fetch-events","events":{{result.Events.Count}}}""", cancellationToken);
    return Results.Ok(result);
})
    .RequireRateLimiting(WriteRateLimitPolicy);
customModules.MapPost("/{key}/actions/{actionKey}/execute", async (string key, string actionKey, ExecuteActionRequest request, IWebHostEnvironment environment, IHttpClientFactory httpClientFactory, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
{
    if (ValidateExecuteActionRequest(request) is { } validation)
    {
        return Results.BadRequest(validation);
    }

    var module = (await ReadCustomModulesAsync(environment, cancellationToken)).SingleOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (module is null)
    {
        return Results.NotFound(new OperationResultDto(false, "Custom module not found.", CorrelationId(context)));
    }
    if (module.Enabled == false)
    {
        return Results.Ok(new OperationResultDto(false, "Custom module is disabled.", CorrelationId(context)));
    }

    var action = module.Actions?.SingleOrDefault(item => item.Key.Equals(actionKey, StringComparison.OrdinalIgnoreCase));
    if (action is null)
    {
        return Results.NotFound(new OperationResultDto(false, "Custom module action not found.", CorrelationId(context)));
    }

    if (action.RequiresApproval == true)
    {
        return Results.Ok(new OperationResultDto(false, "Custom module action requires approval before execution.", CorrelationId(context)));
    }

    var result = await CallCustomModuleActionAsync(module, actionKey, request, httpClientFactory.CreateClient("SysAssistIntegrations"), cancellationToken);
    await service.WriteSystemLogAsync(result.Success ? "Information" : "Warning", "CustomModules", result.Message, CorrelationId(context), $$"""{"module":"{{module.Key}}","operation":"action","action":"{{actionKey}}"}""", cancellationToken);
    return Results.Ok(result with { CorrelationId = CorrelationId(context) });
})
    .RequireRateLimiting(WriteRateLimitPolicy);

var actions = api.MapGroup("/actions").RequireAuthorization("RequireEngineerOrHigher").WithTags("Actions");
actions.MapGet("/", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListActionsAsync(cancellationToken)));
actions.MapGet("/{id:guid}", async (Guid id, ISysAssistApiService service, CancellationToken cancellationToken) =>
    await service.GetActionAsync(id, cancellationToken) is { } item ? Results.Ok(item) : Results.NotFound());
actions.MapPost("/{id:guid}/execute", async (Guid id, ExecuteActionRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
{
    if (ValidateExecuteActionRequest(request) is { } validation)
    {
        return Results.BadRequest(validation);
    }

    var action = await service.GetActionAsync(id, cancellationToken);
    if (action is null)
    {
        return Results.NotFound();
    }

    if (action.RiskLevel is "High" or "Critical" && !IsInAnyRole(user, "Admin", "SeniorAdmin"))
    {
        return Results.Forbid();
    }

    return Results.Ok(await service.ExecuteActionAsync(id, request, Actor(user), CorrelationId(context), cancellationToken));
})
    .RequireRateLimiting(WriteRateLimitPolicy);

api.MapGet("/audit", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListAuditAsync(cancellationToken)))
    .RequireAuthorization("RequireAuditorOrAdmin")
    .WithTags("Audit");
api.MapGet("/logs", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListLogsAsync(cancellationToken)))
    .RequireAuthorization("RequireAuditorOrAdmin")
    .WithTags("Logs");
api.MapGet("/notifications", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListNotificationsAsync(cancellationToken)))
    .RequireAuthorization("RequireOperatorOrHigher")
    .WithTags("Notifications");
api.MapPost("/notifications/{id:guid}/mark-read", async (Guid id, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.MarkNotificationReadAsync(id, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireAuthorization("RequireOperatorOrHigher")
    .WithTags("Notifications");

var users = api.MapGroup("/users").RequireAuthorization("RequireAdmin").WithTags("Users");
users.MapGet("/", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListUsersAsync(cancellationToken)));
users.MapPost("/", async (CreateUserRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    ValidateCreateUserRequest(request) is { } validation
        ? Results.BadRequest(validation)
        : Results.Ok(await service.CreateUserAsync(request, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);
users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    ValidateUpdateUserRequest(request) is { } validation
        ? Results.BadRequest(validation)
        : await service.UpdateUserAsync(id, request, Actor(user), CorrelationId(context), cancellationToken) is { } item ? Results.Ok(item) : Results.NotFound())
    .RequireRateLimiting(WriteRateLimitPolicy);
users.MapPut("/{id:guid}/roles", async (Guid id, UpdateUserRolesRequest request, ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    ValidateUserRolesRequest(request) is { } validation
        ? Results.BadRequest(validation)
        : Results.Ok(await service.UpdateUserRolesAsync(id, request, Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);

api.MapGet("/roles", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.ListRolesAsync(cancellationToken)))
    .RequireAuthorization("RequireAdmin")
    .WithTags("Roles");

var diagnostics = api.MapGroup("/diagnostics").RequireAuthorization("RequireEngineerOrHigher").WithTags("Diagnostics");
diagnostics.MapGet("/", async (ISysAssistApiService service, CancellationToken cancellationToken) => Results.Ok(await service.GetDiagnosticsAsync(cancellationToken)));
diagnostics.MapPost("/run", async (ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.RunDiagnosticsAsync(Actor(user), CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WriteRateLimitPolicy);

api.MapGet("/support-bundle", async (ClaimsPrincipal user, HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    {
        var bundle = await service.GetSupportBundleFileAsync(Actor(user), CorrelationId(context), cancellationToken);
        return Results.File(Encoding.UTF8.GetBytes(bundle.ContentJson), "application/json", bundle.FileName);
    })
    .RequireAuthorization("RequireAuditorOrAdmin")
    .WithTags("Support Bundle");

var webhooks = api.MapGroup("/webhooks").AllowAnonymous().WithTags("Webhooks");
webhooks.MapPost("/zabbix", async (HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ProcessWebhookAsync("zabbix", await ReadRawBodyAsync(context), context.Request.Headers["X-SysAssist-Signature"], "webhook:zabbix", CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WebhookRateLimitPolicy);
webhooks.MapPost("/grafana", async (HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ProcessWebhookAsync("grafana", await ReadRawBodyAsync(context), context.Request.Headers["X-SysAssist-Signature"], "webhook:grafana", CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WebhookRateLimitPolicy);
webhooks.MapPost("/alertmanager", async (HttpContext context, ISysAssistApiService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ProcessWebhookAsync("prometheus-alertmanager", await ReadRawBodyAsync(context), context.Request.Headers["X-SysAssist-Signature"], "webhook:alertmanager", CorrelationId(context), cancellationToken)))
    .RequireRateLimiting(WebhookRateLimitPolicy);

app.Run();

static string? CorrelationId(HttpContext context) => context.Items["CorrelationId"]?.ToString();
static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name ?? "unknown";
static async Task WriteLoginSecurityLogAsync(
    ISysAssistApiService apiService,
    string? login,
    bool success,
    string reason,
    HttpContext context,
    CancellationToken cancellationToken)
{
    var details = JsonSerializer.Serialize(new
    {
        login = SafeLogValue(login, 80),
        reason,
        client = SafeLogValue(ClientPartitionKey(context), 120)
    });

    await apiService.WriteSystemLogAsync(
        success ? "Information" : "Warning",
        "Auth",
        success ? "Login succeeded." : "Login failed.",
        CorrelationId(context),
        details,
        cancellationToken);
}

static string SafeLogValue(string? value, int maxLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "<blank>";
    }

    return value.Length <= maxLength ? value : value[..maxLength];
}
static Guid? UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
static bool IsInAnyRole(ClaimsPrincipal user, params string[] roles) => roles.Any(user.IsInRole);
static RateLimitPartition<string> FixedWindowPolicy(string partitionKey, int permitLimit, TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        PermitLimit = Math.Max(1, permitLimit),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        Window = window <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : window
    });
static string ClientPartitionKey(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "unknown-client";
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
}
static string UserOrClientPartitionKey(HttpContext context) =>
    context.User.Identity?.IsAuthenticated == true
        ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity.Name ?? ClientPartitionKey(context)
        : ClientPartitionKey(context);
static bool RequiresValidLicense(PathString path) =>
    path.StartsWithSegments("/api")
    && !path.StartsWithSegments("/api/auth")
    && !path.StartsWithSegments("/api/license")
    && !path.StartsWithSegments("/api/production-readiness");

static object ValidationError(params string[] errors) => new { error = "Validation failed.", errors };
static object? ValidateLoginRequest(LoginRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Login) || request.Login.Length > 80)
    {
        return ValidationError("Login is required and must be at most 80 characters.");
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 512)
    {
        return ValidationError("Password is required and must be at most 512 characters.");
    }

    return null;
}
static object? ValidateModuleSettingsRequest(UpdateModuleSettingsRequest request)
{
    if (request.Settings is null || request.Settings.Count > 128)
    {
        return ValidationError("Settings collection is required and must contain at most 128 items.");
    }

    if (request.Settings.Any(setting => !IsSafeSettingKey(setting.Key)))
    {
        return ValidationError("Every setting key must be 1-120 characters and use letters, numbers, dash, underscore, dot, or colon.");
    }

    if (request.Settings.Any(setting => setting.Value?.Length > 4096))
    {
        return ValidationError("Setting values must be at most 4096 characters.");
    }

    return null;
}
static object? ValidateReplaceSecretRequest(string key, ReplaceSecretRequest request)
{
    if (!IsSafeSettingKey(key))
    {
        return ValidationError("Secret key must be 1-120 characters and use letters, numbers, dash, underscore, dot, or colon.");
    }

    if (string.IsNullOrEmpty(request.Value) || request.Value.Length > 4096)
    {
        return ValidationError("Secret value is required and must be at most 4096 characters.");
    }

    return null;
}
static object? ValidateExecuteActionRequest(ExecuteActionRequest request)
{
    if (request.Target?.Length > 512)
    {
        return ValidationError("Action target must be at most 512 characters.");
    }

    if (request.ParametersJson?.Length > 8192)
    {
        return ValidationError("Action parameters JSON must be at most 8192 characters.");
    }

    if (!string.IsNullOrWhiteSpace(request.ParametersJson))
    {
        try
        {
            using var document = JsonDocument.Parse(request.ParametersJson);
            if (document.RootElement.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array)
            {
                return ValidationError("Action parameters JSON must be an object or array.");
            }
        }
        catch (JsonException)
        {
            return ValidationError("Action parameters must be valid JSON.");
        }
    }

    return null;
}
static object? ValidateCreateUserRequest(CreateUserRequest request)
{
    if (!IsSafeLogin(request.Login))
    {
        return ValidationError("Login is required, must be 1-80 characters, and may contain letters, numbers, dash, underscore, dot, or @.");
    }

    if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 160)
    {
        return ValidationError("Display name is required and must be at most 160 characters.");
    }

    if (!IsReasonableEmail(request.Email))
    {
        return ValidationError("Email is required and must be at most 240 characters.");
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12 || request.Password.Length > 256)
    {
        return ValidationError("Password must be 12-256 characters.");
    }

    return null;
}
static object? ValidateUpdateUserRequest(UpdateUserRequest request)
{
    if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 160)
    {
        return ValidationError("Display name is required and must be at most 160 characters.");
    }

    if (!IsReasonableEmail(request.Email))
    {
        return ValidationError("Email is required and must be at most 240 characters.");
    }

    if (!string.IsNullOrEmpty(request.Password) && (request.Password.Length < 12 || request.Password.Length > 256))
    {
        return ValidationError("Password must be 12-256 characters when provided.");
    }

    return null;
}
static object? ValidateUserRolesRequest(UpdateUserRolesRequest request)
{
    if (request.Roles is null || request.Roles.Count > 16)
    {
        return ValidationError("Roles collection is required and must contain at most 16 roles.");
    }

    if (request.Roles.Any(role => string.IsNullOrWhiteSpace(role) || role.Length > 80))
    {
        return ValidationError("Each role must be 1-80 characters.");
    }

    return null;
}
static bool IsSafeLogin(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && value.Length <= 80
    && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '@');
static bool IsSafeSettingKey(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && value.Length <= 120
    && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':');
static bool IsReasonableEmail(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && value.Length <= 240
    && value.Contains('@', StringComparison.Ordinal)
    && !value.Contains('\r')
    && !value.Contains('\n');

static async Task<ProductionReadinessDto> BuildProductionReadinessAsync(
    HttpContext context,
    IConfiguration configuration,
    IHostEnvironment environment,
    ISysAssistApiService service,
    ILicenseService licenseService,
    IClock clock,
    CancellationToken cancellationToken)
{
    var gates = new List<ProductionReadinessGateDto>();
    var checkedAt = clock.UtcNow;
    var useDemoData = configuration.GetValue("SysAssist:UseDemoData", false);
    var applyMigrationsOnStartup = configuration.GetValue("SysAssist:ApplyMigrationsOnStartup", false);
    var forceEnableModulesOnStartup = configuration.GetValue("SysAssist:ForceEnableModulesOnStartup", false);
    var jwtOptions = JwtOptions.FromConfiguration(configuration);
    var allowedHosts = configuration["AllowedHosts"];
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var encryptionKey = configuration["Security:SecretEncryptionKey"] ?? configuration["SYSASSIST_SECRET_ENCRYPTION_KEY"];
    var bootstrapAdminPassword = configuration["SysAssist:BootstrapAdminPassword"]
        ?? configuration["SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD"];

    void Gate(string key, bool passed, string passedMessage, string failedMessage, bool required = true) =>
        gates.Add(new ProductionReadinessGateDto(key, passed, passed ? passedMessage : failedMessage, required));

    Gate(
        "environment-production",
        environment.IsProduction(),
        $"Environment is {environment.EnvironmentName}.",
        $"Environment is {environment.EnvironmentName}; set ASPNETCORE_ENVIRONMENT=Production.");
    Gate(
        "demo-data-off",
        !useDemoData,
        "Demo data mode is off.",
        "SysAssist:UseDemoData must be false.");
    Gate(
        "startup-migrations-off",
        !applyMigrationsOnStartup,
        "Startup migrations are off.",
        "SysAssist:ApplyMigrationsOnStartup must be false; run migrations in the deployment pipeline.");
    Gate(
        "force-enable-modules-off",
        !forceEnableModulesOnStartup,
        "Startup module auto-enable is off.",
        "SysAssist:ForceEnableModulesOnStartup must be false so disabled modules stay disabled after restart.");
    Gate(
        "allowed-hosts-explicit",
        !string.IsNullOrWhiteSpace(allowedHosts) && allowedHosts != "*",
        "AllowedHosts is explicit.",
        "AllowedHosts is empty or wildcard.");
    Gate(
        "cors-production-origins",
        origins.Length > 0 && origins.All(IsProductionCorsOrigin),
        $"{origins.Length} production CORS origin(s) configured.",
        "Cors:AllowedOrigins must contain deployed HTTPS origins only.");
    Gate(
        "jwt-signing-secret",
        jwtOptions.SigningKey.Length >= 32 && !jwtOptions.SigningKey.StartsWith("dev-only", StringComparison.OrdinalIgnoreCase),
        "JWT signing key is configured with production length.",
        "JWT signing key is missing, too short, or uses the dev-only fallback.");
    Gate(
        "module-secret-encryption",
        !string.IsNullOrWhiteSpace(encryptionKey) && encryptionKey.Length >= 32 && !LooksLikeSecretPlaceholder(encryptionKey),
        "AES-GCM module secret encryption key is configured.",
        "Security:SecretEncryptionKey or SYSASSIST_SECRET_ENCRYPTION_KEY is missing, placeholder, or too short.");
    Gate(
        "bootstrap-admin-secret",
        string.IsNullOrWhiteSpace(bootstrapAdminPassword) || (IsStrongBootstrapAdminPassword(bootstrapAdminPassword) && !LooksLikeSecretPlaceholder(bootstrapAdminPassword)),
        "Bootstrap admin password is absent or strong.",
        "Bootstrap admin password is weak or still a placeholder.");
    Gate(
        "request-body-limit",
        configuration.GetValue<long?>("Security:MaxRequestBodyBytes") is > 0 and <= 10_485_760,
        "Request body limit is bounded.",
        "Security:MaxRequestBodyBytes must be set to a positive bounded value.");
    Gate(
        "rate-limits-configured",
        PositiveRateLimit(configuration, "RateLimiting:Auth")
            && PositiveRateLimit(configuration, "RateLimiting:Webhooks")
            && PositiveRateLimit(configuration, "RateLimiting:Writes"),
        "Auth, webhook, and write rate limits are configured.",
        "RateLimiting Auth/Webhooks/Writes permit limits and windows must be positive.");

    if (useDemoData)
    {
        Gate("database-ready", false, "CockroachDB connection is healthy.", "Demo data mode is enabled; production requires CockroachDB readiness.");
    }
    else
    {
        var dbContext = context.RequestServices.GetService<SysAssistDbContext>();
        if (dbContext is null)
        {
            Gate("database-ready", false, "CockroachDB connection is healthy.", "SysAssistDbContext is not registered.");
        }
        else
        {
            try
            {
                var databaseReady = await dbContext.Database.CanConnectAsync(cancellationToken);
                Gate("database-ready", databaseReady, "CockroachDB connection is healthy.", "CockroachDB connection failed.");
            }
            catch (Exception ex)
            {
                Gate("database-ready", false, "CockroachDB connection is healthy.", $"CockroachDB readiness check failed: {ex.GetType().Name}.");
            }
        }
    }

    var license = licenseService.GetStatus();
    Gate(
        "license-valid",
        license.IsValid,
        $"License is valid for {license.Edition}.",
        $"License is {license.Status}: {license.Message}");
    Gate(
        "license-expiry-window",
        !license.ExpiresAt.HasValue || license.ExpiresAt.Value > checkedAt.AddDays(30),
        "License does not expire within 30 days.",
        "License expires within 30 days.",
        required: false);

    try
    {
        var diagnostics = await service.GetDiagnosticsAsync(cancellationToken);
        var diagnosticsComponents = diagnostics.Components.ToArray();
        var diagnosticsEvidence = ComponentValue(diagnosticsComponents, "diagnosticsEvidence");
        var diagnosticsFreshness = ComponentValue(diagnosticsComponents, "diagnosticsFreshness");

        Gate(
            "diagnostics-healthy",
            diagnostics.Status == "Healthy",
            "Diagnostics status is Healthy.",
            $"Diagnostics status is {diagnostics.Status}.");
        Gate(
            "diagnostics-real-evidence",
            diagnosticsEvidence == "real-adapter-health",
            "Diagnostics are based on real adapter health.",
            "Diagnostics evidence is not real adapter health.");
        Gate(
            "diagnostics-fresh",
            diagnosticsFreshness == "fresh",
            "Diagnostics are fresh.",
            $"Diagnostics freshness is {diagnosticsFreshness ?? "unknown"}.");
    }
    catch (Exception ex)
    {
        Gate("diagnostics-readable", false, "Diagnostics can be read.", $"Diagnostics read failed: {ex.GetType().Name}.");
    }

    try
    {
        var modules = (await service.ListModulesAsync(cancellationToken)).ToArray();
        var enabledModules = modules.Where(module => module.IsEnabled).ToArray();
        var unhealthyEnabled = enabledModules.Where(module => module.HealthStatus != "Healthy").ToArray();
        var fallbackEnabled = enabledModules.Where(module => module.UseFallbackMode).ToArray();
        var unsafeEnabled = enabledModules.Where(module => !module.SafeMode).ToArray();

        Gate(
            "enabled-modules-present",
            enabledModules.Length > 0,
            $"{enabledModules.Length} module(s) enabled.",
            "No modules are enabled.");
        Gate(
            "enabled-modules-healthy",
            unhealthyEnabled.Length == 0,
            $"{enabledModules.Length} enabled module(s) are healthy.",
            $"Enabled unhealthy modules: {JoinModuleKeys(unhealthyEnabled)}.");
        Gate(
            "fallback-mode-off",
            fallbackEnabled.Length == 0,
            "Enabled modules do not use fallback mode.",
            $"Enabled fallback modules: {JoinModuleKeys(fallbackEnabled)}.");
        Gate(
            "safe-mode-on",
            unsafeEnabled.Length == 0,
            "SafeMode is enabled on all enabled modules.",
            $"Enabled modules with SafeMode off: {JoinModuleKeys(unsafeEnabled)}.");
    }
    catch (Exception ex)
    {
        Gate("modules-readable", false, "Modules can be read.", $"Module registry read failed: {ex.GetType().Name}.");
    }

    var status = gates.Where(gate => gate.Required).All(gate => gate.Passed) ? "Ready" : "Blocked";
    return new ProductionReadinessDto(status, checkedAt, gates);
}

static bool PositiveRateLimit(IConfiguration configuration, string section) =>
    configuration.GetValue<int>($"{section}:PermitLimit") > 0
    && configuration.GetValue<int>($"{section}:WindowMinutes") > 0;

static string? ComponentValue(IEnumerable<string> components, string key)
{
    var prefix = $"{key}=";
    var component = components.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return component is null ? null : component[prefix.Length..];
}

static bool IsProductionCorsOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    return uri.Scheme == Uri.UriSchemeHttps
        && !IsLocalHost(uri.Host)
        && origin != "*";
}

static bool IsLocalHost(string host) =>
    host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
    || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
    || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
    || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);

static string JoinModuleKeys(IEnumerable<ModuleDto> modules)
{
    var keys = modules.Select(module => module.Key).Take(8).ToArray();
    return keys.Length == 0 ? "none" : string.Join(", ", keys);
}

static async Task<IResult> BuildReadinessResponseAsync(
    HttpContext context,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILicenseService licenseService,
    IClock clock,
    CancellationToken cancellationToken)
{
    var useDemoData = configuration.GetValue("SysAssist:UseDemoData", false);
    var components = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    var databaseReady = true;

    if (useDemoData)
    {
        components["database"] = new { status = "DemoData", message = "Demo data mode is enabled; no database readiness check is required." };
    }
    else
    {
        var dbContext = context.RequestServices.GetService<SysAssistDbContext>();
        if (dbContext is null)
        {
            databaseReady = false;
            components["database"] = new { status = "Missing", message = "SysAssistDbContext is not registered." };
        }
        else
        {
            try
            {
                databaseReady = await dbContext.Database.CanConnectAsync(cancellationToken);
                components["database"] = new { status = databaseReady ? "Healthy" : "Unavailable", provider = "CockroachDB/Npgsql" };
            }
            catch (Exception ex)
            {
                databaseReady = false;
                components["database"] = new { status = "Error", provider = "CockroachDB/Npgsql", message = ex.Message };
            }
        }
    }

    var license = licenseService.GetStatus();
    components["license"] = new { license.Status, license.Edition, license.ExpiresAt, license.Message };
    components["environment"] = new
    {
        environment.EnvironmentName,
        UseDemoData = useDemoData,
        ApplyMigrationsOnStartup = configuration.GetValue("SysAssist:ApplyMigrationsOnStartup", false)
    };

    var ready = databaseReady && (useDemoData || license.IsValid);
    var payload = new
    {
        status = ready ? "Ready" : "NotReady",
        service = "SysAssist.Api",
        checkedAt = clock.UtcNow,
        components
    };

    return ready ? Results.Ok(payload) : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}

static IApplicationBuilder UseSecurityHeaders(IApplicationBuilder app) =>
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=()");

        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers.TryAdd("Content-Security-Policy", "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'");
        }

        await next();
    });

static void ValidateSecurityConfiguration(IConfiguration configuration, IHostEnvironment environment)
{
    var useDemoData = configuration.GetValue("SysAssist:UseDemoData", false);
    var jwtOptions = JwtOptions.FromConfiguration(configuration);
    var allowDevelopmentJwtFallback = configuration.GetValue("Security:AllowDevelopmentJwtFallback", false) && environment.IsDevelopment();
    if (!useDemoData
        && !allowDevelopmentJwtFallback
        && jwtOptions.SigningKey.StartsWith("dev-only", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Real mode requires Jwt:SigningKey or Auth:JwtSecret from secrets/.env.");
    }

    if (!environment.IsProduction())
    {
        return;
    }

    if (useDemoData)
    {
        throw new InvalidOperationException("Production requires SysAssist:UseDemoData=false.");
    }

    if (configuration.GetValue("SysAssist:ApplyMigrationsOnStartup", false))
    {
        throw new InvalidOperationException("Production must not run EF migrations automatically. Apply migrations through the deployment pipeline.");
    }

    if (jwtOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Production JWT signing key must be at least 32 characters.");
    }

    var bootstrapAdminPassword = configuration["SysAssist:BootstrapAdminPassword"]
        ?? configuration["SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(bootstrapAdminPassword)
        && (!IsStrongBootstrapAdminPassword(bootstrapAdminPassword) || LooksLikeSecretPlaceholder(bootstrapAdminPassword)))
    {
        throw new InvalidOperationException("Production bootstrap admin password must be a real strong secret.");
    }

    var allowedHosts = configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
    {
        throw new InvalidOperationException("Production requires explicit AllowedHosts.");
    }

    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length == 0
        || origins.Any(origin => origin.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || origin.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || origin == "*"))
    {
        throw new InvalidOperationException("Production requires explicit non-local Cors:AllowedOrigins.");
    }

    var licenseKey = configuration["Licensing:LicenseKey"] ?? configuration["SYSASSIST_LICENSE_KEY"];
    var licensePublicKey = configuration["Licensing:PublicKey"] ?? configuration["SYSASSIST_LICENSE_PUBLIC_KEY"];
    if (string.IsNullOrWhiteSpace(licensePublicKey) || string.IsNullOrWhiteSpace(licenseKey))
    {
        throw new InvalidOperationException("Production requires Licensing:PublicKey and Licensing:LicenseKey.");
    }
}

static bool IsStrongBootstrapAdminPassword(string password) =>
    password.Length >= 12
    && password.Any(char.IsUpper)
    && password.Any(char.IsLower)
    && password.Any(char.IsDigit)
    && password.Any(ch => !char.IsLetterOrDigit(ch));

static bool LooksLikeSecretPlaceholder(string value) =>
    value.Contains("replace", StringComparison.OrdinalIgnoreCase)
    || value.Contains("change-me", StringComparison.OrdinalIgnoreCase)
    || value.Contains("changeme", StringComparison.OrdinalIgnoreCase);

static async Task<string> ReadRawBodyAsync(HttpContext context)
{
    using var reader = new StreamReader(context.Request.Body);
    return await reader.ReadToEndAsync();
}

static string CustomModulesPath(IWebHostEnvironment environment) =>
    Path.Combine(environment.ContentRootPath, "data", "custom-modules.json");

static async Task<List<CustomModuleManifestDto>> ReadCustomModulesAsync(IWebHostEnvironment environment, CancellationToken cancellationToken)
{
    var path = CustomModulesPath(environment);
    if (!File.Exists(path))
    {
        return [];
    }

    await using var stream = File.OpenRead(path);
    return await JsonSerializer.DeserializeAsync<List<CustomModuleManifestDto>>(stream, JsonSerializerOptions.Web, cancellationToken) ?? [];
}

static async Task WriteCustomModulesAsync(IWebHostEnvironment environment, List<CustomModuleManifestDto> items, CancellationToken cancellationToken)
{
    var path = CustomModulesPath(environment);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await using var stream = File.Create(path);
    await JsonSerializer.SerializeAsync(stream, items, JsonSerializerOptions.Web, cancellationToken);
}

static string? ValidateCustomModule(CustomModuleManifestDto manifest)
{
    if (manifest.ApiVersion != "sysassist.module/v1")
    {
        return "apiVersion must be sysassist.module/v1.";
    }

    if (string.IsNullOrWhiteSpace(manifest.Key)
        || string.IsNullOrWhiteSpace(manifest.Name)
        || string.IsNullOrWhiteSpace(manifest.Version))
    {
        return "Manifest must include key, name, and version.";
    }

    if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.Key, "^[a-z0-9][a-z0-9_.-]+$"))
    {
        return "Module key must use lowercase letters, numbers, dash, underscore, or dot.";
    }

    if (manifest.Key.Length > 120 || manifest.Name.Length > 160 || manifest.Version.Length > 40)
    {
        return "Module key, name, or version is too long.";
    }

    if (manifest.Author?.Length > 160 || manifest.Category?.Length > 80 || manifest.Description?.Length > 1024)
    {
        return "Module metadata fields exceed allowed length.";
    }

    if (!string.IsNullOrWhiteSpace(manifest.Entrypoint)
        && (!Uri.TryCreate(manifest.Entrypoint, UriKind.Absolute, out var entrypoint)
            || entrypoint.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(entrypoint.Host)))
    {
        return "Entrypoint must be an absolute HTTP or HTTPS URL.";
    }

    if (manifest.Permissions?.Length > 32 || manifest.Tags?.Length > 32 || manifest.Settings?.Length > 128 || manifest.Actions?.Length > 64)
    {
        return "Manifest contains too many permissions, tags, settings, or actions.";
    }

    if (manifest.Tags?.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 64) == true
        || manifest.Permissions?.Any(permission => string.IsNullOrWhiteSpace(permission) || permission.Length > 120) == true)
    {
        return "Manifest tags and permissions must be non-empty and within allowed length.";
    }

    if (manifest.Settings?.Any(setting =>
            !IsSafeSettingKey(setting.Key)
            || string.IsNullOrWhiteSpace(setting.Type)
            || setting.Type.Length > 32
            || setting.Description?.Length > 512
            || setting.DefaultValue?.Length > 4096) == true)
    {
        return "Manifest settings contain invalid keys, types, descriptions, or default values.";
    }

    if (manifest.Actions?.Any(action =>
            !IsSafeSettingKey(action.Key)
            || string.IsNullOrWhiteSpace(action.Name)
            || action.Name.Length > 160
            || action.Risk?.Length > 32
            || action.Description?.Length > 512) == true)
    {
        return "Manifest actions contain invalid keys, names, risk values, or descriptions.";
    }

    return null;
}

static async Task<OperationResultDto> CallCustomModuleHealthAsync(CustomModuleManifestDto module, HttpClient httpClient, CancellationToken cancellationToken)
{
    if (!TryRuntimeEndpoint(module, "health", out var endpoint, out var error))
    {
        return new OperationResultDto(false, error!, null);
    }

    var response = await httpClient.PostAsJsonAsync(endpoint, RuntimeRequest(module), cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return new OperationResultDto(false, $"Runtime health HTTP {(int)response.StatusCode} {response.StatusCode}.", null);
    }

    return ParseRuntimeOperation(body, successWhenStatusHealthy: true);
}

static async Task<CustomModuleEventsResultDto> CallCustomModuleEventsAsync(CustomModuleManifestDto module, HttpClient httpClient, CancellationToken cancellationToken)
{
    if (!TryRuntimeEndpoint(module, "events", out var endpoint, out var error))
    {
        return new CustomModuleEventsResultDto(false, error!, []);
    }

    var response = await httpClient.PostAsJsonAsync(endpoint, RuntimeRequest(module), cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return new CustomModuleEventsResultDto(false, $"Runtime events HTTP {(int)response.StatusCode} {response.StatusCode}.", []);
    }

    try
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var success = !root.TryGetProperty("success", out var successElement) || successElement.GetBoolean();
        var message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "Runtime events completed." : "Runtime events completed.";
        var eventsElement = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("events", out var nestedEvents) ? nestedEvents : default;
        var events = eventsElement.ValueKind == JsonValueKind.Array
            ? eventsElement.EnumerateArray()
                .Select(item => new CustomModuleRuntimeEventDto(
                    item.TryGetProperty("eventType", out var eventType) ? eventType.GetString() ?? "custom.event" : "custom.event",
                    item.TryGetProperty("severity", out var severity) ? severity.GetString() ?? "Warning" : "Warning",
                    item.TryGetProperty("target", out var target) ? target.GetString() ?? module.Key : module.Key,
                    item.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "Custom module event" : "Custom module event",
                    item.TryGetProperty("externalEventId", out var externalEventId) ? externalEventId.GetString() : null))
                .ToArray()
            : [];

        return new CustomModuleEventsResultDto(success, $"{message} Events: {events.Length}.", events);
    }
    catch (JsonException ex)
    {
        return new CustomModuleEventsResultDto(false, $"Runtime events returned invalid JSON: {ex.Message}", []);
    }
}

static async Task<OperationResultDto> CallCustomModuleActionAsync(CustomModuleManifestDto module, string actionKey, ExecuteActionRequest request, HttpClient httpClient, CancellationToken cancellationToken)
{
    if (!TryRuntimeEndpoint(module, $"actions/{Uri.EscapeDataString(actionKey)}", out var endpoint, out var error))
    {
        return new OperationResultDto(false, error!, null);
    }

    var response = await httpClient.PostAsJsonAsync(endpoint, RuntimeRequest(module, request.Target, request.ParametersJson), cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return new OperationResultDto(false, $"Runtime action HTTP {(int)response.StatusCode} {response.StatusCode}.", null);
    }

    return ParseRuntimeOperation(body, successWhenStatusHealthy: false);
}

static OperationResultDto ParseRuntimeOperation(string body, bool successWhenStatusHealthy)
{
    try
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var message = root.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString() ?? "Runtime operation completed."
            : "Runtime operation completed.";
        if (root.TryGetProperty("success", out var successElement) && successElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new OperationResultDto(successElement.GetBoolean(), message, null);
        }

        if (root.TryGetProperty("status", out var statusElement))
        {
            var status = statusElement.GetString();
            return new OperationResultDto(successWhenStatusHealthy && status == "Healthy", $"{message} Status: {status}.", null);
        }

        return new OperationResultDto(false, "Runtime response did not include success or status.", null);
    }
    catch (JsonException ex)
    {
        return new OperationResultDto(false, $"Runtime returned invalid JSON: {ex.Message}", null);
    }
}

static bool TryRuntimeEndpoint(CustomModuleManifestDto module, string path, out Uri endpoint, out string? error)
{
    endpoint = new Uri("http://localhost/");
    error = null;
    if (string.IsNullOrWhiteSpace(module.Entrypoint))
    {
        error = "Custom module entrypoint is missing.";
        return false;
    }

    if (!Uri.TryCreate(module.Entrypoint, UriKind.Absolute, out var baseUri)
        || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
    {
        error = "Only HTTP/HTTPS custom module runtime entrypoints are executable by this SysAssist build.";
        return false;
    }

    endpoint = new Uri($"{module.Entrypoint.TrimEnd('/')}/{path.TrimStart('/')}");
    return true;
}

static CustomModuleRuntimeRequest RuntimeRequest(CustomModuleManifestDto module, string? target = null, string? parametersJson = null) =>
    new(module.Key, module.Settings?.ToDictionary(item => item.Key, item => item.DefaultValue, StringComparer.OrdinalIgnoreCase) ?? [], target, parametersJson);

public sealed record CustomModuleManifestDto(
    string ApiVersion,
    string Key,
    string Name,
    string Version,
    string? Author,
    string? Category,
    string? Description,
    string? Entrypoint,
    bool? Enabled,
    string[]? Permissions,
    string[]? Tags,
    CustomModuleSettingDto[]? Settings,
    CustomModuleActionDto[]? Actions);

public sealed record CustomModuleSettingDto(
    string Key,
    string Type,
    bool? Required,
    bool? Secret,
    string? Description,
    string? DefaultValue);

public sealed record CustomModuleActionDto(
    string Key,
    string Name,
    string? Risk,
    bool? RequiresApproval,
    string? Description);

public sealed record CustomModuleRuntimeRequest(
    string ModuleKey,
    IReadOnlyDictionary<string, string?> Settings,
    string? Target,
    string? ParametersJson);

public sealed record CustomModuleRuntimeEventDto(
    string EventType,
    string Severity,
    string Target,
    string Summary,
    string? ExternalEventId);

public sealed record CustomModuleEventsResultDto(
    bool Success,
    string Message,
    IReadOnlyCollection<CustomModuleRuntimeEventDto> Events);
