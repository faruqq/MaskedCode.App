using MaskedCode.App.Masking;
using MaskedCode.App.Masking.CSharp;
using Microsoft.CodeAnalysis.CSharp;

namespace MaskedCode.App.Tests.Masking.CSharp;

public sealed class CSharpCodeMaskerTests
{
    [Fact]
    public void Mask_WithIdentifiers_ShouldMaskIdentifiersAndPreserveKeywords()
    {
        const string sourceCode =
            """
            namespace CustomerManagement;

            public sealed class CustomerService
            {
                private readonly string customerNumber;

                public string GetCustomerNumber()
                {
                    return customerNumber;
                }
            }
            """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var expectedIdentifiers =
            new[]
            {
                "CustomerManagement",
                "CustomerService",
                "customerNumber",
                "GetCustomerNumber"
            };

        foreach (var identifier in expectedIdentifiers)
        {
            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind == MaskingValueKind.Identifier &&
                    mapping.OriginalValue == identifier);

            Assert.DoesNotContain(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        var preservedKeywords =
            new[]
            {
                "namespace",
                "public",
                "sealed",
                "class",
                "private",
                "readonly",
                "string",
                "return"
            };

        foreach (var keyword in preservedKeywords)
        {
            Assert.DoesNotContain(
                result.Mappings,
                mapping =>
                    mapping.OriginalValue == keyword);

            Assert.Contains(
                keyword,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        Assert.Equal(
            SourceLanguage.CSharp,
            result.SourceLanguage);

        Assert.Equal(
            expectedIdentifiers.Length,
            result.IdentifierCount);
    }

    [Fact]
    public void Mask_WithRepeatedAndCaseSensitiveIdentifiers_ShouldReuseOnlyExactIdentifierMapping()
    {
        const string sourceCode =
            """
            public sealed class CustomerService
            {
                private string customerNumber;
                private string CustomerNumber;

                public string GetValue()
                {
                    customerNumber = CustomerNumber;
                    return customerNumber;
                }
            }
            """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var lowerCaseMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind == MaskingValueKind.Identifier &&
                        mapping.OriginalValue == "customerNumber"));

        var upperCaseMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind == MaskingValueKind.Identifier &&
                        mapping.OriginalValue == "CustomerNumber"));

        Assert.NotEqual(
            lowerCaseMapping.MaskedValue,
            upperCaseMapping.MaskedValue);

        Assert.Equal(
            3,
            CountOccurrences(
                result.MaskedCode,
                lowerCaseMapping.MaskedValue));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                upperCaseMapping.MaskedValue));
    }

    [Fact]
    public void Mask_WithEscapedIdentifier_ShouldPreserveAtPrefixAndReuseMapping()
    {
        const string sourceCode =
            """
            public sealed class SampleService
            {
                public string GetValue()
                {
                    var @class = "premium";
                    return @class;
                }
            }
            """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var escapedIdentifierMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind == MaskingValueKind.Identifier &&
                        mapping.OriginalValue == "@class"));

        Assert.StartsWith(
            "@CS_",
            escapedIdentifierMapping.MaskedValue);

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                escapedIdentifierMapping.MaskedValue));

        Assert.DoesNotContain(
            "@class",
            result.MaskedCode,
            StringComparison.Ordinal);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode);

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Mask_WithFormatPreservingMode_ShouldPreserveIdentifierCharacterStructure()
    {
        const string sourceCode =
            """
            public sealed class CustomerService
            {
                private string Customer_Number01;

                public string GetValue()
                {
                    return Customer_Number01;
                }
            }
            """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.FormatPreserving);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind == MaskingValueKind.Identifier &&
                        mapping.OriginalValue == "Customer_Number01"));

        Assert.Equal(
            mapping.OriginalValue.Length,
            mapping.MaskedValue.Length);

        Assert.NotEqual(
            mapping.OriginalValue,
            mapping.MaskedValue);

        for (var index = 0;
             index < mapping.OriginalValue.Length;
             index++)
        {
            var originalCharacter =
                mapping.OriginalValue[index];

            var maskedCharacter =
                mapping.MaskedValue[index];

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

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                mapping.MaskedValue));
    }

    [Fact]
    public void Mask_WithInvalidMode_ShouldThrowArgumentOutOfRangeException()
    {
        const string sourceCode =
            """
            public sealed class CustomerService
            {
            }
            """;

        var masker =
            new CSharpCodeMasker();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => masker.Mask(
                sourceCode,
                (MaskingMode)999));
    }

    private static string GetNumericSuffix(string literal)
    {
        var suffixStart =
            literal.Length;

        while (suffixStart > 0 &&
               literal[suffixStart - 1] is
                   'u' or 'U' or
                   'l' or 'L' or
                   'f' or 'F' or
                   'd' or 'D' or
                   'm' or 'M')
        {
            suffixStart--;
        }

        return literal[suffixStart..];
    }

    private static void AssertValidCSharp(string sourceCode)
    {
        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                sourceCode,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    private static int CountOccurrences(string source, string value)
    {
        return source
            .Split(
                value,
                StringSplitOptions.None)
            .Length - 1;
    }

    [Fact]
    public void Mask_WithNormalAndRepeatedStringLiteral_ShouldReuseStringLiteralMapping()
    {
        const string sourceCode =
            """
        public sealed class SampleService
        {
            public string GetValue()
            {
                var firstValue = "premium";
                var secondValue = "premium";

                return firstValue + secondValue;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue ==
                            "\"premium\""));

        Assert.StartsWith(
            "\"STR_",
            mapping.MaskedValue);

        Assert.EndsWith(
            "\"",
            mapping.MaskedValue);

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                mapping.MaskedValue));

        Assert.Equal(
            1,
            result.StringLiteralCount);
    }

    [Fact]
    public void Mask_WithVerbatimStringLiteral_ShouldPreserveVerbatimLiteralStructure()
    {
        const string sourceCode =
            """
        public sealed class SampleService
        {
            public string GetPath()
            {
                return @"C:\Internal\Customers";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral));

        Assert.Equal(
            "@\"C:\\Internal\\Customers\"",
            mapping.OriginalValue);

        Assert.StartsWith(
            "@\"STR_",
            mapping.MaskedValue);

        Assert.DoesNotContain(
            "Internal",
            result.MaskedCode,
            StringComparison.Ordinal);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode);

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Mask_WithCharacterLiteral_ShouldProduceValidDifferentCharacterLiteral()
    {
        const string sourceCode =
            """
        public sealed class SampleService
        {
            public bool IsValid(char status)
            {
                return status == 'X';
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue == "'X'"));

        Assert.NotEqual(
            mapping.OriginalValue,
            mapping.MaskedValue);

        Assert.StartsWith(
            "'",
            mapping.MaskedValue);

        Assert.EndsWith(
            "'",
            mapping.MaskedValue);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode);

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Mask_WithInterpolatedString_ShouldMaskTextAndInterpolationIdentifier()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreateMessage(string customerNumber)
            {
                return $"Customer number: {customerNumber}";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var textMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue ==
                            "Customer number: "));

        var identifierMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "customerNumber"));

        Assert.StartsWith(
            "STR_",
            textMapping.MaskedValue);

        Assert.DoesNotContain(
            "Customer number:",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "customerNumber",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            identifierMapping.MaskedValue,
            result.MaskedCode,
            StringComparison.Ordinal);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode);

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Mask_WithRepeatedInterpolatedText_ShouldReuseTextMapping()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreateMessage(string firstValue, string secondValue)
            {
                var firstMessage = $"Customer: {firstValue}";
                var secondMessage = $"Customer: {secondValue}";

                return firstMessage + secondMessage;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue ==
                            "Customer: "));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                mapping.MaskedValue));

        Assert.Equal(
            1,
            result.Mappings.Count(
                candidate =>
                    candidate.Kind ==
                        MaskingValueKind.StringLiteral &&
                    candidate.OriginalValue ==
                        "Customer: "));
    }

    [Fact]
    public void Mask_WithVerbatimInterpolatedString_ShouldPreserveValidSyntax()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreatePath(string customerNumber)
            {
                return $@"C:\Internal\Customers\{customerNumber}";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        Assert.DoesNotContain(
            "Internal",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Customers",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "customerNumber",
            result.MaskedCode,
            StringComparison.Ordinal);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode);

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Mask_WithFormatPreservingInterpolatedString_ShouldPreserveTextStructure()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreateMessage(string customerNumber)
            {
                return $"Customer-01: {customerNumber}";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.FormatPreserving);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue ==
                            "Customer-01: "));

        Assert.Equal(
            mapping.OriginalValue.Length,
            mapping.MaskedValue.Length);

        Assert.NotEqual(
            mapping.OriginalValue,
            mapping.MaskedValue);

        for (var index = 0;
             index < mapping.OriginalValue.Length;
             index++)
        {
            var originalCharacter =
                mapping.OriginalValue[index];

            var maskedCharacter =
                mapping.MaskedValue[index];

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

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode);

        Assert.DoesNotContain(
            syntaxTree.GetDiagnostics(),
            diagnostic =>
                diagnostic.Severity ==
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Mask_WithSingleLineRawString_ShouldMaskContentAndPreserveDelimiter()
    {
        const string sourceCode =
            """""
        public sealed class CustomerService
        {
            public string GetJson()
            {
                return """{"customerNumber":"123456"}""";
            }
        }
        """"";

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue.Contains(
                            "customerNumber",
                            StringComparison.Ordinal)));

        Assert.StartsWith(
            "\"\"\"STR_",
            mapping.MaskedValue);

        Assert.EndsWith(
            "\"\"\"",
            mapping.MaskedValue);

        Assert.DoesNotContain(
            "customerNumber",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithMultiLineRawString_ShouldMaskContentAndPreserveValidSyntax()
    {
        const string sourceCode =
            """""
        public sealed class CustomerService
        {
            public string GetJson()
            {
                return """
                    {
                        "customerNumber": "123456",
                        "customerName": "Internal Customer"
                    }
                    """;
            }
        }
        """"";

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        Assert.DoesNotContain(
            "customerNumber",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "customerName",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Internal Customer",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithRawInterpolatedString_ShouldMaskTextAndInterpolationIdentifier()
    {
        const string sourceCode =
            """""
        public sealed class CustomerService
        {
            public string CreateJson(string customerNumber)
            {
                return $$"""
                    {
                        "customerNumber": "{{customerNumber}}"
                    }
                    """;
            }
        }
        """"";

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var identifierMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "customerNumber"));

        Assert.DoesNotContain(
            "\"customerNumber\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            identifierMapping.MaskedValue,
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithFormatPreservingRawString_ShouldPreserveLiteralLength()
    {
        const string sourceCode =
            """""
        public sealed class CustomerService
        {
            public string GetValue()
            {
                return """Customer-01""";
            }
        }
        """"";

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.FormatPreserving);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.StringLiteral &&
                        mapping.OriginalValue ==
                            "\"\"\"Customer-01\"\"\""));

        Assert.Equal(
            mapping.OriginalValue.Length,
            mapping.MaskedValue.Length);

        Assert.NotEqual(
            mapping.OriginalValue,
            mapping.MaskedValue);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithDecimalNumericLiterals_ShouldMaskValuesAndPreserveSuffixes(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class PaymentService
        {
            public decimal Calculate()
            {
                var customerNumber = 123456;
                var balance = -1250.75M;
                var ratio = 12.50D;
                var percentage = 9.25F;

                return balance;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var expectedLiterals =
            new[]
            {
            "123456",
            "1250.75M",
            "12.50D",
            "9.25F"
            };

        foreach (var literal in expectedLiterals)
        {
            var mapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.NumericLiteral &&
                            mapping.OriginalValue ==
                                literal));

            Assert.NotEqual(
                mapping.OriginalValue,
                mapping.MaskedValue);

            Assert.Equal(
                GetNumericSuffix(
                    mapping.OriginalValue),
                GetNumericSuffix(
                    mapping.MaskedValue));
        }

        Assert.Contains(
            "-",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            expectedLiterals.Length,
            result.NumericLiteralCount);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithHexadecimalAndBinaryLiterals_ShouldPreserveBaseAndSeparators(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class PermissionService
        {
            public void Configure()
            {
                var permissionMask = 0x7F_A2U;
                var featureFlags = 0b1010_1100;
                var longMask = 0x00FF_AA11UL;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var hexadecimalMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "0x7F_A2U"));

        var binaryMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "0b1010_1100"));

        var unsignedLongMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "0x00FF_AA11UL"));

        Assert.StartsWith(
            "0x",
            hexadecimalMapping.MaskedValue);

        Assert.EndsWith(
            "U",
            hexadecimalMapping.MaskedValue);

        Assert.StartsWith(
            "0b",
            binaryMapping.MaskedValue);

        Assert.Equal(
            CountOccurrences(
                binaryMapping.OriginalValue,
                "_"),
            CountOccurrences(
                binaryMapping.MaskedValue,
                "_"));

        Assert.EndsWith(
            "UL",
            unsignedLongMapping.MaskedValue);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithScientificLiteral_ShouldMaskMantissaAndPreserveExponent(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CalculationService
        {
            public double Calculate()
            {
                var positiveRate = 1.25E+10;
                var negativeRate = 9.75E-03;

                return positiveRate + negativeRate;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var positiveMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "1.25E+10"));

        var negativeMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "9.75E-03"));

        Assert.NotEqual(
            positiveMapping.OriginalValue,
            positiveMapping.MaskedValue);

        Assert.NotEqual(
            negativeMapping.OriginalValue,
            negativeMapping.MaskedValue);

        Assert.EndsWith(
            "E+10",
            positiveMapping.MaskedValue);

        Assert.EndsWith(
            "E-03",
            negativeMapping.MaskedValue);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithRepeatedNumericLiteral_ShouldReuseNumericMapping()
    {
        const string sourceCode =
            """
        public sealed class LimitService
        {
            public int Calculate()
            {
                var firstLimit = 2500;
                var secondLimit = 2500;

                return firstLimit + secondLimit;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        var mapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            "2500"));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                mapping.MaskedValue));

        Assert.Equal(
            1,
            result.NumericLiteralCount);

        AssertValidCSharp(
            result.MaskedCode);
    }
}