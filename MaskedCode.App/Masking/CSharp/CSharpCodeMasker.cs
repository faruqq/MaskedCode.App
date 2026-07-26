using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text;

namespace MaskedCode.App.Masking.CSharp;

internal sealed class CSharpCodeMasker
{
    private const int MaximumCandidateAttemptCount = 10_000;

    public CSharpMaskingResult Mask(string sourceCode)
    {
        return Mask(
            sourceCode,
            MaskingMode.MaximumPrivacy);
    }

    public CSharpMaskingResult Mask(string sourceCode, MaskingMode mode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        ValidateMaskingMode(mode);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                sourceCode,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var root =
            syntaxTree.GetRoot();

        var originalIdentifiers =
            CollectOriginalIdentifiers(
                root);

        var originalLiterals =
            CollectOriginalLiterals(
                root);

        var originalNumericLiterals =
            CollectOriginalNumericLiterals(
                root);

        var identifierMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var literalMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var numericLiteralMappings =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        var usedMaskedIdentifiers =
            new HashSet<string>(
                StringComparer.Ordinal);

        var usedMaskedLiterals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var usedMaskedNumericLiterals =
            new HashSet<string>(
                StringComparer.Ordinal);

        var sessionId =
            Guid.NewGuid()
                .ToString("N")[..8]
                .ToUpperInvariant();

        var literalMasker =
            new CSharpLiteralMasker(
                mode,
                sessionId,
                literalMappings,
                usedMaskedLiterals,
                originalLiterals);

        var numericLiteralMasker =
            new CSharpNumericLiteralMasker(
                mode,
                numericLiteralMappings,
                usedMaskedNumericLiterals,
                originalNumericLiterals);

        var rewriter =
            new IdentifierMaskingRewriter(
                identifierMappings,
                usedMaskedIdentifiers,
                originalIdentifiers,
                literalMasker,
                numericLiteralMasker,
                sessionId,
                mode);

        var maskedRoot =
            rewriter.Visit(
                root);

        if (maskedRoot is null)
        {
            throw new InvalidOperationException(
                "C# syntax ağacı maskelenemedi.");
        }

        var mappings =
            CreateMappings(
                identifierMappings,
                literalMappings,
                numericLiteralMappings);

        return new CSharpMaskingResult(
            maskedRoot.ToFullString(),
            mappings,
            mode);
    }

    private static HashSet<string> CollectOriginalIdentifiers(SyntaxNode root)
    {
        return root
            .DescendantTokens(
                descendIntoTrivia: true)
            .Where(token =>
                token.IsKind(
                    SyntaxKind.IdentifierToken))
            .Select(token =>
                token.Text)
            .ToHashSet(
                StringComparer.Ordinal);
    }

    private static HashSet<string> CollectOriginalLiterals(SyntaxNode root)
    {
        return root
            .DescendantTokens(
                descendIntoTrivia: true)
            .Where(token =>
                token.IsKind(
                    SyntaxKind.StringLiteralToken) ||
                token.IsKind(
                    SyntaxKind.SingleLineRawStringLiteralToken) ||
                token.IsKind(
                    SyntaxKind.MultiLineRawStringLiteralToken) ||
                token.IsKind(
                    SyntaxKind.CharacterLiteralToken) ||
                token.IsKind(
                    SyntaxKind.InterpolatedStringTextToken))
            .Select(token =>
                token.Text)
            .ToHashSet(
                StringComparer.Ordinal);
    }

    private static HashSet<string> CollectOriginalNumericLiterals(SyntaxNode root)
    {
        return root
            .DescendantTokens(
                descendIntoTrivia: true)
            .Where(token =>
                token.IsKind(
                    SyntaxKind.NumericLiteralToken))
            .Select(token =>
                token.Text)
            .ToHashSet(
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<MaskingMapping> CreateMappings(IReadOnlyDictionary<string, string> identifierMappings, IReadOnlyDictionary<string, string> literalMappings, IReadOnlyDictionary<string, string> numericLiteralMappings)
    {
        var mappings =
            new List<MaskingMapping>(
                identifierMappings.Count +
                literalMappings.Count +
                numericLiteralMappings.Count);

        foreach (var mapping in identifierMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.Identifier,
                    mapping.Key,
                    mapping.Value));
        }

        foreach (var mapping in literalMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.StringLiteral,
                    mapping.Key,
                    mapping.Value));
        }

        foreach (var mapping in numericLiteralMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.NumericLiteral,
                    mapping.Key,
                    mapping.Value));
        }

        return mappings;
    }

    private static string CreateUniqueMaskedIdentifier(string identifier, int ordinal, string sessionId, MaskingMode mode, ISet<string> usedMaskedIdentifiers, ISet<string> originalIdentifiers)
    {
        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidate =
                mode switch
                {
                    MaskingMode.MaximumPrivacy =>
                        CreateMaximumPrivacyIdentifier(
                            identifier,
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

            if (string.Equals(
                candidate,
                identifier,
                StringComparison.Ordinal) ||
            originalIdentifiers.Contains(candidate) ||
            usedMaskedIdentifiers.Contains(candidate) ||
            IsCSharpKeyword(candidate))
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException(
            $"'{identifier}' C# identifier'ı için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private static bool IsCSharpKeyword(string identifier)
    {
        if (identifier.StartsWith(
                '@'))
        {
            return false;
        }

        return SyntaxFacts.GetKeywordKind(identifier) !=
                   SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) !=
                   SyntaxKind.None;
    }

    private static string CreateMaximumPrivacyIdentifier(string identifier, string sessionId, int ordinal)
    {
        var prefix =
            identifier.StartsWith(
                '@')
                ? "@"
                : string.Empty;

        return $"{prefix}CS_{sessionId}_{ordinal:D4}";
    }

    private static string CreateFormatPreservingIdentifier(string identifier)
    {
        var maskedIdentifier =
            new StringBuilder(
                identifier.Length);

        foreach (var character in identifier)
        {
            maskedIdentifier.Append(
                CreateFormatPreservingCharacter(
                    character));
        }

        return maskedIdentifier.ToString();
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

    private static void ValidateMaskingMode(MaskingMode mode)
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

    private sealed class IdentifierMaskingRewriter : CSharpSyntaxRewriter
    {
        private readonly IDictionary<string, string> _mappings;
        private readonly ISet<string> _usedMaskedIdentifiers;
        private readonly ISet<string> _originalIdentifiers;
        private readonly string _sessionId;
        private readonly MaskingMode _mode;
        private readonly CSharpLiteralMasker _literalMasker;
        private readonly CSharpNumericLiteralMasker _numericLiteralMasker;

        public IdentifierMaskingRewriter(IDictionary<string, string> mappings, ISet<string> usedMaskedIdentifiers, ISet<string> originalIdentifiers, CSharpLiteralMasker literalMasker, CSharpNumericLiteralMasker numericLiteralMasker, string sessionId, MaskingMode mode)
        {
            _mappings = mappings;
            _usedMaskedIdentifiers = usedMaskedIdentifiers;
            _originalIdentifiers = originalIdentifiers;
            _literalMasker = literalMasker;
            _numericLiteralMasker = numericLiteralMasker;
            _sessionId = sessionId;
            _mode = mode;
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(
                    SyntaxKind.NumericLiteralToken))
            {
                return _numericLiteralMasker.MaskToken(
                    token);
            }

            if (token.IsKind(
                    SyntaxKind.StringLiteralToken) ||
                token.IsKind(
                    SyntaxKind.SingleLineRawStringLiteralToken) ||
                token.IsKind(
                    SyntaxKind.MultiLineRawStringLiteralToken) ||
                token.IsKind(
                    SyntaxKind.CharacterLiteralToken) ||
                token.IsKind(
                    SyntaxKind.InterpolatedStringTextToken))
            {
                return _literalMasker.MaskToken(
                    token);
            }

            if (!token.IsKind(
                    SyntaxKind.IdentifierToken))
            {
                return base.VisitToken(
                    token);
            }

            var originalIdentifier =
                token.Text;

            if (!_mappings.TryGetValue(
                    originalIdentifier,
                    out var maskedIdentifier))
            {
                maskedIdentifier =
                    CreateUniqueMaskedIdentifier(
                        originalIdentifier,
                        _mappings.Count + 1,
                        _sessionId,
                        _mode,
                        _usedMaskedIdentifiers,
                        _originalIdentifiers);

                _mappings.Add(
                    originalIdentifier,
                    maskedIdentifier);

                _usedMaskedIdentifiers.Add(
                    maskedIdentifier);
            }

            var maskedValueText =
                maskedIdentifier.StartsWith(
                    '@')
                    ? maskedIdentifier[1..]
                    : maskedIdentifier;

            return SyntaxFactory.Identifier(
                token.LeadingTrivia,
                SyntaxKind.IdentifierToken,
                maskedIdentifier,
                maskedValueText,
                token.TrailingTrivia);
        }
    }
}