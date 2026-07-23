using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking.Egl;

internal sealed class EglCodeMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    public EglMaskingResult Mask(string sourceCode)
    {
        return Mask(
            sourceCode,
            MaskingMode.MaximumPrivacy);
    }

    public EglMaskingResult Mask(string sourceCode, MaskingMode mode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        ValidateMaskingMode(mode);

        var identifierMappings =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var stringLiteralMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var numericLiteralMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var commentMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var usedMaskedIdentifiers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var usedMaskedStringLiterals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var usedMaskedNumericLiterals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var originalIdentifiers =
            CollectOriginalIdentifiers(
                sourceCode,
                out var originalNumericLiterals);

        var originalStringLiterals =
            CollectOriginalStringLiterals(sourceCode);

        var sessionId = Guid.NewGuid()
            .ToString("N")[..8]
            .ToUpperInvariant();

        var maskedCode =
            new StringBuilder(sourceCode.Length);

        var index = 0;

        while (index < sourceCode.Length)
        {
            if (IsLineCommentStart(
                    sourceCode,
                    index))
            {
                AppendMaskedLineComment(
                    sourceCode,
                    maskedCode,
                    commentMappings,
                    sessionId,
                    ref index);

                continue;
            }

            if (IsBlockCommentStart(
                    sourceCode,
                    index))
            {
                AppendMaskedBlockComment(
                    sourceCode,
                    maskedCode,
                    commentMappings,
                    sessionId,
                    ref index);

                continue;
            }

            if (IsDocBlockStart(
                    sourceCode,
                    index))
            {
                AppendMaskedDocBlock(
                    sourceCode,
                    maskedCode,
                    commentMappings,
                    sessionId,
                    ref index);

                continue;
            }

            if (IsStringDelimiter(sourceCode[index]))
            {
                AppendMaskedStringLiteral(
                    sourceCode,
                    maskedCode,
                    stringLiteralMappings,
                    usedMaskedStringLiterals,
                    originalStringLiterals,
                    sessionId,
                    mode,
                    ref index);

                continue;
            }

            if (EglNumericLiteralMasker.TryReadNumericLiteral(
                    sourceCode,
                    index,
                    out var numericLiteral,
                    out var numericLiteralKind))
            {
                var maskedNumericLiteral =
                    EglNumericLiteralMasker.MaskLiteral(
                        sourceCode,
                        index,
                        numericLiteral,
                        numericLiteralKind,
                        numericLiteralMappings,
                        usedMaskedNumericLiterals,
                        originalNumericLiterals);

                maskedCode.Append(maskedNumericLiteral);
                index += numericLiteral.Length;

                continue;
            }

            RejectUnsupportedContext(
                sourceCode,
                index);

            if (!IsIdentifierStart(sourceCode[index]))
            {
                maskedCode.Append(sourceCode[index]);
                index++;

                continue;
            }

            AppendIdentifier(
                sourceCode,
                maskedCode,
                identifierMappings,
                usedMaskedIdentifiers,
                originalIdentifiers,
                sessionId,
                mode,
                ref index);
        }

        var mappings =
            CreateMappings(
                identifierMappings,
                stringLiteralMappings,
                numericLiteralMappings,
                commentMappings);

        return new EglMaskingResult(
            maskedCode.ToString(),
            mappings,
            mode);
    }

    private static IReadOnlyList<MaskingMapping>
    CreateMappings(
        IReadOnlyDictionary<string, string>
            identifierMappings,
        IReadOnlyDictionary<string, string>
            stringLiteralMappings,
        IReadOnlyDictionary<string, string>
            numericLiteralMappings,
        IReadOnlyDictionary<string, string>
            commentMappings)
    {
        var mappings =
            new List<MaskingMapping>(
                identifierMappings.Count +
                stringLiteralMappings.Count +
                numericLiteralMappings.Count +
                commentMappings.Count);

        foreach (var mapping in identifierMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    mapping.Key,
                    mapping.Value));
        }

        foreach (var mapping in stringLiteralMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.StringLiteral,
                    mapping.Key,
                    mapping.Value));
        }

        foreach (var mapping in numericLiteralMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.NumericLiteral,
                    mapping.Key,
                    mapping.Value));
        }

        foreach (var mapping in commentMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.Comment,
                    mapping.Key,
                    mapping.Value));
        }

        return mappings;
    }

    private static void AppendIdentifier(
        string sourceCode,
        StringBuilder maskedCode,
        IDictionary<string, string> mappings,
        ISet<string> usedMaskedIdentifiers,
        ISet<string> originalIdentifiers,
        string sessionId,
        MaskingMode mode,
        ref int index)
    {
        var startIndex = index;

        var identifier =
            ReadIdentifier(
                sourceCode,
                ref index);

        if (IsDirectiveReference(
                sourceCode,
                startIndex))
        {
            throw new NotSupportedException(
                $"EGL #{identifier} bloğu maskelemesi " +
                "henüz desteklenmiyor.");
        }

        if (ShouldPreserveIdentifier(
                sourceCode,
                identifier,
                startIndex,
                index))
        {
            maskedCode.Append(identifier);
            return;
        }

        if (!mappings.TryGetValue(
                identifier,
                out var maskedIdentifier))
        {
            maskedIdentifier =
                CreateUniqueMaskedIdentifier(
                    identifier,
                    mappings.Count + 1,
                    sessionId,
                    mode,
                    usedMaskedIdentifiers,
                    originalIdentifiers);

            mappings.Add(
                identifier,
                maskedIdentifier);

            usedMaskedIdentifiers.Add(
                maskedIdentifier);
        }

        maskedCode.Append(maskedIdentifier);
    }

    private static void AppendMaskedStringLiteral(
        string sourceCode,
        StringBuilder maskedCode,
        IDictionary<string, string> mappings,
        ISet<string> usedMaskedValues,
        ISet<string> originalValues,
        string sessionId,
        MaskingMode mode,
        ref int index)
    {
        var value =
            ReadStringLiteral(
                sourceCode,
                ref index);

        maskedCode.Append('"');

        if (!ContainsMaskableCharacter(value))
        {
            maskedCode.Append(value);
            maskedCode.Append('"');

            return;
        }

        if (!mappings.TryGetValue(
                value,
                out var maskedValue))
        {
            maskedValue =
                CreateUniqueMaskedStringLiteral(
                    value,
                    mappings.Count + 1,
                    sessionId,
                    mode,
                    usedMaskedValues,
                    originalValues);

            mappings.Add(
                value,
                maskedValue);

            usedMaskedValues.Add(
                maskedValue);
        }

        maskedCode.Append(maskedValue);
        maskedCode.Append('"');
    }

    private static string ReadStringLiteral(
        string sourceCode,
        ref int index)
    {
        index++;

        var value =
            new StringBuilder();

        while (index < sourceCode.Length)
        {
            var current =
                sourceCode[index];

            if (current == '\\')
            {
                value.Append(current);
                index++;

                if (index < sourceCode.Length)
                {
                    value.Append(
                        sourceCode[index]);

                    index++;
                }

                continue;
            }

            if (current == '"')
            {
                index++;
                return value.ToString();
            }

            value.Append(current);
            index++;
        }

        throw new InvalidDataException(
            "Sonlandırılmamış EGL string literal bulundu.");
    }

    private static string
        CreateUniqueMaskedStringLiteral(
            string originalValue,
            int ordinal,
            string sessionId,
            MaskingMode mode,
            ISet<string> usedMaskedValues,
            ISet<string> originalValues)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate = mode switch
            {
                MaskingMode.MaximumPrivacy =>
                    CreateMaximumPrivacyStringLiteral(
                        sessionId,
                        ordinal + attempt),

                MaskingMode.FormatPreserving =>
                    CreateFormatPreservingStringLiteral(
                        originalValue),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Desteklenmeyen maskeleme modu.")
            };

            if (string.Equals(
                    candidate,
                    originalValue,
                    StringComparison.Ordinal) ||
                originalValues.Contains(candidate) ||
                usedMaskedValues.Contains(candidate))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            "EGL string literal için benzersiz bir " +
            "maskeleme değeri üretilemedi.");
    }

    private static string
        CreateMaximumPrivacyStringLiteral(
            string sessionId,
            int ordinal)
    {
        return $"EGL_STR_{sessionId}_{ordinal:D4}";
    }

    private static string
        CreateFormatPreservingStringLiteral(
            string originalValue)
    {
        var maskedValue =
            new StringBuilder(
                originalValue.Length);

        var index = 0;

        while (index < originalValue.Length)
        {
            if (originalValue[index] == '\\' &&
                index + 1 < originalValue.Length)
            {
                maskedValue.Append(
                    originalValue[index]);

                maskedValue.Append(
                    originalValue[index + 1]);

                index += 2;
                continue;
            }

            maskedValue.Append(
                CreateFormatPreservingCharacter(
                    originalValue[index]));

            index++;
        }

        return maskedValue.ToString();
    }

    private static void AppendMaskedLineComment(
        string sourceCode,
        StringBuilder maskedCode,
        IDictionary<string, string> mappings,
        string sessionId,
        ref int index)
    {
        maskedCode.Append("//");
        index += 2;

        var contentStartIndex = index;

        while (index < sourceCode.Length &&
               sourceCode[index] is not '\r' and not '\n')
        {
            index++;
        }

        var originalValue =
            sourceCode[contentStartIndex..index];

        AppendMaskedCommentBody(
            originalValue,
            maskedCode,
            mappings,
            sessionId);
    }

    private static void AppendMaskedBlockComment(
        string sourceCode,
        StringBuilder maskedCode,
        IDictionary<string, string> mappings,
        string sessionId,
        ref int index)
    {
        maskedCode.Append("/*");
        index += 2;

        var contentStartIndex = index;

        while (index < sourceCode.Length &&
               !IsBlockCommentEnd(
                   sourceCode,
                   index))
        {
            index++;
        }

        if (index >= sourceCode.Length)
        {
            throw new InvalidDataException(
                "Sonlandırılmamış EGL block comment bulundu.");
        }

        var originalValue =
            sourceCode[contentStartIndex..index];

        AppendMaskedCommentBody(
            originalValue,
            maskedCode,
            mappings,
            sessionId);

        maskedCode.Append("*/");
        index += 2;
    }

    private static void AppendMaskedDocBlock(string sourceCode, StringBuilder maskedCode, IDictionary<string, string> mappings, string sessionId, ref int index)
    {
        maskedCode.Append(
            sourceCode,
            index,
            5);

        var originalValue =
            ReadDocBlockContent(
                sourceCode,
                ref index);

        AppendMaskedCommentBody(
            originalValue,
            maskedCode,
            mappings,
            sessionId);

        maskedCode.Append('}');
    }

    private static string ReadDocBlockContent(string sourceCode, ref int index)
    {
        if (!IsDocBlockStart(
                sourceCode,
                index))
        {
            throw new InvalidDataException(
                "Geçersiz EGL #doc bloğu başlangıcı bulundu.");
        }

        index += 5;

        var contentStartIndex = index;
        var braceDepth = 1;

        while (index < sourceCode.Length)
        {
            if (sourceCode[index] == '{')
            {
                braceDepth++;
                index++;

                continue;
            }

            if (sourceCode[index] != '}')
            {
                index++;
                continue;
            }

            braceDepth--;

            if (braceDepth == 0)
            {
                var content =
                    sourceCode[contentStartIndex..index];

                index++;

                return content;
            }

            index++;
        }

        throw new InvalidDataException(
            "Sonlandırılmamış EGL #doc bloğu bulundu.");
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

    private static void AppendMaskedCommentBody(
        string originalValue,
        StringBuilder maskedCode,
        IDictionary<string, string> mappings,
        string sessionId)
    {
        if (string.IsNullOrWhiteSpace(
                originalValue))
        {
            maskedCode.Append(originalValue);
            return;
        }

        if (!mappings.TryGetValue(
                originalValue,
                out var maskedValue))
        {
            var placeholder =
                CreateMaximumPrivacyComment(
                    sessionId,
                    mappings.Count + 1);

            maskedValue =
                CreateMaskedCommentBody(
                    originalValue,
                    placeholder);

            mappings.Add(
                originalValue,
                maskedValue);
        }

        maskedCode.Append(maskedValue);
    }

    private static string CreateMaximumPrivacyComment(
        string sessionId,
        int ordinal)
    {
        return $"EGL_CMT_{sessionId}_{ordinal:D4}";
    }

    private static string CreateMaskedCommentBody(
        string originalValue,
        string placeholder)
    {
        var maskedValue =
            new StringBuilder();

        maskedValue.Append(' ');
        maskedValue.Append(placeholder);

        var index = 0;

        while (index < originalValue.Length)
        {
            if (originalValue[index] == '\r')
            {
                maskedValue.Append('\r');
                index++;

                if (index < originalValue.Length &&
                    originalValue[index] == '\n')
                {
                    maskedValue.Append('\n');
                    index++;
                }

                AppendLineIndentation(
                    originalValue,
                    maskedValue,
                    ref index);

                continue;
            }

            if (originalValue[index] == '\n')
            {
                maskedValue.Append('\n');
                index++;

                AppendLineIndentation(
                    originalValue,
                    maskedValue,
                    ref index);

                continue;
            }

            index++;
        }

        return maskedValue.ToString();
    }

    private static void AppendLineIndentation(
        string originalValue,
        StringBuilder maskedValue,
        ref int index)
    {
        while (index < originalValue.Length &&
               originalValue[index] is ' ' or '\t')
        {
            maskedValue.Append(
                originalValue[index]);

            index++;
        }
    }

    private static bool ShouldPreserveIdentifier(
        string sourceCode,
        string identifier,
        int startIndex,
        int endIndex)
    {
        if (EglKeywordCatalog.IsKeyword(identifier) ||
            EglKeywordCatalog.IsBuiltInType(identifier) ||
            EglKeywordCatalog.IsSystemRoot(identifier))
        {
            return true;
        }

        if (EglKeywordCatalog.IsMetadataProperty(identifier) &&
            IsFollowedByAssignment(
                sourceCode,
                endIndex))
        {
            return true;
        }

        if (EglKeywordCatalog.IsEntryPointName(identifier) &&
            IsFunctionName(
                sourceCode,
                startIndex))
        {
            return true;
        }

        return IsSystemMember(
            sourceCode,
            startIndex);
    }

    private static bool IsFunctionName(
        string sourceCode,
        int identifierStartIndex)
    {
        var previousIdentifier =
            ReadPreviousIdentifier(
                sourceCode,
                identifierStartIndex);

        return previousIdentifier.Equals(
            "function",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemMember(
        string sourceCode,
        int identifierStartIndex)
    {
        var index = identifierStartIndex - 1;

        SkipWhitespaceBackward(
            sourceCode,
            ref index);

        if (index < 0 ||
            sourceCode[index] != '.')
        {
            return false;
        }

        index--;

        SkipWhitespaceBackward(
            sourceCode,
            ref index);

        var rootEndIndex = index + 1;

        while (index >= 0 &&
               IsIdentifierPart(
                   sourceCode[index]))
        {
            index--;
        }

        var rootStartIndex = index + 1;

        if (rootStartIndex >= rootEndIndex)
        {
            return false;
        }

        var root =
            sourceCode[rootStartIndex..rootEndIndex];

        return EglKeywordCatalog.IsSystemRoot(root);
    }

    private static string ReadPreviousIdentifier(
        string sourceCode,
        int identifierStartIndex)
    {
        var index = identifierStartIndex - 1;

        SkipWhitespaceBackward(
            sourceCode,
            ref index);

        var endIndex = index + 1;

        while (index >= 0 &&
               IsIdentifierPart(
                   sourceCode[index]))
        {
            index--;
        }

        var startIndex = index + 1;

        return startIndex < endIndex
            ? sourceCode[startIndex..endIndex]
            : string.Empty;
    }

    private static void SkipWhitespaceBackward(
        string sourceCode,
        ref int index)
    {
        while (index >= 0 &&
               char.IsWhiteSpace(
                   sourceCode[index]))
        {
            index--;
        }
    }

    private static bool IsFollowedByAssignment(
        string sourceCode,
        int identifierEndIndex)
    {
        var index = identifierEndIndex;

        while (index < sourceCode.Length &&
               char.IsWhiteSpace(
                   sourceCode[index]))
        {
            index++;
        }

        return index < sourceCode.Length &&
               sourceCode[index] == '=';
    }

    private static bool IsDirectiveReference(
        string sourceCode,
        int identifierStartIndex)
    {
        return identifierStartIndex > 0 &&
               sourceCode[identifierStartIndex - 1] == '#';
    }

    private static string CreateUniqueMaskedIdentifier(
        string identifier,
        int ordinal,
        string sessionId,
        MaskingMode mode,
        ISet<string> usedMaskedIdentifiers,
        ISet<string> originalIdentifiers)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate = mode switch
            {
                MaskingMode.MaximumPrivacy =>
                    CreateMaximumPrivacyIdentifier(
                        sessionId,
                        ordinal + attempt),

                MaskingMode.FormatPreserving =>
                    CreateFormatPreservingIdentifier(
                        identifier),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Desteklenmeyen maskeleme modu.")
            };

            if (EglKeywordCatalog.IsReservedCandidate(candidate) ||
                originalIdentifiers.Contains(candidate) ||
                usedMaskedIdentifiers.Contains(candidate))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{identifier}' EGL identifier'ı için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private static string CreateMaximumPrivacyIdentifier(
        string sessionId,
        int ordinal)
    {
        return $"EGL_{sessionId}_{ordinal:D4}";
    }

    private static string CreateFormatPreservingIdentifier(
        string identifier)
    {
        var maskedIdentifier =
            new StringBuilder(
                identifier.Length);

        foreach (var character in identifier)
        {
            maskedIdentifier.Append(
                CreateFormatPreservingCharacter(
                    character));
        }

        return maskedIdentifier.ToString();
    }

    private static char CreateFormatPreservingCharacter(
        char character)
    {
        if (char.IsUpper(character))
        {
            return (char)(
                'A' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (char.IsLower(character))
        {
            return (char)(
                'a' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (char.IsLetter(character))
        {
            return (char)(
                'A' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (char.IsDigit(character))
        {
            return (char)(
                '0' +
                RandomNumberGenerator.GetInt32(10));
        }

        return character;
    }

    private static HashSet<string> CollectOriginalIdentifiers(string sourceCode, out HashSet<string> originalNumericLiterals)
    {
        var identifiers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        originalNumericLiterals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var index = 0;

        while (index < sourceCode.Length)
        {
            if (IsLineCommentStart(
                    sourceCode,
                    index))
            {
                SkipLineComment(
                    sourceCode,
                    ref index);

                continue;
            }

            if (IsBlockCommentStart(
                    sourceCode,
                    index))
            {
                SkipBlockComment(
                    sourceCode,
                    ref index);

                continue;
            }

            if (IsDocBlockStart(
                    sourceCode,
                    index))
            {
                ReadDocBlockContent(
                    sourceCode,
                    ref index);

                continue;
            }

            if (IsStringDelimiter(sourceCode[index]))
            {
                ReadStringLiteral(
                    sourceCode,
                    ref index);

                continue;
            }

            if (EglNumericLiteralMasker.TryReadNumericLiteral(
                    sourceCode,
                    index,
                    out var numericLiteral,
                    out _))
            {
                originalNumericLiterals.Add(numericLiteral);
                index += numericLiteral.Length;

                continue;
            }

            if (!IsIdentifierStart(sourceCode[index]))
            {
                index++;
                continue;
            }

            identifiers.Add(
                ReadIdentifier(
                    sourceCode,
                    ref index));
        }

        return identifiers;
    }

    private static HashSet<string> CollectOriginalStringLiterals(string sourceCode)
    {
        var literals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var index = 0;

        while (index < sourceCode.Length)
        {
            if (IsLineCommentStart(
                    sourceCode,
                    index))
            {
                SkipLineComment(
                    sourceCode,
                    ref index);

                continue;
            }

            if (IsBlockCommentStart(
                    sourceCode,
                    index))
            {
                SkipBlockComment(
                    sourceCode,
                    ref index);

                continue;
            }

            if (IsDocBlockStart(
                    sourceCode,
                    index))
            {
                ReadDocBlockContent(
                    sourceCode,
                    ref index);

                continue;
            }

            if (!IsStringDelimiter(sourceCode[index]))
            {
                index++;
                continue;
            }

            var value =
                ReadStringLiteral(
                    sourceCode,
                    ref index);

            if (ContainsMaskableCharacter(value))
            {
                literals.Add(value);
            }
        }

        return literals;
    }

    private static string ReadIdentifier(
        string sourceCode,
        ref int index)
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

    private static void SkipLineComment(
        string sourceCode,
        ref int index)
    {
        index += 2;

        while (index < sourceCode.Length &&
               sourceCode[index] is not '\r' and not '\n')
        {
            index++;
        }
    }

    private static void SkipBlockComment(
        string sourceCode,
        ref int index)
    {
        index += 2;

        while (index < sourceCode.Length &&
               !IsBlockCommentEnd(
                   sourceCode,
                   index))
        {
            index++;
        }

        if (index >= sourceCode.Length)
        {
            throw new InvalidDataException(
                "Sonlandırılmamış EGL block comment bulundu.");
        }

        index += 2;
    }

    private static bool ContainsMaskableCharacter(
        string value)
    {
        return value.Any(
            character =>
                char.IsLetterOrDigit(character));
    }

    private static void RejectUnsupportedContext(
    string sourceCode,
    int index)
    {
        if (char.IsLetter(sourceCode[index]) &&
            !IsAsciiLetter(sourceCode[index]))
        {
            throw new NotSupportedException(
                "ASCII dışındaki EGL identifier karakterleri " +
                "henüz desteklenmiyor.");
        }
    }

    private static bool IsLineCommentStart(
        string sourceCode,
        int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '/' &&
               sourceCode[index + 1] == '/';
    }

    private static bool IsBlockCommentStart(
        string sourceCode,
        int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '/' &&
               sourceCode[index + 1] == '*';
    }

    private static bool IsBlockCommentEnd(
        string sourceCode,
        int index)
    {
        return index + 1 < sourceCode.Length &&
               sourceCode[index] == '*' &&
               sourceCode[index + 1] == '/';
    }

    private static bool IsStringDelimiter(
        char character)
    {
        return character == '"';
    }

    private static bool IsIdentifierStart(
        char character)
    {
        return IsAsciiLetter(character) ||
               character == '_';
    }

    private static bool IsIdentifierPart(
        char character)
    {
        return IsIdentifierStart(character) ||
               character is >= '0' and <= '9';
    }

    private static bool IsAsciiLetter(
        char character)
    {
        return character is >= 'A' and <= 'Z' or
               >= 'a' and <= 'z';
    }

    private static void ValidateMaskingMode(
        MaskingMode mode)
    {
        if (Enum.IsDefined(
                typeof(MaskingMode),
                mode))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(mode),
            mode,
            "Desteklenmeyen maskeleme modu.");
    }
}