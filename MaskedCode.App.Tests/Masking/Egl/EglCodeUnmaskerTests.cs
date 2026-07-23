using MaskedCode.App.Masking;
using MaskedCode.App.Masking.Egl;

namespace MaskedCode.App.Tests.Masking.Egl;

public sealed class EglCodeUnmaskerTests
{
    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void MaskAndUnmask_WithRealisticEglContent_ShouldRestoreExactSource(MaskingMode mode)
    {
        const string sourceCode =
            """
            package com.company.customer;

            #doc{
                Confidential customer program.
                Internal rule: {CUSTOMER_STATUS}
            }

            program CUSTOMERPROGRAM type BasicProgram

                // Confidential initialization rule
                CustomerName string;
                StatusText string;

                function main()
                    CustomerName = "PRIVATE \"CUSTOMER\"";

                    /* Internal customer lookup */
                    get CustomerRecord with #sql{
                        SELECT CUSTOMER_NO,
                               CUSTOMER_NAME
                          FROM MY_SCHEMA.CUSTOMER
                         WHERE STATUS = 'ACTIVE'
                           AND LAST_NAME = 'O''BRIEN'
                           AND BRANCH_NO = 1453
                        -- Confidential SQL filter } 'SECRET'
                         FETCH FIRST 25 ROWS ONLY
                         WITH UR
                    };
                end

            end
            """;

        var masker =
            new EglCodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                mode);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings);

        var unmasker =
            new EglCodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void Unmask_WithUnusedVaultMapping_ShouldRejectVaultContent()
    {
        const string maskedCode =
            """
            program EGL_TEST_0001
            end
            """;

        var mappings =
            new[]
            {
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    "CUSTOMERPROGRAM",
                    "EGL_TEST_0001"),

                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    "CUSTOMERRECORD",
                    "EGL_TEST_0002")
            };

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                MaskingMode.MaximumPrivacy,
                mappings);

        var unmasker =
            new EglCodeUnmasker();

        var exception =
            Assert.Throws<InvalidDataException>(
                () => unmasker.Unmask(
                    maskedCode,
                    vaultContent));

        Assert.Contains(
            "Kasa içindeki bir eşleme maskelenmiş EGL kodunda bulunamadı.",
            exception.Message);
    }

    [Fact]
    public void Unmask_WithDuplicateMaskedValues_ShouldRejectAmbiguousMappings()
    {
        const string maskedCode =
            """
            program EGL_TEST_0001
            end
            """;

        var mappings =
            new[]
            {
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    "CUSTOMERPROGRAM",
                    "EGL_TEST_0001"),

                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    "ACCOUNTPROGRAM",
                    "EGL_TEST_0001")
            };

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                MaskingMode.MaximumPrivacy,
                mappings);

        var unmasker =
            new EglCodeUnmasker();

        var exception =
            Assert.Throws<InvalidDataException>(
                () => unmasker.Unmask(
                    maskedCode,
                    vaultContent));

        Assert.Contains(
            "Kasa içinde aynı maskelenmiş değere sahip birden fazla eşleme bulundu.",
            exception.Message);
    }
}