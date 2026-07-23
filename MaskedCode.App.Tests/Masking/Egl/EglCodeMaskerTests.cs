using MaskedCode.App.Masking;
using MaskedCode.App.Masking.Egl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaskedCode.App.Tests.Masking.Egl
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
    function MüşteriKayıtEkle()
    end
    """)]
        public void Mask_WithNotYetSupportedSensitiveContext_ShouldRejectSource(string sourceCode)
        {
            var masker =
                new EglCodeMasker();

            Assert.Throws<NotSupportedException>(
                () => masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy));
        }

        [Theory]
        [InlineData(MaskingMode.MaximumPrivacy)]
        [InlineData(MaskingMode.FormatPreserving)]
        public void Mask_WithRuntimeAndStructuralNumbers_ShouldMaskOnlyRuntimeValues(MaskingMode mode)
        {
            const string sourceCode =
                """
        record CustomerRecord type BasicRecord
            10 CustomerCode char(4);
        end

        CustomerNo decimal(10);
        AccountBalance decimal(15,2);
        CustomerNumbers int[20] { maxSize = 30 };

        CustomerNo = 001;
        AccountBalance = -15478.35;
        CalculationRate = 1.25E+03;
        CustomerNumbers[2] = 9876;

        CoreBusinessException(001);
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    mode);

            var expectedRuntimeLiterals =
                new[]
                {
            "001",
            "15478.35",
            "1.25E+03",
            "2",
            "9876"
                };

            foreach (var literal in expectedRuntimeLiterals)
            {
                var mapping =
                    Assert.Single(
                        result.Mappings.Where(
                            candidate =>
                                candidate.Kind ==
                                    MaskingValueKind.NumericLiteral &&
                                candidate.OriginalValue ==
                                    literal));

                Assert.NotEqual(
                    mapping.OriginalValue,
                    mapping.MaskedValue);

                Assert.Equal(
                    mapping.OriginalValue.Length,
                    mapping.MaskedValue.Length);
            }

            var structuralLiterals =
                new[]
                {
            "10",
            "15",
            "20",
            "30",
            "4"
                };

            foreach (var literal in structuralLiterals)
            {
                Assert.DoesNotContain(
                    result.Mappings,
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.NumericLiteral &&
                        mapping.OriginalValue ==
                            literal);
            }

            Assert.Contains(
                "decimal(10)",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "decimal(15,2)",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "int[20]",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "maxSize = 30",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "char(4)",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            var repeatedLiteralMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.NumericLiteral &&
                            mapping.OriginalValue ==
                                "001"));

            var repeatedUsageCount =
                result.MaskedCode
                    .Split(
                        repeatedLiteralMapping.MaskedValue,
                        StringSplitOptions.None)
                    .Length - 1;

            Assert.Equal(
                2,
                repeatedUsageCount);

            var scientificMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.NumericLiteral &&
                            mapping.OriginalValue ==
                                "1.25E+03"));

            Assert.EndsWith(
                "E+03",
                scientificMapping.MaskedValue);

            Assert.Equal(
                5,
                result.NumericLiteralCount);
        }

        [Theory]
        [InlineData(MaskingMode.MaximumPrivacy)]
        [InlineData(MaskingMode.FormatPreserving)]
        public void Mask_WithDocBlock_ShouldMaskContentAndPreserveDirectiveStructure(MaskingMode mode)
        {
            const string sourceCode =
                """
        program CustomerProgram type BasicProgram
            Description = #doc{
                Customer onboarding for branch 1453
                Owner: Faruk Yazici
            };

            function main()
            end
        end
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    mode);

            var docMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.Comment));

            Assert.Contains(
                "Customer onboarding for branch 1453",
                docMapping.OriginalValue);

            Assert.Contains(
                "Owner: Faruk Yazici",
                docMapping.OriginalValue);

            Assert.Contains(
                "EGL_CMT_",
                docMapping.MaskedValue);

            Assert.DoesNotContain(
                "Customer onboarding for branch 1453",
                result.MaskedCode);

            Assert.DoesNotContain(
                "Faruk Yazici",
                result.MaskedCode);

            Assert.Contains(
                "#doc{",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Equal(
                sourceCode.Count(character => character == '\n'),
                result.MaskedCode.Count(character => character == '\n'));

            Assert.Equal(
                1,
                result.CommentCount);

            Assert.Equal(
                0,
                result.NumericLiteralCount);
        }

        [Fact]
        public void Mask_WithUnterminatedDocBlock_ShouldRejectSource()
        {
            const string sourceCode =
                """
        Description = #doc{Customer sensitive description
        """;

            var masker =
                new EglCodeMasker();

            var exception =
                Assert.Throws<InvalidDataException>(
                    () => masker.Mask(
                        sourceCode,
                        MaskingMode.MaximumPrivacy));

            Assert.Contains(
                "Sonlandırılmamış EGL #doc bloğu",
                exception.Message);
        }

        [Theory]
        [InlineData(MaskingMode.MaximumPrivacy)]
        [InlineData(MaskingMode.FormatPreserving)]
        public void Mask_WithSqlBlock_ShouldPreserveSqlKeywordsAndMaskSensitiveValues(MaskingMode mode)
        {
            const string sourceCode =
                """
        get CustomerRecord singleRow with #sql{
            SELECT MY_SCHEMA.CUSTOMER.CUSTOMER_NO,
                   MY_SCHEMA.CUSTOMER.CUSTOMER_NAME
            FROM MY_SCHEMA.CUSTOMER
            WHERE CUSTOMER_NO = :CustomerInput.CustomerNo
              AND BRANCH_NO = 1453
        };
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    mode);

            var preservedSqlKeywords =
                new[]
                {
            "SELECT",
            "FROM",
            "WHERE",
            "AND"
                };

            foreach (var keyword in preservedSqlKeywords)
            {
                Assert.DoesNotContain(
                    result.Mappings,
                    mapping =>
                        mapping.OriginalValue.Equals(
                            keyword,
                            StringComparison.OrdinalIgnoreCase));

                Assert.Contains(
                    keyword,
                    result.MaskedCode,
                    StringComparison.OrdinalIgnoreCase);
            }

            var sensitiveIdentifiers =
                new[]
                {
            "MY_SCHEMA",
            "CUSTOMER",
            "CUSTOMER_NO",
            "CUSTOMER_NAME",
            "CustomerInput",
            "CustomerNo",
            "BRANCH_NO"
                };

            foreach (var identifier in sensitiveIdentifiers)
            {
                Assert.Contains(
                    result.Mappings,
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue.Equals(
                            identifier,
                            StringComparison.OrdinalIgnoreCase));
            }

            Assert.DoesNotContain(
                "MY_SCHEMA",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain(
                "CUSTOMER_NO",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                "#sql{",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.NumericLiteral &&
                    mapping.OriginalValue ==
                        "1453");
        }


        [Theory]
        [InlineData(MaskingMode.MaximumPrivacy)]
        [InlineData(MaskingMode.FormatPreserving)]
        public void Mask_WithSqlStringLiteralsAndLineComment_ShouldMaskSensitiveContent(MaskingMode mode)
        {
            const string sourceCode =
                """
        get CustomerRecord with #sql{
            SELECT CUSTOMER_NO
            FROM CUSTOMER
            WHERE STATUS = 'ACTIVE'
              AND LAST_NAME = 'O''BRIEN'
              AND EXTERNAL_CODE = 'A\B'
              AND DESCRIPTION = 'CLOSING } MARKER'
            -- Confidential customer filter } 'SECRET'
              AND BRANCH_NO = 1453
        };
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    mode);

            var originalSqlValues =
                new[]
                {
            "ACTIVE",
            "O''BRIEN",
            @"A\B",
            "CLOSING } MARKER"
                };

            foreach (var originalValue in originalSqlValues)
            {
                var mapping =
                    Assert.Single(
                        result.Mappings.Where(
                            candidate =>
                                candidate.Kind ==
                                    MaskingValueKind.StringLiteral &&
                                candidate.OriginalValue ==
                                    originalValue));

                Assert.NotEqual(
                    mapping.OriginalValue,
                    mapping.MaskedValue);

                Assert.DoesNotContain(
                    originalValue,
                    result.MaskedCode,
                    StringComparison.Ordinal);

                Assert.Contains(
                    $"'{mapping.MaskedValue}'",
                    result.MaskedCode,
                    StringComparison.Ordinal);

                if (mode == MaskingMode.FormatPreserving)
                {
                    Assert.Equal(
                        mapping.OriginalValue.Length,
                        mapping.MaskedValue.Length);
                }
            }

            var escapedQuoteMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.StringLiteral &&
                            mapping.OriginalValue ==
                                "O''BRIEN"));

            if (mode == MaskingMode.FormatPreserving)
            {
                Assert.Contains(
                    "''",
                    escapedQuoteMapping.MaskedValue,
                    StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain(
                    "O''BRIEN",
                    escapedQuoteMapping.MaskedValue,
                    StringComparison.Ordinal);
            }

            var sqlCommentMapping =
                Assert.Single(
                    result.Mappings.Where(
                        mapping =>
                            mapping.Kind ==
                                MaskingValueKind.Comment));

            Assert.Contains(
                "Confidential customer filter",
                sqlCommentMapping.OriginalValue,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Confidential customer filter",
                result.MaskedCode,
                StringComparison.Ordinal);

            Assert.Contains(
                "-- EGL_CMT_",
                result.MaskedCode,
                StringComparison.Ordinal);

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.NumericLiteral &&
                    mapping.OriginalValue ==
                        "1453");

            Assert.Contains(
                "AND",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Equal(
                4,
                result.StringLiteralCount);

            Assert.Equal(
                1,
                result.CommentCount);
        }

        [Fact]
        public void Mask_WithRepeatedSqlStringContainingCommentMarker_ShouldReuseMapping()
        {
            const string sourceCode =
                """
        get CustomerRecord with #sql{
            SELECT CUSTOMER_NO
            FROM CUSTOMER
            WHERE STATUS = 'ACTIVE--VALUE'
               OR PREVIOUS_STATUS = 'ACTIVE--VALUE'
        };
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    MaskingMode.MaximumPrivacy);

            var mapping =
                Assert.Single(
                    result.Mappings.Where(
                        candidate =>
                            candidate.Kind ==
                                MaskingValueKind.StringLiteral &&
                            candidate.OriginalValue ==
                                "ACTIVE--VALUE"));

            var repeatedUsageCount =
                result.MaskedCode
                    .Split(
                        $"'{mapping.MaskedValue}'",
                        StringSplitOptions.None)
                    .Length - 1;

            Assert.Equal(
                2,
                repeatedUsageCount);

            Assert.Equal(
                1,
                result.StringLiteralCount);

            Assert.Equal(
                0,
                result.CommentCount);
        }

        [Fact]
        public void Mask_WithUnterminatedSqlStringLiteral_ShouldRejectSource()
        {
            const string sourceCode =
                """
        get CustomerRecord with #sql{
            SELECT CUSTOMER_NO
            FROM CUSTOMER
            WHERE STATUS = 'ACTIVE
        };
        """;

            var masker =
                new EglCodeMasker();

            var exception =
                Assert.Throws<InvalidDataException>(
                    () => masker.Mask(
                        sourceCode,
                        MaskingMode.MaximumPrivacy));

            Assert.Contains(
                "Sonlandırılmamış EGL #sql string literal",
                exception.Message);
        }

        [Fact]
        public void Mask_WithUnterminatedSqlBlock_ShouldRejectSource()
        {
            const string sourceCode =
                """
        get CustomerRecord with #sql{
            SELECT CUSTOMER_NO
            FROM CUSTOMER
        """;

            var masker =
                new EglCodeMasker();

            var exception =
                Assert.Throws<InvalidDataException>(
                    () => masker.Mask(
                        sourceCode,
                        MaskingMode.MaximumPrivacy));

            Assert.Contains(
                "Sonlandırılmamış EGL #sql bloğu",
                exception.Message);
        }

        [Theory]
        [InlineData(MaskingMode.MaximumPrivacy)]
        [InlineData(MaskingMode.FormatPreserving)]
        public void
Mask_WithDb2SqlClauses_ShouldPreserveStructureAndMaskSensitiveIdentifiers(
    MaskingMode mode)
        {
            const string sourceCode =
                """
        Ur char(2);

        get CustomerRecord with #sql{
            SELECT CUSTOMER_NO,
                   CUSTOMER_NAME
              FROM MY_SCHEMA.CUSTOMER
             WHERE STATUS = 'ACTIVE'
             ORDER BY CUSTOMER_NAME
             FETCH FIRST 25 ROWS ONLY
             OPTIMIZE FOR 25 ROWS
             WITH UR
        };

        execute #sql{
            MERGE INTO MY_SCHEMA.CUSTOMER AS TARGET
            USING MY_SCHEMA.CUSTOMER_STAGE AS SOURCE
               ON TARGET.CUSTOMER_NO = SOURCE.CUSTOMER_NO
            WHEN MATCHED THEN
                UPDATE
                   SET TARGET.STATUS = SOURCE.STATUS
            WHEN NOT MATCHED THEN
                INSERT (
                    CUSTOMER_NO,
                    STATUS
                )
                VALUES (
                    SOURCE.CUSTOMER_NO,
                    SOURCE.STATUS
                )
        };
        """;

            var masker =
                new EglCodeMasker();

            var result =
                masker.Mask(
                    sourceCode,
                    mode);

            var structuralSqlValues =
                new[]
                {
            "SELECT",
            "ORDER",
            "BY",
            "FETCH",
            "FIRST",
            "ROWS",
            "ONLY",
            "OPTIMIZE",
            "FOR",
            "WITH",
            "UR",
            "MERGE",
            "INTO",
            "USING",
            "ON",
            "WHEN",
            "MATCHED",
            "THEN",
            "UPDATE",
            "SET",
            "NOT",
            "INSERT",
            "VALUES"
                };

            foreach (var structuralValue in structuralSqlValues)
            {
                Assert.Contains(
                    structuralValue,
                    result.MaskedCode,
                    StringComparison.OrdinalIgnoreCase);
            }

            var sensitiveIdentifiers =
                new[]
                {
            "MY_SCHEMA",
            "CUSTOMER",
            "CUSTOMER_STAGE",
            "CUSTOMER_NO",
            "CUSTOMER_NAME",
            "STATUS",
            "TARGET",
            "SOURCE"
                };

            foreach (var identifier in sensitiveIdentifiers)
            {
                Assert.Contains(
                    result.Mappings,
                    mapping =>
                        mapping.Kind ==
                            MaskingValueKind.Identifier &&
                        mapping.OriginalValue.Equals(
                            identifier,
                            StringComparison.OrdinalIgnoreCase));
            }

            Assert.DoesNotContain(
                "MY_SCHEMA",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain(
                "CUSTOMER_STAGE",
                result.MaskedCode,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.StringLiteral &&
                    mapping.OriginalValue ==
                        "ACTIVE");

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.NumericLiteral &&
                    mapping.OriginalValue ==
                        "25");

            Assert.Contains(
                result.Mappings,
                mapping =>
                    mapping.Kind ==
                        MaskingValueKind.Identifier &&
                    mapping.OriginalValue.Equals(
                        "Ur",
                        StringComparison.Ordinal));
        }


    }
}
