using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking;

internal sealed class EglCodeMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    public EglMaskingResult Mask(
        string sourceCode)
    {
        return Mask(
            sourceCode,
            MaskingMode.MaximumPrivacy);
    }

    public EglMaskingResult Mask(
        string sourceCode,
        MaskingMode mode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        ValidateMaskingMode(mode);

        var identifierMappings =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var usedMaskedIdentifiers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var originalIdentifiers =
            CollectOriginalIdentifiers(sourceCode);

        var sessionId = Guid.NewGuid()
            .ToString("N")[..8]
            .ToUpperInvariant();

        var maskedCode =
            new StringBuilder(sourceCode.Length);

        var index = 0;

        while (index < sourceCode.Length)
        {
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

        var mappings = identifierMappings
            .Select(mapping =>
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    mapping.Key,
                    mapping.Value))
            .ToArray();

        return new EglMaskingResult(
            maskedCode.ToString(),
            mappings,
            mode);
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
               IsIdentifierPart(sourceCode[index]))
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
               IsIdentifierPart(sourceCode[index]))
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
               char.IsWhiteSpace(sourceCode[index]))
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
               char.IsWhiteSpace(sourceCode[index]))
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
            new StringBuilder(identifier.Length);

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
        if (character is >= 'A' and <= 'Z')
        {
            return (char)(
                'A' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (character is >= 'a' and <= 'z')
        {
            return (char)(
                'a' +
                RandomNumberGenerator.GetInt32(26));
        }

        if (character is >= '0' and <= '9')
        {
            return (char)(
                '0' +
                RandomNumberGenerator.GetInt32(10));
        }

        return character;
    }

    private static HashSet<string>
        CollectOriginalIdentifiers(
            string sourceCode)
    {
        var identifiers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var index = 0;

        while (index < sourceCode.Length)
        {
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

    private static string ReadIdentifier(
        string sourceCode,
        ref int index)
    {
        var startIndex = index;
        index++;

        while (index < sourceCode.Length &&
               IsIdentifierPart(sourceCode[index]))
        {
            index++;
        }

        return sourceCode[startIndex..index];
    }

    private static void RejectUnsupportedContext(
        string sourceCode,
        int index)
    {
        if (IsCommentStart(sourceCode, index))
        {
            throw new NotSupportedException(
                "EGL yorum maskelemesi henüz desteklenmiyor.");
        }

        if (sourceCode[index] is '\'' or '"')
        {
            throw new NotSupportedException(
                "EGL string literal maskelemesi henüz " +
                "desteklenmiyor.");
        }

        if (char.IsDigit(sourceCode[index]))
        {
            throw new NotSupportedException(
                "EGL numeric literal maskelemesi henüz " +
                "desteklenmiyor.");
        }

        if (char.IsLetter(sourceCode[index]) &&
            !IsAsciiLetter(sourceCode[index]))
        {
            throw new NotSupportedException(
                "ASCII dışındaki EGL identifier karakterleri " +
                "henüz desteklenmiyor.");
        }
    }

    private static bool IsCommentStart(
        string sourceCode,
        int index)
    {
        if (index + 1 >= sourceCode.Length ||
            sourceCode[index] != '/')
        {
            return false;
        }

        return sourceCode[index + 1] is '/' or '*';
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