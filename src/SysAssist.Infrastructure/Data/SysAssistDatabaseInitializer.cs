using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SysAssist.Application.Security;

namespace SysAssist.Infrastructure.Data;

public static class SysAssistDatabaseInitializer
{
    public static async Task InitializeSysAssistDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<SysAssistDbContext>();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
        var secretProtector = scope.ServiceProvider.GetService<ISecretProtector>();

        if (dbContext is null)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SysAssistDbContext>>();
        logger.LogInformation("Applying SysAssist database migrations and seed data.");

        var connectionString = dbContext.Database.GetConnectionString();
        if (connectionString?.Contains("cockroachlabs.cloud", StringComparison.OrdinalIgnoreCase) == true)
        {
            logger.LogInformation("Cockroach Cloud detected; creating schema without PostgreSQL migration locks.");
            if (!await HasApplicationSchemaAsync(dbContext, cancellationToken))
            {
                logger.LogWarning("Cockroach Cloud application schema is missing; creating tables from the EF model.");
                await dbContext.Database.ExecuteSqlRawAsync(@"DROP TABLE IF EXISTS ""__EFMigrationsHistory""", cancellationToken);
                await ExecuteCreateScriptAsync(dbContext, logger, cancellationToken);
            }
        }
        else
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        logger.LogInformation("Seeding SysAssist database.");
        await SysAssistDbSeeder.SeedAsync(dbContext, configuration, secretProtector, cancellationToken);
        logger.LogInformation("SysAssist database is ready.");
    }

    private static async Task ExecuteCreateScriptAsync(SysAssistDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        var statements = dbContext.Database.GenerateCreateScript()
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.CommandTimeout = 120;

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState is "42P07" or "42710")
            {
                logger.LogDebug("Cockroach schema object already exists: {Message}", ex.MessageText);
            }
        }
    }

    private static async Task<bool> HasApplicationSchemaAsync(SysAssistDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM roles LIMIT 1";
        command.CommandTimeout = 15;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return false;
        }
    }
}
