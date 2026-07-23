using System.IO;
using System.Text;

namespace MaskedCode.App.Masking.Pl1;

public sealed class Pl1CodeUnmasker
{
    public string Unmask(
        string maskedCode,
        MappingVaultContent vaultContent)
    {
        ArgumentNullException.ThrowIfNull(maskedCode);
        ArgumentNullException.ThrowIfNull(vaultContent);

        if (maskedCode.Length == 0)
        {
            throw new ArgumentException(
                "Geri açılacak maskelenmiş kod boş olamaz.",
                nameof(maskedCode));
        }

        if (vaultContent.Mappings is null ||
            vaultContent.Mappings.Count == 0)
        {
            throw new InvalidDataException(
                "Kasa içinde geri açılacak eşleme bulunamadı.");
        }

        var lookup = MappingLookup.Create(
            vaultContent.Mappings);

        var restoredCode =
            new StringBuilder(maskedCode.Length);

        var declarationContext =
            new DeclarationContext();

        var index = 0;

        while (index < maskedCode.Length)
        {
            if (IsCommentStart(maskedCode, index))
            {
                AppendRestoredComment(
                    maskedCode,
                    restoredCode,
                    lookup,
                    ref index);

                continue;
            }

            if (IsQuote(maskedCode[index]))
            {
                AppendRestoredQuotedText(
                    maskedCode,
                    restoredCode,
                    lookup,
                    declarationContext,
                    ref index);

                continue;
            }

            if (TryReadNumericLiteral(
                    maskedCode,
                    index,
                    out var numericLiteral))
            {
                AppendRestoredNumericLiteral(
                    numericLiteral,
                    restoredCode,
                    lookup,
                    declarationContext);

                index += numericLiteral.Length;
                continue;
            }

            if (IsIdentifierStart(maskedCode[index]))
            {
                var identifier =
                    ReadIdentifier(
                        maskedCode,
                        ref index);

                declarationContext.ObserveIdentifier(
                    identifier);

                AppendRestoredValue(
                    identifier,
                    restoredCode,
                    lookup.IdentifierMappings,
                    lookup);

                continue;
            }

            var current = maskedCode[index];

            restoredCode.Append(current);
            declarationContext.ObserveSymbol(current);

            index++;
        }

        lookup.ValidateAllMappingsWereUsed();

        return restoredCode.ToString();
    }

    private static void AppendRestoredComment(
        string maskedCode,
        StringBuilder restoredCode,
        MappingLookup lookup,
        ref int index)
    {
        restoredCode.Append("/*");
        index += 2;

        var contentStartIndex = index;

        while (index < maskedCode.Length &&
               !IsCommentEnd(maskedCode, index))
        {
            index++;
        }

        var maskedComment =
            maskedCode[contentStartIndex..index];

        AppendRestoredValue(
            maskedComment,
            restoredCode,
            lookup.CommentMappings,
            lookup);

        if (index < maskedCode.Length)
        {
            restoredCode.Append("*/");
            index += 2;
        }
    }

    private static void AppendRestoredQuotedText(
        string maskedCode,
        StringBuilder restoredCode,
        MappingLookup lookup,
        DeclarationContext declarationContext,
        ref int index)
    {
        var maskedValue =
            ReadQuotedText(
                maskedCode,
                ref index,
                out var quote,
                out var isTerminated);

        restoredCode.Append(quote);

        if (declarationContext
            .IsStructuralDeclarationContent)
        {
            restoredCode.Append(maskedValue);
        }
        else
        {
            AppendRestoredValue(
                maskedValue,
                restoredCode,
                lookup.StringLiteralMappings,
                lookup);
        }

        if (isTerminated)
        {
            restoredCode.Append(quote);
        }
    }

    private static void
        AppendRestoredNumericLiteral(
            string numericLiteral,
            StringBuilder restoredCode,
            MappingLookup lookup,
            DeclarationContext declarationContext)
    {
        if (declarationContext
            .IsStructuralDeclarationContent)
        {
            restoredCode.Append(numericLiteral);
            return;
        }

        AppendRestoredValue(
            numericLiteral,
            restoredCode,
            lookup.NumericLiteralMappings,
            lookup);
    }

    private static void AppendRestoredValue(
        string maskedValue,
        StringBuilder restoredCode,
        IReadOnlyDictionary<string, MappingEntry>
            mappings,
        MappingLookup lookup)
    {
        if (!mappings.TryGetValue(
                maskedValue,
                out var mapping))
        {
            restoredCode.Append(maskedValue);
            return;
        }

        restoredCode.Append(mapping.OriginalValue);
        lookup.MarkAsUsed(mapping.Index);
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

    private static bool TryReadNumericLiteral(
        string sourceCode,
        int startIndex,
        out string literal)
    {
        literal = string.Empty;

        if (!IsNumericLiteralStart(
                sourceCode,
                startIndex))
        {
            return false;
        }

        var index = startIndex;

        while (index < sourceCode.Length &&
               char.IsDigit(sourceCode[index]))
        {
            index++;
        }

        if (index < sourceCode.Length &&
            sourceCode[index] == '.')
        {
            index++;

            while (index < sourceCode.Length &&
                   char.IsDigit(sourceCode[index]))
            {
                index++;
            }
        }

        if (index < sourceCode.Length &&
            IsExponentMarker(sourceCode[index]))
        {
            var exponentIndex = index;
            var exponentCursor = index + 1;

            if (exponentCursor < sourceCode.Length &&
                sourceCode[exponentCursor] is '+' or '-')
            {
                exponentCursor++;
            }

            var exponentDigitStart = exponentCursor;

            while (exponentCursor < sourceCode.Length &&
                   char.IsDigit(sourceCode[exponentCursor]))
            {
                exponentCursor++;
            }

            if (exponentCursor > exponentDigitStart)
            {
                index = exponentCursor;
            }
            else
            {
                index = exponentIndex;
            }
        }

        literal = sourceCode[startIndex..index];

        return true;
    }

    private static bool IsNumericLiteralStart(
        string sourceCode,
        int index)
    {
        if (char.IsDigit(sourceCode[index]))
        {
            return true;
        }

        return sourceCode[index] == '.' &&
               index + 1 < sourceCode.Length &&
               char.IsDigit(sourceCode[index + 1]);
    }

    private static bool IsExponentMarker(char value)
    {
        return value is 'E' or 'e' or 'D' or 'd';
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

    private sealed class MappingLookup
    {
        private readonly IReadOnlyList<MaskingMapping>
            _mappings;

        private readonly bool[] _usedMappings;

        private MappingLookup(
            IReadOnlyList<MaskingMapping> mappings,
            Dictionary<string, MappingEntry>
                identifierMappings,
            Dictionary<string, MappingEntry>
                stringLiteralMappings,
            Dictionary<string, MappingEntry>
                numericLiteralMappings,
            Dictionary<string, MappingEntry>
                commentMappings)
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

        public IReadOnlyDictionary<string, MappingEntry>
            IdentifierMappings
        { get; }

        public IReadOnlyDictionary<string, MappingEntry>
            StringLiteralMappings
        { get; }

        public IReadOnlyDictionary<string, MappingEntry>
            NumericLiteralMappings
        { get; }

        public IReadOnlyDictionary<string, MappingEntry>
            CommentMappings
        { get; }

        public static MappingLookup Create(
            IReadOnlyList<MaskingMapping> mappings)
        {
            var identifierMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.OrdinalIgnoreCase);

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
                var mapping = mappings[index];

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
                        "Kasa içinde boş değere sahip bir " +
                        "eşleme bulundu.");
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
                    "kodda bulunamadı. Geri açma işlemi " +
                    $"durduruldu. Tür: {unusedMapping.Kind}");
            }
        }
    }

    private sealed record MappingEntry(
        int Index,
        string OriginalValue);

    private sealed class DeclarationContext
    {
        private bool _isDeclaration;
        private bool _isWaitingForInitParenthesis;
        private int _parenthesisDepth;
        private int? _initParenthesisDepth;

        public bool IsStructuralDeclarationContent =>
            _isDeclaration &&
            !_initParenthesisDepth.HasValue;

        public void ObserveIdentifier(
            string identifier)
        {
            if (identifier.Equals(
                    "DCL",
                    StringComparison.OrdinalIgnoreCase))
            {
                _isDeclaration = true;
                return;
            }

            if (_isDeclaration &&
                identifier.Equals(
                    "INIT",
                    StringComparison.OrdinalIgnoreCase))
            {
                _isWaitingForInitParenthesis = true;
            }
        }

        public void ObserveSymbol(char symbol)
        {
            if (symbol == '(')
            {
                _parenthesisDepth++;

                if (_isWaitingForInitParenthesis)
                {
                    _initParenthesisDepth =
                        _parenthesisDepth;

                    _isWaitingForInitParenthesis = false;
                }

                return;
            }

            if (symbol == ')')
            {
                if (_initParenthesisDepth ==
                    _parenthesisDepth)
                {
                    _initParenthesisDepth = null;
                }

                if (_parenthesisDepth > 0)
                {
                    _parenthesisDepth--;
                }

                return;
            }

            if (symbol != ';')
            {
                return;
            }

            _isDeclaration = false;
            _isWaitingForInitParenthesis = false;
            _parenthesisDepth = 0;
            _initParenthesisDepth = null;
        }
    }
}