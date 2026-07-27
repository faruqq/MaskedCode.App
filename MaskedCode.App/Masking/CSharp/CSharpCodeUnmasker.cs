using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;

namespace MaskedCode.App.Masking.CSharp;

public sealed class CSharpCodeUnmasker
{
    public string Unmask(
        string maskedCode,
        MappingVaultContent vaultContent)
    {
        ArgumentNullException.ThrowIfNull(
            maskedCode);

        ArgumentNullException.ThrowIfNull(
            vaultContent);

        if (maskedCode.Length == 0)
        {
            throw new ArgumentException(
                "Geri açılacak maskelenmiş C# kodu boş olamaz.",
                nameof(maskedCode));
        }

        if (vaultContent.Mappings is null ||
            vaultContent.Mappings.Count == 0)
        {
            throw new InvalidDataException(
                "Kasa içinde geri açılacak eşleme bulunamadı.");
        }

        if (vaultContent.SourceLanguage !=
            SourceLanguage.CSharp)
        {
            throw new InvalidDataException(
                "Seçilen kasa C# kaynak koduna ait değil.");
        }

        var lookup =
            MappingLookup.Create(
                vaultContent.Mappings);

        var codeWithRestoredTrivia =
            lookup.RestoreCommentMappings(
                maskedCode);

        var syntaxTree =
            CSharpSyntaxTree.ParseText(
                codeWithRestoredTrivia,
                new CSharpParseOptions(
                    LanguageVersion.Preview));

        var root =
            syntaxTree.GetRoot();

        var rewriter =
            new UnmaskingRewriter(
                lookup);

        var restoredRoot =
            rewriter.Visit(
                root);

        if (restoredRoot is null)
        {
            throw new InvalidOperationException(
                "C# syntax ağacı geri açılamadı.");
        }

        lookup.ValidateAllMappingsWereUsed();

        return restoredRoot.ToFullString();
    }

    private sealed class UnmaskingRewriter
        : CSharpSyntaxRewriter
    {
        private readonly MappingLookup _lookup;

        public UnmaskingRewriter(
            MappingLookup lookup)
            : base(
                visitIntoStructuredTrivia: true)
        {
            _lookup =
                lookup;
        }

        public override SyntaxToken VisitToken(
            SyntaxToken token)
        {
            var visitedToken =
                base.VisitToken(
                    token);

            if (visitedToken.IsKind(
                    SyntaxKind.IdentifierToken))
            {
                return RestoreToken(
                    visitedToken,
                    _lookup.IdentifierMappings);
            }

            if (visitedToken.IsKind(
                    SyntaxKind.NumericLiteralToken))
            {
                return RestoreToken(
                    visitedToken,
                    _lookup.NumericLiteralMappings);
            }

            if (visitedToken.IsKind(
                    SyntaxKind.StringLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.SingleLineRawStringLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.MultiLineRawStringLiteralToken) ||
                visitedToken.IsKind(
                    SyntaxKind.CharacterLiteralToken))
            {
                return RestoreToken(
                    visitedToken,
                    _lookup.StringLiteralMappings);
            }

            if (visitedToken.IsKind(
                    SyntaxKind.InterpolatedStringTextToken))
            {
                return RestoreInterpolatedStringText(
                    visitedToken);
            }

            return visitedToken;
        }

        private SyntaxToken RestoreInterpolatedStringText(
            SyntaxToken token)
        {
            if (!_lookup.StringLiteralMappings.TryGetValue(
                    token.Text,
                    out var mapping))
            {
                return token;
            }

            _lookup.MarkAsUsed(
                mapping.Index);

            return SyntaxFactory.Token(
                token.LeadingTrivia,
                SyntaxKind.InterpolatedStringTextToken,
                mapping.OriginalValue,
                mapping.OriginalValue,
                token.TrailingTrivia);
        }

        private SyntaxToken RestoreToken(
            SyntaxToken token,
            IReadOnlyDictionary<string, MappingEntry> mappings)
        {
            if (!mappings.TryGetValue(
                    token.Text,
                    out var mapping))
            {
                return token;
            }

            var restoredToken =
                SyntaxFactory.ParseToken(
                    mapping.OriginalValue);

            if (!restoredToken.IsKind(token.Kind()))
            {
                throw new InvalidDataException(
                    "Kasa içindeki C# eşlemesi beklenen token türünü üretmedi. " +
                    $"Tür: {token.Kind()}");
            }

            

            _lookup.MarkAsUsed(
                mapping.Index);

            return restoredToken
                .WithLeadingTrivia(
                    token.LeadingTrivia)
                .WithTrailingTrivia(
                    token.TrailingTrivia);
        }
    }

    private sealed class MappingLookup
    {
        private readonly IReadOnlyList<MaskingMapping> _mappings;
        private readonly bool[] _usedMappings;

        private MappingLookup(
            IReadOnlyList<MaskingMapping> mappings,
            IReadOnlyDictionary<string, MappingEntry> identifierMappings,
            IReadOnlyDictionary<string, MappingEntry> stringLiteralMappings,
            IReadOnlyDictionary<string, MappingEntry> numericLiteralMappings,
            IReadOnlyDictionary<string, MappingEntry> commentMappings)
        {
            _mappings =
                mappings;

            _usedMappings =
                new bool[mappings.Count];

            IdentifierMappings =
                identifierMappings;

            StringLiteralMappings =
                stringLiteralMappings;

            NumericLiteralMappings =
                numericLiteralMappings;

            CommentMappings =
                commentMappings;
        }

        public IReadOnlyDictionary<string, MappingEntry> IdentifierMappings
        {
            get;
        }

        public IReadOnlyDictionary<string, MappingEntry> StringLiteralMappings
        {
            get;
        }

        public IReadOnlyDictionary<string, MappingEntry> NumericLiteralMappings
        {
            get;
        }

        public IReadOnlyDictionary<string, MappingEntry> CommentMappings
        {
            get;
        }

        public static MappingLookup Create(
            IReadOnlyList<MaskingMapping> mappings)
        {
            var identifierMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            var stringLiteralMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            var numericLiteralMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            var commentMappings =
                new Dictionary<string, MappingEntry>(
                    StringComparer.Ordinal);

            for (var index = 0;
                 index < mappings.Count;
                 index++)
            {
                var mapping =
                    mappings[index];

                if (mapping is null)
                {
                    throw new InvalidDataException(
                        "Kasa içinde geçersiz bir eşleme bulundu.");
                }

                if (string.IsNullOrEmpty(
                        mapping.OriginalValue) ||
                    string.IsNullOrEmpty(
                        mapping.MaskedValue))
                {
                    throw new InvalidDataException(
                        "Kasa içinde boş değere sahip bir eşleme bulundu.");
                }

                var targetDictionary =
                    mapping.Kind switch
                    {
                        MaskingValueKind.Identifier =>
                            identifierMappings,

                        MaskingValueKind.StringLiteral =>
                            stringLiteralMappings,

                        MaskingValueKind.NumericLiteral =>
                            numericLiteralMappings,

                        MaskingValueKind.Comment =>
                            commentMappings,

                        _ => throw new InvalidDataException(
                            "Kasa içinde desteklenmeyen bir eşleme türü bulundu.")
                    };

                var entry =
                    new MappingEntry(
                        index,
                        mapping.OriginalValue);

                if (!targetDictionary.TryAdd(
                        mapping.MaskedValue,
                        entry))
                {
                    throw new InvalidDataException(
                        "Kasa içinde aynı maskelenmiş değere sahip " +
                        "birden fazla C# eşlemesi bulundu.");
                }
            }

            return new MappingLookup(
                mappings,
                identifierMappings,
                stringLiteralMappings,
                numericLiteralMappings,
                commentMappings);
        }

        public void MarkAsUsed(
            int mappingIndex)
        {
            _usedMappings[mappingIndex] =
                true;
        }

        public string RestoreCommentMappings(
            string maskedCode)
        {
            var restoredCode =
                maskedCode;

            foreach (var pair in CommentMappings
                         .OrderByDescending(item =>
                             item.Key.Length))
            {
                if (!restoredCode.Contains(
                        pair.Key,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                restoredCode =
                    restoredCode.Replace(
                        pair.Key,
                        pair.Value.OriginalValue,
                        StringComparison.Ordinal);

                MarkAsUsed(
                    pair.Value.Index);
            }

            return restoredCode;
        }

        public void ValidateAllMappingsWereUsed()
        {
            for (var index = 0;
                 index < _usedMappings.Length;
                 index++)
            {
                if (_usedMappings[index])
                {
                    continue;
                }

                var unusedMapping =
                    _mappings[index];

                throw new InvalidDataException(
                    "Kasa içindeki bir eşleme maskelenmiş C# kodunda bulunamadı. " +
                    "Geri açma işlemi durduruldu. " +
                    $"Tür: {unusedMapping.Kind}");
            }
        }
    }

    private sealed record MappingEntry(
        int Index,
        string OriginalValue);
}