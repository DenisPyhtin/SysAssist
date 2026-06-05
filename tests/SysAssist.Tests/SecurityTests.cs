using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SysAssist.Infrastructure.Security;

namespace SysAssist.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void AesGcmSecretProtector_EncryptsAndDecryptsSecretWithoutPlaintextLeak()
    {
        var protector = new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        const string plaintext = "postgres://sysadmin:redacted-test-password@example.invalid:5432/sysassist";

        var protectedValue = protector.Protect(plaintext);
        var protectedAgain = protector.Protect(plaintext);

        Assert.NotNull(protectedValue);
        Assert.StartsWith("enc:v1:", protectedValue);
        Assert.NotEqual(plaintext, protectedValue);
        Assert.NotEqual(protectedValue, protectedAgain);
        Assert.DoesNotContain("redacted-test-password", protectedValue, StringComparison.OrdinalIgnoreCase);
        Assert.True(protector.IsProtected(protectedValue));
        Assert.Equal(plaintext, protector.Unprotect(protectedValue));
    }

    [Fact]
    public void AesGcmSecretProtector_RejectsTamperedCiphertext()
    {
        var protector = new AesGcmSecretProtector("test-encryption-key-that-is-long-enough");
        var protectedValue = protector.Protect("telegram-token")!;
        var tampered = protectedValue[..^2] + "aa";

        Assert.Throws<InvalidOperationException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void AesGcmSecretProtector_RejectsWeakConfiguredKey()
    {
        Assert.Throws<InvalidOperationException>(() => new AesGcmSecretProtector("too-short"));
    }

    [Fact]
    public void LicenseService_ValidatesSignedLicense()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var licenseKey = CreateLicenseKey(ecdsa, new
        {
            product = "SysAssist",
            edition = "Enterprise",
            tenantId = "tenant-prod",
            subject = "Prod Cluster",
            issuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            notBefore = DateTimeOffset.UtcNow.AddMinutes(-1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(30),
            features = new[] { "modules", "diagnostics" },
            moduleLimit = 64,
            fingerprint = (string?)null
        });

        var status = new LicenseService(ConfigurationForLicense(ecdsa, licenseKey)).GetStatus();

        Assert.True(status.IsValid);
        Assert.Equal("Valid", status.Status);
        Assert.Equal("Enterprise", status.Edition);
        Assert.Contains("diagnostics", status.Features);
    }

    [Fact]
    public void LicenseService_RejectsTamperedLicense()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var licenseKey = CreateLicenseKey(ecdsa, new
        {
            product = "SysAssist",
            edition = "Enterprise",
            tenantId = "tenant-prod",
            subject = "Prod Cluster",
            issuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            notBefore = DateTimeOffset.UtcNow.AddMinutes(-1),
            expiresAt = DateTimeOffset.UtcNow.AddDays(30),
            features = new[] { "*" },
            moduleLimit = 64,
            fingerprint = (string?)null
        });

        var parts = licenseKey.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0])).Replace("Enterprise", "UltimateBank");
        var tampered = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{parts[1]}";

        var status = new LicenseService(ConfigurationForLicense(ecdsa, tampered)).GetStatus();

        Assert.False(status.IsValid);
        Assert.Equal("Invalid", status.Status);
        Assert.Contains("signature", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LicenseService_RejectsExpiredLicense()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var licenseKey = CreateLicenseKey(ecdsa, new
        {
            product = "SysAssist",
            edition = "Enterprise",
            tenantId = "tenant-prod",
            subject = "Prod Cluster",
            issuedAt = DateTimeOffset.UtcNow.AddDays(-10),
            notBefore = DateTimeOffset.UtcNow.AddDays(-10),
            expiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            features = new[] { "*" },
            moduleLimit = 64,
            fingerprint = (string?)null
        });

        var status = new LicenseService(ConfigurationForLicense(ecdsa, licenseKey)).GetStatus();

        Assert.False(status.IsValid);
        Assert.Equal("Invalid", status.Status);
        Assert.Contains("expired", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration ConfigurationForLicense(ECDsa ecdsa, string licenseKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SysAssist:UseDemoData"] = "false",
                ["Licensing:LicenseKey"] = licenseKey,
                ["Licensing:PublicKey"] = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo())
            })
            .Build();

    private static string CreateLicenseKey(ECDsa ecdsa, object payload)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
