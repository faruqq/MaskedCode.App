using System.IO;
using System.Text;

namespace MaskedCode.App.Masking.Egl;

public sealed class EglCodeUnmasker
{
    public string Unmask(string maskedCode, MappingVaultContent vaultContent)
    {
        ArgumentNullException.ThrowIfNull(maskedCode);
        ArgumentNullException.ThrowIfNull(vaultContent);

        if (maskedCode.Length == 0)
        {
            throw new ArgumentException(
                "Geri açılacak maskelenmiş EGL kodu boş olamaz.",
                nameof(maskedCode));
        }

        if (vaultContent.Mappings is null ||
            vaultContent.Mappings.Count == 0)
        {
            throw new InvalidDataException(
                "Kasa içinde geri açılacak eşleme bulunamadı.");
        }

        var lookup =
            MappingLookup.Create(
                vaultContent.Mappings);

        var restoredCode =
            new StringBuilder(maskedCode.Length);

        var index = 0;
        var isInsideSqlBlock = false;

        while (index < maskedCode.Length)
        {
            if (!isInsideSqlBlock &&
                IsDocBlockStart(
                    maskedCode,
                    index))
            {
                AppendRestoredDocBlock(
                    maskedCode,
                    restoredCode,
                    lookup,
                    ref index);

                continue;
            }

            if (IsLineCommentStart(
                    maskedCode,
                    index))
            {
                AppendRestoredLineComment(
                    maskedCode,
                    restoredCode,
                    lookup,
                    "//",
                    ref index);

                continue;
            }

            if (IsBlockCommentStart(
                    maskedCode,
                    index))
            {
                AppendRestoredBlockComment(
                    maskedCode,
                    restoredCode,
                    lookup,
                    ref index);

                continue;
            }

            if (!isInsideSqlBlock &&
                IsSqlBlockStart(
                    maskedCode,
                    index))
            {
                restoredCode.Append(
                    maskedCode,
                    index,
                    5);

                index += 5;
                isInsideSqlBlock = true;

                continue;
            }

            if (isInsideSqlBlock &&
                IsSqlLineCommentStart(
                    maskedCode,
                    index))
            {
                AppendRestoredLineComment(
                    maskedCode,
                    restoredCode,
                    lookup,
                    "--",
                    ref index);

                continue;
            }

            if (isInsideSqlBlock &&
                maskedCode[index] == '\'')
            {
                AppendRestoredSqlStringLiteral(
                    maskedCode,
                    restoredCode,
                    lookup,
                    ref index);

                continue;
            }

            if (isInsideSqlBlock &&
                maskedCode[index] == '}')
            {
                restoredCode.Append('}');
                index++;
                isInsideSqlBlock = false;

                continue;
            }

            if (maskedCode[index] == '"')
            {
                AppendRestoredStringLiteral(
                    maskedCode,
                    restoredCode,
                    lookup,
                    ref index);

                continue;
            }

            if (EglNumericLiteralMasker.TryReadNumericLiteral(
                    maskedCode,
                    index,
                    out var numericLiteral,
                    out _))
            {
                AppendRestoredValue(
                    numericLiteral,
                    restoredCode,
                    lookup.NumericLiteralMappings,
                    lookup);

                index += numericLiteral.Length;

                continue;
            }

            if (IsIdentifierStart(maskedCode[index]))
            {
                var identifier =
                    ReadIdentifier(
                        maskedCode,
                        ref index);

                AppendRestoredValue(
                    identifier,
                    restoredCode,
                    lookup.IdentifierMappings,
                    lookup);

                continue;
            }

            restoredCode.Append(
                maskedCode[index]);

            index++;
        }

        if (isInsideSqlBlock)
        {
            throw new InvalidDataException(
                "Sonlandırılmamış maskelenmiş EGL #sql bloğu bulundu.");
        }

        lookup.ValidateAllMappingsWereUsed();

        return restoredCode.ToString();
    }

    private static void AppendRestoredLineComment(string maskedCode, StringBuilder restoredCode, MappingLookup lookup, string delimiter, ref int index)
    {
        restoredCode.Append(delimiter);
        index += delimiter.Length;

        var contentStartIndex = index;

        while (index < maskedCode.Length &&
               maskedCode[index] is not '\r' and not '\n')
        {
            index++;
        }

        var maskedValue =
            maskedCode[contentStartIndex..index];

        AppendRestoredValue(
            maskedValue,
            restoredCode,
            lookup.CommentMappings,
            lookup);
    }

    private static void AppendRestoredBlockComment(string maskedCode, StringBuilder restoredCode, MappingLookup lookup, ref int index)
    {
        restoredCode.Append("/*");
        index += 2;

        var contentStartIndex = index;

        while (index < maskedCode.Length &&
               !IsBlockCommentEnd(
                   maskedCode,
                   index))
        {
            index++;
        }

        if (index >= maskedCode.Length)
        {
            throw new InvalidDataException(
                "Sonlandırılmamış maskelenmiş EGL block comment bulundu.");
        }

        var maskedValue =
            maskedCode[contentStartIndex..index];

        AppendRestoredValue(
            maskedValue,
            restoredCode,
            lookup.CommentMappings,
            lookup);

        restoredCode.Append("*/");
        index += 2;
    }

    private static void AppendRestoredDocBlock(string maskedCode, StringBuilder restoredCode, MappingLookup lookup, ref int index)
    {
        restoredCode.Append(
            maskedCode,
            index,
            5);

        index += 5;

        var contentStartIndex = index;

        while (index < maskedCode.Length &&
               maskedCode[index] != '}')
        {
            index++;
        }

        if (index >= maskedCode.Length)
        {
            throw new InvalidDataException(
                "Sonlandırılmamış maskelenmiş EGL #doc bloğu bulundu.");
        }

        var maskedValue =
            maskedCode[contentStartIndex..index];

        AppendRestoredValue(
            maskedValue,
            restoredCode,
            lookup.CommentMappings,
            lookup);

        restoredCode.Append('}');
        index++;
    }

    private static void AppendRestoredStringLiteral(string maskedCode, StringBuilder restoredCode, MappingLookup lookup, ref int index)
    {
        restoredCode.Append('"');
        index++;

        var value =
            new StringBuilder();

        while (index < maskedCode.Length)
        {
            var current =
                maskedCode[index];

            if (current == '\\')
            {
                value.Append(current);
                index++;

                if (index < maskedCode.Length)
                {
                    value.Append(
                        maskedCode[index]);

                    index++;
                }

                continue;
            }

            if (current == '"')
            {
                AppendRestoredValue(
                    value.ToString(),
                    restoredCode,
                    lookup.StringLiteralMappings,
                    lookup);

                restoredCode.Append('"');
                index++;

                return;
            }

            value.Append(current);
            index++;
        }

        throw new InvalidDataException(
            "Sonlandırılmamış maskelenmiş EGL string literal bulundu.");
    }

    private static void AppendRestoredSqlStringLiteral(string maskedCode, StringBuilder restoredCode, MappingLookup lookup, ref int index)
    {
        restoredCode.Append('\'');
        index++;

        var value =
            new StringBuilder();

        while (index < maskedCode.Length)
        {
            if (maskedCode[index] != '\'')
            {
                value.Append(
                    maskedCode[index]);

                index++;
                continue;
            }

            if (index + 1 < maskedCode.Length &&
                maskedCode[index + 1] == '\'')
            {
                value.Append("''");
                index += 2;

                continue;
            }

            AppendRestoredValue(
                value.ToString(),
                restoredCode,
                lookup.StringLiteralMappings,
                lookup);

            restoredCode.Append('\'');
            index++;

            return;
        }

        throw new InvalidDataException(
            "Sonlandırılmamış maskelenmiş EGL #sql string literal bulundu.");
    }

    private static void AppendRestoredValue(string maskedValue, StringBuilder restoredCode, IReadOnlyDictionary<string, MappingEntry> mappings, MappingLookup lookup)
    {
        if (!mappings.TryGetValue(
                maskedValue,
                out var mapping))
        {
            restoredCode.Append(maskedValue);
            return;
        }

        restoredCode.Append(
            mapping.OriginalValue);

        lookup.MarkAsUsed(
            mapping.Index);
    }

    private static string ReadIdentifier(string sourceCode, ref int index)
    {
        var startIndex = index;
        index++;

        while (index < sourceCode.Length &&
               IsIdentifierPart(
                   sourceCode[index]))
        {
            index++;
        }

        return sourceCode[startIndex..index];
    }

    private static bool IsDocBlockStart(string sourceCode, int index)
    {
        return index + 4 < sourceCode.Length &&
               sourceCode[index] == '#' &&
               sourceCode[index + 4] == '{' &&
               string.Compare(
                   sourceCode,
                   index + 1,
                   "doc",
                   0,
                   3,
                   StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsSqlBlockStart(string sourceCode, int index)
    {
        return index + 4 < sourceCode.Length &&
               sourceCode[index] == '#' &&
               sourceCode[index + 4] == '{' &&
               string.Compare(
                   sourceCode,
                   index + 1,
                   "sql",
                   0,
                   3,
                   StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsLineCommentStart(string sourceCode, int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '/' &&
               sourceCode[index + 1] == '/';
    }

    private static bool IsSqlLineCommentStart(string sourceCode, int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '-' &&
               sourceCode[index + 1] == '-';
    }

    private static bool IsBlockCommentStart(string sourceCode, int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '/' &&
               sourceCode[index + 1] == '*';
    }

    private static bool IsBlockCommentEnd(string sourceCode, int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '*' &&
               sourceCode[index + 1] == '/';
    }

    private static bool IsIdentifierStart(char character)
    {
        return character is >= 'A' and <= 'Z' or
               >= 'a' and <= 'z' or
               '_';
    }

    private static bool IsIdentifierPart(char character)
    {
        return IsIdentifierStart(character) ||
               character is >= '0' and <= '9';
    }

    private sealed class MappingLookup
    {
        private readonly IReadOnlyList<MaskingMapping> _mappings;
        private readonly bool[] _usedMappings;

        private MappingLookup(IReadOnlyList<MaskingMapping> mappings, Dictionary<string, MappingEntry> identifierMappings, Dictionary<string, MappingEntry> stringLiteralMappings, Dictionary<string, MappingEntry> numericLiteralMappings, Dictionary<string, MappingEntry> commentMappings)
        {
            _mappings = mappings;
            _usedMappings = new bool[mappings.Count];

            IdentifierMappings =
                identifierMappings;

            StringLiteralMappings =
                stringLiteralMappings;

            NumericLiteralMappings =
                numericLiteralMappings;

            CommentMappings =
                commentMappings;
        }

        public IReadOnlyDictionary<string, MappingEntry> IdentifierMappings { get; }

        public IReadOnlyDictionary<string, MappingEntry> StringLiteralMappings { get; }

        public IReadOnlyDictionary<string, MappingEntry> NumericLiteralMappings { get; }

        public IReadOnlyDictionary<string, MappingEntry> CommentMappings { get; }

        public static MappingLookup Create(IReadOnlyList<MaskingMapping> mappings)
        {
            var identifierMappings =
            new Dictionary<string, MappingEntry>(
                StringComparer.Ordinal);

            var stringLiteralMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            var numericLiteralMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            var commentMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            for (var index = 0;
                 index < mappings.Count;
                 index++)
            {
                var mapping =
                    mappings[index];

                if (mapping is null)
                {
                    throw new InvalidDataException(
                        "Kasa içinde geçersiz bir eşleme bulundu.");
                }

                if (string.IsNullOrEmpty(
                        mapping.OriginalValue) ||
                    string.IsNullOrEmpty(
                        mapping.MaskedValue))
                {
                    throw new InvalidDataException(
                        "Kasa içinde boş değere sahip bir eşleme bulundu.");
                }

                var targetDictionary =
                    mapping.Kind switch
                    {
                        MaskingValueKind.Identifier =>
                            identifierMappings,

                        MaskingValueKind.StringLiteral =>
                            stringLiteralMappings,

                        MaskingValueKind.NumericLiteral =>
                            numericLiteralMappings,

                        MaskingValueKind.Comment =>
                            commentMappings,

                        _ => throw new InvalidDataException(
                            "Kasa içinde desteklenmeyen bir " +
                            "eşleme türü bulundu.")
                    };

                var entry =
                    new MappingEntry(
                        index,
                        mapping.OriginalValue);

                if (!targetDictionary.TryAdd(
                        mapping.MaskedValue,
                        entry))
                {
                    throw new InvalidDataException(
                        "Kasa içinde aynı maskelenmiş değere " +
                        "sahip birden fazla eşleme bulundu.");
                }
            }

            return new MappingLookup(
                mappings,
                identifierMappings,
                stringLiteralMappings,
                numericLiteralMappings,
                commentMappings);
        }

        public void MarkAsUsed(int mappingIndex)
        {
            _usedMappings[mappingIndex] = true;
        }

        public void ValidateAllMappingsWereUsed()
        {
            for (var index = 0;
                 index < _usedMappings.Length;
                 index++)
            {
                if (_usedMappings[index])
                {
                    continue;
                }

                var unusedMapping =
                    _mappings[index];

                throw new InvalidDataException(
                    "Kasa içindeki bir eşleme maskelenmiş " +
                    "EGL kodunda bulunamadı. Geri açma işlemi " +
                    $"durduruldu. Tür: {unusedMapping.Kind}");
            }
        }
    }

    private sealed record MappingEntry(int Index, string OriginalValue);
}