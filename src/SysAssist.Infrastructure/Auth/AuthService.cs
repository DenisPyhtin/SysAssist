using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SysAssist.Application.Auth;
using SysAssist.Contracts.Auth;
using SysAssist.Domain.Entities;
using SysAssist.Infrastructure.Data;

namespace SysAssist.Infrastructure.Auth;

public sealed class AuthService(
    IConfiguration configuration,
    IPasswordHasher passwordHasher,
    SysAssistDbContext? dbContext = null) : IAuthService
{
    private readonly JwtOptions _jwtOptions = JwtOptions.FromConfiguration(configuration);

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.Login, cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var roles = user.UserRoles.Select(userRole => userRole.Role?.Name)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var token = CreateToken(user, roles, expiresAt);
        var dto = new CurrentUserDto(user.Id, user.Login, user.DisplayName, user.Email, roles);

        return new LoginResponse(token, expiresAt, dto);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var subject = principal.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return null;
        }

        var user = await FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = user.UserRoles.Select(userRole => userRole.Role?.Name)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .ToArray();

        return new CurrentUserDto(user.Id, user.Login, user.DisplayName, user.Email, roles);
    }

    private async Task<User?> FindUserAsync(string login, CancellationToken cancellationToken)
    {
        if (dbContext is not null)
        {
            return await dbContext.Users
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .SingleOrDefaultAsync(user => user.Login == login, cancellationToken);
        }

        return DemoAuthData.FindUser(login, configuration, passwordHasher);
    }

    private async Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (dbContext is not null)
        {
            return await dbContext.Users
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        }

        return DemoAuthData.FindUserById(userId, configuration, passwordHasher);
    }

    private string CreateToken(User user, IReadOnlyCollection<string> roles, DateTimeOffset expiresAt)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Login),
            new("display_name", user.DisplayName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal static class DemoAuthData
{
    private static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Role AdminRole = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "Admin",
        Description = "Full platform administration."
    };

    public static User? FindUser(string login, IConfiguration configuration, IPasswordHasher passwordHasher) =>
        login.Equals("admin", StringComparison.OrdinalIgnoreCase)
            ? BuildAdmin(configuration, passwordHasher)
            : null;

    public static User? FindUserById(Guid userId, IConfiguration configuration, IPasswordHasher passwordHasher) =>
        userId == AdminUserId ? BuildAdmin(configuration, passwordHasher) : null;

    private static User? BuildAdmin(IConfiguration configuration, IPasswordHasher passwordHasher)
    {
        var password = configuration["SysAssist:BootstrapAdminPassword"]
            ?? configuration["SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password) || !IsStrongBootstrapAdminPassword(password) || LooksLikePlaceholder(password))
        {
            return null;
        }

        return new User
        {
            Id = AdminUserId,
            Login = "admin",
            DisplayName = "SysAssist Admin",
            Email = "admin@sysassist.local",
            PasswordHash = passwordHasher.Hash(password),
            IsActive = true,
            UserRoles = [new UserRole { RoleId = AdminRole.Id, Role = AdminRole }]
        };
    }

    private static bool IsStrongBootstrapAdminPassword(string password) =>
        password.Length >= 12
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(ch => !char.IsLetterOrDigit(ch));

    private static bool LooksLikePlaceholder(string value) =>
        value.Contains("replace", StringComparison.OrdinalIgnoreCase)
        || value.Contains("change-me", StringComparison.OrdinalIgnoreCase)
        || value.Contains("changeme", StringComparison.OrdinalIgnoreCase);
}
