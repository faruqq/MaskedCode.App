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

        [Theory]
        [InlineData(
            """
    ErrorText = "CUSTOMER NOT FOUND";
    """)]
        [InlineData(
            """
    // Müşteri kontrol açıklaması
    CorePreMain();
    """)]
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
    function MusteriKayitEkle()
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
