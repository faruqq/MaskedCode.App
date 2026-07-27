using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        ArgumentNullException.ThrowIfNull(
            sourceCode);

        ValidateMaskingMode(
            mode);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                sourceCode,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var root =
            syntaxTree.GetRoot();

        ValidateNoUnsafeTrivia(
            root);

        var frameworkSymbolClassifier =
            CSharpFrameworkSymbolClassifier.Create(
                syntaxTree);

        var originalIdentifiers =
            CollectOriginalIdentifiers(
                root);

        var originalLiterals =
            CollectOriginalLiterals(
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

        var commentMappings =
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

        var usedMaskedComments =
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
                 usedMaskedNumericLiterals);

        var commentMasker =
            new CSharpCommentMasker(
                mode,
                sessionId,
                commentMappings,
                usedMaskedComments);

        var directiveMasker =
            new CSharpDirectiveMasker(
                mode,
                sessionId,
                commentMappings,
                usedMaskedComments);

        var rewriter =
            new IdentifierMaskingRewriter(
                identifierMappings,
                usedMaskedIdentifiers,
                originalIdentifiers,
                literalMasker,
                numericLiteralMasker,
                commentMasker,
                directiveMasker,
                frameworkSymbolClassifier,
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
                numericLiteralMappings,
                commentMappings);

        return new CSharpMaskingResult(
            maskedRoot.ToFullString(),
            mappings,
            mode);
    }

    private static void ValidateNoUnsafeTrivia(SyntaxNode root)
    {
        var triviaList =
            root
                .DescendantTrivia(
                    descendIntoTrivia: true)
                .ToArray();

        var containsConflictMarker =
            triviaList.Any(trivia =>
                trivia.IsKind(
                    SyntaxKind.ConflictMarkerTrivia));

        if (containsConflictMarker)
        {
            throw new InvalidOperationException(
                "C# kaynak kodunda çözümlenmemiş birleştirme çakışması bulundu. " +
                "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.");
        }

        var containsDisabledText =
            triviaList.Any(trivia =>
                trivia.IsKind(
                    SyntaxKind.DisabledTextTrivia));

        if (containsDisabledText)
        {
            throw new InvalidOperationException(
                "C# kaynak kodunda etkin olmayan koşullu derleme içeriği bulundu. " +
                "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.");
        }

        var containsBadDirective =
            triviaList.Any(trivia =>
                trivia.IsKind(
                    SyntaxKind.BadDirectiveTrivia));

        if (containsBadDirective)
        {
            throw new InvalidOperationException(
                "C# kaynak kodunda geçersiz veya desteklenmeyen directive bulundu. " +
                "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.");
        }

        var containsSkippedTokens =
            triviaList.Any(trivia =>
                trivia.IsKind(
                    SyntaxKind.SkippedTokensTrivia));

        var containsUnsafeSyntaxErrors =
            root
                .GetDiagnostics()
                .Any(diagnostic =>
                    diagnostic.Severity ==
                        DiagnosticSeverity.Error &&
                    !IsSupportedDirectiveDiagnostic(
                        diagnostic));

        if (containsSkippedTokens ||
            containsUnsafeSyntaxErrors)
        {
            throw new InvalidOperationException(
                "C# kaynak kodunda ayrıştırılamayan token içeriği bulundu. " +
                "Bu içerik güvenli biçimde maskelenemediği için işlem durduruldu.");
        }
    }

    private static bool IsSupportedDirectiveDiagnostic(Diagnostic diagnostic)
    {
        return string.Equals(
            diagnostic.Id,
            "CS1029",
            StringComparison.Ordinal);
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

    private static IReadOnlyList<MaskingMapping> CreateMappings(
        IReadOnlyDictionary<string, string> identifierMappings,
        IReadOnlyDictionary<string, string> literalMappings,
        IReadOnlyDictionary<string, string> numericLiteralMappings,
        IReadOnlyDictionary<string, string> commentMappings)
    {
        var mappings =
            new List<MaskingMapping>(
                identifierMappings.Count +
                literalMappings.Count +
                numericLiteralMappings.Count +
                commentMappings.Count);

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

        foreach (var mapping in commentMappings)
        {
            mappings.Add(
                new MaskingMapping(
                    MaskingValueKind.Comment,
                    mapping.Key,
                    mapping.Value));
        }

        return mappings;
    }

    private static string CreateUniqueMaskedAttributeRoot(
    string shortAttributeName,
    int ordinal,
    string sessionId,
    MaskingMode mode,
    ISet<string> usedMaskedIdentifiers,
    ISet<string> originalIdentifiers)
    {
        const string attributeSuffix =
            "Attribute";

        for (var attempt = 0;
             attempt < MaximumCandidateAttemptCount;
             attempt++)
        {
            var candidateRoot =
                mode switch
                {
                    MaskingMode.MaximumPrivacy =>
                        CreateMaximumPrivacyIdentifier(
                            shortAttributeName,
                            sessionId,
                            ordinal + attempt),

                    MaskingMode.FormatPreserving =>
                        CreateFormatPreservingIdentifier(
                            shortAttributeName),

                    _ => throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Desteklenmeyen maskeleme modu.")
                };

            var candidateFullName =
                candidateRoot +
                attributeSuffix;

            if (string.Equals(
                    candidateRoot,
                    shortAttributeName,
                    StringComparison.Ordinal) ||
                originalIdentifiers.Contains(
                    candidateRoot) ||
                originalIdentifiers.Contains(
                    candidateFullName) ||
                usedMaskedIdentifiers.Contains(
                    candidateRoot) ||
                usedMaskedIdentifiers.Contains(
                    candidateFullName) ||
                IsCSharpKeyword(
                    candidateRoot))
            {
                continue;
            }

            return candidateRoot;
        }

        throw new InvalidOperationException(
            $"'{shortAttributeName}' C# attribute adı için benzersiz " +
            "bir maskeleme değeri üretilemedi.");
    }

    private static string CreateUniqueMaskedIdentifier(
        string identifier,
        int ordinal,
        string sessionId,
        MaskingMode mode,
        ISet<string> usedMaskedIdentifiers,
        ISet<string> originalIdentifiers)
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
                originalIdentifiers.Contains(
                    candidate) ||
                usedMaskedIdentifiers.Contains(
                    candidate) ||
                IsCSharpKeyword(
                    candidate))
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

        return SyntaxFacts.GetKeywordKind(
                   identifier) !=
               SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(
                   identifier) !=
               SyntaxKind.None;
    }

    private static string CreateMaximumPrivacyIdentifier(
        string identifier,
        string sessionId,
        int ordinal)
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
        private readonly CSharpLiteralMasker _literalMasker;
        private readonly CSharpNumericLiteralMasker _numericLiteralMasker;
        private readonly CSharpCommentMasker _commentMasker;
        private readonly CSharpDirectiveMasker _directiveMasker;
        private readonly string _sessionId;
        private readonly MaskingMode _mode;
        private readonly CSharpFrameworkSymbolClassifier _frameworkSymbolClassifier;

        public IdentifierMaskingRewriter(IDictionary<string, string> mappings, ISet<string> usedMaskedIdentifiers, ISet<string> originalIdentifiers, CSharpLiteralMasker literalMasker, CSharpNumericLiteralMasker numericLiteralMasker, CSharpCommentMasker commentMasker, CSharpDirectiveMasker directiveMasker, CSharpFrameworkSymbolClassifier frameworkSymbolClassifier, string sessionId, MaskingMode mode)
        {
            _mappings =
                mappings;

            _usedMaskedIdentifiers =
                usedMaskedIdentifiers;

            _originalIdentifiers =
                originalIdentifiers;

            _literalMasker =
                literalMasker;

            _numericLiteralMasker =
                numericLiteralMasker;

            _commentMasker =
                commentMasker;

            _directiveMasker =
                directiveMasker;

            _frameworkSymbolClassifier =
                frameworkSymbolClassifier;

            _sessionId =
                sessionId;

            _mode =
                mode;
        }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(
                    SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(
                    SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(
                    SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(
                    SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                return _commentMasker.MaskTrivia(
                    trivia);
            }

            if (trivia.IsKind(
                    SyntaxKind.RegionDirectiveTrivia) ||
                trivia.IsKind(
                    SyntaxKind.EndRegionDirectiveTrivia) ||
                trivia.IsKind(
                    SyntaxKind.ErrorDirectiveTrivia) ||
                trivia.IsKind(
                    SyntaxKind.WarningDirectiveTrivia))
            {
                return _directiveMasker.MaskTrivia(
                    trivia);
            }

            if (trivia.IsKind(
                    SyntaxKind.DefineDirectiveTrivia) ||
                trivia.IsKind(
                    SyntaxKind.UndefDirectiveTrivia) ||
                trivia.IsKind(
                    SyntaxKind.IfDirectiveTrivia) ||
                trivia.IsKind(
                    SyntaxKind.ElifDirectiveTrivia))
            {
                if (trivia.GetStructure() is not DirectiveTriviaSyntax directiveSyntax)
                {
                    throw new InvalidOperationException(
                        "C# koşullu derleme directive yapısı ayrıştırılamadı.");
                }

                var identifierTokens =
                    directiveSyntax
                        .DescendantTokens()
                        .Where(token =>
                            token.IsKind(
                                SyntaxKind.IdentifierToken))
                        .ToArray();

                if (identifierTokens.Length == 0)
                {
                    return trivia;
                }

                var updatedDirective =
                    directiveSyntax.ReplaceTokens(
                        identifierTokens,
                        (originalToken, _) =>
                            VisitToken(
                                originalToken));

                return SyntaxFactory.Trivia(
                    updatedDirective);
            }

            if (trivia.IsKind(
                    SyntaxKind.LineDirectiveTrivia))
            {
                if (trivia.GetStructure() is not LineDirectiveTriviaSyntax lineDirective)
                {
                    throw new InvalidOperationException(
                        "C# line directive yapısı ayrıştırılamadı.");
                }

                if (!lineDirective.File.IsKind(
                        SyntaxKind.StringLiteralToken))
                {
                    return trivia;
                }

                var maskedFile =
                    _literalMasker.MaskToken(
                        lineDirective.File);

                var updatedDirective =
                    lineDirective.WithFile(
                        maskedFile);

                return SyntaxFactory.Trivia(
                    updatedDirective);
            }

            if (trivia.IsKind(
                    SyntaxKind.LineSpanDirectiveTrivia))
            {
                if (trivia.GetStructure() is not LineSpanDirectiveTriviaSyntax lineSpanDirective)
                {
                    throw new InvalidOperationException(
                        "C# line span directive yapısı ayrıştırılamadı.");
                }

                if (!lineSpanDirective.File.IsKind(
                        SyntaxKind.StringLiteralToken))
                {
                    return trivia;
                }

                var maskedFile =
                    _literalMasker.MaskToken(
                        lineSpanDirective.File);

                var updatedDirective =
                    lineSpanDirective.WithFile(
                        maskedFile);

                return SyntaxFactory.Trivia(
                    updatedDirective);
            }

            if (trivia.IsKind(
                    SyntaxKind.PragmaChecksumDirectiveTrivia))
            {
                if (trivia.GetStructure() is not PragmaChecksumDirectiveTriviaSyntax checksumDirective)
                {
                    throw new InvalidOperationException(
                        "C# pragma checksum directive yapısı ayrıştırılamadı.");
                }

                if (!checksumDirective.File.IsKind(
                        SyntaxKind.StringLiteralToken))
                {
                    throw new InvalidOperationException(
                        "C# pragma checksum directive içindeki dosya adı ayrıştırılamadı.");
                }

                var maskedFile =
                    _literalMasker.MaskToken(
                        checksumDirective.File);

                var updatedDirective =
                    checksumDirective.WithFile(
                        maskedFile);

                return SyntaxFactory.Trivia(
                    updatedDirective);
            }

            return base.VisitTrivia(
                trivia);
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            var isImplicitlyTypedVariableKeyword =
                IsImplicitlyTypedVariableKeyword(
                    token);

            var shouldPreserveFrameworkSymbol =
                _frameworkSymbolClassifier.ShouldPreserve(
                    token);

            var visitedToken =
                base.VisitToken(
                    token);

            if (visitedToken.IsKind(
                    SyntaxKind.NumericLiteralToken))
            {
                return _numericLiteralMasker.MaskToken(
                    visitedToken);
            }

            if (visitedToken.IsKind(
                    SyntaxKind.StringLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.SingleLineRawStringLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.MultiLineRawStringLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.CharacterLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.InterpolatedStringTextToken))
            {
                return _literalMasker.MaskToken(
                    visitedToken);
            }

            if (!visitedToken.IsKind(
                    SyntaxKind.IdentifierToken))
            {
                return visitedToken;
            }

            if (isImplicitlyTypedVariableKeyword ||
    IsNameOfOperator(
        token) ||
    shouldPreserveFrameworkSymbol)
            {
                return visitedToken;
            }

            if (_frameworkSymbolClassifier.TryGetSourceAttributeIdentifier(
                    token,
                    out var attributeTypeName,
                    out var usesShortAttributeName))
            {
                return MaskAttributeIdentifier(
                    visitedToken,
                    attributeTypeName,
                    usesShortAttributeName);
            }

            var originalIdentifier =
                visitedToken.Text;

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

            return CreateIdentifierToken(visitedToken,maskedIdentifier);
        }

        private SyntaxToken MaskAttributeIdentifier(
    SyntaxToken token,
    string attributeTypeName,
    bool usesShortName)
        {
            const string attributeSuffix =
                "Attribute";

            var shortAttributeName =
                attributeTypeName[..^attributeSuffix.Length];

            if (!_mappings.TryGetValue(
                    shortAttributeName,
                    out var maskedRoot))
            {
                if (_mappings.TryGetValue(
                        attributeTypeName,
                        out var maskedFullName) &&
                    maskedFullName.EndsWith(
                        attributeSuffix,
                        StringComparison.Ordinal))
                {
                    maskedRoot =
                        maskedFullName[..^attributeSuffix.Length];
                }
                else
                {
                    maskedRoot =
                        CreateUniqueMaskedAttributeRoot(
                            shortAttributeName,
                            _mappings.Count + 1,
                            _sessionId,
                            _mode,
                            _usedMaskedIdentifiers,
                            _originalIdentifiers);
                }

                AddAttributeMappingIfMissing(
                    shortAttributeName,
                    maskedRoot);
            }

            var maskedAttributeTypeName =
                maskedRoot +
                attributeSuffix;

            AddAttributeMappingIfMissing(
                attributeTypeName,
                maskedAttributeTypeName);

            var maskedIdentifier =
                usesShortName
                    ? maskedRoot
                    : maskedAttributeTypeName;

            return CreateIdentifierToken(
                token,
                maskedIdentifier);
        }

        private void AddAttributeMappingIfMissing(
            string originalIdentifier,
            string maskedIdentifier)
        {
            if (_mappings.ContainsKey(
                    originalIdentifier))
            {
                return;
            }

            if (_usedMaskedIdentifiers.Contains(
                    maskedIdentifier))
            {
                throw new InvalidOperationException(
                    $"'{originalIdentifier}' C# attribute adı için üretilen " +
                    "maskeleme değeri başka bir identifier tarafından kullanılıyor.");
            }

            _mappings.Add(
                originalIdentifier,
                maskedIdentifier);

            _usedMaskedIdentifiers.Add(
                maskedIdentifier);
        }

        private static SyntaxToken CreateIdentifierToken(
            SyntaxToken token,
            string maskedIdentifier)
        {
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

        private static bool IsNameOfOperator(
    SyntaxToken token)
        {
            if (!string.Equals(
                    token.Text,
                    "nameof",
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (token.Parent is not IdentifierNameSyntax identifierName)
            {
                return false;
            }

            return identifierName.Parent is InvocationExpressionSyntax invocation &&
                   ReferenceEquals(
                       invocation.Expression,
                       identifierName);
        }

        private static bool IsImplicitlyTypedVariableKeyword(SyntaxToken token)
        {
            return string.Equals(
                       token.ValueText,
                       "var",
                       StringComparison.Ordinal) &&
                   token.Parent is IdentifierNameSyntax;
        }
    }
}