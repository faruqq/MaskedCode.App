using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text;

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
        if (!IsSupportedLiteral(token))
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

        var maskedToken =
            SyntaxFactory.ParseToken(
                maskedValue);

        if (maskedToken.Kind() != token.Kind())
        {
            throw new InvalidOperationException(
                $"'{originalValue}' C# literalı geçerli biçimde maskelenemedi.");
        }

        return maskedToken
            .WithLeadingTrivia(token.LeadingTrivia)
            .WithTrailingTrivia(token.TrailingTrivia);
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

            SyntaxKind.CharacterLiteralToken =>
                CreateMaskedCharacterLiteral(
                    token,
                    ordinal),

            _ => throw new ArgumentOutOfRangeException(
                nameof(token),
                token.Kind(),
                "Desteklenmeyen C# literal türü.")
        };
    }

    private string CreateMaskedStringLiteral(SyntaxToken token, int ordinal)
    {
        var maskedContent =
            _mode switch
            {
                MaskingMode.MaximumPrivacy =>
                    $"STR_{_sessionId}_{ordinal:D4}",

                MaskingMode.FormatPreserving =>
                    CreateFormatPreservingContent(
                        token.ValueText),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(_mode),
                    _mode,
                    "Desteklenmeyen maskeleme modu.")
            };

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
            .Literal(maskedContent)
            .Text;
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
                   SyntaxKind.CharacterLiteralToken);
    }
}