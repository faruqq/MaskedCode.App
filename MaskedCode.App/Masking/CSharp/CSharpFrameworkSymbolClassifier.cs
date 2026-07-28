using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

namespace MaskedCode.App.Masking.CSharp;

/// <summary>
/// Neden var?
/// C# kaynak kodundaki kullanıcı tanımlı identifier'lar ile .NET framework
/// sembollerini yalnızca metinsel karşılaştırmayla güvenilir biçimde ayırmak
/// mümkün değildir.
///
/// Ne çözüyor?
/// Roslyn semantic model üzerinden çözümlenen identifier'ın kaynak kodda mı
/// tanımlandığını, yoksa .NET runtime assembly'lerinden mi geldiğini belirler.
///
/// Hangi örneği destekliyor?
/// System, Console, DateTime, Guid, Task, List, IEnumerable, WriteLine,
/// InvariantCulture ve LINQ extension metotları gibi framework sembollerinin
/// maskelenmeden korunmasını destekler.
///
/// Nerede kullanılır?
/// IdentifierMaskingRewriter, her IdentifierToken için maskeleme kararı
/// vermeden önce bu sınıfı kullanır.
///
/// Gelecekte neye temel olur?
/// İleride üçüncü taraf assembly'ler veya kullanıcı tarafından belirlenen
/// sembol koruma kuralları eklenmek istenirse semantik sınıflandırma noktası
/// olarak genişletilebilir.
/// </summary>
internal sealed class CSharpFrameworkSymbolClassifier
{
    private readonly SemanticModel _semanticModel;
    private readonly IAssemblySymbol _sourceAssembly;

    private const string KnownGlobalUsingsSource =
    """
    global using System;
    global using System.Collections.Generic;
    global using System.IO;
    global using System.Linq;
    global using System.Net.Http;
    global using System.Threading;
    global using System.Threading.Tasks;
    global using Xunit;
    """;
    private CSharpFrameworkSymbolClassifier(SemanticModel semanticModel, IAssemblySymbol sourceAssembly)
    {
        _semanticModel =
            semanticModel;

        _sourceAssembly =
            sourceAssembly;
    }

    public static CSharpFrameworkSymbolClassifier Create(
     SyntaxTree syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(
            syntaxTree);

        var references =
            CreateRuntimeMetadataReferences();

        var parseOptions =
            syntaxTree.Options as CSharpParseOptions ??
            CSharpParseOptions.Default;

        var knownGlobalUsingsSyntaxTree =
            CSharpSyntaxTree.ParseText(
                KnownGlobalUsingsSource,
                parseOptions,
                path: "__MaskedCode.KnownGlobalUsings.g.cs");

        var compilation =
            CSharpCompilation.Create(
                assemblyName:
                    $"MaskedCode.SemanticAnalysis.{Guid.NewGuid():N}",
                syntaxTrees:
                    new[]
                    {
                    knownGlobalUsingsSyntaxTree,
                    syntaxTree
                    },
                references:
                    references,
                options:
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary));

        var semanticModel =
            compilation.GetSemanticModel(
                syntaxTree,
                ignoreAccessibility: true);

        return new CSharpFrameworkSymbolClassifier(
            semanticModel,
            compilation.Assembly);
    }

    public bool ShouldPreserve(SyntaxToken token)
    {
        if (!token.IsKind(
                SyntaxKind.IdentifierToken))
        {
            return false;
        }

        if (IsSourceDeclaration(
                token))
        {
            return false;
        }

        var symbol =
            ResolveSymbol(
                token);

        return symbol is not null &&
               IsFrameworkSymbol(
                   symbol);
    }

    public bool TryGetSourceAttributeIdentifier(
    SyntaxToken token,
    out string attributeTypeName,
    out bool usesShortName)
    {
        attributeTypeName =
            string.Empty;

        usesShortName =
            false;

        if (!token.IsKind(
                SyntaxKind.IdentifierToken))
        {
            return false;
        }

        var attributeType =
            ResolveSourceAttributeType(
                token);

        if (attributeType is null)
        {
            return false;
        }

        const string attributeSuffix =
            "Attribute";

        if (!attributeType.Name.EndsWith(
                attributeSuffix,
                StringComparison.Ordinal))
        {
            return false;
        }

        var shortName =
            attributeType.Name[..^attributeSuffix.Length];

        var tokenName =
            token.ValueText;

        if (!string.Equals(
                tokenName,
                attributeType.Name,
                StringComparison.Ordinal) &&
            !string.Equals(
                tokenName,
                shortName,
                StringComparison.Ordinal))
        {
            return false;
        }

        attributeTypeName =
            attributeType.Name;

        usesShortName =
            string.Equals(
                tokenName,
                shortName,
                StringComparison.Ordinal);

        return true;
    }

    private bool IsSourceDeclaration(SyntaxToken token)
    {
        var identifierValue =
            token.ValueText;

        foreach (var node in token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>())
        {
            var declaredSymbol =
                _semanticModel.GetDeclaredSymbol(
                    node);

            if (declaredSymbol is null)
            {
                continue;
            }

            if (string.Equals(
                    declaredSymbol.Name,
                    identifierValue,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private INamedTypeSymbol? ResolveSourceAttributeType(
    SyntaxToken token)
    {
        if (token.Parent is TypeDeclarationSyntax typeDeclaration &&
            typeDeclaration.Identifier == token)
        {
            var declaredType =
                _semanticModel.GetDeclaredSymbol(
                    typeDeclaration);

            return IsSourceAttributeType(
                    declaredType)
                ? declaredType
                : null;
        }

        var attributeSyntax =
            token
                .Parent?
                .AncestorsAndSelf()
                .OfType<AttributeSyntax>()
                .FirstOrDefault();

        if (attributeSyntax is null ||
            !attributeSyntax.Name
                .DescendantTokens()
                .Contains(
                    token))
        {
            return null;
        }

        var symbolInfo =
            _semanticModel.GetSymbolInfo(
                attributeSyntax);

        var attributeConstructor =
            symbolInfo.Symbol as IMethodSymbol ??
            symbolInfo
                .CandidateSymbols
                .OfType<IMethodSymbol>()
                .FirstOrDefault();

        var attributeType =
            attributeConstructor?
                .ContainingType;

        return IsSourceAttributeType(
                attributeType)
            ? attributeType
            : null;
    }

    private bool IsSourceAttributeType(
        INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol is null ||
            !SymbolEqualityComparer.Default.Equals(
                typeSymbol.ContainingAssembly,
                _sourceAssembly))
        {
            return false;
        }

        for (var currentType = typeSymbol;
             currentType is not null;
             currentType = currentType.BaseType)
        {
            if (string.Equals(
                    currentType.ToDisplayString(),
                    "System.Attribute",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private ISymbol? ResolveSymbol(SyntaxToken token)
    {
        if (token.Parent is not SyntaxNode identifierNode)
        {
            return null;
        }

        if (identifierNode is IdentifierNameSyntax identifierName)
        {
            var aliasSymbol =
                _semanticModel.GetAliasInfo(
                    identifierName);

            if (aliasSymbol is not null)
            {
                return aliasSymbol;
            }
        }

        var symbolInfo =
            _semanticModel.GetSymbolInfo(
                identifierNode);

        if (symbolInfo.Symbol is not null)
        {
            return symbolInfo.Symbol;
        }

        if (symbolInfo.CandidateSymbols.Length == 0)
        {
            return null;
        }

        var frameworkCandidates =
            symbolInfo
                .CandidateSymbols
                .Where(
                    IsFrameworkSymbol)
                .ToArray();

        return frameworkCandidates.Length ==
               symbolInfo.CandidateSymbols.Length
            ? frameworkCandidates[0]
            : null;
    }

    private bool IsFrameworkSymbol(ISymbol symbol)
    {
        if (symbol is IAliasSymbol)
        {
            return false;
        }

        if (symbol is INamespaceSymbol namespaceSymbol)
        {
            return namespaceSymbol
                .ConstituentNamespaces
                .Any(
                    IsFrameworkNamespace);
        }

        var containingAssembly =
            symbol.ContainingAssembly;

        return containingAssembly is not null &&
               !SymbolEqualityComparer.Default.Equals(
                   containingAssembly,
                   _sourceAssembly);
    }

    private bool IsFrameworkNamespace(INamespaceSymbol namespaceSymbol)
    {
        var containingAssembly =
            namespaceSymbol.ContainingAssembly;

        if (containingAssembly is not null)
        {
            return !SymbolEqualityComparer.Default.Equals(
                containingAssembly,
                _sourceAssembly);
        }

        return namespaceSymbol
            .ConstituentNamespaces
            .Any(namespaceCandidate =>
                !SymbolEqualityComparer.Default.Equals(
                    namespaceCandidate,
                    namespaceSymbol) &&
                IsFrameworkNamespace(
                    namespaceCandidate));
    }

    private static IReadOnlyList<MetadataReference>
    CreateRuntimeMetadataReferences()
    {
        var trustedPlatformAssemblies =
            AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (string.IsNullOrWhiteSpace(
                trustedPlatformAssemblies))
        {
            throw new InvalidOperationException(
                ".NET runtime assembly listesi alınamadığı için " +
                "C# framework sembolleri semantik olarak çözümlenemedi.");
        }

        var runtimeAssemblyPaths =
            trustedPlatformAssemblies.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var knownFrameworkAssemblyPaths =
            CreateKnownFrameworkAssemblyPaths();

        return runtimeAssemblyPaths
            .Concat(
                knownFrameworkAssemblyPaths)
            .Where(path =>
                !string.IsNullOrWhiteSpace(
                    path))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Select(path =>
                MetadataReference.CreateFromFile(
                    path))
            .ToArray();
    }

    private static IReadOnlyList<string>
    CreateKnownFrameworkAssemblyPaths()
    {
        return new[]
        {
        typeof(Xunit.Assert)
            .Assembly
            .Location,

        typeof(Xunit.FactAttribute)
            .Assembly
            .Location,

        typeof(Xunit.TheoryAttribute)
            .Assembly
            .Location,

        typeof(Xunit.InlineDataAttribute)
            .Assembly
            .Location,

        typeof(Xunit.Abstractions.ITest)
            .Assembly
            .Location
    };
    }
}