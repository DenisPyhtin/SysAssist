using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SysAssist.Application.Security;

namespace SysAssist.Infrastructure.Security;

public sealed class SecretProtectionOptions
{
    public string? Key { get; init; }
    public bool RequireEncryptionInRealMode { get; init; } = true;

    public static SecretProtectionOptions FromConfiguration(IConfiguration configuration) =>
        new()
        {
            Key = configuration["Security:SecretEncryptionKey"] ?? configuration["SYSASSIST_SECRET_ENCRYPTION_KEY"],
            RequireEncryptionInRealMode = configuration.GetValue("Security:RequireEncryptionInRealMode", true)
        };
}

public sealed class NoOpSecretProtector : ISecretProtector
{
    public static readonly NoOpSecretProtector Instance = new();

    private NoOpSecretProtector()
    {
    }

    public bool IsProtected(string? value) => false;
    public string? Protect(string? value) => value;
    public string? Unprotect(string? value) => value;
}

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string Prefix = "enc:v1:";
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("SysAssist.IntegrationSetting.Secret.v1");
    private readonly byte[] _key;

    public AesGcmSecretProtector(string configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("Security:SecretEncryptionKey is required for encrypted secret storage.");
        }

        _key = DecodeKey(configuredKey.Trim());
    }

    public bool IsProtected(string? value) =>
        value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
        {
            return value;
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return $"{Prefix}{Convert.ToBase64String(payload)}";
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value))
        {
            return value;
        }

        try
        {
            var payload = Convert.FromBase64String(value[Prefix.Length..]);
            if (payload.Length <= 28)
            {
                throw new CryptographicException("Encrypted secret payload is too short.");
            }

            var nonce = payload[..12];
            var tag = payload[12..28];
            var ciphertext = payload[28..];
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, AssociatedData);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new InvalidOperationException("A configured module secret could not be decrypted. Check Security:SecretEncryptionKey.", ex);
        }
    }

    private static byte[] DecodeKey(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length is 32)
            {
                return bytes;
            }
        }
        catch (FormatException)
        {
        }

        if (value.Length >= 32)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(value));
        }

        throw new InvalidOperationException("Security:SecretEncryptionKey must be a base64 256-bit key or at least 32 characters.");
    }
}
