using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking.CSharp;

internal sealed class CSharpDirectiveMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    private readonly MaskingMode _mode;
    private readonly string _sessionId;
    private readonly IDictionary<string, string> _mappings;
    private readonly ISet<string> _usedMaskedValues;

    public CSharpDirectiveMasker(MaskingMode mode, string sessionId, IDictionary<string, string> mappings, ISet<string> usedMaskedValues)
    {
        _mode =
            mode;

        _sessionId =
            sessionId;

        _mappings =
            mappings;

        _usedMaskedValues =
            usedMaskedValues;
    }

    public SyntaxTrivia MaskTrivia(SyntaxTrivia trivia)
    {
        if (!IsSupportedDirective(
                trivia))
        {
            return trivia;
        }

        if (trivia.GetStructure() is not DirectiveTriviaSyntax directiveSyntax)
        {
            return trivia;
        }

        var originalValue =
            trivia.ToFullString();

        var contentStart =
            FindContentStart(
                originalValue);

        if (!HasMaskableContent(
                originalValue,
                contentStart))
        {
            return trivia;
        }

        if (!_mappings.TryGetValue(
                originalValue,
                out var maskedValue))
        {
            maskedValue =
                CreateUniqueMaskedDirective(
                    originalValue,
                    contentStart,
                    _mappings.Count + 1);

            _mappings.Add(
                originalValue,
                maskedValue);

            _usedMaskedValues.Add(
                maskedValue);
        }

        return CreateMaskedTrivia(
            directiveSyntax,
            maskedValue,
            contentStart);
    }

    private string CreateUniqueMaskedDirective(string originalValue, int contentStart, int ordinal)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate =
                CreateMaskedDirective(
                    originalValue,
                    contentStart,
                    ordinal + attempt);

            if (string.Equals(
                    candidate,
                    originalValue,
                    StringComparison.Ordinal) ||
                _usedMaskedValues.Contains(
                    candidate))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{originalValue}' C# directive metni için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private SyntaxTrivia CreateMaskedTrivia(DirectiveTriviaSyntax directiveSyntax, string maskedValue, int contentStart)
    {
        var originalValue =
            directiveSyntax
                .ParentTrivia
                .ToFullString();

        var originalContent =
            originalValue[contentStart..];

        var maskedContent =
            maskedValue[contentStart..];

        var endOfDirectiveToken =
            directiveSyntax.EndOfDirectiveToken;

        var originalTrailingTriviaText =
            endOfDirectiveToken
                .LeadingTrivia
                .ToFullString();

        var contentWithoutLineBreak =
            RemoveTrailingLineBreak(
                maskedContent);

        var lineBreak =
            GetTrailingLineBreak(
                originalContent);

        var replacementTrivia =
            SyntaxFactory.ParseTrailingTrivia(
                contentWithoutLineBreak);

        var updatedEndOfDirectiveToken =
            endOfDirectiveToken.WithLeadingTrivia(
                replacementTrivia);

        var updatedDirective =
            directiveSyntax.ReplaceToken(
                endOfDirectiveToken,
                updatedEndOfDirectiveToken);

        var updatedTrivia =
            SyntaxFactory.Trivia(
                updatedDirective);

        var updatedValue =
            updatedTrivia.ToFullString();

        if (!string.IsNullOrEmpty(
                lineBreak) &&
            !updatedValue.EndsWith(
                lineBreak,
                StringComparison.Ordinal))
        {
            updatedDirective =
                updatedDirective.ReplaceToken(
                    updatedDirective.EndOfDirectiveToken,
                    updatedDirective.EndOfDirectiveToken.WithTrailingTrivia(
                        SyntaxFactory.EndOfLine(
                            lineBreak)));

            updatedTrivia =
                SyntaxFactory.Trivia(
                    updatedDirective);
        }

        if (string.Equals(
                updatedTrivia.ToFullString(),
                originalValue,
                StringComparison.Ordinal) ||
            string.Equals(
                originalTrailingTriviaText,
                updatedDirective
                    .EndOfDirectiveToken
                    .LeadingTrivia
                    .ToFullString(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{originalValue}' C# directive metni geçerli biçimde maskelenemedi.");
        }

        return updatedTrivia;
    }

    private string CreateMaskedDirective(string directive, int contentStart, int ordinal)
    {
        var prefix =
            directive[..contentStart];

        var content =
            directive[contentStart..];

        var maskedContent =
            _mode switch
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

        return prefix +
               maskedContent;
    }

    private string CreateMaximumPrivacyContent(string content, int ordinal)
    {
        var result =
            new StringBuilder(
                content.Length);

        var placeholderWritten =
            false;

        foreach (var character in content)
        {
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
                    $"DIR_{_sessionId}_{ordinal:D4}");

                placeholderWritten =
                    true;

                continue;
            }

            if (!placeholderWritten)
            {
                result.Append(
                    character);
            }
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
        if (char.IsUpper(
                character))
        {
            return (char)(
                'A' +
                RandomNumberGenerator.GetInt32(
                    26));
        }

        if (char.IsLower(
                character))
        {
            return (char)(
                'a' +
                RandomNumberGenerator.GetInt32(
                    26));
        }

        if (char.IsLetter(
                character))
        {
            return (char)(
                'A' +
                RandomNumberGenerator.GetInt32(
                    26));
        }

        if (char.IsDigit(
                character))
        {
            return (char)(
                '0' +
                RandomNumberGenerator.GetInt32(
                    10));
        }

        return character;
    }

    private static int FindContentStart(string directive)
    {
        var index =
            directive.IndexOf(
                '#');

        if (index < 0)
        {
            return directive.Length;
        }

        index++;

        while (index < directive.Length &&
               char.IsWhiteSpace(
                   directive[index]) &&
               directive[index] is not '\r' and not '\n')
        {
            index++;
        }

        while (index < directive.Length &&
               char.IsLetter(
                   directive[index]))
        {
            index++;
        }

        return index;
    }

    private static bool HasMaskableContent(string directive, int contentStart)
    {
        return directive
            .Skip(
                contentStart)
            .Any(character =>
                char.IsLetterOrDigit(
                    character));
    }

    private static string RemoveTrailingLineBreak(string value)
    {
        if (value.EndsWith(
                "\r\n",
                StringComparison.Ordinal))
        {
            return value[..^2];
        }

        if (value.EndsWith(
                '\r') ||
            value.EndsWith(
                '\n'))
        {
            return value[..^1];
        }

        return value;
    }

    private static string GetTrailingLineBreak(string value)
    {
        if (value.EndsWith(
                "\r\n",
                StringComparison.Ordinal))
        {
            return "\r\n";
        }

        if (value.EndsWith(
                '\r'))
        {
            return "\r";
        }

        if (value.EndsWith(
                '\n'))
        {
            return "\n";
        }

        return string.Empty;
    }

    private static bool IsSupportedDirective(SyntaxTrivia trivia)
    {
        return trivia.IsKind(
                   SyntaxKind.RegionDirectiveTrivia) ||
               trivia.IsKind(
                   SyntaxKind.EndRegionDirectiveTrivia) ||
               trivia.IsKind(
                   SyntaxKind.ErrorDirectiveTrivia) ||
               trivia.IsKind(
                   SyntaxKind.WarningDirectiveTrivia);
    }
}