namespace MaskedCode.App.Animations;

public sealed class MaskStateHeaderAnimationProfile : IHeaderAnimationProfile
{
    public string Id => "mask-state";

    public string AssetDirectoryName => "MaskState";

    public HeaderVisualState InitialState => HeaderVisualState.CodeMasking;

    public HeaderAnimationPlan CreatePlan(HeaderAnimationEvent animationEvent, HeaderVisualState currentState)
    {
        return animationEvent switch
        {
            HeaderAnimationEvent.ApplicationStarted =>
                CreateApplicationStartedPlan(),

            HeaderAnimationEvent.CodeRestoreTabActivated =>
                CreateCodeRestorePlan(currentState),

            HeaderAnimationEvent.CodeMaskingTabActivated =>
                CreateCodeMaskingPlan(currentState),

            HeaderAnimationEvent.MaskingStarted =>
                CreateMaskingStartedPlan(currentState),

            HeaderAnimationEvent.ErrorOccurred =>
                CreateErrorPlan(currentState),

            _ => throw new ArgumentOutOfRangeException(
                nameof(animationEvent),
                animationEvent,
                "Desteklenmeyen başlık animasyonu olayı.")
        };
    }

    private static HeaderAnimationPlan CreateApplicationStartedPlan()
    {
        return new HeaderAnimationPlan(
            [
                CreateLoopVideoStep("application-started.mp4")
            ],
            HeaderVisualState.CodeMasking);
    }

    private static HeaderAnimationPlan CreateCodeRestorePlan(HeaderVisualState currentState)
    {
        var transitionFileName = currentState == HeaderVisualState.Error
            ? "code-restore-tab-activated-from-error.mp4"
            : "code-restore-tab-activated.mp4";

        return new HeaderAnimationPlan(
            [
                CreateOnceVideoStep(transitionFileName),
                CreateStaticImageStep("code-restore-tab-activated.png")
            ],
            HeaderVisualState.CodeRestore);
    }

    private static HeaderAnimationPlan CreateCodeMaskingPlan(HeaderVisualState currentState)
    {
        var transitionFileName = currentState == HeaderVisualState.Error
            ? "code-masking-tab-activated-from-error.mp4"
            : "code-masking-tab-activated.mp4";

        return new HeaderAnimationPlan(
            [
                CreateOnceVideoStep(transitionFileName),
                CreateLoopVideoStep("application-started.mp4")
            ],
            HeaderVisualState.CodeMasking);
    }

    private static HeaderAnimationPlan CreateMaskingStartedPlan(HeaderVisualState currentState)
    {
        var transitionFileName = currentState == HeaderVisualState.Error
            ? "masking-started-from-error.mp4"
            : "masking-started.mp4";

        return new HeaderAnimationPlan(
            [
                CreateOnceVideoStep(transitionFileName),
                CreateStaticImageStep("masking-started.png")
            ],
            HeaderVisualState.MaskingCompleted);
    }

    private static HeaderAnimationPlan CreateErrorPlan(HeaderVisualState currentState)
    {
        var transitionFileName = currentState switch
        {
            HeaderVisualState.CodeMasking =>
                "error-occurred-from-application-started.mp4",

            HeaderVisualState.CodeRestore =>
                "error-occurred-from-code-restore-tab-activated.mp4",

            HeaderVisualState.MaskingCompleted =>
                "error-occurred-from-masking-started.mp4",

            HeaderVisualState.Error =>
                null,

            _ => throw new ArgumentOutOfRangeException(
                nameof(currentState),
                currentState,
                "Desteklenmeyen başlık görsel durumu.")
        };

        if (transitionFileName is null)
        {
            return new HeaderAnimationPlan(
                [
                    CreateStaticImageStep("error-occurred.png")
                ],
                HeaderVisualState.Error);
        }

        return new HeaderAnimationPlan(
            [
                CreateOnceVideoStep(transitionFileName),
                CreateStaticImageStep("error-occurred.png")
            ],
            HeaderVisualState.Error);
    }

    private static HeaderAnimationStep CreateOnceVideoStep(string fileName)
    {
        return new HeaderAnimationStep(
            fileName,
            HeaderAnimationAssetType.Video,
            HeaderAnimationPlayback.Once);
    }

    private static HeaderAnimationStep CreateLoopVideoStep(string fileName)
    {
        return new HeaderAnimationStep(
            fileName,
            HeaderAnimationAssetType.Video,
            HeaderAnimationPlayback.Loop);
    }

    private static HeaderAnimationStep CreateStaticImageStep(string fileName)
    {
        return new HeaderAnimationStep(
            fileName,
            HeaderAnimationAssetType.Image,
            HeaderAnimationPlayback.Static);
    }
}