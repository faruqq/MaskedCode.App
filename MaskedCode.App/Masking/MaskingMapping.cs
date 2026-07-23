namespace MaskedCode.App.Masking;

public sealed record MaskingMapping(
    MaskingValueKind Kind,
    string OriginalValue,
    string MaskedValue);