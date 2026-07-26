namespace MaskedCode.App.Masking.CSharp;

public sealed record CSharpMaskingResult(
    string MaskedCode,
    IReadOnlyList<MaskingMapping> Mappings,
    MaskingMode Mode) : IMaskingResult
{
    public SourceLanguage SourceLanguage =>
        SourceLanguage.CSharp;

    public int IdentifierCount =>
        Mappings.Count(mapping =>
            mapping.Kind == MaskingValueKind.Identifier);

    public int StringLiteralCount =>
        Mappings.Count(mapping =>
            mapping.Kind == MaskingValueKind.StringLiteral);

    public int NumericLiteralCount =>
        Mappings.Count(mapping =>
            mapping.Kind == MaskingValueKind.NumericLiteral);

    public int CommentCount =>
        Mappings.Count(mapping =>
            mapping.Kind == MaskingValueKind.Comment);
}