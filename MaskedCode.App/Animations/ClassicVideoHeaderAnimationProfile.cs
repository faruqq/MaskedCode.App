namespace MaskedCode.App.Animations;

public sealed class ClassicVideoHeaderAnimationProfile : IHeaderAnimationProfile
{
    public string Id => "classic-video";

    public string AssetDirectoryName => "ClassicVideo";

    public HeaderVisualState InitialState => HeaderVisualState.CodeMasking;

    public HeaderAnimationPlan CreatePlan(HeaderAnimationEvent animationEvent, HeaderVisualState currentState)
    {
        return animationEvent switch
        {
            HeaderAnimationEvent.ApplicationStarted =>
                CreateLoopPlan(
                    "application-started.mp4",
                    HeaderVisualState.CodeMasking),

            HeaderAnimationEvent.CodeRestoreTabActivated =>
                CreateLoopPlan(
                    "code-restore-tab-activated.mp4",
                    HeaderVisualState.CodeRestore),

            HeaderAnimationEvent.CodeMaskingTabActivated =>
                CreateLoopPlan(
                    "code-masking-tab-activated.mp4",
                    HeaderVisualState.CodeMasking),

            HeaderAnimationEvent.MaskingStarted =>
                CreateTemporaryPlan(
                    "masking-started.mp4",
                    HeaderVisualState.CodeMasking),

            HeaderAnimationEvent.ErrorOccurred =>
                CreateTemporaryPlan(
                    "error-occurred.mp4",
                    NormalizeDefaultState(currentState)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(animationEvent),
                animationEvent,
                "Desteklenmeyen başlık animasyonu olayı.")
        };
    }

    private static HeaderAnimationPlan CreateLoopPlan(string fileName, HeaderVisualState finalState)
    {
        return new HeaderAnimationPlan(
            [
                new HeaderAnimationStep(
                    fileName,
                    HeaderAnimationAssetType.Video,
                    HeaderAnimationPlayback.Loop)
            ],
            finalState);
    }

    private static HeaderAnimationPlan CreateTemporaryPlan(string fileName, HeaderVisualState returnState)
    {
        return new HeaderAnimationPlan(
            [
                new HeaderAnimationStep(
                    fileName,
                    HeaderAnimationAssetType.Video,
                    HeaderAnimationPlayback.Once),

                CreateDefaultLoopStep(returnState)
            ],
            returnState);
    }

    private static HeaderAnimationStep CreateDefaultLoopStep(HeaderVisualState state)
    {
        var fileName = state == HeaderVisualState.CodeRestore
            ? "code-restore-tab-activated.mp4"
            : "code-masking-tab-activated.mp4";

        return new HeaderAnimationStep(
            fileName,
            HeaderAnimationAssetType.Video,
            HeaderAnimationPlayback.Loop);
    }

    private static HeaderVisualState NormalizeDefaultState(HeaderVisualState state)
    {
        return state == HeaderVisualState.CodeRestore
            ? HeaderVisualState.CodeRestore
            : HeaderVisualState.CodeMasking;
    }
}