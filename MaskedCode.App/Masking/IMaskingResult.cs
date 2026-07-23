namespace MaskedCode.App.Masking;

public interface IMaskingResult
{
    string MaskedCode { get; }

    IReadOnlyList<MaskingMapping> Mappings { get; }

    MaskingMode Mode { get; }

    SourceLanguage SourceLanguage { get; }
}