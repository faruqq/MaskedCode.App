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

    public SyntaxTrivia MaskTrivia(
    SyntaxTrivia trivia)
    {
        if (!IsSupportedDirective(
                trivia))
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

        if (_mappings.TryGetValue(
                originalValue,
                out var existingMaskedValue))
        {
            return CreateMaskedTrivia(
                existingMaskedValue,
                trivia.Kind());
        }

        var maskedTrivia =
            CreateUniqueMaskedTrivia(
                trivia,
                originalValue,
                contentStart,
                _mappings.Count + 1);

        var actualMaskedValue =
            maskedTrivia.ToFullString();

        _mappings.Add(
            originalValue,
            actualMaskedValue);

        _usedMaskedValues.Add(
            actualMaskedValue);

        return maskedTrivia;
    }

    private SyntaxTrivia CreateUniqueMaskedTrivia(
    SyntaxTrivia trivia,
    string originalValue,
    int contentStart,
    int ordinal)
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

            var maskedTrivia =
                CreateMaskedTrivia(
                    candidate,
                    trivia.Kind());

            var actualMaskedValue =
                maskedTrivia.ToFullString();

            if (string.Equals(
                    actualMaskedValue,
                    originalValue,
                    StringComparison.Ordinal) ||
                _usedMaskedValues.Contains(
                    actualMaskedValue))
            {
                continue;
            }

            return maskedTrivia;
        }

        throw new InvalidOperationException(
            $"'{originalValue}' C# directive metni için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private static SyntaxTrivia CreateMaskedTrivia(
    string maskedValue,
    SyntaxKind expectedKind)
    {
        var parsingSource =
            CreateDirectiveParsingSource(
                maskedValue,
                expectedKind);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                parsingSource,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var parsedDirective =
            syntaxTree
                .GetRoot()
                .DescendantTrivia(
                    descendIntoTrivia: true)
                .FirstOrDefault(
                    trivia =>
                        trivia.IsKind(
                            expectedKind));

        if (parsedDirective.RawKind == 0)
        {
            throw new InvalidOperationException(
                $"'{maskedValue}' C# directive metni " +
                "geçerli biçimde ayrıştırılamadı.");
        }

        var actualValue =
            parsedDirective.ToFullString();

        if (!string.Equals(
                actualValue,
                maskedValue,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{maskedValue}' C# directive metni " +
                "ayrıştırılırken karakter yapısı korunamadı.");
        }

        return parsedDirective;
    }

    private static string CreateDirectiveParsingSource(
    string maskedValue,
    SyntaxKind expectedKind)
    {
        if (expectedKind !=
            SyntaxKind.EndRegionDirectiveTrivia)
        {
            return maskedValue;
        }

        var lineBreak =
            GetPreferredLineBreak(
                maskedValue);

        return
            $"#region MASKING_CONTEXT{lineBreak}" +
            maskedValue;
    }

    private static string GetPreferredLineBreak(
    string value)
    {
        return value.Contains(
            "\r\n",
            StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
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