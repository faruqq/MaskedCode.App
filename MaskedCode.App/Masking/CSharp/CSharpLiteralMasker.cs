using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MaskedCode.App.Masking.CSharp;

internal sealed class CSharpLiteralMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    private readonly MaskingMode _mode;
    private readonly string _sessionId;
    private readonly IDictionary<string, string> _mappings;
    private readonly ISet<string> _usedMaskedValues;
    private readonly ISet<string> _originalValues;

    public CSharpLiteralMasker(MaskingMode mode, string sessionId, IDictionary<string, string> mappings, ISet<string> usedMaskedValues, ISet<string> originalValues)
    {
        _mode = mode;
        _sessionId = sessionId;
        _mappings = mappings;
        _usedMaskedValues = usedMaskedValues;
        _originalValues = originalValues;
    }

    public SyntaxToken MaskToken(SyntaxToken token)
    {
        if (!IsSupportedLiteral(
                token))
        {
            return token;
        }

        if (IsInterpolationFormatToken(
                token))
        {
            return token;
        }

        if (!CanCreateDifferentMaskedValue(
                token))
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
                    token,
                    _mappings.Count + 1);

            _mappings.Add(
                originalValue,
                maskedValue);

            _usedMaskedValues.Add(
                maskedValue);
        }

        if (token.IsKind(
                SyntaxKind.InterpolatedStringTextToken))
        {
            return SyntaxFactory.Token(
                token.LeadingTrivia,
                SyntaxKind.InterpolatedStringTextToken,
                maskedValue,
                maskedValue,
                token.TrailingTrivia);
        }

        var maskedToken =
            SyntaxFactory.ParseToken(
                maskedValue);

        if (maskedToken.Kind() !=
            token.Kind())
        {
            throw new InvalidOperationException(
                $"'{originalValue}' C# literalı geçerli biçimde maskelenemedi.");
        }

        return maskedToken
            .WithLeadingTrivia(
                token.LeadingTrivia)
            .WithTrailingTrivia(
                token.TrailingTrivia);
    }

    private static bool IsInterpolationFormatToken(
        SyntaxToken token)
    {
        return token.IsKind(
                   SyntaxKind.InterpolatedStringTextToken) &&
               token.Parent is
                   InterpolationFormatClauseSyntax;
    }

    private bool CanCreateDifferentMaskedValue(SyntaxToken token)
    {
        if (token.IsKind(
                SyntaxKind.StringLiteralToken))
        {
            return ContainsMaskableStringContent(
                token.ValueText);
        }

        if (_mode ==
            MaskingMode.FormatPreserving)
        {
            return token.ValueText.Any(
                char.IsLetterOrDigit);
        }

        if (token.IsKind(
                SyntaxKind.MultiLineRawStringLiteralToken))
        {
            return token.ValueText.Any(
                character =>
                    !char.IsWhiteSpace(
                        character));
        }

        return true;
    }

    private static bool ContainsMaskableStringContent(string content)
    {
        var index =
            0;

        while (index < content.Length)
        {
            if (TryFindCompositeFormatItemEnd(
                    content,
                    index,
                    out var formatItemEnd))
            {
                index =
                    formatItemEnd;

                continue;
            }

            if (char.IsLetterOrDigit(
                    content[index]))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private string CreateUniqueMaskedLiteral(SyntaxToken token, int ordinal)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var maskedValue =
                CreateMaskedLiteral(
                    token,
                    ordinal + attempt);

            if (string.Equals(
                    maskedValue,
                    token.Text,
                    StringComparison.Ordinal) ||
                _originalValues.Contains(maskedValue) ||
                _usedMaskedValues.Contains(maskedValue))
            {
                continue;
            }

            return maskedValue;
        }

        throw new InvalidOperationException(
            $"'{token.Text}' C# literalı için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private string CreateMaskedLiteral(SyntaxToken token, int ordinal)
    {
        return token.Kind() switch
        {
            SyntaxKind.StringLiteralToken =>
                CreateMaskedStringLiteral(
                    token,
                    ordinal),

            SyntaxKind.SingleLineRawStringLiteralToken =>
                CreateMaskedRawStringLiteral(
                    token,
                    ordinal),

            SyntaxKind.MultiLineRawStringLiteralToken =>
                CreateMaskedRawStringLiteral(
                    token,
                    ordinal),

            SyntaxKind.CharacterLiteralToken =>
                CreateMaskedCharacterLiteral(
                    token,
                    ordinal),

            SyntaxKind.InterpolatedStringTextToken =>
                CreateMaskedInterpolatedStringText(
                    token,
                    ordinal),

            _ => throw new ArgumentOutOfRangeException(
                nameof(token),
                token.Kind(),
                "Desteklenmeyen C# literal türü.")
        };
    }

    private string CreateMaskedStringLiteral(
    SyntaxToken token,
    int ordinal)
    {
        var maskedContent =
            CreateMaskedStringContent(
                token.ValueText,
                ordinal);

        if (token.Text.StartsWith(
                "@\"",
                StringComparison.Ordinal))
        {
            return "@\"" +
                   maskedContent.Replace(
                       "\"",
                       "\"\"",
                       StringComparison.Ordinal) +
                   "\"";
        }

        return SyntaxFactory
            .Literal(
                maskedContent)
            .Text;
    }

    private string CreateMaskedStringContent(
        string content,
        int ordinal)
    {
        var result =
            new StringBuilder(
                content.Length);

        var segmentStart =
            0;

        var index =
            0;

        while (index < content.Length)
        {
            if (!TryFindCompositeFormatItemEnd(
                    content,
                    index,
                    out var formatItemEnd))
            {
                index++;
                continue;
            }

            AppendMaskedStringSegment(
                result,
                content[segmentStart..index],
                ordinal);

            result.Append(
                content,
                index,
                formatItemEnd - index);

            index =
                formatItemEnd;

            segmentStart =
                formatItemEnd;
        }

        AppendMaskedStringSegment(
            result,
            content[segmentStart..],
            ordinal);

        return result.ToString();
    }

    private void AppendMaskedStringSegment(
        StringBuilder result,
        string segment,
        int ordinal)
    {
        if (segment.Length == 0)
        {
            return;
        }

        var maskedSegment =
            _mode switch
            {
                MaskingMode.MaximumPrivacy =>
                    segment.Any(
                        char.IsLetterOrDigit)
                        ? $"STR_{_sessionId}_{ordinal:D4}"
                        : segment,

                MaskingMode.FormatPreserving =>
                    CreateFormatPreservingContent(
                        segment),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(_mode),
                    _mode,
                    "Desteklenmeyen maskeleme modu.")
            };

        result.Append(
            maskedSegment);
    }

    private static bool TryFindCompositeFormatItemEnd(
        string content,
        int startIndex,
        out int endIndex)
    {
        endIndex =
            startIndex;

        if (startIndex >= content.Length ||
            content[startIndex] != '{')
        {
            return false;
        }

        if (startIndex + 1 < content.Length &&
            content[startIndex + 1] == '{')
        {
            return false;
        }

        var index =
            startIndex + 1;

        var digitStart =
            index;

        while (index < content.Length &&
               char.IsDigit(
                   content[index]))
        {
            index++;
        }

        if (index == digitStart)
        {
            return false;
        }

        while (index < content.Length &&
               content[index] != '}')
        {
            if (content[index] == '{' ||
                content[index] is '\r' or '\n')
            {
                return false;
            }

            index++;
        }

        if (index >= content.Length ||
            content[index] != '}')
        {
            return false;
        }

        endIndex =
            index + 1;

        return true;
    }

    private string CreateMaskedRawStringLiteral(SyntaxToken token, int ordinal)
    {
        var delimiterLength =
            CountLeadingQuotationMarks(
                token.Text);

        if (delimiterLength < 3)
        {
            throw new InvalidOperationException(
                $"'{token.Text}' geçerli bir C# raw string literalı değildir.");
        }

        return token.IsKind(
                SyntaxKind.SingleLineRawStringLiteralToken)
            ? CreateMaskedSingleLineRawStringLiteral(
                token,
                ordinal,
                delimiterLength)
            : CreateMaskedMultiLineRawStringLiteral(
                token,
                ordinal,
                delimiterLength);
    }

    private string CreateMaskedSingleLineRawStringLiteral(SyntaxToken token, int ordinal, int delimiterLength)
    {
        var delimiter =
            new string(
                '"',
                delimiterLength);

        var originalContent =
            token.Text.Substring(
                delimiterLength,
                token.Text.Length -
                (delimiterLength * 2));

        var maskedContent =
            _mode switch
            {
                MaskingMode.MaximumPrivacy =>
                    $"STR_{_sessionId}_{ordinal:D4}",

                MaskingMode.FormatPreserving =>
                    CreateFormatPreservingContent(
                        originalContent),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(_mode),
                    _mode,
                    "Desteklenmeyen maskeleme modu.")
            };

        return delimiter +
               maskedContent +
               delimiter;
    }

    private string CreateMaskedMultiLineRawStringLiteral(SyntaxToken token, int ordinal, int delimiterLength)
    {
        var firstLineBreakEnd =
            FindFirstLineBreakEnd(
                token.Text);

        var closingDelimiterStart =
            token.Text.LastIndexOf(
                new string(
                    '"',
                    delimiterLength),
                StringComparison.Ordinal);

        if (firstLineBreakEnd < 0 ||
            closingDelimiterStart <= firstLineBreakEnd)
        {
            throw new InvalidOperationException(
                $"'{token.Text}' çok satırlı C# raw string literalı geçerli biçimde maskelenemedi.");
        }

        var content =
            token.Text.Substring(
                firstLineBreakEnd,
                closingDelimiterStart -
                firstLineBreakEnd);

        var maskedContent =
            CreateMaskedMultiLineRawContent(
                content,
                ordinal);

        return token.Text[..firstLineBreakEnd] +
               maskedContent +
               token.Text[closingDelimiterStart..];
    }

    private string CreateMaskedMultiLineRawContent(string content, int ordinal)
    {
        var maskedContent =
            new StringBuilder(
                content.Length);

        var replacement =
            $"STR{_sessionId}{ordinal:D4}";

        var replacementIndex =
            0;

        foreach (var character in content)
        {
            if (char.IsWhiteSpace(character))
            {
                maskedContent.Append(
                    character);

                continue;
            }

            if (_mode == MaskingMode.FormatPreserving)
            {
                maskedContent.Append(
                    CreateFormatPreservingCharacter(
                        character));

                continue;
            }

            maskedContent.Append(
                replacement[
                    replacementIndex %
                    replacement.Length]);

            replacementIndex++;
        }

        return maskedContent.ToString();
    }

    private static int CountLeadingQuotationMarks(string value)
    {
        var count =
            0;

        while (count < value.Length &&
               value[count] == '"')
        {
            count++;
        }

        return count;
    }

    private static int FindFirstLineBreakEnd(string value)
    {
        for (var index = 0;
             index < value.Length;
             index++)
        {
            if (value[index] == '\n')
            {
                return index + 1;
            }

            if (value[index] == '\r')
            {
                return index + 1 < value.Length &&
                       value[index + 1] == '\n'
                    ? index + 2
                    : index + 1;
            }
        }

        return -1;
    }

    private string CreateMaskedCharacterLiteral(SyntaxToken token, int ordinal)
    {
        var maskedCharacter =
            _mode switch
            {
                MaskingMode.MaximumPrivacy =>
                    (char)(
                        'A' +
                        ((ordinal - 1) % 26)),

                MaskingMode.FormatPreserving =>
                    CreateFormatPreservingCharacter(
                        token.ValueText[0]),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(_mode),
                    _mode,
                    "Desteklenmeyen maskeleme modu.")
            };

        return SyntaxFactory
            .Literal(maskedCharacter)
            .Text;
    }

    private string CreateMaskedInterpolatedStringText(SyntaxToken token, int ordinal)
    {
        if (IsMultiLineRawInterpolatedStringText(token))
        {
            return CreateMaskedMultiLineRawContent(
                token.Text,
                ordinal);
        }

        return _mode switch
        {
            MaskingMode.MaximumPrivacy =>
                $"STR_{_sessionId}_{ordinal:D4}",

            MaskingMode.FormatPreserving =>
                CreateFormatPreservingInterpolatedText(
                    token.Text),

            _ => throw new ArgumentOutOfRangeException(
                nameof(_mode),
                _mode,
                "Desteklenmeyen maskeleme modu.")
        };
    }

    private static bool IsMultiLineRawInterpolatedStringText(SyntaxToken token)
    {
        var interpolatedString =
            token.Parent?
                .AncestorsAndSelf()
                .OfType<InterpolatedStringExpressionSyntax>()
                .FirstOrDefault();

        return interpolatedString?
            .StringStartToken
            .IsKind(
                SyntaxKind.InterpolatedMultiLineRawStringStartToken) ==
            true;
    }

    private static string CreateFormatPreservingInterpolatedText(string originalText)
    {
        var maskedText =
            new StringBuilder(
                originalText.Length);

        for (var index = 0;
             index < originalText.Length;
             index++)
        {
            var character =
                originalText[index];

            if (character == '\\' &&
                index + 1 < originalText.Length)
            {
                maskedText.Append(
                    character);

                maskedText.Append(
                    originalText[index + 1]);

                index++;
                continue;
            }

            if ((character == '{' ||
                 character == '}' ||
                 character == '"') &&
                index + 1 < originalText.Length &&
                originalText[index + 1] == character)
            {
                maskedText.Append(
                    character);

                maskedText.Append(
                    character);

                index++;
                continue;
            }

            maskedText.Append(
                CreateFormatPreservingCharacter(
                    character));
        }

        return maskedText.ToString();
    }

    private static string CreateFormatPreservingContent(string originalValue)
    {
        var maskedValue =
            new StringBuilder(
                originalValue.Length);

        foreach (var character in originalValue)
        {
            maskedValue.Append(
                CreateFormatPreservingCharacter(
                    character));
        }

        return maskedValue.ToString();
    }

    private static char CreateFormatPreservingCharacter(char character)
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

    private static bool IsSupportedLiteral(SyntaxToken token)
    {
        return token.IsKind(
                   SyntaxKind.StringLiteralToken) ||
               token.IsKind(
                   SyntaxKind.SingleLineRawStringLiteralToken) ||
               token.IsKind(
                   SyntaxKind.MultiLineRawStringLiteralToken) ||
               token.IsKind(
                   SyntaxKind.CharacterLiteralToken) ||
               token.IsKind(
                   SyntaxKind.InterpolatedStringTextToken);
    }
}