using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaskedCode.App.Masking;

public sealed class EncryptedMappingVault
{
    private const string FileFormat =
        "MaskedCode.MappingVault";

    private const string KeyDerivation =
        "PBKDF2-HMAC-SHA256";

    private const string Cipher =
        "AES-256-GCM";

    private const int FileFormatVersion = 1;
    private const int SaltSizeInBytes = 16;
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;
    private const int KeySizeInBytes = 32;
    private const int Pbkdf2IterationCount = 600_000;

    private const int MaximumVaultFileSizeInBytes =
        64 * 1024 * 1024;

    private static readonly byte[] AdditionalAuthenticatedData =
        Encoding.UTF8.GetBytes(
            $"{FileFormat}|{FileFormatVersion}|" +
            $"{KeyDerivation}|{Pbkdf2IterationCount}|" +
            Cipher);

    public byte[] Encrypt(
    IMaskingResult maskingResult,
    string password)
    {
        ArgumentNullException.ThrowIfNull(maskingResult);
        ValidatePassword(password);

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
                DeriveEncryptionKey(
                    password,
                    salt);

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
                KeyDerivation,
                Pbkdf2IterationCount,
                Cipher,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(authenticationTag),
                Convert.ToBase64String(cipherText));

            return JsonSerializer.SerializeToUtf8Bytes(
                envelope);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainText);

            if (encryptionKey.Length > 0)
            {
                CryptographicOperations.ZeroMemory(
                    encryptionKey);
            }
        }
    }

    public MappingVaultContent Decrypt(
        byte[] encryptedVault,
        string password,
        string maskedCode)
    {
        ArgumentNullException.ThrowIfNull(
            encryptedVault);

        ArgumentNullException.ThrowIfNull(
            maskedCode);

        ValidatePassword(password);

        if (encryptedVault.Length == 0)
        {
            throw new InvalidDataException(
                "Şifreli kasa dosyası boş.");
        }

        if (encryptedVault.Length >
            MaximumVaultFileSizeInBytes)
        {
            throw new InvalidDataException(
                "Şifreli kasa dosyası izin verilen " +
                "azami boyutu aşıyor.");
        }

        if (maskedCode.Length == 0)
        {
            throw new ArgumentException(
                "Kasa ile doğrulanacak maskelenmiş kod boş olamaz.",
                nameof(maskedCode));
        }

        var envelope =
            DeserializeEnvelope(encryptedVault);

        ValidateEnvelope(envelope);

        var salt =
            DecodeBase64(
                envelope.Salt,
                "Salt");

        var nonce =
            DecodeBase64(
                envelope.Nonce,
                "Nonce");

        var authenticationTag =
            DecodeBase64(
                envelope.AuthenticationTag,
                "AuthenticationTag");

        var cipherText =
            DecodeBase64(
                envelope.CipherText,
                "CipherText");

        ValidateCryptographicFieldLengths(
            salt,
            nonce,
            authenticationTag,
            cipherText);

        var plainText =
            new byte[cipherText.Length];

        byte[] encryptionKey = [];

        try
        {
            encryptionKey =
                DeriveEncryptionKey(
                    password,
                    salt);

            using var aesGcm =
                new AesGcm(
                    encryptionKey,
                    TagSizeInBytes);

            try
            {
                aesGcm.Decrypt(
                    nonce,
                    cipherText,
                    authenticationTag,
                    plainText,
                    AdditionalAuthenticatedData);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException(
                    "Kasa parolası yanlış veya kasa dosyası " +
                    "oluşturulduktan sonra değiştirilmiş.",
                    exception);
            }

            var payload =
                DeserializePayload(plainText);

            ValidatePayload(payload);

            var actualMaskedCodeHash =
                CalculateSha256(maskedCode);

            if (!string.Equals(
                    payload.MaskedCodeSha256,
                    actualMaskedCodeHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Seçilen şifreli kasa bu maskelenmiş " +
                    "kodla eşleşmiyor.");
            }

            return new MappingVaultContent(
                payload.CreatedAtUtc,
                payload.MaskingMode,
                payload.Mappings.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainText);

            if (encryptionKey.Length > 0)
            {
                CryptographicOperations.ZeroMemory(
                    encryptionKey);
            }
        }
    }

    private static void ValidatePassword(
        string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (password.Length < 12)
        {
            throw new ArgumentException(
                "Kasa parolası en az 12 karakter olmalıdır.",
                nameof(password));
        }
    }

    private static byte[] DeriveEncryptionKey(
        string password,
        byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Pbkdf2IterationCount,
            HashAlgorithmName.SHA256,
            KeySizeInBytes);
    }

    private static MappingVaultEnvelope
        DeserializeEnvelope(
            byte[] encryptedVault)
    {
        try
        {
            return JsonSerializer.Deserialize
                <MappingVaultEnvelope>(encryptedVault)
                ?? throw new InvalidDataException(
                    "Şifreli kasa dosyasının üst bilgisi bulunamadı.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Seçilen dosya geçerli bir MaskedCode " +
                "kasa dosyası değil.",
                exception);
        }
    }

    private static MappingVaultPayload
        DeserializePayload(
            byte[] plainText)
    {
        try
        {
            return JsonSerializer.Deserialize
                <MappingVaultPayload>(plainText)
                ?? throw new InvalidDataException(
                    "Şifreli kasa içeriği boş.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Şifreli kasa içeriği geçerli değil.",
                exception);
        }
    }

    private static void ValidateEnvelope(
        MappingVaultEnvelope envelope)
    {
        if (!string.Equals(
                envelope.Format,
                FileFormat,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Seçilen dosyanın kasa formatı desteklenmiyor.");
        }

        if (envelope.Version != FileFormatVersion)
        {
            throw new InvalidDataException(
                $"Kasa dosyası sürümü desteklenmiyor: " +
                $"{envelope.Version}");
        }

        if (!string.Equals(
                envelope.KeyDerivation,
                KeyDerivation,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki anahtar türetme " +
                "yöntemi desteklenmiyor.");
        }

        if (envelope.Iterations !=
            Pbkdf2IterationCount)
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki PBKDF2 iterasyon " +
                "değeri geçerli değil.");
        }

        if (!string.Equals(
                envelope.Cipher,
                Cipher,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki şifreleme " +
                "algoritması desteklenmiyor.");
        }
    }

    private static byte[] DecodeBase64(
        string value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Kasa dosyasındaki {fieldName} alanı boş.");
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                $"Kasa dosyasındaki {fieldName} alanı " +
                "geçerli Base64 biçiminde değil.",
                exception);
        }
    }

    private static void
        ValidateCryptographicFieldLengths(
            byte[] salt,
            byte[] nonce,
            byte[] authenticationTag,
            byte[] cipherText)
    {
        if (salt.Length != SaltSizeInBytes)
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki salt uzunluğu geçerli değil.");
        }

        if (nonce.Length != NonceSizeInBytes)
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki nonce uzunluğu geçerli değil.");
        }

        if (authenticationTag.Length !=
            TagSizeInBytes)
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki doğrulama etiketi " +
                "uzunluğu geçerli değil.");
        }

        if (cipherText.Length == 0)
        {
            throw new InvalidDataException(
                "Kasa dosyasındaki şifreli içerik boş.");
        }
    }

    private static void ValidatePayload(
        MappingVaultPayload payload)
    {
        if (!Enum.IsDefined(
                typeof(MaskingMode),
                payload.MaskingMode))
        {
            throw new InvalidDataException(
                "Kasa içindeki maskeleme modu geçerli değil.");
        }

        ValidateSha256(
            payload.MaskedCodeSha256);

        if (payload.Mappings is null ||
            payload.Mappings.Count == 0)
        {
            throw new InvalidDataException(
                "Kasa içinde maskeleme eşlemesi bulunamadı.");
        }

        foreach (var mapping in payload.Mappings)
        {
            if (mapping is null)
            {
                throw new InvalidDataException(
                    "Kasa içinde geçersiz bir eşleme bulundu.");
            }

            if (!Enum.IsDefined(
                    typeof(MaskingValueKind),
                    mapping.Kind))
            {
                throw new InvalidDataException(
                    "Kasa içindeki eşleme türlerinden " +
                    "biri geçerli değil.");
            }

            if (string.IsNullOrEmpty(
                    mapping.OriginalValue) ||
                string.IsNullOrEmpty(
                    mapping.MaskedValue))
            {
                throw new InvalidDataException(
                    "Kasa içinde boş özgün veya maskelenmiş " +
                    "değere sahip bir eşleme bulundu.");
            }
        }
    }

    private static void ValidateSha256(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 64)
        {
            throw new InvalidDataException(
                "Kasa içindeki maskelenmiş kod özeti " +
                "geçerli değil.");
        }

        try
        {
            _ = Convert.FromHexString(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "Kasa içindeki maskelenmiş kod özeti " +
                "geçerli SHA-256 biçiminde değil.",
                exception);
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