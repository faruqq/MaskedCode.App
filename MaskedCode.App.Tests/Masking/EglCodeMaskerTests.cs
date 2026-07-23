using MaskedCode.App.Masking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaskedCode.App.Tests.Masking
{
    public sealed class EglCodeMaskerTests
    {
        [Fact]
        public void Mask_WithEglProgramIdentifiers_ShouldMaskCustomIdentifiers()
        {
            const string sourceCode =
                """
        package com.company.programs;

        program MYPROGRAMNAME type BasicProgram(
            HeaderInput HeaderInput,
            MyProgramNameInput MyProgramNameInput)
            {includeReferencedFunctions = yes}

            function main()
                CorePreMain();
            end

        end
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy);

            var expectedMaskedIdentifiers =
                new[]
                {
            "com",
            "company",
            "programs",
            "MYPROGRAMNAME",
            "HeaderInput",
            "MyProgramNameInput",
            "CorePreMain"
                };

            foreach (var identifier in expectedMaskedIdentifiers)
            {
                Assert.Contains(
                    result.Mappings,
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue == identifier);

                Assert.DoesNotContain(
                    identifier,
                    result.MaskedCode,
                    StringComparison.Ordinal);
            }

            var preservedValues =
                new[]
                {
            "package",
            "program",
            "type",
            "BasicProgram",
            "includeReferencedFunctions",
            "yes",
            "function",
            "main",
            "end"
                };

            foreach (var value in preservedValues)
            {
                Assert.DoesNotContain(
                    result.Mappings,
                    mapping =>
                        mapping.OriginalValue.Equals(
                            value,
                            StringComparison.OrdinalIgnoreCase));

                Assert.Contains(
                    value,
                    result.MaskedCode,
                    StringComparison.OrdinalIgnoreCase);
            }

            Assert.All(
                result.Mappings,
                mapping => Assert.StartsWith(
                    "EGL_",
                    mapping.MaskedValue));
        }

        [Fact]
        public void
Mask_WithFormatPreservingMode_ShouldPreserveIdentifierFormatAndReuseMapping()
        {
            const string sourceCode =
                """
        record MyProgramNameInput type BasicRecord
            MyProgramNameInput MyProgramNameInput;
            DbMyTableName DbMyTableName;
            MyTableName_Upd01();
            MyTableName_Upd01();
        end
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.FormatPreserving);

            var functionMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.OriginalValue ==
                                "MyTableName_Upd01"));

            Assert.Equal(
                functionMapping.OriginalValue.Length,
                functionMapping.MaskedValue.Length);

            for (var index = 0;
                 index < functionMapping.OriginalValue.Length;
                 index++)
            {
                var originalCharacter =
                    functionMapping.OriginalValue[index];

                var maskedCharacter =
                    functionMapping.MaskedValue[index];

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

            var usageCount =
                result.MaskedCode
                    .Split(
                        functionMapping.MaskedValue,
                        StringSplitOptions.None)
                    .Length - 1;

            Assert.Equal(
                2,
                usageCount);
        }

        [Fact]
        public void
Mask_WithSystemPath_ShouldPreserveOnlySysVarPath()
        {
            const string sourceCode =
                """
        function MyProgramNameMain()
            HeaderOutput.CurrentFunctionName =
                sysVar.currentFunctionName;
        end
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy);

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.OriginalValue ==
                        "MyProgramNameMain");

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.OriginalValue ==
                        "HeaderOutput");

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.OriginalValue ==
                        "CurrentFunctionName");

            Assert.DoesNotContain(
                result.Mappings,
                mapping =>
                    mapping.OriginalValue.Equals(
                        "sysVar",
                        StringComparison.OrdinalIgnoreCase));

            Assert.Contains(
                "sysVar.currentFunctionName",
                result.MaskedCode);
        }

        [Fact]
        public void
Mask_WithRepeatedEscapedStringLiteral_ShouldReuseStringMapping()
        {
            const string sourceCode =
                """
        ErrorText = "CUSTOMER \"NOT FOUND\"";
        AuditText = "CUSTOMER \"NOT FOUND\"";
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy);

            var stringMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.StringLiteral));

            Assert.Equal(
                "CUSTOMER \\\"NOT FOUND\\\"",
                stringMapping.OriginalValue);

            Assert.StartsWith(
                "EGL_STR_",
                stringMapping.MaskedValue);

            Assert.DoesNotContain(
                "CUSTOMER",
                result.MaskedCode);

            var usageCount =
                result.MaskedCode
                    .Split(
                        $"\"{stringMapping.MaskedValue}\"",
                        StringSplitOptions.None)
                    .Length - 1;

            Assert.Equal(
                2,
                usageCount);

            Assert.Equal(
                1,
                result.StringLiteralCount);
        }

        [Fact]
        public void
Mask_WithFormatPreservingStringLiteral_ShouldPreserveFormatAndEscapes()
        {
            const string sourceCode =
                """
        ErrorText = "Customer-01 \"Not Found\"";
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.FormatPreserving);

            var stringMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.StringLiteral));

            Assert.Equal(
                stringMapping.OriginalValue.Length,
                stringMapping.MaskedValue.Length);

            Assert.NotEqual(
                stringMapping.OriginalValue,
                stringMapping.MaskedValue);

            for (var index = 0;
                 index < stringMapping.OriginalValue.Length;
                 index++)
            {
                var originalCharacter =
                    stringMapping.OriginalValue[index];

                var maskedCharacter =
                    stringMapping.MaskedValue[index];

                if (originalCharacter == '\\' &&
                    index + 1 <
                        stringMapping.OriginalValue.Length)
                {
                    Assert.Equal(
                        originalCharacter,
                        maskedCharacter);

                    Assert.Equal(
                        stringMapping.OriginalValue[index + 1],
                        stringMapping.MaskedValue[index + 1]);

                    index++;
                    continue;
                }

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

            Assert.Contains(
                "\\\"",
                stringMapping.MaskedValue);

            Assert.DoesNotContain(
                "Customer-01",
                result.MaskedCode);
        }

        [Fact]
        public void
Mask_WithLineAndBlockComments_ShouldMaskContentAndPreserveLineStructure()
        {
            const string sourceCode =
                """
        // Müşteri kontrol açıklaması
        function CustomerCheck()
            /*
             * Gerçek müşteri iş kuralı
             * İkinci açıklama satırı
             */
            CustomerRead();
        end
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy);

            Assert.Equal(
                2,
                result.CommentCount);

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Comment &&
                    mapping.OriginalValue.Contains(
                        "Müşteri kontrol açıklaması",
                        StringComparison.Ordinal));

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Comment &&
                    mapping.OriginalValue.Contains(
                        "Gerçek müşteri iş kuralı",
                        StringComparison.Ordinal));

            Assert.DoesNotContain(
                "Müşteri kontrol açıklaması",
                result.MaskedCode);

            Assert.DoesNotContain(
                "Gerçek müşteri iş kuralı",
                result.MaskedCode);

            Assert.DoesNotContain(
                "İkinci açıklama satırı",
                result.MaskedCode);

            Assert.Contains(
                "// EGL_CMT_",
                result.MaskedCode);

            Assert.Contains(
                "/* EGL_CMT_",
                result.MaskedCode);

            Assert.Contains(
                "*/",
                result.MaskedCode);

            Assert.Equal(
                sourceCode.Count(
                    character => character == '\n'),
                result.MaskedCode.Count(
                    character => character == '\n'));
        }

        [Fact]
        public void Mask_WithCommentMarkersInsideString_ShouldTreatMarkersAsStringContent()
        {
            const string sourceCode =
                """
        ErrorText =
            "/* Müşteri sırrı */ // Bu metin string içindedir";

        // Bu ise gerçek EGL yorumudur
        WriteError(ErrorText);
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy);

            Assert.Equal(
                1,
                result.StringLiteralCount);

            Assert.Equal(
                1,
                result.CommentCount);

            Assert.DoesNotContain(
                "Müşteri sırrı",
                result.MaskedCode);

            Assert.DoesNotContain(
                "Bu metin string içindedir",
                result.MaskedCode);

            Assert.DoesNotContain(
                "Bu ise gerçek EGL yorumudur",
                result.MaskedCode);

            Assert.Contains(
                "\"EGL_STR_",
                result.MaskedCode);

            Assert.Contains(
                "// EGL_CMT_",
                result.MaskedCode);
        }

        [Theory]
        [InlineData(
     """
    CoreBusinessException(001);
    """)]
        [InlineData(
     """
    Description = #doc{Program açıklaması};
    """)]
        [InlineData(
     """
    get MyTable with #sql{select PARAM1 from MY_TABLE};
    """)]
        [InlineData(
     """
    function MüşteriKayıtEkle()
    end
    """)]
        public void
 Mask_WithNotYetSupportedSensitiveContext_ShouldRejectSource(
     string sourceCode)
        {
            var masker =
                new EglCodeMasker();

            Assert.Throws<NotSupportedException>(
                () => masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy));
        }


    }
}
