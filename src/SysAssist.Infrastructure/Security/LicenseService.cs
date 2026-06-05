using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SysAssist.Application.Security;
using SysAssist.Contracts.Api;

namespace SysAssist.Infrastructure.Security;

public sealed class LicenseService(IConfiguration configuration) : ILicenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<LicenseStatusDto> _status = new(() => Evaluate(configuration));

    public LicenseStatusDto GetStatus() => _status.Value;

    private static LicenseStatusDto Evaluate(IConfiguration configuration)
    {
        var useDemoData = configuration.GetValue("SysAssist:UseDemoData", false);
        var allowDevelopmentBypass = configuration.GetValue("Licensing:AllowDevelopmentBypass", false);
        var licenseKey = configuration["Licensing:LicenseKey"] ?? configuration["SYSASSIST_LICENSE_KEY"];
        var publicKey = configuration["Licensing:PublicKey"] ?? configuration["SYSASSIST_LICENSE_PUBLIC_KEY"];
        var fingerprint = MachineFingerprint(configuration);

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            if (useDemoData || allowDevelopmentBypass)
            {
                return new LicenseStatusDto(
                    "Development",
                    "Developer",
                    "local",
                    Environment.MachineName,
                    null,
                    true,
                    ["*"],
                    null,
                    "Development license bypass is active for local/demo operation.",
                    fingerprint);
            }

            return Invalid("Missing license key. Set Licensing:LicenseKey or SYSASSIST_LICENSE_KEY.", fingerprint);
        }

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            return Invalid("Missing license public key. Set Licensing:PublicKey or SYSASSIST_LICENSE_PUBLIC_KEY.", fingerprint);
        }

        var parts = licenseKey.Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return Invalid("License key format is invalid.", fingerprint);
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var signature = Base64UrlDecode(parts[1]);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);

            if (!VerifySignature(ecdsa, payloadBytes, signature))
            {
                return Invalid("License signature is invalid.", fingerprint);
            }

            var payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, JsonOptions);
            if (payload is null)
            {
                return Invalid("License payload is empty.", fingerprint);
            }

            if (string.IsNullOrWhiteSpace(payload.Product)
                || !payload.Product.Equals("SysAssist", StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("License product does not match SysAssist.", fingerprint);
            }

            var now = DateTimeOffset.UtcNow;
            if (payload.NotBefore is not null && now < payload.NotBefore.Value)
            {
                return Invalid("License is not active yet.", fingerprint, payload);
            }

            if (payload.ExpiresAt is not null && now > payload.ExpiresAt.Value)
            {
                return Invalid("License has expired.", fingerprint, payload);
            }

            if (!string.IsNullOrWhiteSpace(payload.Fingerprint)
                && !payload.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("License machine fingerprint does not match this host.", fingerprint, payload);
            }

            return new LicenseStatusDto(
                "Valid",
                payload.Edition,
                payload.TenantId,
                payload.Subject,
                payload.ExpiresAt,
                true,
                payload.Features ?? [],
                payload.ModuleLimit,
                "License signature, product, time window, and fingerprint are valid.",
                fingerprint);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            return Invalid($"License validation failed: {ex.Message}", fingerprint);
        }
    }

    private static LicenseStatusDto Invalid(string message, string fingerprint, LicensePayload? payload = null) =>
        new(
            "Invalid",
            payload?.Edition ?? "Unlicensed",
            payload?.TenantId,
            payload?.Subject,
            payload?.ExpiresAt,
            false,
            payload?.Features ?? [],
            payload?.ModuleLimit,
            message,
            fingerprint);

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static bool VerifySignature(ECDsa ecdsa, byte[] payloadBytes, byte[] signature) =>
        ecdsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256)
        || ecdsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    private static string MachineFingerprint(IConfiguration configuration)
    {
        var salt = configuration["Licensing:FingerprintSalt"] ?? "sysassist-v1";
        var source = $"{salt}|{Environment.MachineName}|{Environment.OSVersion.Platform}|{Environment.ProcessorCount}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private sealed record LicensePayload(
        string Product,
        string Edition,
        string? TenantId,
        string? Subject,
        DateTimeOffset? IssuedAt,
        DateTimeOffset? NotBefore,
        DateTimeOffset? ExpiresAt,
        string[]? Features,
        int? ModuleLimit,
        string? Fingerprint);
}
