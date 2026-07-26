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
    Image,
    PngFrameSequence
}

public enum HeaderAnimationPlayback
{
    Once,
    Loop,
    Static
}

public sealed record HeaderAnimationStep(
    string AssetPath,
    HeaderAnimationAssetType AssetType,
    HeaderAnimationPlayback Playback,
    int FrameRate = 0);

public sealed record HeaderAnimationPlan(
    IReadOnlyList<HeaderAnimationStep> Steps,
    HeaderVisualState FinalState);