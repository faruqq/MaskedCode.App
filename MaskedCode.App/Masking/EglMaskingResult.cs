namespace MaskedCode.App.Masking;

public sealed record EglMaskingResult(
    string MaskedCode,
    IReadOnlyList<MaskingMapping> Mappings,
    MaskingMode Mode) : IMaskingResult
{
    public int IdentifierCount =>
        Mappings.Count(x =>
            x.Kind == MaskingValueKind.Identifier);

    public int StringLiteralCount =>
        Mappings.Count(x =>
            x.Kind == MaskingValueKind.StringLiteral);

    public int NumericLiteralCount =>
        Mappings.Count(x =>
            x.Kind == MaskingValueKind.NumericLiteral);

    public int CommentCount =>
        Mappings.Count(x =>
            x.Kind == MaskingValueKind.Comment);
}