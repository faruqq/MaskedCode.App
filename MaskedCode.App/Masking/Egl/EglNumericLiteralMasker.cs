using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking.Egl;

internal static class EglNumericLiteralMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    public static bool TryReadNumericLiteral(
        string sourceCode,
        int startIndex,
        out string literal,
        out NumericLiteralKind kind)
    {
        literal = string.Empty;
        kind = NumericLiteralKind.Integer;

        if (!IsNumericLiteralStart(sourceCode, startIndex))
        {
            return false;
        }

        var index = startIndex;
        var containsDecimalPoint = false;
        var containsExponent = false;

        while (index < sourceCode.Length &&
               IsAsciiDigit(sourceCode[index]))
        {
            index++;
        }

        if (index < sourceCode.Length &&
            sourceCode[index] == '.')
        {
            containsDecimalPoint = true;
            index++;

            while (index < sourceCode.Length &&
                   IsAsciiDigit(sourceCode[index]))
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
                   IsAsciiDigit(sourceCode[exponentCursor]))
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

    public static string MaskLiteral(
        string sourceCode,
        int startIndex,
        string literal,
        NumericLiteralKind kind,
        IDictionary<string, string> mappings,
        ISet<string> usedMaskedValues,
        ISet<string> originalNumericLiterals)
    {
        if (IsStructuralNumericLiteral(
                sourceCode,
                startIndex,
                literal,
                kind))
        {
            return literal;
        }

        if (mappings.TryGetValue(
                literal,
                out var existingMaskedLiteral))
        {
            return existingMaskedLiteral;
        }

        var maskedLiteral = CreateUniqueMaskedLiteral(
            literal,
            kind,
            usedMaskedValues,
            originalNumericLiterals);

        mappings.Add(
            literal,
            maskedLiteral);

        usedMaskedValues.Add(maskedLiteral);

        return maskedLiteral;
    }

    private static string CreateUniqueMaskedLiteral(
        string literal,
        NumericLiteralKind kind,
        ISet<string> usedMaskedValues,
        ISet<string> originalNumericLiterals)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate = CreateMaskedLiteral(
                literal,
                kind);

            if (string.Equals(
                    candidate,
                    literal,
                    StringComparison.Ordinal) ||
                originalNumericLiterals.Contains(candidate) ||
                usedMaskedValues.Contains(candidate))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{literal}' EGL numeric literal değeri için " +
            "benzersiz bir maskeleme değeri üretilemedi.");
    }

    private static string CreateMaskedLiteral(string literal, NumericLiteralKind kind)
    {
        var mantissaEnd = kind == NumericLiteralKind.Scientific
            ? FindExponentIndex(literal)
            : literal.Length;

        var decimalPointIndex = literal.IndexOf(
            '.',
            0,
            mantissaEnd);

        var preserveZeroIntegerPart = ShouldPreserveZeroIntegerPart(
            literal,
            decimalPointIndex,
            mantissaEnd);

        var maskedLiteral = new StringBuilder(literal.Length);

        for (var index = 0;
             index < mantissaEnd;
             index++)
        {
            var current = literal[index];

            if (!IsAsciiDigit(current))
            {
                maskedLiteral.Append(current);
                continue;
            }

            if (preserveZeroIntegerPart &&
                index < decimalPointIndex)
            {
                maskedLiteral.Append(current);
                continue;
            }

            maskedLiteral.Append(
                MaskDigit(current));
        }

        if (mantissaEnd < literal.Length)
        {
            maskedLiteral.Append(
                literal[mantissaEnd..]);
        }

        return maskedLiteral.ToString();
    }

    private static char MaskDigit(char digit)
    {
        var numericValue = digit - '0';
        var offset = RandomNumberGenerator.GetInt32(1, 10);

        return (char)(
            '0' +
            ((numericValue + offset) % 10));
    }

    private static bool IsStructuralNumericLiteral(
        string sourceCode,
        int startIndex,
        string literal,
        NumericLiteralKind kind)
    {
        return IsBuiltInTypeArgument(
                   sourceCode,
                   startIndex) ||
               IsArrayDeclarationSize(
                   sourceCode,
                   startIndex,
                   literal.Length) ||
               IsRecordLevelNumber(
                   sourceCode,
                   startIndex,
                   literal.Length,
                   kind) ||
               IsMetadataNumericValue(
                   sourceCode,
                   startIndex);
    }

    private static bool IsBuiltInTypeArgument(string sourceCode, int literalStartIndex)
    {
        var openingParenthesisIndex = FindEnclosingOpeningParenthesis(
            sourceCode,
            literalStartIndex);

        if (openingParenthesisIndex < 0)
        {
            return false;
        }

        var index = openingParenthesisIndex - 1;

        var identifier = ReadIdentifierBackward(
            sourceCode,
            ref index,
            out _);

        return EglKeywordCatalog.IsBuiltInType(identifier);
    }

    private static int FindEnclosingOpeningParenthesis(string sourceCode, int startIndex)
    {
        var depth = 0;

        for (var index = startIndex - 1;
             index >= 0;
             index--)
        {
            if (sourceCode[index] == ')')
            {
                depth++;
                continue;
            }

            if (sourceCode[index] == '(')
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
                continue;
            }

            if (depth == 0 &&
                sourceCode[index] == ';')
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool IsArrayDeclarationSize(
        string sourceCode,
        int literalStartIndex,
        int literalLength)
    {
        var openingBracketIndex = literalStartIndex - 1;

        SkipWhitespaceBackward(
            sourceCode,
            ref openingBracketIndex);

        if (openingBracketIndex < 0 ||
            sourceCode[openingBracketIndex] != '[')
        {
            return false;
        }

        var closingBracketIndex =
            literalStartIndex + literalLength;

        SkipWhitespaceForward(
            sourceCode,
            ref closingBracketIndex);

        if (closingBracketIndex >= sourceCode.Length ||
            sourceCode[closingBracketIndex] != ']')
        {
            return false;
        }

        var index = openingBracketIndex - 1;

        SkipWhitespaceBackward(
            sourceCode,
            ref index);

        if (index < 0)
        {
            return false;
        }

        if (sourceCode[index] == ')')
        {
            var typeName = ReadTypeNameBeforeClosingParenthesis(
                sourceCode,
                index);

            return EglKeywordCatalog.IsBuiltInType(typeName);
        }

        if (!IsIdentifierPart(sourceCode[index]))
        {
            return false;
        }

        var possibleTypeName = ReadIdentifierBackward(
            sourceCode,
            ref index,
            out var typeNameStartIndex);

        if (EglKeywordCatalog.IsBuiltInType(possibleTypeName))
        {
            return true;
        }

        var separatorIndex = typeNameStartIndex - 1;

        if (separatorIndex < 0 ||
            !char.IsWhiteSpace(sourceCode[separatorIndex]))
        {
            return false;
        }

        index = separatorIndex;

        var declarationName = ReadIdentifierBackward(
            sourceCode,
            ref index,
            out _);

        return !string.IsNullOrEmpty(declarationName) &&
               !EglKeywordCatalog.IsKeyword(declarationName) &&
               !EglKeywordCatalog.IsBuiltInType(declarationName);
    }

    private static string ReadTypeNameBeforeClosingParenthesis(
        string sourceCode,
        int closingParenthesisIndex)
    {
        var depth = 0;

        for (var index = closingParenthesisIndex;
             index >= 0;
             index--)
        {
            if (sourceCode[index] == ')')
            {
                depth++;
                continue;
            }

            if (sourceCode[index] != '(')
            {
                continue;
            }

            depth--;

            if (depth != 0)
            {
                continue;
            }

            index--;

            return ReadIdentifierBackward(
                sourceCode,
                ref index,
                out _);
        }

        return string.Empty;
    }

    private static bool IsRecordLevelNumber(
        string sourceCode,
        int literalStartIndex,
        int literalLength,
        NumericLiteralKind kind)
    {
        if (kind != NumericLiteralKind.Integer)
        {
            return false;
        }

        var index = literalStartIndex - 1;

        while (index >= 0 &&
               sourceCode[index] is not '\r' and not '\n')
        {
            if (!char.IsWhiteSpace(sourceCode[index]))
            {
                return false;
            }

            index--;
        }

        index = literalStartIndex + literalLength;

        if (index >= sourceCode.Length ||
            !char.IsWhiteSpace(sourceCode[index]))
        {
            return false;
        }

        SkipWhitespaceForward(
            sourceCode,
            ref index);

        return index < sourceCode.Length &&
               IsIdentifierStart(sourceCode[index]);
    }

    private static bool IsMetadataNumericValue(string sourceCode, int literalStartIndex)
    {
        var index = literalStartIndex - 1;

        SkipWhitespaceBackward(
            sourceCode,
            ref index);

        if (index < 0 ||
            sourceCode[index] != '=')
        {
            return false;
        }

        index--;

        var propertyName = ReadIdentifierBackward(
            sourceCode,
            ref index,
            out _);

        return EglKeywordCatalog.IsMetadataProperty(propertyName);
    }

    private static string ReadIdentifierBackward(
        string sourceCode,
        ref int index,
        out int startIndex)
    {
        SkipWhitespaceBackward(
            sourceCode,
            ref index);

        var endIndex = index + 1;

        while (index >= 0 &&
               IsIdentifierPart(sourceCode[index]))
        {
            index--;
        }

        startIndex = index + 1;

        return startIndex < endIndex
            ? sourceCode[startIndex..endIndex]
            : string.Empty;
    }

    private static bool ShouldPreserveZeroIntegerPart(
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

    private static int FindExponentIndex(string literal)
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

    private static bool IsNumericLiteralStart(string sourceCode, int index)
    {
        if (IsAsciiDigit(sourceCode[index]))
        {
            return true;
        }

        return sourceCode[index] == '.' &&
               index + 1 < sourceCode.Length &&
               IsAsciiDigit(sourceCode[index + 1]);
    }

    private static bool IsExponentMarker(char character)
    {
        return character is 'E' or 'e';
    }

    private static bool IsIdentifierStart(char character)
    {
        return IsAsciiLetter(character) ||
               character == '_';
    }

    private static bool IsIdentifierPart(char character)
    {
        return IsIdentifierStart(character) ||
               IsAsciiDigit(character);
    }

    private static bool IsAsciiLetter(char character)
    {
        return character is >= 'A' and <= 'Z' or
               >= 'a' and <= 'z';
    }

    private static bool IsAsciiDigit(char character)
    {
        return character is >= '0' and <= '9';
    }

    private static void SkipWhitespaceBackward(string sourceCode, ref int index)
    {
        while (index >= 0 &&
               char.IsWhiteSpace(sourceCode[index]))
        {
            index--;
        }
    }

    private static void SkipWhitespaceForward(string sourceCode, ref int index)
    {
        while (index < sourceCode.Length &&
               char.IsWhiteSpace(sourceCode[index]))
        {
            index++;
        }
    }
}