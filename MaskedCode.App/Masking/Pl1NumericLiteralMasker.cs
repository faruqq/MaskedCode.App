using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking;

internal sealed class Pl1NumericLiteralMasker
{
    private readonly Dictionary<int, int[]>
        _digitPermutations = new();

    public Pl1NumericMaskingResult Mask(
    string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var mappings = new Dictionary<string, string>(
            StringComparer.Ordinal);

        var structuralQuotedTextStartIndexes =
            new HashSet<int>();

        var scientificExponentMarkerIndexes =
            new HashSet<int>();

        var context = new DeclarationContext();
        var maskedCode = new StringBuilder(sourceCode.Length);
        var index = 0;

        while (index < sourceCode.Length)
        {
            if (IsCommentStart(sourceCode, index))
            {
                AppendComment(
                    sourceCode,
                    maskedCode,
                    ref index);

                continue;
            }

            if (IsQuote(sourceCode[index]))
            {
                if (context.IsStructuralDeclarationContent)
                {
                    structuralQuotedTextStartIndexes.Add(index);
                }

                AppendQuotedText(
                    sourceCode,
                    maskedCode,
                    ref index);

                continue;
            }

            if (IsIdentifierStart(sourceCode[index]))
            {
                var identifier = ReadIdentifier(
                    sourceCode,
                    ref index);

                maskedCode.Append(identifier);
                context.ObserveIdentifier(identifier);

                continue;
            }

            if (TryReadNumericLiteral(
                    sourceCode,
                    index,
                    out var literal,
                    out var kind))
            {
                if (kind == NumericLiteralKind.Scientific)
                {
                    var exponentMarkerOffset =
                        FindExponentIndex(literal);

                    scientificExponentMarkerIndexes.Add(
                        index + exponentMarkerOffset);
                }

                if (context.IsStructuralDeclarationContent)
                {
                    maskedCode.Append(literal);
                }
                else
                {
                    if (!mappings.TryGetValue(
                            literal,
                            out var maskedLiteral))
                    {
                        maskedLiteral = CreateMaskedLiteral(
                            literal,
                            kind);

                        mappings.Add(
                            literal,
                            maskedLiteral);
                    }

                    maskedCode.Append(maskedLiteral);
                }

                index += literal.Length;
                continue;
            }

            var current = sourceCode[index];

            maskedCode.Append(current);
            context.ObserveSymbol(current);

            index++;
        }

        return new Pl1NumericMaskingResult(
            maskedCode.ToString(),
            mappings,
            structuralQuotedTextStartIndexes,
            scientificExponentMarkerIndexes);
    }

    private string CreateMaskedLiteral(
        string literal,
        NumericLiteralKind kind)
    {
        var mantissaEnd = kind == NumericLiteralKind.Scientific
            ? FindExponentIndex(literal)
            : literal.Length;

        var decimalPointIndex =
            literal.IndexOf(
                '.',
                0,
                mantissaEnd);

        var preserveZeroIntegerPart =
            ShouldPreserveZeroIntegerPart(
                literal,
                decimalPointIndex,
                mantissaEnd);

        var maskedLiteral =
            new StringBuilder(literal.Length);

        var digitPosition = 0;

        for (var index = 0;
             index < mantissaEnd;
             index++)
        {
            var current = literal[index];

            if (!char.IsDigit(current))
            {
                maskedLiteral.Append(current);
                continue;
            }

            if (preserveZeroIntegerPart &&
                index < decimalPointIndex)
            {
                maskedLiteral.Append(current);
                digitPosition++;
                continue;
            }

            maskedLiteral.Append(
                MaskDigit(
                    current,
                    digitPosition));

            digitPosition++;
        }

        if (mantissaEnd < literal.Length)
        {
            // Exponenti değiştirmek sayının aralığını bozabileceği için
            // bilimsel gösterimin exponent bölümü korunur.
            maskedLiteral.Append(
                literal[mantissaEnd..]);
        }

        return maskedLiteral.ToString();
    }

    private char MaskDigit(
        char digit,
        int position)
    {
        if (!_digitPermutations.TryGetValue(
                position,
                out var permutation))
        {
            permutation = CreateDerangedDigitPermutation();
            _digitPermutations.Add(position, permutation);
        }

        var numericValue = digit - '0';

        return (char)('0' + permutation[numericValue]);
    }

    private static int[]
        CreateDerangedDigitPermutation()
    {
        while (true)
        {
            var permutation =
                Enumerable.Range(0, 10).ToArray();

            for (var index = permutation.Length - 1;
                 index > 0;
                 index--)
            {
                var targetIndex =
                    RandomNumberGenerator.GetInt32(index + 1);

                (
                    permutation[index],
                    permutation[targetIndex]
                ) = (
                    permutation[targetIndex],
                    permutation[index]
                );
            }

            var containsUnchangedDigit = false;

            for (var index = 0;
                 index < permutation.Length;
                 index++)
            {
                if (permutation[index] != index)
                {
                    continue;
                }

                containsUnchangedDigit = true;
                break;
            }

            if (!containsUnchangedDigit)
            {
                return permutation;
            }
        }
    }

    private static bool TryReadNumericLiteral(
        string sourceCode,
        int startIndex,
        out string literal,
        out NumericLiteralKind kind)
    {
        literal = string.Empty;
        kind = NumericLiteralKind.Integer;

        if (!IsNumericLiteralStart(
                sourceCode,
                startIndex))
        {
            return false;
        }

        var index = startIndex;
        var containsDecimalPoint = false;
        var containsExponent = false;

        while (index < sourceCode.Length &&
               char.IsDigit(sourceCode[index]))
        {
            index++;
        }

        if (index < sourceCode.Length &&
            sourceCode[index] == '.')
        {
            containsDecimalPoint = true;
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
                containsExponent = true;
                index = exponentCursor;
            }
            else
            {
                index = exponentIndex;
            }
        }

        literal = sourceCode[startIndex..index];

        kind = containsExponent
            ? NumericLiteralKind.Scientific
            : containsDecimalPoint
                ? NumericLiteralKind.Decimal
                : NumericLiteralKind.Integer;

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

    private static int FindExponentIndex(
        string literal)
    {
        for (var index = 0;
             index < literal.Length;
             index++)
        {
            if (IsExponentMarker(literal[index]))
            {
                return index;
            }
        }

        return literal.Length;
    }

    private static bool
        ShouldPreserveZeroIntegerPart(
            string literal,
            int decimalPointIndex,
            int mantissaEnd)
    {
        if (decimalPointIndex <= 0 ||
            decimalPointIndex + 1 >= mantissaEnd)
        {
            return false;
        }

        for (var index = 0;
             index < decimalPointIndex;
             index++)
        {
            if (literal[index] != '0')
            {
                return false;
            }
        }

        return true;
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

    private static void AppendComment(
        string sourceCode,
        StringBuilder output,
        ref int index)
    {
        output.Append("/*");
        index += 2;

        while (index < sourceCode.Length)
        {
            if (IsCommentEnd(sourceCode, index))
            {
                output.Append("*/");
                index += 2;
                return;
            }

            output.Append(sourceCode[index]);
            index++;
        }
    }

    private static void AppendQuotedText(
        string sourceCode,
        StringBuilder output,
        ref int index)
    {
        var quote = sourceCode[index];

        output.Append(quote);
        index++;

        while (index < sourceCode.Length)
        {
            var current = sourceCode[index];

            output.Append(current);
            index++;

            if (current != quote)
            {
                continue;
            }

            if (index < sourceCode.Length &&
                sourceCode[index] == quote)
            {
                output.Append(sourceCode[index]);
                index++;
                continue;
            }

            return;
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