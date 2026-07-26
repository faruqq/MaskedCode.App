using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking.CSharp;

internal sealed class CSharpCommentMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    private readonly MaskingMode _mode;
    private readonly string _sessionId;
    private readonly IDictionary<string, string> _mappings;
    private readonly ISet<string> _usedMaskedValues;

    public CSharpCommentMasker(MaskingMode mode, string sessionId, IDictionary<string, string> mappings, ISet<string> usedMaskedValues)
    {
        _mode = mode;
        _sessionId = sessionId;
        _mappings = mappings;
        _usedMaskedValues = usedMaskedValues;
    }

    public SyntaxTrivia MaskTrivia(SyntaxTrivia trivia)
    {
        if (!IsSupportedComment(trivia))
        {
            return trivia;
        }

        var originalValue =
            trivia.ToFullString();

        if (!HasMaskableContent(
                originalValue,
                trivia.Kind()))
        {
            return trivia;
        }

        if (!_mappings.TryGetValue(
                originalValue,
                out var maskedValue))
        {
            maskedValue =
                CreateUniqueMaskedComment(
                    trivia,
                    _mappings.Count + 1);

            _mappings.Add(
                originalValue,
                maskedValue);

            _usedMaskedValues.Add(
                maskedValue);
        }

        var maskedTrivia =
            SyntaxFactory
                .ParseLeadingTrivia(
                    maskedValue)
                .FirstOrDefault(candidate =>
                    candidate.Kind() ==
                    trivia.Kind());

        if (maskedTrivia.RawKind == 0)
        {
            throw new InvalidOperationException(
                $"'{originalValue}' C# yorumu geçerli biçimde maskelenemedi.");
        }

        return maskedTrivia;
    }

    private string CreateUniqueMaskedComment(SyntaxTrivia trivia, int ordinal)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate =
                CreateMaskedComment(
                    trivia,
                    ordinal + attempt);

            if (string.Equals(
                    candidate,
                    trivia.ToFullString(),
                    StringComparison.Ordinal) ||
                _usedMaskedValues.Contains(
                    candidate))
            {
                continue;
            }

            var candidateTrivia =
                SyntaxFactory
                    .ParseLeadingTrivia(
                        candidate)
                    .FirstOrDefault(parsedTrivia =>
                        parsedTrivia.Kind() ==
                        trivia.Kind());

            if (candidateTrivia.RawKind == 0)
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{trivia.ToFullString()}' C# yorumu için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private string CreateMaskedComment(SyntaxTrivia trivia, int ordinal)
    {
        return trivia.Kind() switch
        {
            SyntaxKind.SingleLineCommentTrivia =>
                CreateSingleLineComment(
                    trivia.ToFullString(),
                    "//",
                    ordinal),

            SyntaxKind.MultiLineCommentTrivia =>
                CreateBlockComment(
                    trivia.ToFullString(),
                    "/*",
                    "*/",
                    ordinal),

            SyntaxKind.SingleLineDocumentationCommentTrivia =>
                CreateDocumentationLineComment(
                    trivia.ToFullString(),
                    ordinal),

            SyntaxKind.MultiLineDocumentationCommentTrivia =>
                CreateBlockComment(
                    trivia.ToFullString(),
                    "/**",
                    "*/",
                    ordinal),

            _ => throw new ArgumentOutOfRangeException(
                nameof(trivia),
                trivia.Kind(),
                "Desteklenmeyen C# yorum türü.")
        };
    }

    private string CreateSingleLineComment(string comment, string delimiter, int ordinal)
    {
        var content =
            comment[delimiter.Length..];

        var maskedContent =
            CreateMaskedContent(
                content,
                ordinal);

        return delimiter +
               maskedContent;
    }

    private string CreateBlockComment(string comment, string openingDelimiter, string closingDelimiter, int ordinal)
    {
        if (!comment.EndsWith(
                closingDelimiter,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{comment}' sonlandırılmış bir C# blok yorumu değildir.");
        }

        var content =
            comment.Substring(
                openingDelimiter.Length,
                comment.Length -
                openingDelimiter.Length -
                closingDelimiter.Length);

        var maskedContent =
            CreateMaskedContent(
                content,
                ordinal);

        return openingDelimiter +
               maskedContent +
               closingDelimiter;
    }

    private string CreateDocumentationLineComment(string comment, int ordinal)
    {
        var result =
            new StringBuilder(
                comment.Length);

        var lineStart =
            0;

        while (lineStart < comment.Length)
        {
            var lineEnd =
                FindLineEnd(
                    comment,
                    lineStart);

            var line =
                comment[lineStart..lineEnd];

            var delimiterIndex =
                line.IndexOf(
                    "///",
                    StringComparison.Ordinal);

            if (delimiterIndex >= 0)
            {
                result.Append(
                    line,
                    0,
                    delimiterIndex + 3);

                result.Append(
                    CreateMaskedContent(
                        line[(delimiterIndex + 3)..],
                        ordinal));
            }
            else
            {
                result.Append(
                    CreateMaskedContent(
                        line,
                        ordinal));
            }

            AppendLineBreak(
                comment,
                lineEnd,
                result,
                out lineStart);
        }

        return result.ToString();
    }

    private string CreateMaskedContent(string content, int ordinal)
    {
        return _mode switch
        {
            MaskingMode.MaximumPrivacy =>
                CreateMaximumPrivacyContent(
                    content,
                    ordinal),

            MaskingMode.FormatPreserving =>
                CreateFormatPreservingContent(
                    content),

            _ => throw new ArgumentOutOfRangeException(
                nameof(_mode),
                _mode,
                "Desteklenmeyen maskeleme modu.")
        };
    }

    private string CreateMaximumPrivacyContent(string content, int ordinal)
    {
        var result =
            new StringBuilder(
                content.Length);

        var placeholderWritten =
            false;

        for (var index = 0;
             index < content.Length;
             index++)
        {
            var character =
                content[index];

            if (character is '\r' or '\n')
            {
                result.Append(
                    character);

                continue;
            }

            if (!placeholderWritten &&
                !char.IsWhiteSpace(
                    character))
            {
                result.Append(
                    $"CMT_{_sessionId}_{ordinal:D4}");

                placeholderWritten =
                    true;
            }
        }

        if (!placeholderWritten)
        {
            return content;
        }

        return result.ToString();
    }

    private static string CreateFormatPreservingContent(string content)
    {
        var result =
            new StringBuilder(
                content.Length);

        foreach (var character in content)
        {
            result.Append(
                CreateFormatPreservingCharacter(
                    character));
        }

        return result.ToString();
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

    private static bool HasMaskableContent(string comment, SyntaxKind kind)
    {
        var content =
            kind switch
            {
                SyntaxKind.SingleLineCommentTrivia =>
                    comment[2..],

                SyntaxKind.MultiLineCommentTrivia =>
                    comment.Length >= 4
                        ? comment[2..^2]
                        : string.Empty,

                SyntaxKind.SingleLineDocumentationCommentTrivia =>
                    comment.Replace(
                        "///",
                        string.Empty,
                        StringComparison.Ordinal),

                SyntaxKind.MultiLineDocumentationCommentTrivia =>
                    comment.Length >= 5
                        ? comment[3..^2]
                        : string.Empty,

                _ => string.Empty
            };

        return content.Any(
            character =>
                char.IsLetterOrDigit(
                    character));
    }

    private static bool IsSupportedComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(
                   SyntaxKind.SingleLineCommentTrivia) ||
               trivia.IsKind(
                   SyntaxKind.MultiLineCommentTrivia) ||
               trivia.IsKind(
                   SyntaxKind.SingleLineDocumentationCommentTrivia) ||
               trivia.IsKind(
                   SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    private static int FindLineEnd(string text, int lineStart)
    {
        var index =
            lineStart;

        while (index < text.Length &&
               text[index] is not '\r' and not '\n')
        {
            index++;
        }

        return index;
    }

    private static void AppendLineBreak(string text, int lineEnd, StringBuilder result, out int nextLineStart)
    {
        nextLineStart =
            lineEnd;

        if (nextLineStart >= text.Length)
        {
            return;
        }

        if (text[nextLineStart] == '\r')
        {
            result.Append('\r');
            nextLineStart++;
        }

        if (nextLineStart < text.Length &&
            text[nextLineStart] == '\n')
        {
            result.Append('\n');
            nextLineStart++;
        }
    }
}