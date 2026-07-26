namespace MaskedCode.App.Animations;

public enum HeaderAnimationEvent
{
    ApplicationStarted,
    CodeRestoreTabActivated,
    CodeMaskingTabActivated,
    MaskingStarted,
    ErrorOccurred
}

public enum HeaderVisualState
{
    CodeMasking,
    CodeRestore,
    MaskingCompleted,
    Error
}

public enum HeaderAnimationAssetType
{
    Video,
    Image
}

public enum HeaderAnimationPlayback
{
    Once,
    Loop,
    Static
}

public sealed record HeaderAnimationStep(
    string FileName,
    HeaderAnimationAssetType AssetType,
    HeaderAnimationPlayback Playback);

public sealed record HeaderAnimationPlan(
    IReadOnlyList<HeaderAnimationStep> Steps,
    HeaderVisualState FinalState);