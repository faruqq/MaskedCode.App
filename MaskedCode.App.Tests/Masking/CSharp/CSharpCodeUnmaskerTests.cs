using MaskedCode.App.Masking;
using MaskedCode.App.Masking.CSharp;
using System.IO;

namespace MaskedCode.App.Tests.Masking.CSharp;

public sealed class CSharpCodeUnmaskerTests
{
    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void MaskAndUnmask_WithSupportedTokens_ShouldRestoreExactSource(
        MaskingMode mode)
    {
        const string sourceCode =
            """"
            namespace Company.Customer;

            public sealed class CustomerService
            {
                public string GetCustomerName(
                    int customerNumber,
                    decimal accountBalance)
                {
                    var customerName =
                        $"PrivateCustomer-{customerNumber}-100";

                    var rawMessage =
                        """Internal customer message""";

                    var statusCode = 'A';
                    var limit = 1453;
                    var rate = 15.75M;

                    return customerName;
                }
            }
            """";

        var masker =
            new CSharpCodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                mode);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings,
                SourceLanguage.CSharp);

        var unmasker =
            new CSharpCodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.NotEqual(
            sourceCode,
            maskingResult.MaskedCode);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Fact]
    public void Unmask_WithNonCSharpVault_ShouldRejectVaultContent()
    {
        const string maskedCode =
            """
            public sealed class CS_TEST_0001
            {
            }
            """;

        var mappings =
            new[]
            {
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    "CustomerService",
                    "CS_TEST_0001")
            };

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                MaskingMode.MaximumPrivacy,
                mappings,
                SourceLanguage.Egl);

        var unmasker =
            new CSharpCodeUnmasker();

        var exception =
            Assert.Throws<InvalidDataException>(() =>
                unmasker.Unmask(
                    maskedCode,
                    vaultContent));

        Assert.Equal(
            "Seçilen kasa C# kaynak koduna ait değil.",
            exception.Message);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void MaskAndUnmask_WithComments_ShouldRestoreExactSource(
        MaskingMode mode)
    {
        const string sourceCode =
            """
            namespace Company.Customer;

            /// <summary>
            /// Returns the internal customer name.
            /// </summary>
            public sealed class CustomerService
            {
                // Internal customer lookup
                public string GetCustomerName()
                {
                    /*
                     * Private customer information
                     */
                    return "InternalCustomer";
                }
            }
            """;

        var masker =
            new CSharpCodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                mode);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings,
                SourceLanguage.CSharp);

        var unmasker =
            new CSharpCodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.NotEqual(
            sourceCode,
            maskingResult.MaskedCode);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void MaskAndUnmask_WithDirectives_ShouldRestoreExactSource(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        #define INTERNAL_CUSTOMER_FEATURE
        #if INTERNAL_CUSTOMER_FEATURE
        #endif
        #undef INTERNAL_CUSTOMER_FEATURE

        public sealed class CustomerService
        {
            public string GetCustomerName()
            {
                return "InternalCustomer";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var maskingResult =
            masker.Mask(
                sourceCode,
                mode);

        var vaultContent =
            new MappingVaultContent(
                DateTimeOffset.UtcNow,
                maskingResult.Mode,
                maskingResult.Mappings,
                SourceLanguage.CSharp);

        var unmasker =
            new CSharpCodeUnmasker();

        var restoredCode =
            unmasker.Unmask(
                maskingResult.MaskedCode,
                vaultContent);

        Assert.NotEqual(
            sourceCode,
            maskingResult.MaskedCode);

        Assert.Equal(
            sourceCode,
            restoredCode);
    }
}