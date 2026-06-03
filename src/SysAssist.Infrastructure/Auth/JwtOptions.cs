namespace SysAssist.Infrastructure.Auth;

using Microsoft.Extensions.Configuration;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "SysAssist";
    public string Audience { get; init; } = "sysassist-api";
    public string SigningKey { get; init; } = "dev-only-change-this-signing-key-32-chars-minimum";
    public int ExpirationMinutes { get; init; } = 120;

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var authJwtSecret = configuration["Auth:JwtSecret"];

        return new JwtOptions
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            SigningKey = string.IsNullOrWhiteSpace(authJwtSecret) ? options.SigningKey : authJwtSecret,
            ExpirationMinutes = options.ExpirationMinutes
        };
    }
}
