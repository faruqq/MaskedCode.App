namespace MaskedCode.App.Masking;

public sealed record MappingVaultContent(
    DateTimeOffset CreatedAtUtc,
    MaskingMode MaskingMode,
    IReadOnlyList<MaskingMapping> Mappings,
    SourceLanguage SourceLanguage = SourceLanguage.Pl1);