using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaskedCode.App.Masking;

public sealed class EncryptedMappingVault
{
    private const string FileFormat =
        "MaskedCode.MappingVault";

    private const int FileFormatVersion = 1;
    private const int SaltSizeInBytes = 16;
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;
    private const int KeySizeInBytes = 32;
    private const int Pbkdf2IterationCount = 600_000;

    private static readonly byte[] AdditionalAuthenticatedData =
        Encoding.UTF8.GetBytes(
            $"{FileFormat}|{FileFormatVersion}|" +
            $"PBKDF2-HMAC-SHA256|{Pbkdf2IterationCount}|" +
            "AES-256-GCM");

    public byte[] Encrypt(
        Pl1MaskingResult maskingResult,
        string password)
    {
        ArgumentNullException.ThrowIfNull(maskingResult);
        ArgumentNullException.ThrowIfNull(password);

        if (password.Length < 12)
        {
            throw new ArgumentException(
                "Kasa parolası en az 12 karakter olmalıdır.",
                nameof(password));
        }

        if (maskingResult.Mappings.Count == 0)
        {
            throw new InvalidOperationException(
                "Şifrelenecek maskeleme eşlemesi bulunamadı.");
        }

        var maskedCodeHash =
            CalculateSha256(maskingResult.MaskedCode);

        var payload = new MappingVaultPayload(
            DateTimeOffset.UtcNow,
            maskingResult.Mode,
            maskedCodeHash,
            maskingResult.Mappings);

        var plainText =
            JsonSerializer.SerializeToUtf8Bytes(payload);

        var salt =
            RandomNumberGenerator.GetBytes(
                SaltSizeInBytes);

        var nonce =
            RandomNumberGenerator.GetBytes(
                NonceSizeInBytes);

        var cipherText =
            new byte[plainText.Length];

        var authenticationTag =
            new byte[TagSizeInBytes];

        byte[] encryptionKey = [];

        try
        {
            encryptionKey =
                Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    Pbkdf2IterationCount,
                    HashAlgorithmName.SHA256,
                    KeySizeInBytes);

            using var aesGcm =
                new AesGcm(
                    encryptionKey,
                    TagSizeInBytes);

            aesGcm.Encrypt(
                nonce,
                plainText,
                cipherText,
                authenticationTag,
                AdditionalAuthenticatedData);

            var envelope = new MappingVaultEnvelope(
                FileFormat,
                FileFormatVersion,
                "PBKDF2-HMAC-SHA256",
                Pbkdf2IterationCount,
                "AES-256-GCM",
                Convert.ToBase64String(salt),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(authenticationTag),
                Convert.ToBase64String(cipherText));

            return JsonSerializer.SerializeToUtf8Bytes(
                envelope);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainText);

            if (encryptionKey.Length > 0)
            {
                CryptographicOperations.ZeroMemory(
                    encryptionKey);
            }
        }
    }

    private static string CalculateSha256(
        string value)
    {
        var valueBytes =
            Encoding.UTF8.GetBytes(value);

        try
        {
            return Convert.ToHexString(
                SHA256.HashData(valueBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                valueBytes);
        }
    }

    private sealed record MappingVaultPayload(
        DateTimeOffset CreatedAtUtc,
        MaskingMode MaskingMode,
        string MaskedCodeSha256,
        IReadOnlyList<MaskingMapping> Mappings);

    private sealed record MappingVaultEnvelope(
        string Format,
        int Version,
        string KeyDerivation,
        int Iterations,
        string Cipher,
        string Salt,
        string Nonce,
        string AuthenticationTag,
        string CipherText);
}