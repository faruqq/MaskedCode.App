using MaskedCode.App.Masking;
using MaskedCode.App.Masking.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;

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

    private static void AssertCompilableCSharp(
    string sourceCode)
    {
        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                sourceCode,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var trustedPlatformAssemblies =
            AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES") as string;

        Assert.False(
            string.IsNullOrWhiteSpace(
                trustedPlatformAssemblies));

        var references =
            trustedPlatformAssemblies!
                .Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                    MetadataReference.CreateFromFile(
                        path))
                .ToArray();

        var compilation =
            CSharpCompilation.Create(
                assemblyName:
                    $"MaskedCode.CompilationTest.{Guid.NewGuid():N}",
                syntaxTrees:
                    new[]
                    {
                    syntaxTree
                    },
                references:
                    references,
                options:
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary));

        var errors =
            compilation
                .GetDiagnostics()
                .Where(diagnostic =>
                    diagnostic.Severity ==
                        DiagnosticSeverity.Error)
                .ToArray();

        Assert.True(
            errors.Length == 0,
            string.Join(
                Environment.NewLine,
                errors.Select(
                    diagnostic =>
                        diagnostic.ToString())));
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

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithIntegralBoundaryValues_ShouldPreserveNumericValueTypes(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class NumericBoundaryService
        {
            public void Configure()
            {
                var intMaximum = 2147483647;
                var uintMaximum = 4294967295U;
                var longMaximum = 9223372036854775807L;
                var ulongMaximum = 18446744073709551615UL;
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
            "2147483647",
            "4294967295U",
            "9223372036854775807L",
            "18446744073709551615UL"
            };

        foreach (var originalLiteral in expectedLiterals)
        {
            var mapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.NumericLiteral &&
                            mapping.OriginalValue ==
                                originalLiteral));

            var originalToken =
                SyntaxFactory.ParseToken(
                    mapping.OriginalValue);

            var maskedToken =
                SyntaxFactory.ParseToken(
                    mapping.MaskedValue);

            Assert.NotEqual(
                mapping.OriginalValue,
                mapping.MaskedValue);

            Assert.NotNull(
                originalToken.Value);

            Assert.NotNull(
                maskedToken.Value);

            Assert.Equal(
                originalToken.Value.GetType(),
                maskedToken.Value.GetType());
        }

        Assert.Equal(
            expectedLiterals.Length,
            result.NumericLiteralCount);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithHexadecimalBoundaryValues_ShouldPreserveNumericValueTypes(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class HexadecimalBoundaryService
        {
            public void Configure()
            {
                var intMaximum = 0x7FFFFFFF;
                var uintMaximum = 0xFFFFFFFFU;
                var longMaximum = 0x7FFFFFFFFFFFFFFFL;
                var ulongMaximum = 0xFFFFFFFFFFFFFFFFUL;
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
            "0x7FFFFFFF",
            "0xFFFFFFFFU",
            "0x7FFFFFFFFFFFFFFFL",
            "0xFFFFFFFFFFFFFFFFUL"
            };

        foreach (var originalLiteral in expectedLiterals)
        {
            var mapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.NumericLiteral &&
                            mapping.OriginalValue ==
                                originalLiteral));

            var originalToken =
                SyntaxFactory.ParseToken(
                    mapping.OriginalValue);

            var maskedToken =
                SyntaxFactory.ParseToken(
                    mapping.MaskedValue);

            Assert.StartsWith(
                "0x",
                mapping.MaskedValue);

            Assert.NotEqual(
                mapping.OriginalValue,
                mapping.MaskedValue);

            Assert.NotNull(
                originalToken.Value);

            Assert.NotNull(
                maskedToken.Value);

            Assert.Equal(
                originalToken.Value.GetType(),
                maskedToken.Value.GetType());
        }

        Assert.Equal(
            expectedLiterals.Length,
            result.NumericLiteralCount);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithLineAndBlockComments_ShouldMaskContentAndPreserveDelimiters(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            // Internal customer number must not be logged.
            public void Process()
            {
                /* Customer balance comes from the private database. */
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.DoesNotContain(
            "Internal customer number",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Customer balance",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "//",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "/*",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "*/",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            result.CommentCount);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithRepeatedComment_ShouldReuseCommentMapping()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            // Internal customer reference
            public void First()
            {
            }

            // Internal customer reference
            public void Second()
            {
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
                            MaskingValueKind.Comment));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                mapping.MaskedValue));

        Assert.Equal(
            1,
            result.CommentCount);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithFormatPreservingComment_ShouldPreserveCommentLength()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            // Customer-123 must remain confidential.
            public void Process()
            {
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
                            MaskingValueKind.Comment));

        Assert.Equal(
            mapping.OriginalValue.Length,
            mapping.MaskedValue.Length);

        Assert.StartsWith(
            "//",
            mapping.MaskedValue);

        Assert.NotEqual(
            mapping.OriginalValue,
            mapping.MaskedValue);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithXmlDocumentationComments_ShouldMaskSensitiveText(MaskingMode mode)
    {
        const string sourceCode =
            """
        /// <summary>
        /// Returns the internal customer account.
        /// </summary>
        /// <param name="customerNumber">Private customer number.</param>
        public sealed class CustomerService
        {
            /** Internal account processing documentation. */
            public void Process(string customerNumber)
            {
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.DoesNotContain(
            "Returns the internal customer account",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Private customer number",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Internal account processing documentation",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "///",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "/**",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "*/",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.True(
            result.CommentCount >= 2);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithMultiLineBlockComment_ShouldPreserveLineBreakCount()
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            /*
             * Internal customer number
             * Private account balance
             */
            public void Process()
            {
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
                            MaskingValueKind.Comment));

        Assert.Equal(
            CountOccurrences(
                mapping.OriginalValue,
                "\n"),
            CountOccurrences(
                mapping.MaskedValue,
                "\n"));

        Assert.DoesNotContain(
            "Internal customer number",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Private account balance",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithRegionDirectives_ShouldMaskNamesAndPreserveDirectiveStructure(MaskingMode mode)
    {
        const string sourceCode =
            """
        #region Internal Customer Operations

        public sealed class CustomerService
        {
        }

        #endregion Internal Customer Operations
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.DoesNotContain(
            "Internal Customer Operations",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#region",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#endregion",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            result.CommentCount);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var directiveKinds =
            syntaxTree
                .GetRoot()
                .DescendantTrivia(
                    descendIntoTrivia: true)
                .Where(trivia =>
                    trivia.IsDirective)
                .Select(trivia =>
                    trivia.Kind())
                .ToArray();

        Assert.Contains(
            SyntaxKind.RegionDirectiveTrivia,
            directiveKinds);

        Assert.Contains(
            SyntaxKind.EndRegionDirectiveTrivia,
            directiveKinds);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Fact]
    public void Mask_WithUnnamedEndRegionDirective_ShouldNotCreateMapping()
    {
        const string sourceCode =
            """
        #region Internal Services

        public sealed class CustomerService
        {
        }

        #endregion
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                MaskingMode.MaximumPrivacy);

        Assert.DoesNotContain(
            "Internal Services",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#endregion",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            1,
            result.CommentCount);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithErrorAndWarningDirectives_ShouldMaskMessagesAndPreserveDirectiveKinds(MaskingMode mode)
    {
        const string sourceCode =
            """
        #warning Internal customer configuration is active
        #error Private database connection must not be published

        public sealed class CustomerService
        {
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.DoesNotContain(
            "Internal customer configuration",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Private database connection",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#warning",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#error",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            result.CommentCount);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                result.MaskedCode,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var directiveKinds =
            syntaxTree
                .GetRoot()
                .DescendantTrivia(
                    descendIntoTrivia: true)
                .Where(trivia =>
                    trivia.IsDirective)
                .Select(trivia =>
                    trivia.Kind())
                .ToArray();

        Assert.Contains(
            SyntaxKind.WarningDirectiveTrivia,
            directiveKinds);

        Assert.Contains(
            SyntaxKind.ErrorDirectiveTrivia,
            directiveKinds);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithConditionalDirectiveSymbol_ShouldReuseIdentifierMapping(MaskingMode mode)
    {
        const string sourceCode =
            """
        #define INTERNAL_CUSTOMER_FEATURE
        #if INTERNAL_CUSTOMER_FEATURE
        #endif
        #undef INTERNAL_CUSTOMER_FEATURE

        public sealed class CustomerService
        {
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var symbolMapping =
            Assert.Single(
                result.Mappings.Where(mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        "INTERNAL_CUSTOMER_FEATURE"));

        Assert.DoesNotContain(
            "INTERNAL_CUSTOMER_FEATURE",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            3,
            CountOccurrences(
                result.MaskedCode,
                symbolMapping.MaskedValue));

        Assert.Contains(
            "#define",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#if",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#endif",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#undef",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithDisabledConditionalContent_ShouldRejectMasking(MaskingMode mode)
    {
        const string sourceCode =
            """
        #if UNDEFINED_PRIVATE_FEATURE

        internal sealed class PrivateCustomerService
        {
            private const string ConnectionName = "InternalCustomerDatabase";
        }

        #endif
        """;

        var masker =
            new CSharpCodeMasker();

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                masker.Mask(
                    sourceCode,
                    mode));

        Assert.Equal(
            "C# kaynak kodunda etkin olmayan koşullu derleme içeriği bulundu. " +
            "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.",
            exception.Message);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithLineDirective_ShouldMaskFileNameAndPreserveLineNumber(MaskingMode mode)
    {
        const string sourceCode =
            """
        #line 200 "Internal/CustomerService.cs"

        public sealed class CustomerService
        {
        }

        #line default
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var fileNameMapping =
            Assert.Single(
                result.Mappings.Where(mapping =>
                    mapping.Kind ==
                        MaskingValueKind.StringLiteral &&
                    mapping.OriginalValue ==
                        "\"Internal/CustomerService.cs\""));

        Assert.DoesNotContain(
            "Internal/CustomerService.cs",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            $"#line 200 {fileNameMapping.MaskedValue}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#line default",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithRepeatedLineDirectiveFileName_ShouldReuseStringMapping(MaskingMode mode)
    {
        const string sourceCode =
            """
        #line 100 "Internal/CustomerService.cs"

        public sealed class CustomerService
        {
        }

        #line 300 "Internal/CustomerService.cs"
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var fileNameMapping =
            Assert.Single(
                result.Mappings.Where(mapping =>
                    mapping.Kind ==
                        MaskingValueKind.StringLiteral &&
                    mapping.OriginalValue ==
                        "\"Internal/CustomerService.cs\""));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                fileNameMapping.MaskedValue));

        Assert.Contains(
            "#line 100",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#line 300",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithDefaultAndHiddenLineDirectives_ShouldPreserveDirectivesWithoutCreatingStringMappings(MaskingMode mode)
    {
        const string sourceCode =
            """
        #line hidden

        public sealed class CustomerService
        {
        }

        #line default
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            "#line hidden",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#line default",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            result.Mappings,
            mapping =>
                mapping.Kind ==
                MaskingValueKind.StringLiteral);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithPragmaChecksum_ShouldMaskFileNameAndPreserveChecksumData(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        #pragma checksum "Internal/Generated/CustomerService.cs" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "0123456789ABCDEF"

        public sealed class CustomerService
        {
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var fileNameMapping =
            Assert.Single(
                result.Mappings.Where(mapping =>
                    mapping.Kind ==
                        MaskingValueKind.StringLiteral &&
                    mapping.OriginalValue ==
                        "\"Internal/Generated/CustomerService.cs\""));

        Assert.DoesNotContain(
            "Internal/Generated/CustomerService.cs",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            $"#pragma checksum {fileNameMapping.MaskedValue}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"{406ea660-64cf-4c82-b6f0-42d48172a799}\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"0123456789ABCDEF\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            result.Mappings,
            mapping =>
                mapping.OriginalValue ==
                    "\"{406ea660-64cf-4c82-b6f0-42d48172a799}\"" ||
                mapping.OriginalValue ==
                    "\"0123456789ABCDEF\"");

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithRepeatedPragmaChecksumFileName_ShouldReuseStringMapping(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        #pragma checksum "Internal/Generated/CustomerService.cs" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "0123456789ABCDEF"
        #pragma checksum "Internal/Generated/CustomerService.cs" "{8829d00f-11b8-4213-878b-770e8597ac16}" "FEDCBA9876543210"

        public sealed class CustomerService
        {
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var fileNameMapping =
            Assert.Single(
                result.Mappings.Where(mapping =>
                    mapping.Kind ==
                        MaskingValueKind.StringLiteral &&
                    mapping.OriginalValue ==
                        "\"Internal/Generated/CustomerService.cs\""));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                fileNameMapping.MaskedValue));

        Assert.Contains(
            "\"{406ea660-64cf-4c82-b6f0-42d48172a799}\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"{8829d00f-11b8-4213-878b-770e8597ac16}\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"0123456789ABCDEF\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"FEDCBA9876543210\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithNonChecksumPragmaDirectives_ShouldPreserveDirectiveStructure(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        #pragma warning disable CS0168

        public sealed class CustomerService
        {
            public void Process()
            {
                int unusedValue;
            }
        }

        #pragma warning restore CS0168
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            "#pragma warning disable CS0168",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "#pragma warning restore CS0168",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            result.Mappings,
            mapping =>
                mapping.OriginalValue ==
                    "CS0168");

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithSkippedTokens_ShouldRejectSourceCode(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string GetCustomerName()
            {
                var customerName = "PublicValue" "InternalCustomerName";
                return customerName;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                masker.Mask(
                    sourceCode,
                    mode));

        Assert.Equal(
            "C# kaynak kodunda ayrıştırılamayan token içeriği bulundu. " +
            "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.",
            exception.Message);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithBadDirective_ShouldRejectSourceCode(MaskingMode mode)
    {
        const string sourceCode =
            """
        #company InternalCustomerPlatform

        public sealed class CustomerService
        {
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                masker.Mask(
                    sourceCode,
                    mode));

        Assert.Equal(
            "C# kaynak kodunda geçersiz veya desteklenmeyen directive bulundu. " +
            "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.",
            exception.Message);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithConflictMarkers_ShouldRejectSourceCode(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string GetCustomerName()
            {
        <<<<<<< HEAD
                return "InternalCustomerName";
        =======
                return "PublicCustomerName";
        >>>>>>> feature/public-customer
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                masker.Mask(
                    sourceCode,
                    mode));

        Assert.Equal(
            "C# kaynak kodunda çözümlenmemiş birleştirme çakışması bulundu. " +
            "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.",
            exception.Message);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithCommentBeforeVarDeclaration_ShouldMaskCommentAndPreserveDelimiter(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public decimal CalculateBalance(
                decimal currentBalance,
                decimal blockedAmount)
            {
                // Müşterinin kullanılabilir bakiyesini hesaplar.
                var availableBalance =
                    currentBalance - blockedAmount;

                return availableBalance;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var commentMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Comment));

        var availableBalanceMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "availableBalance"));

        Assert.StartsWith(
            "//",
            commentMapping.MaskedValue,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Müşterinin kullanılabilir bakiyesini hesaplar.",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            commentMapping.MaskedValue,
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            $"var {availableBalanceMapping.MaskedValue}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            result.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.Identifier &&
                mapping.OriginalValue ==
                    "var");

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithWarningDirective_ShouldMaskMessageAndPreserveDirective(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        #warning Minimum müşteri bakiyesi kontrol edilmelidir

        public sealed class CustomerService
        {
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            "#warning ",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "#warning  \r",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "#warning  \n",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Minimum müşteri bakiyesi kontrol edilmelidir",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            result.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.Comment &&
                mapping.OriginalValue.Contains(
                    "Minimum müşteri bakiyesi",
                    StringComparison.Ordinal));

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithInterpolatedFormatClause_ShouldPreserveFormatClause(
        MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreateMessage(
                decimal availableBalance)
            {
                return $"Balance: {availableBalance:N2} TRY";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            ":N2}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            ":R7}",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithCompositeFormatString_ShouldPreserveFormatItems(
        MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreateMessage(
                string customerNumber,
                decimal availableBalance,
                string auditMessage)
            {
                return string.Format(
                    "{0}: {1:N2} TRY - {2}",
                    customerNumber,
                    availableBalance,
                    auditMessage);
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            "{0}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "{1:N2}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Contains(
            "{2}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "{7:A3}",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "TRY",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithCompositeFormatStringContainingOnlyFormatItemsAndPunctuation_ShouldPreserveLiteralWithoutMapping(MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public string CreateMessage(
                string customerNumber,
                decimal availableBalance)
            {
                return string.Format(
                    "{0} - {1:N2}",
                    customerNumber,
                    availableBalance);
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            "\"{0} - {1:N2}\"",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            result.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.StringLiteral &&
                mapping.OriginalValue ==
                    "\"{0} - {1:N2}\"");

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithFrameworkSymbols_ShouldPreserveFrameworkSymbolsAndMaskSourceIdentifiers(MaskingMode mode)
    {
        const string sourceCode =
            """
        using System;
        using System.Collections.Generic;
        using System.Globalization;
        using System.Linq;
        using System.Threading.Tasks;

        namespace Company.Customer;

        public sealed class CustomerService
        {
            public async Task<List<string>> GetCustomerNamesAsync(
                IEnumerable<string> customerNames)
            {
                var normalizedNames =
                    customerNames
                        .Select(customerName =>
                            customerName.ToUpper(
                                CultureInfo.InvariantCulture))
                        .ToList();

                Console.WriteLine(
                    Guid.NewGuid());

                await Task.CompletedTask;

                return normalizedNames;
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var sourceIdentifiers =
            new[]
            {
            "Company",
            "Customer",
            "CustomerService",
            "GetCustomerNamesAsync",
            "customerNames",
            "normalizedNames",
            "customerName"
            };

        foreach (var identifier in sourceIdentifiers)
        {
            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.DoesNotContain(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        var frameworkIdentifiers =
            new[]
            {
            "System",
            "Collections",
            "Generic",
            "Globalization",
            "Linq",
            "Threading",
            "Tasks",
            "Task",
            "List",
            "IEnumerable",
            "Select",
            "ToUpper",
            "CultureInfo",
            "InvariantCulture",
            "ToList",
            "Console",
            "WriteLine",
            "Guid",
            "NewGuid",
            "CompletedTask"
            };

        foreach (var identifier in frameworkIdentifiers)
        {
            Assert.DoesNotContain(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.Contains(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithFrameworkAlias_ShouldMaskAliasAndPreserveFrameworkTarget(MaskingMode mode)
    {
        const string sourceCode =
            """
        using Runtime = System;

        public sealed class CustomerService
        {
            public void WriteCustomer()
            {
                Runtime.Console.WriteLine(
                    "Internal customer");
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var aliasMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "Runtime"));

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                aliasMapping.MaskedValue));

        Assert.DoesNotContain(
            "Runtime",
            result.MaskedCode,
            StringComparison.Ordinal);

        var preservedFrameworkIdentifiers =
            new[]
            {
            "System",
            "Console",
            "WriteLine"
            };

        foreach (var identifier in preservedFrameworkIdentifiers)
        {
            Assert.DoesNotContain(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.Contains(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithUnresolvedSymbols_ShouldMaskSymbolsForSafeFallback(MaskingMode mode)
    {
        const string sourceCode =
            """
        using ExternalCompany.Framework;

        public sealed class CustomerService
        {
            private ExternalClient client;

            public ExternalResult Execute(
                ExternalRequest request)
            {
                return client.Send(
                    request);
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var unresolvedIdentifiers =
            new[]
            {
            "ExternalCompany",
            "Framework",
            "ExternalClient",
            "ExternalResult",
            "ExternalRequest",
            "Send"
            };

        foreach (var identifier in unresolvedIdentifiers)
        {
            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.DoesNotContain(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void MaskAndUnmask_WithFrameworkSymbolsAndAlias_ShouldRestoreExactSource(MaskingMode mode)
    {
        const string sourceCode =
            """
        using Runtime = System;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        namespace Company.Customer;

        public sealed class CustomerService
        {
            public async Task<List<string>> GetCustomersAsync()
            {
                Runtime.Console.WriteLine(
                    Runtime.Guid.NewGuid());

                await Task.CompletedTask;

                return new List<string>();
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
    public void Mask_WithNameOfExpression_ShouldPreserveOperatorAndMaskOperand(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class CustomerService
        {
            public CustomerService(
                string customerNumber)
            {
                ArgumentNullException.ThrowIfNull(
                    customerNumber);

                CustomerNumber =
                    customerNumber;
            }

            public string CustomerNumber
            {
                get;
            }

            public string GetParameterName(
                string customerNumber)
            {
                return nameof(customerNumber);
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        Assert.Contains(
            "nameof(",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "nameof(customerNumber)",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            result.Mappings,
            mapping =>
                mapping.Kind ==
                    MaskingValueKind.Identifier &&
                mapping.OriginalValue ==
                    "nameof");

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithShortAttributeName_ShouldPreserveAttributeSuffixRelationship(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        using System;

        [AttributeUsage(
            AttributeTargets.Class |
            AttributeTargets.Method)]
        public sealed class CustomerAuditAttribute : Attribute
        {
            public CustomerAuditAttribute(
                string operationName)
            {
                OperationName =
                    operationName;
            }

            public string OperationName
            {
                get;
            }
        }

        [CustomerAudit("Customer operation")]
        public sealed class CustomerService
        {
            [CustomerAudit("Get customer")]
            public string GetCustomer()
            {
                return "Customer";
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var fullNameMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CustomerAuditAttribute"));

        var shortNameMapping =
            Assert.Single(
                result.Mappings.Where(
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue ==
                            "CustomerAudit"));

        Assert.Equal(
            shortNameMapping.MaskedValue +
            "Attribute",
            fullNameMapping.MaskedValue);

        Assert.Contains(
            $"class {fullNameMapping.MaskedValue} : Attribute",
            result.MaskedCode,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            CountOccurrences(
                result.MaskedCode,
                $"[{shortNameMapping.MaskedValue}("));

        Assert.DoesNotContain(
            "CustomerAudit",
            result.MaskedCode,
            StringComparison.Ordinal);

        AssertCompilableCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithManyDistinctSingleDigitLiterals_ShouldMaskAndRestoreAllValues(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        public sealed class SingleDigitService
        {
            public int Calculate(int value)
            {
                return value switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    3 => 4,
                    4 => 5,
                    5 => 6,
                    6 => 8,
                    8 => 0,
                    _ => 2
                };
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var numericMappings =
            result.Mappings
                .Where(mapping =>
                    mapping.Kind ==
                        MaskingValueKind.NumericLiteral)
                .ToArray();

        Assert.Equal(
            8,
            numericMappings.Length);

        Assert.Equal(
            numericMappings.Length,
            numericMappings
                .Select(mapping =>
                    mapping.MaskedValue)
                .Distinct(
                    StringComparer.Ordinal)
                .Count());

        Assert.All(
            numericMappings,
            mapping =>
                Assert.NotEqual(
                    mapping.OriginalValue,
                    mapping.MaskedValue));

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithXunitSymbols_ShouldPreserveXunitSymbolsAndMaskSourceIdentifiers(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        using System;
        using System.Threading.Tasks;
        using Xunit;

        namespace Company.Customer.Tests;

        public sealed class CustomerServiceTests
        {
            [Fact]
            public void Calculate_ShouldReturnExpectedResult()
            {
                var service =
                    new CustomerService();

                var expected =
                    10;

                var result =
                    service.Calculate();

                Assert.Equal(
                    expected,
                    result);
            }

            [Theory]
            [InlineData(10)]
            [InlineData(20)]
            public async Task ExecuteAsync_WithInvalidValue_ShouldThrow(
                int customerValue)
            {
                var service =
                    new CustomerService();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.ExecuteAsync(
                            customerValue));
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var frameworkIdentifiers =
            new[]
            {
            "Xunit",
            "Fact",
            "Theory",
            "InlineData",
            "Assert",
            "Equal",
            "ThrowsAsync"
            };

        foreach (var identifier in frameworkIdentifiers)
        {
            Assert.DoesNotContain(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.Contains(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        var sourceIdentifiers =
            new[]
            {
            "Company",
            "Customer",
            "Tests",
            "CustomerServiceTests",
            "Calculate_ShouldReturnExpectedResult",
            "service",
            "expected",
            "result",
            "CustomerService",
            "Calculate",
            "ExecuteAsync_WithInvalidValue_ShouldThrow",
            "customerValue",
            "ExecuteAsync"
            };

        foreach (var identifier in sourceIdentifiers)
        {
            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.DoesNotContain(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithSourceSymbolsNamedLikeXunitSymbols_ShouldMaskSourceSymbols(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        using System;

        namespace Company.CustomTesting;

        public sealed class FactAttribute : Attribute
        {
        }

        public static class Assert
        {
            public static void Equal(
                int expected,
                int actual)
            {
            }

            public static void ThrowsAsync(
                Action operation)
            {
                operation();
            }
        }

        public sealed class CustomerServiceTests
        {
            [Fact]
            public void Execute()
            {
                Assert.Equal(
                    10,
                    20);

                Assert.ThrowsAsync(
                    () =>
                    {
                    });
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var sourceIdentifiers =
            new[]
            {
            "FactAttribute",
            "Fact",
            "Assert",
            "Equal",
            "ThrowsAsync"
            };

        foreach (var identifier in sourceIdentifiers)
        {
            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.DoesNotContain(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        AssertValidCSharp(
            result.MaskedCode);
    }

    [Theory]
    [InlineData(MaskingMode.MaximumPrivacy)]
    [InlineData(MaskingMode.FormatPreserving)]
    public void Mask_WithImplicitAndProjectGlobalUsings_ShouldPreserveFrameworkSymbols(
    MaskingMode mode)
    {
        const string sourceCode =
            """
        namespace Company.Customer.Tests
        {
            public sealed class CustomerServiceTests
            {
                [Fact]
                public void Generate_ShouldReturnExpectedResult()
                {
                    var expected =
                        "expected" + Environment.NewLine;

                    var result =
                        "result" + Environment.NewLine;

                    Assert.Equal(
                        expected,
                        result);
                }

                [Theory]
                [InlineData(10)]
                public async Task ExecuteAsync_ShouldThrow(
                    int customerValue)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () =>
                            Task.FromException(
                                new InvalidOperationException()));
                }
            }
        }
        """;

        var masker =
            new CSharpCodeMasker();

        var result =
            masker.Mask(
                sourceCode,
                mode);

        var frameworkIdentifiers =
            new[]
            {
            "Fact",
            "Theory",
            "InlineData",
            "Assert",
            "Equal",
            "ThrowsAsync",
            "Environment",
            "NewLine",
            "Task",
            "FromException",
            "InvalidOperationException"
            };

        foreach (var identifier in frameworkIdentifiers)
        {
            Assert.DoesNotContain(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);

            Assert.Contains(
                identifier,
                result.MaskedCode,
                StringComparison.Ordinal);
        }

        var sourceIdentifiers =
            new[]
            {
            "Company",
            "Customer",
            "Tests",
            "CustomerServiceTests",
            "Generate_ShouldReturnExpectedResult",
            "expected",
            "result",
            "ExecuteAsync_ShouldThrow",
            "customerValue"
            };

        foreach (var identifier in sourceIdentifiers)
        {
            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue ==
                        identifier);
        }

        AssertValidCSharp(
            result.MaskedCode);
    }
}