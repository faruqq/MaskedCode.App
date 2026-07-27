using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking.CSharp;

internal sealed class CSharpNumericLiteralMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    private readonly MaskingMode _mode;
    private readonly IDictionary<string, string> _mappings;
    private readonly ISet<string> _usedMaskedValues;

    public CSharpNumericLiteralMasker(
    MaskingMode mode,
    IDictionary<string, string> mappings,
    ISet<string> usedMaskedValues)
    {
        _mode = mode;
        _mappings = mappings;
        _usedMaskedValues = usedMaskedValues;
    }

    public SyntaxToken MaskToken(SyntaxToken token)
    {
        if (!token.IsKind(
                SyntaxKind.NumericLiteralToken))
        {
            return token;
        }

        var originalValue =
            token.Text;

        if (!_mappings.TryGetValue(
                originalValue,
                out var maskedValue))
        {
            maskedValue =
                CreateUniqueMaskedLiteral(
                    token);

            _mappings.Add(
                originalValue,
                maskedValue);

            _usedMaskedValues.Add(
                maskedValue);
        }

        var maskedToken =
            SyntaxFactory.ParseToken(
                maskedValue);

        if (!IsValidCandidate(
                token,
                maskedToken))
        {
            throw new InvalidOperationException(
                $"'{originalValue}' C# numeric literalı geçerli biçimde maskelenemedi.");
        }

        return maskedToken
            .WithLeadingTrivia(token.LeadingTrivia)
            .WithTrailingTrivia(token.TrailingTrivia);
    }

    private string CreateUniqueMaskedLiteral(SyntaxToken token)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate =
                CreateMaskedLiteral(
                    token.Text);

            if (string.Equals(
                candidate,
                token.Text,
                StringComparison.Ordinal) ||
            _usedMaskedValues.Contains(
                candidate))
            {
                continue;
            }

            var candidateToken =
                SyntaxFactory.ParseToken(
                    candidate);

            if (!IsValidCandidate(
                    token,
                    candidateToken))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{token.Text}' C# numeric literalı için özgün sayısal türü " +
            "koruyan benzersiz bir maskeleme değeri üretilemedi.");
    }

    private static bool IsValidCandidate(SyntaxToken originalToken, SyntaxToken candidateToken)
    {
        if (!candidateToken.IsKind(
                SyntaxKind.NumericLiteralToken) ||
            candidateToken.ContainsDiagnostics)
        {
            return false;
        }

        var originalValueType =
            originalToken.Value?
                .GetType();

        var candidateValueType =
            candidateToken.Value?
                .GetType();

        return originalValueType is not null &&
               candidateValueType == originalValueType;
    }

    private string CreateMaskedLiteral(string literal)
    {
        var prefixLength =
            GetPrefixLength(
                literal);

        var suffixStart =
            FindSuffixStart(
                literal,
                prefixLength);

        var exponentStart =
            FindExponentStart(
                literal,
                prefixLength,
                suffixStart);

        var maskedLiteral =
            new StringBuilder(
                literal.Length);

        maskedLiteral.Append(
            literal,
            0,
            prefixLength);

        var radix =
            GetRadix(
                literal,
                prefixLength);

        for (var index = prefixLength;
             index < suffixStart;
             index++)
        {
            var character =
                literal[index];

            if (exponentStart >= 0 &&
                index >= exponentStart)
            {
                maskedLiteral.Append(
                    character);

                continue;
            }

            if (!IsDigitForRadix(
                    character,
                    radix))
            {
                maskedLiteral.Append(
                    character);

                continue;
            }

            maskedLiteral.Append(
                CreateMaskedDigit(
                    character,
                    radix));
        }

        maskedLiteral.Append(
            literal,
            suffixStart,
            literal.Length - suffixStart);

        return maskedLiteral.ToString();
    }

    private char CreateMaskedDigit(char character, int radix)
    {
        return _mode switch
        {
            MaskingMode.MaximumPrivacy =>
                CreateMaximumPrivacyDigit(
                    character,
                    radix),

            MaskingMode.FormatPreserving =>
                CreateFormatPreservingDigit(
                    character,
                    radix),

            _ => throw new ArgumentOutOfRangeException(
                nameof(_mode),
                _mode,
                "Desteklenmeyen maskeleme modu.")
        };
    }

    private static char CreateMaximumPrivacyDigit(char character, int radix)
    {
        if (radix == 2)
        {
            return character == '0'
                ? '1'
                : '0';
        }

        if (radix == 16)
        {
            const string hexadecimalDigits =
                "0123456789ABCDEF";

            char candidate;

            do
            {
                candidate =
                    hexadecimalDigits[
                        RandomNumberGenerator.GetInt32(
                            hexadecimalDigits.Length)];
            }
            while (char.ToUpperInvariant(candidate) ==
                   char.ToUpperInvariant(character));

            return char.IsLower(character)
                ? char.ToLowerInvariant(candidate)
                : candidate;
        }

        return CreateDifferentDecimalDigit(
            character);
    }

    private static char CreateFormatPreservingDigit(char character, int radix)
    {
        if (radix == 2)
        {
            return character == '0'
                ? '1'
                : '0';
        }

        if (radix == 16 &&
            IsHexadecimalLetter(character))
        {
            const string hexadecimalLetters =
                "ABCDEF";

            char candidate;

            do
            {
                candidate =
                    hexadecimalLetters[
                        RandomNumberGenerator.GetInt32(
                            hexadecimalLetters.Length)];
            }
            while (char.ToUpperInvariant(candidate) ==
                   char.ToUpperInvariant(character));

            return char.IsLower(character)
                ? char.ToLowerInvariant(candidate)
                : candidate;
        }

        return CreateDifferentDecimalDigit(
            character);
    }

    private static char CreateDifferentDecimalDigit(char character)
    {
        char candidate;

        do
        {
            candidate =
                (char)(
                    '0' +
                    RandomNumberGenerator.GetInt32(10));
        }
        while (candidate == character);

        return candidate;
    }

    private static int GetPrefixLength(string literal)
    {
        if (literal.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase) ||
            literal.StartsWith(
                "0b",
                StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 0;
    }

    private static int GetRadix(string literal, int prefixLength)
    {
        if (prefixLength == 0)
        {
            return 10;
        }

        return literal[1] is 'x' or 'X'
            ? 16
            : 2;
    }

    private static int FindSuffixStart(string literal, int prefixLength)
    {
        var index =
            literal.Length;

        if (prefixLength == 2)
        {
            while (index > prefixLength &&
                   literal[index - 1] is
                       'u' or 'U' or 'l' or 'L')
            {
                index--;
            }

            return index;
        }

        while (index > prefixLength &&
               literal[index - 1] is
                   'u' or 'U' or
                   'l' or 'L' or
                   'f' or 'F' or
                   'd' or 'D' or
                   'm' or 'M')
        {
            index--;
        }

        return index;
    }

    private static int FindExponentStart(string literal, int startIndex, int endIndex)
    {
        for (var index = startIndex;
             index < endIndex;
             index++)
        {
            if (literal[index] is 'e' or 'E')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsDigitForRadix(char character, int radix)
    {
        return radix switch
        {
            2 =>
                character is '0' or '1',

            10 =>
                char.IsAsciiDigit(
                    character),

            16 =>
                char.IsAsciiHexDigit(
                    character),

            _ => false
        };
    }

    private static bool IsHexadecimalLetter(char character)
    {
        return character is
            >= 'a' and <= 'f' or
            >= 'A' and <= 'F';
    }
}