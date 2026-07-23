using MaskedCode.App.Masking;
using MaskedCode.App.Masking.Egl;
using MaskedCode.App.Masking.Pl1;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace MaskedCode.App.Tests.Masking;

public sealed class MaskingWorkflowTests
{
    private const string VaultPassword = "Strong-Test-Password-123!";
    private const string SecurityTestSourceCode =
    """
     DCL CUSTOMER_NO FIXED DECIMAL(10);

     CUSTOMER_NO = 1234567890;
     CALL WRITE_CUSTOMER;
    """;

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void
        MaskEncryptDecryptAndUnmask_WithValidPl1Code_ShouldRestoreOriginalCode(
            MaskingMode maskingMode)
    {
        const string sourceCode =
            """
             /*
              Gerçek müşteri bilgilerini işler.
             */
             DCL CUSTOMER_NO FIXED DECIMAL(10)
                 INIT(1234567890);
             DCL CUSTOMER_NAME CHAR(40)
                 INIT('FARUK YAZICI');
             DCL ACCOUNT_BALANCE FIXED DECIMAL(15,2);

             CUSTOMER_NO = 9876543210;
             CUSTOMER_NAME = 'PRIVATE CUSTOMER';
             ACCOUNT_BALANCE = 15478.35;

             IF ACCOUNT_BALANCE > 10000.00 THEN
                 CALL WRITE_CUSTOMER;
            """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                maskingMode);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var vaultContent =
            vault.Decrypt(
                encryptedVault,
                VaultPassword,
                maskingResult.MaskedCode);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.NotEqual(
            sourceCode,
            maskingResult.MaskedCode);

        Assert.NotEmpty(
            maskingResult.Mappings);

        Assert.Equal(
            maskingMode,
            vaultContent.MaskingMode);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    Decrypt_WithWrongPassword_ShouldRejectVault()
    {
        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                SecurityTestSourceCode,
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var exception =
            Assert.Throws<InvalidDataException>(
                () => vault.Decrypt(
                    encryptedVault,
                    "Different-Test-Password-456!",
                    maskingResult.MaskedCode));

        Assert.Contains(
            "Kasa parolası yanlış veya kasa dosyası " +
            "oluşturulduktan sonra değiştirilmiş.",
            exception.Message);
    }

    [Fact]
    public void
    Decrypt_WithModifiedMaskedCode_ShouldRejectVaultCodePair()
    {
        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                SecurityTestSourceCode,
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var modifiedMaskedCode =
            maskingResult.MaskedCode + " ";

        var exception =
            Assert.Throws<InvalidDataException>(
                () => vault.Decrypt(
                    encryptedVault,
                    VaultPassword,
                    modifiedMaskedCode));

        Assert.Contains(
            "Seçilen şifreli kasa bu maskelenmiş " +
            "kodla eşleşmiyor.",
            exception.Message);
    }

    [Fact]
    public void
    Decrypt_WithModifiedCipherText_ShouldRejectVault()
    {
        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                SecurityTestSourceCode,
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var envelope =
            JsonNode.Parse(encryptedVault)!
                .AsObject();

        var cipherText =
            envelope["CipherText"]!
                .GetValue<string>();

        var cipherTextBytes =
            Convert.FromBase64String(cipherText);

        cipherTextBytes[0] ^= 0x01;

        envelope["CipherText"] =
            Convert.ToBase64String(cipherTextBytes);

        var modifiedVault =
            JsonSerializer.SerializeToUtf8Bytes(
                envelope);

        var exception =
            Assert.Throws<InvalidDataException>(
                () => vault.Decrypt(
                    modifiedVault,
                    VaultPassword,
                    maskingResult.MaskedCode));

        Assert.Contains(
            "Kasa parolası yanlış veya kasa dosyası " +
            "oluşturulduktan sonra değiştirilmiş.",
            exception.Message);
    }

    [Fact]
    public void
    Decrypt_WithVaultBelongingToDifferentCode_ShouldRejectVaultCodePair()
    {
        const string firstSourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10);

         CUSTOMER_NO = 1234567890;
        """;

        const string secondSourceCode =
            """
         DCL ACCOUNT_NO FIXED DECIMAL(12);

         ACCOUNT_NO = 987654321012;
        """;

        var masker =
            new Pl1CodeMasker();

        var firstMaskingResult =
            masker.Mask(
                firstSourceCode,
                MaskingMode.MaximumPrivacy);

        var secondMaskingResult =
            masker.Mask(
                secondSourceCode,
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var firstEncryptedVault =
            vault.Encrypt(
                firstMaskingResult,
                VaultPassword);

        var exception =
            Assert.Throws<InvalidDataException>(
                () => vault.Decrypt(
                    firstEncryptedVault,
                    VaultPassword,
                    secondMaskingResult.MaskedCode));

        Assert.Contains(
            "Seçilen şifreli kasa bu maskelenmiş " +
            "kodla eşleşmiyor.",
            exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-masked-code-vault")]
    public void
    Decrypt_WithEmptyOrInvalidVaultData_ShouldRejectVault(
        string vaultData)
    {
        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            Encoding.UTF8.GetBytes(
                vaultData);

        Assert.Throws<InvalidDataException>(
            () => vault.Decrypt(
                encryptedVault,
                VaultPassword,
                "MASKED_CODE"));
    }

    [Fact]
    public void
    MaskAndUnmask_WithRepeatedIdentifier_ShouldUseSameMapping()
    {
        const string sourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10);

         CUSTOMER_NO = 1234567890;

         IF CUSTOMER_NO > 0 THEN
             CALL WRITE_CUSTOMER;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var customerNumberMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CUSTOMER_NO"));

        var maskedUsageCount =
            maskingResult.MaskedCode
                .Split(
                    customerNumberMapping.MaskedValue,
                    StringSplitOptions.None)
                .Length - 1;

        Assert.Equal(
            3,
            maskedUsageCount);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    MaskAndUnmask_WithSimilarIdentifiers_ShouldPreserveBoundaries()
    {
        const string sourceCode =
            """
         DCL CUSTOMER FIXED DECIMAL(10);
         DCL CUSTOMER_NO FIXED DECIMAL(10);

         CUSTOMER = 12345;
         CUSTOMER_NO = 1234567890;

         CALL WRITE_CUSTOMER;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var customerMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CUSTOMER"));

        var customerNumberMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CUSTOMER_NO"));

        Assert.NotEqual(
            customerMapping.MaskedValue,
            customerNumberMapping.MaskedValue);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    MaskAndUnmask_WithArrayDeclarationAndRuntimeIndex_ShouldPreserveStructuralNumbers()
    {
        const string sourceCode =
            """
         DCL CUSTOMER_LIST(20) CHAR(30);

         CUSTOMER_LIST(2) = 'PRIVATE CUSTOMER';
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var numericMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral));

        Assert.Equal(
            "2",
            numericMapping.OriginalValue);

        Assert.DoesNotContain(
            maskingResult.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.NumericLiteral &&
                mapping.OriginalValue is "20" or "30");

        Assert.NotEqual(
            "2",
            numericMapping.MaskedValue);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    MaskAndUnmask_WithDeclarationInitialValues_ShouldMaskInitValues()
    {
        const string sourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10)
             INIT(1234567890);
         DCL CUSTOMER_NAME CHAR(40)
             INIT('PRIVATE CUSTOMER');
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        Assert.Contains(
            maskingResult.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.NumericLiteral &&
                mapping.OriginalValue ==
                    "1234567890");

        Assert.Contains(
            maskingResult.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.StringLiteral &&
                mapping.OriginalValue ==
                    "PRIVATE CUSTOMER");

        Assert.DoesNotContain(
            "1234567890",
            maskingResult.MaskedCode);

        Assert.DoesNotContain(
            "PRIVATE CUSTOMER",
            maskingResult.MaskedCode);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void
    MaskAndUnmask_WithCommentsAndEscapedQuote_ShouldRestoreExactCode(
        MaskingMode maskingMode)
    {
        const string sourceCode =
            """
         /*
          CUSTOMER_NO alanı gerçek müşteri numarasını içerir.
          Bu bilgi şirket dışına çıkarılmamalıdır.
         */
         DCL CUSTOMER_NO FIXED DECIMAL(10);
         DCL ERROR_TEXT CHAR(80);

         CUSTOMER_NO = 1234567890;
         ERROR_TEXT = 'CUSTOMER''S ACCOUNT WAS NOT FOUND';

         /*
          1234567890 numaralı müşteri için hata oluştu.
         */
         CALL WRITE_ERROR;
         CALL WRITE_ERROR;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                maskingMode);

        var commentMappings =
            maskingResult.Mappings.Where(
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Comment);

        Assert.Equal(
            2,
            commentMappings.Count());

        Assert.Contains(
            maskingResult.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.StringLiteral &&
                mapping.OriginalValue ==
                    "CUSTOMER''S ACCOUNT WAS NOT FOUND");

        Assert.DoesNotContain(
            "gerçek müşteri numarasını",
            maskingResult.MaskedCode);

        Assert.DoesNotContain(
            "CUSTOMER''S ACCOUNT WAS NOT FOUND",
            maskingResult.MaskedCode);

        Assert.Equal(
            sourceCode.Count(
                character => character == '\n'),
            maskingResult.MaskedCode.Count(
                character => character == '\n'));

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    MaskAndUnmask_WithProcedureAndScientificLiteral_ShouldPreserveExponent()
    {
        const string sourceCode =
            """
         DCL CALCULATION_RATE FLOAT;

         CUSTOMER_PROCESS: PROCEDURE;
             CALCULATION_RATE = 1.25E+03;
             CALL WRITE_CUSTOMER_NOTE;
         END CUSTOMER_PROCESS;

         CALL CUSTOMER_PROCESS;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var procedureMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CUSTOMER_PROCESS"));

        var procedureUsageCount =
            maskingResult.MaskedCode
                .Split(
                    procedureMapping.MaskedValue,
                    StringSplitOptions.None)
                .Length - 1;

        Assert.Equal(
            3,
            procedureUsageCount);

        var scientificMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "1.25E+03"));

        Assert.NotEqual(
            scientificMapping.OriginalValue,
            scientificMapping.MaskedValue);

        Assert.EndsWith(
            "E+03",
            scientificMapping.MaskedValue);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    Mask_WithFormatPreservingMode_ShouldPreserveValueFormats()
    {
        const string sourceCode =
            """
         DCL Customer_No_01 FIXED DECIMAL(10);
         DCL Customer_Name CHAR(40);

         Customer_No_01 = 1234567890;
         Customer_Name = 'Private Customer 123';
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.FormatPreserving);

        var identifierMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "Customer_No_01"));

        var stringMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue ==
                            "Private Customer 123"));

        var numericMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "1234567890"));

        Assert.Equal(
            identifierMapping.OriginalValue.Length,
            identifierMapping.MaskedValue.Length);

        Assert.Equal(
            stringMapping.OriginalValue.Length,
            stringMapping.MaskedValue.Length);

        Assert.Equal(
            numericMapping.OriginalValue.Length,
            numericMapping.MaskedValue.Length);

        for (var index = 0;
             index < identifierMapping.OriginalValue.Length;
             index++)
        {
            var originalCharacter =
                identifierMapping.OriginalValue[index];

            var maskedCharacter =
                identifierMapping.MaskedValue[index];

            Assert.Equal(
                char.IsUpper(originalCharacter),
                char.IsUpper(maskedCharacter));

            Assert.Equal(
                char.IsLower(originalCharacter),
                char.IsLower(maskedCharacter));

            Assert.Equal(
                char.IsDigit(originalCharacter),
                char.IsDigit(maskedCharacter));

            if (!char.IsLetterOrDigit(originalCharacter))
            {
                Assert.Equal(
                    originalCharacter,
                    maskedCharacter);
            }
        }

        for (var index = 0;
             index < stringMapping.OriginalValue.Length;
             index++)
        {
            var originalCharacter =
                stringMapping.OriginalValue[index];

            var maskedCharacter =
                stringMapping.MaskedValue[index];

            Assert.Equal(
                char.IsLetter(originalCharacter),
                char.IsLetter(maskedCharacter));

            Assert.Equal(
                char.IsDigit(originalCharacter),
                char.IsDigit(maskedCharacter));

            if (!char.IsLetterOrDigit(originalCharacter))
            {
                Assert.Equal(
                    originalCharacter,
                    maskedCharacter);
            }
        }

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    Mask_WithPl1Keywords_ShouldNotMaskLanguageKeywords()
    {
        const string sourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10);

         IF CUSTOMER_NO > 0 THEN
             CALL WRITE_CUSTOMER;
         ELSE
             RETURN;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var keywords =
            new[]
            {
            "DCL",
            "FIXED",
            "DECIMAL",
            "IF",
            "THEN",
            "CALL",
            "ELSE",
            "RETURN"
            };

        foreach (var keyword in keywords)
        {
            Assert.DoesNotContain(
                maskingResult.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue.Equals(
                        keyword,
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                keyword,
                maskingResult.MaskedCode);
        }

        Assert.Contains(
            maskingResult.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.Identifier &&
                mapping.OriginalValue ==
                    "CUSTOMER_NO");

        Assert.Contains(
            maskingResult.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.Identifier &&
                mapping.OriginalValue ==
                    "WRITE_CUSTOMER");

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
    Unmask_WithUnusedVaultMapping_ShouldRejectVaultContent()
    {
        const string maskedCode =
            """
         DCL MC_TEST_0001 FIXED DECIMAL(10);
        """;

        var mappings =
            new[]
            {
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "CUSTOMER_NO",
                "MC_TEST_0001"),

            new MaskingMapping(
                MaskingValueKind.Identifier,
                "ACCOUNT_NO",
                "MC_TEST_0002")
            };

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                MaskingMode.MaximumPrivacy,
                mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var exception =
            Assert.Throws<InvalidDataException>(
                () => unmasker.Unmask(
                    maskedCode,
                    vaultContent));

        Assert.Contains(
            "Kasa içindeki bir eşleme maskelenmiş " +
            "kodda bulunamadı.",
            exception.Message);
    }

    [Fact]
    public void
    Unmask_WithDuplicateMaskedValues_ShouldRejectAmbiguousMappings()
    {
        const string maskedCode =
            """
         DCL MC_TEST_0001 FIXED DECIMAL(10);
        """;

        var mappings =
            new[]
            {
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "CUSTOMER_NO",
                "MC_TEST_0001"),

            new MaskingMapping(
                MaskingValueKind.Identifier,
                "ACCOUNT_NO",
                "MC_TEST_0001")
            };

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                MaskingMode.MaximumPrivacy,
                mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var exception =
            Assert.Throws<InvalidDataException>(
                () => unmasker.Unmask(
                    maskedCode,
                    vaultContent));

        Assert.Contains(
            "Kasa içinde aynı maskelenmiş değere " +
            "sahip birden fazla eşleme bulundu.",
            exception.Message);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void
MaskAndUnmask_WithEmbeddedSql_ShouldPreserveSqlKeywordsAndRestoreExactCode(
    MaskingMode maskingMode)
    {
        const string sourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10)
             INIT(1234567890);
         DCL ACCOUNT_STATUS CHAR(2)
             INIT('AC');
         DCL CURRENT_BALANCE FIXED DECIMAL(15,2)
             INIT(0);
         DCL SQLCODE FIXED DECIMAL(10)
             INIT(0);

         READ_ACCOUNT: PROCEDURE;
             EXEC SQL
                 SELECT ACCOUNT_STATUS, CURRENT_BALANCE
                   INTO :ACCOUNT_STATUS, :CURRENT_BALANCE
                   FROM COREBANK.CUSTOMER_ACCOUNT
                  WHERE CUSTOMER_NO = :CUSTOMER_NO
                    AND BRANCH_CODE = 'TR01';

             IF SQLCODE < 0 THEN
                 ACCOUNT_STATUS = 'ER';

             EXEC SQL
                 UPDATE COREBANK.CUSTOMER_ACCOUNT
                    SET ACCOUNT_STATUS = 'BL'
                  WHERE CUSTOMER_NO = :CUSTOMER_NO;
         END READ_ACCOUNT;

         CALL READ_ACCOUNT;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                maskingMode);

        var sqlKeywords =
            new[]
            {
            "EXEC",
            "SQL",
            "SELECT",
            "INTO",
            "FROM",
            "WHERE",
            "AND",
            "UPDATE",
            "SET"
            };

        foreach (var sqlKeyword in sqlKeywords)
        {
            Assert.DoesNotContain(
                maskingResult.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue.Equals(
                        sqlKeyword,
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                sqlKeyword,
                maskingResult.MaskedCode);
        }

        var schemaMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "COREBANK"));

        var tableMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CUSTOMER_ACCOUNT"));

        var accountStatusMapping =
            Assert.Single(
                maskingResult.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "ACCOUNT_STATUS"));

        Assert.DoesNotContain(
            "COREBANK",
            maskingResult.MaskedCode);

        Assert.DoesNotContain(
            "CUSTOMER_ACCOUNT",
            maskingResult.MaskedCode);

        Assert.Contains(
            $"{schemaMapping.MaskedValue}." +
            tableMapping.MaskedValue,
            maskingResult.MaskedCode);

        Assert.Contains(
            $":{accountStatusMapping.MaskedValue}",
            maskingResult.MaskedCode);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void
EncryptAndDecrypt_WithEglMaskingResult_ShouldPreserveMappingsAndMode()
    {
        const string maskedCode =
            """
        package PKG_0001;

        program PGM_0001
        end
        """;

        var mappings =
            new[]
            {
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "com.company.programs",
                "PKG_0001"),
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "MYPROGRAMNAME",
                "PGM_0001")
            };

        var maskingResult =
            new EglMaskingResult(
                maskedCode,
                mappings,
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var vaultContent =
            vault.Decrypt(
                encryptedVault,
                VaultPassword,
                maskedCode);

        Assert.Equal(
            MaskingMode.MaximumPrivacy,
            vaultContent.MaskingMode);

        Assert.Equal(
            mappings,
            vaultContent.Mappings);
    }

    [Fact]
    public void
Decrypt_WithEglVaultAndModifiedMaskedCode_ShouldRejectVaultCodePair()
    {
        const string maskedCode =
            """
        program PGM_0001
        end
        """;

        var maskingResult =
            new EglMaskingResult(
                maskedCode,
                new[]
                {
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    "MYPROGRAMNAME",
                    "PGM_0001")
                },
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var modifiedMaskedCode =
            maskedCode.Replace(
                "PGM_0001",
                "PGM_0002",
                StringComparison.Ordinal);

        var exception =
            Assert.Throws<InvalidDataException>(
                () => vault.Decrypt(
                    encryptedVault,
                    VaultPassword,
                    modifiedMaskedCode));

        Assert.Contains(
            "Seçilen şifreli kasa bu maskelenmiş " +
            "kodla eşleşmiyor.",
            exception.Message);
    }

    [Fact]
    public void
Encrypt_WithEglMaskingResultWithoutMappings_ShouldRejectResult()
    {
        var maskingResult =
            new EglMaskingResult(
                "program MYPROGRAMNAME end",
                Array.Empty<MaskingMapping>(),
                MaskingMode.FormatPreserving);

        var vault =
            new EncryptedMappingVault();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => vault.Encrypt(
                    maskingResult,
                    VaultPassword));

        Assert.Contains(
            "Şifrelenecek maskeleme eşlemesi bulunamadı.",
            exception.Message);
    }

    [Fact]
    public void
EglMaskingResult_WithDifferentMappingKinds_ShouldCalculateCounts()
    {
        var mappings =
            new[]
            {
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "MYPROGRAMNAME",
                "PGM_0001"),
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "MyProgramNameInput",
                "REC_0001"),
            new MaskingMapping(
                MaskingValueKind.StringLiteral,
                "Gerçek hata mesajı",
                "STR_0001"),
            new MaskingMapping(
                MaskingValueKind.NumericLiteral,
                "001",
                "742"),
            new MaskingMapping(
                MaskingValueKind.Comment,
                "Gerçek iş kuralı açıklaması",
                "COMMENT_0001")
            };

        var maskingResult =
            new EglMaskingResult(
                "MASKED_EGL_CODE",
                mappings,
                MaskingMode.MaximumPrivacy);

        Assert.Equal(
            2,
            maskingResult.IdentifierCount);

        Assert.Equal(
            1,
            maskingResult.StringLiteralCount);

        Assert.Equal(
            1,
            maskingResult.NumericLiteralCount);

        Assert.Equal(
            1,
            maskingResult.CommentCount);
    }

    [Fact]
    public void
EncryptAndDecrypt_WithPl1ResultAsCommonContract_ShouldPreserveMappings()
    {
        const string maskedCode =
            """
         DCL ID_0001 FIXED DECIMAL(10);

         ID_0001 = NUM_0001;
        """;

        var mappings =
            new[]
            {
            new MaskingMapping(
                MaskingValueKind.Identifier,
                "CUSTOMER_NO",
                "ID_0001"),
            new MaskingMapping(
                MaskingValueKind.NumericLiteral,
                "1234567890",
                "NUM_0001")
            };

        IMaskingResult maskingResult =
            new Pl1MaskingResult(
                maskedCode,
                mappings,
                MaskingMode.MaximumPrivacy);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var vaultContent =
            vault.Decrypt(
                encryptedVault,
                VaultPassword,
                maskedCode);

        Assert.Equal(
            MaskingMode.MaximumPrivacy,
            vaultContent.MaskingMode);

        Assert.Equal(
            mappings,
            vaultContent.Mappings);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void EncryptAndDecrypt_WithPl1MaskingResult_ShouldPreserveSourceLanguage(MaskingMode maskingMode)
    {
        const string sourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10);

         CUSTOMER_NO = 1234567890;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                maskingMode);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var vaultContent =
            vault.Decrypt(
                encryptedVault,
                VaultPassword,
                maskingResult.MaskedCode);

        Assert.Equal(
            SourceLanguage.Pl1,
            maskingResult.SourceLanguage);

        Assert.Equal(
            SourceLanguage.Pl1,
            vaultContent.SourceLanguage);

        Assert.Equal(
            maskingMode,
            vaultContent.MaskingMode);

        Assert.Equal(
            maskingResult.Mappings,
            vaultContent.Mappings);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void EncryptAndDecrypt_WithEglMaskingResult_ShouldPreserveSourceLanguage(MaskingMode maskingMode)
    {
        const string sourceCode =
            """
        package com.company.customer;

        program CUSTOMERPROGRAM type BasicProgram

            function main()
                CustomerName = "PRIVATE CUSTOMER";
            end

        end
        """;

        var masker =
            new EglCodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                maskingMode);

        var vault =
            new EncryptedMappingVault();

        var encryptedVault =
            vault.Encrypt(
                maskingResult,
                VaultPassword);

        var vaultContent =
            vault.Decrypt(
                encryptedVault,
                VaultPassword,
                maskingResult.MaskedCode);

        Assert.Equal(
            SourceLanguage.Egl,
            maskingResult.SourceLanguage);

        Assert.Equal(
            SourceLanguage.Egl,
            vaultContent.SourceLanguage);

        Assert.Equal(
            maskingMode,
            vaultContent.MaskingMode);

        Assert.Equal(
            maskingResult.Mappings,
            vaultContent.Mappings);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Decrypt_WithLegacyVaultWithoutSourceLanguage_ShouldTreatVaultAsPl1(MaskingMode maskingMode)
    {
        const string sourceCode =
            """
         DCL CUSTOMER_NO FIXED DECIMAL(10);

         CUSTOMER_NO = 1234567890;
         CALL WRITE_CUSTOMER;
        """;

        var masker =
            new Pl1CodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                maskingMode);

        var legacyEncryptedVault =
            CreateLegacyEncryptedVault(
                maskingResult.MaskedCode,
                maskingResult.Mappings,
                maskingResult.Mode);

        var vault =
            new EncryptedMappingVault();

        var vaultContent =
            vault.Decrypt(
                legacyEncryptedVault,
                VaultPassword,
                maskingResult.MaskedCode);

        Assert.Equal(
            SourceLanguage.Pl1,
            vaultContent.SourceLanguage);

        Assert.Equal(
            maskingMode,
            vaultContent.MaskingMode);

        Assert.Equal(
            maskingResult.Mappings,
            vaultContent.Mappings);

        var unmasker =
            new Pl1CodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    private static byte[] CreateLegacyEncryptedVault(string maskedCode, IReadOnlyList<MaskingMapping> mappings, MaskingMode maskingMode)
    {
        const string fileFormat =
            "MaskedCode.MappingVault";

        const string keyDerivation =
            "PBKDF2-HMAC-SHA256";

        const string cipher =
            "AES-256-GCM";

        const int fileFormatVersion = 1;
        const int pbkdf2IterationCount = 600_000;
        const int saltSizeInBytes = 16;
        const int nonceSizeInBytes = 12;
        const int tagSizeInBytes = 16;
        const int keySizeInBytes = 32;

        var maskedCodeBytes =
            Encoding.UTF8.GetBytes(maskedCode);

        string maskedCodeHash;

        try
        {
            maskedCodeHash =
                Convert.ToHexString(
                    SHA256.HashData(maskedCodeBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                maskedCodeBytes);
        }

        var payload =
            new LegacyMappingVaultPayload(
                DateTimeOffset.UtcNow,
                maskingMode,
                maskedCodeHash,
                mappings);

        var plainText =
            JsonSerializer.SerializeToUtf8Bytes(
                payload);

        var salt =
            RandomNumberGenerator.GetBytes(
                saltSizeInBytes);

        var nonce =
            RandomNumberGenerator.GetBytes(
                nonceSizeInBytes);

        var cipherText =
            new byte[plainText.Length];

        var authenticationTag =
            new byte[tagSizeInBytes];

        var additionalAuthenticatedData =
            Encoding.UTF8.GetBytes(
                $"{fileFormat}|{fileFormatVersion}|" +
                $"{keyDerivation}|{pbkdf2IterationCount}|" +
                cipher);

        byte[] encryptionKey = [];

        try
        {
            encryptionKey =
                Rfc2898DeriveBytes.Pbkdf2(
                    VaultPassword,
                    salt,
                    pbkdf2IterationCount,
                    HashAlgorithmName.SHA256,
                    keySizeInBytes);

            using var aesGcm =
                new AesGcm(
                    encryptionKey,
                    tagSizeInBytes);

            aesGcm.Encrypt(
                nonce,
                plainText,
                cipherText,
                authenticationTag,
                additionalAuthenticatedData);

            var envelope =
                new LegacyMappingVaultEnvelope(
                    fileFormat,
                    fileFormatVersion,
                    keyDerivation,
                    pbkdf2IterationCount,
                    cipher,
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

            CryptographicOperations.ZeroMemory(
                additionalAuthenticatedData);

            if (encryptionKey.Length > 0)
            {
                CryptographicOperations.ZeroMemory(
                    encryptionKey);
            }
        }
    }

    private sealed record LegacyMappingVaultPayload(
        DateTimeOffset CreatedAtUtc,
        MaskingMode MaskingMode,
        string MaskedCodeSha256,
        IReadOnlyList<MaskingMapping> Mappings);

    private sealed record LegacyMappingVaultEnvelope(
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