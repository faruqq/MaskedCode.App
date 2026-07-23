namespace MaskedCode.App.Masking.Pl1;

internal sealed record Pl1NumericMaskingResult(
    string MaskedCode,
    IReadOnlyDictionary<string, string> Mappings,
    IReadOnlySet<int> StructuralQuotedTextStartIndexes,
    IReadOnlySet<int> ScientificExponentMarkerIndexes);