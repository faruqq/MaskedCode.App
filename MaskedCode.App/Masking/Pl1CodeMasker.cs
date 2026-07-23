using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking;

public sealed class Pl1CodeMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    public Pl1MaskingResult Mask(string sourceCode)
    {
        return Mask(
            sourceCode,
            MaskingMode.MaximumPrivacy);
    }

    public Pl1MaskingResult Mask(
    string sourceCode,
    MaskingMode mode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        ValidateMaskingMode(mode);

        var numericMasker =
            new Pl1NumericLiteralMasker();

        var numericResult =
            numericMasker.Mask(sourceCode);

        var workingCode =
            numericResult.MaskedCode;

        var identifierMappings =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var stringLiteralMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var usedMaskedIdentifiers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var usedMaskedStringLiterals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var originalIdentifiers =
            CollectOriginalIdentifiers(workingCode);

        var originalStringLiterals =
            CollectOriginalStringLiterals(workingCode);

        var sessionId = Guid.NewGuid()
            .ToString("N")[..8]
            .ToUpperInvariant();

        var maskedCode =
            new StringBuilder(workingCode.Length);

        var index = 0;

        while (index < workingCode.Length)
        {
            if (IsCommentStart(workingCode, index))
            {
                AppendComment(
                    workingCode,
                    maskedCode,
                    ref index);

                continue;
            }

            if (IsQuote(workingCode[index]))
            {
                if (numericResult
                    .StructuralQuotedTextStartIndexes
                    .Contains(index))
                {
                    AppendUnmaskedQuotedText(
                        workingCode,
                        maskedCode,
                        ref index);
                }
                else
                {
                    AppendMaskedQuotedText(
                        workingCode,
                        maskedCode,
                        stringLiteralMappings,
                        usedMaskedStringLiterals,
                        originalStringLiterals,
                        sessionId,
                        mode,
                        ref index);
                }

                continue;
            }

            if (numericResult
                .ScientificExponentMarkerIndexes
                .Contains(index))
            {
                maskedCode.Append(workingCode[index]);
                index++;

                continue;
            }

            if (IsIdentifierStart(workingCode[index]))
            {
                AppendMaskedIdentifier(
                    workingCode,
                    maskedCode,
                    identifierMappings,
                    usedMaskedIdentifiers,
                    originalIdentifiers,
                    sessionId,
                    mode,
                    ref index);

                continue;
            }

            maskedCode.Append(workingCode[index]);
            index++;
        }

        var mappings = CreateMappings(
            identifierMappings,
            stringLiteralMappings,
            numericResult.Mappings);

        return new Pl1MaskingResult(
            maskedCode.ToString(),
            mappings,
            mode);
    }

    private static void ValidateMaskingMode(
        MaskingMode mode)
    {
        if (Enum.IsDefined(typeof(MaskingMode), mode))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(mode),
            mode,
            "Desteklenmeyen maskeleme modu.");
    }

    private static IReadOnlyList<MaskingMapping>
        CreateMappings(
            IReadOnlyDictionary<string, string>
                identifierMappings,
            IReadOnlyDictionary<string, string>
                stringLiteralMappings,
            IReadOnlyDictionary<string, string>
                numericLiteralMappings)
    {
        var mappings = new List<MaskingMapping>(
            identifierMappings.Count +
            stringLiteralMappings.Count +
            numericLiteralMappings.Count);

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

        return mappings;
    }

    private static HashSet<string>
        CollectOriginalIdentifiers(
            string sourceCode)
    {
        var identifiers = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var index = 0;

        while (index < sourceCode.Length)
        {
            if (IsCommentStart(sourceCode, index))
            {
                SkipComment(sourceCode, ref index);
                continue;
            }

            if (IsQuote(sourceCode[index]))
            {
                ReadQuotedText(
                    sourceCode,
                    ref index,
                    out _,
                    out _);

                continue;
            }

            if (!IsIdentifierStart(sourceCode[index]))
            {
                index++;
                continue;
            }

            var startIndex = index;
            index++;

            while (index < sourceCode.Length &&
                   IsIdentifierPart(sourceCode[index]))
            {
                index++;
            }

            identifiers.Add(
                sourceCode[startIndex..index]);
        }

        return identifiers;
    }

    private static HashSet<string>
        CollectOriginalStringLiterals(
            string sourceCode)
    {
        var literals = new HashSet<string>(
            StringComparer.Ordinal);

        var index = 0;

        while (index < sourceCode.Length)
        {
            if (IsCommentStart(sourceCode, index))
            {
                SkipComment(sourceCode, ref index);
                continue;
            }

            if (!IsQuote(sourceCode[index]))
            {
                index++;
                continue;
            }

            var value = ReadQuotedText(
                sourceCode,
                ref index,
                out _,
                out _);

            if (ContainsMaskableCharacter(value))
            {
                literals.Add(value);
            }
        }

        return literals;
    }

    private static void AppendMaskedIdentifier(
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
        index++;

        while (index < sourceCode.Length &&
               IsIdentifierPart(sourceCode[index]))
        {
            index++;
        }

        var identifier = sourceCode[startIndex..index];

        if (Pl1KeywordCatalog.Contains(identifier))
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

    private static void AppendMaskedQuotedText(
        string sourceCode,
        StringBuilder maskedCode,
        IDictionary<string, string> mappings,
        ISet<string> usedMaskedValues,
        ISet<string> originalValues,
        string sessionId,
        MaskingMode mode,
        ref int index)
    {
        var value = ReadQuotedText(
            sourceCode,
            ref index,
            out var quote,
            out var isTerminated);

        maskedCode.Append(quote);

        if (!ContainsMaskableCharacter(value))
        {
            maskedCode.Append(value);

            if (isTerminated)
            {
                maskedCode.Append(quote);
            }

            return;
        }

        if (!mappings.TryGetValue(
                value,
                out var maskedValue))
        {
            maskedValue =
                CreateUniqueMaskedStringLiteral(
                    value,
                    quote,
                    mappings.Count + 1,
                    sessionId,
                    mode,
                    usedMaskedValues,
                    originalValues);

            mappings.Add(value, maskedValue);
            usedMaskedValues.Add(maskedValue);
        }

        maskedCode.Append(maskedValue);

        if (isTerminated)
        {
            maskedCode.Append(quote);
        }
    }

    private static void AppendUnmaskedQuotedText(
        string sourceCode,
        StringBuilder maskedCode,
        ref int index)
    {
        var value = ReadQuotedText(
            sourceCode,
            ref index,
            out var quote,
            out var isTerminated);

        maskedCode.Append(quote);
        maskedCode.Append(value);

        if (isTerminated)
        {
            maskedCode.Append(quote);
        }
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
                    CreateFormatPreservingValue(
                        identifier,
                        quote: null),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Desteklenmeyen maskeleme modu.")
            };

            if (Pl1KeywordCatalog.Contains(candidate) ||
                originalIdentifiers.Contains(candidate) ||
                usedMaskedIdentifiers.Contains(candidate))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{identifier}' identifier'ı için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private static string
        CreateUniqueMaskedStringLiteral(
            string originalValue,
            char quote,
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
                    CreateFormatPreservingValue(
                        originalValue,
                        quote),

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
            "String literal için benzersiz bir " +
            "maskeleme değeri üretilemedi.");
    }

    private static string CreateMaximumPrivacyIdentifier(
        string sessionId,
        int ordinal)
    {
        return $"MC_{sessionId}_{ordinal:D4}";
    }

    private static string
        CreateMaximumPrivacyStringLiteral(
            string sessionId,
            int ordinal)
    {
        return $"STR_{sessionId}_{ordinal:D4}";
    }

    private static string CreateFormatPreservingValue(
        string originalValue,
        char? quote)
    {
        var candidate = new StringBuilder(
            originalValue.Length);

        var index = 0;

        while (index < originalValue.Length)
        {
            var current = originalValue[index];

            if (quote.HasValue &&
                current == quote.Value &&
                index + 1 < originalValue.Length &&
                originalValue[index + 1] == quote.Value)
            {
                candidate.Append(current);
                candidate.Append(current);
                index += 2;
                continue;
            }

            candidate.Append(
                CreateFormatPreservingCharacter(current));

            index++;
        }

        return candidate.ToString();
    }

    private static char
        CreateFormatPreservingCharacter(
            char originalCharacter)
    {
        if (char.IsUpper(originalCharacter))
        {
            return (char)('A' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (char.IsLower(originalCharacter))
        {
            return (char)('a' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (char.IsLetter(originalCharacter))
        {
            return (char)('A' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (char.IsDigit(originalCharacter))
        {
            return (char)('0' +
                RandomNumberGenerator.GetInt32(10));
        }

        return originalCharacter;
    }

    private static string ReadQuotedText(
        string sourceCode,
        ref int index,
        out char quote,
        out bool isTerminated)
    {
        quote = sourceCode[index];
        index++;

        var value = new StringBuilder();
        isTerminated = false;

        while (index < sourceCode.Length)
        {
            var current = sourceCode[index];

            if (current != quote)
            {
                value.Append(current);
                index++;
                continue;
            }

            if (index + 1 < sourceCode.Length &&
                sourceCode[index + 1] == quote)
            {
                value.Append(current);
                value.Append(current);
                index += 2;
                continue;
            }

            index++;
            isTerminated = true;
            break;
        }

        return value.ToString();
    }

    private static bool ContainsMaskableCharacter(
        string value)
    {
        return value.Any(character =>
            char.IsLetterOrDigit(character));
    }

    private static void AppendComment(
        string sourceCode,
        StringBuilder maskedCode,
        ref int index)
    {
        maskedCode.Append("/*");
        index += 2;

        while (index < sourceCode.Length)
        {
            if (IsCommentEnd(sourceCode, index))
            {
                maskedCode.Append("*/");
                index += 2;
                return;
            }

            maskedCode.Append(sourceCode[index]);
            index++;
        }
    }

    private static void SkipComment(
        string sourceCode,
        ref int index)
    {
        index += 2;

        while (index < sourceCode.Length)
        {
            if (IsCommentEnd(sourceCode, index))
            {
                index += 2;
                return;
            }

            index++;
        }
    }

    private static bool IsCommentStart(
        string sourceCode,
        int index)
    {
        return sourceCode[index] == '/' &&
               index + 1 < sourceCode.Length &&
               sourceCode[index + 1] == '*';
    }

    private static bool IsCommentEnd(
        string sourceCode,
        int index)
    {
        return sourceCode[index] == '*' &&
               index + 1 < sourceCode.Length &&
               sourceCode[index + 1] == '/';
    }

    private static bool IsQuote(char value)
    {
        return value is '\'' or '"';
    }

    private static bool IsIdentifierStart(char value)
    {
        return char.IsLetter(value) ||
               value is '_' or '@' or '#' or '$';
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value) ||
               value is '_' or '@' or '#' or '$';
    }
}