namespace MaskedCode.App.Animations;

public sealed class MaskStateHeaderAnimationProfile : IHeaderAnimationProfile
{
    private const int TransitionFrameRate = 60;

    public string Id => "mask-state";

    public string AssetDirectoryName => "MaskState";

    public HeaderVisualState InitialState =>
        HeaderVisualState.CodeMasking;

    public HeaderAnimationPlan CreatePlan(
        HeaderAnimationEvent animationEvent,
        HeaderVisualState currentState)
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
                CreateStaticImageStep(
                    "States/application-started.png")
            ],
            HeaderVisualState.CodeMasking);
    }

    private static HeaderAnimationPlan CreateCodeRestorePlan(
        HeaderVisualState currentState)
    {
        var transitionDirectory = currentState ==
                                  HeaderVisualState.Error
            ? "code-restore-tab-activated-from-error"
            : "code-restore-tab-activated";

        return new HeaderAnimationPlan(
            [
                CreateFrameSequenceStep(transitionDirectory),
                CreateStaticImageStep(
                    "States/code-restore-tab-activated.png")
            ],
            HeaderVisualState.CodeRestore);
    }

    private static HeaderAnimationPlan CreateCodeMaskingPlan(
        HeaderVisualState currentState)
    {
        if (currentState == HeaderVisualState.CodeMasking)
        {
            return new HeaderAnimationPlan(
                [
                    CreateStaticImageStep(
                        "States/application-started.png")
                ],
                HeaderVisualState.CodeMasking);
        }

        var transitionDirectory = currentState ==
                                  HeaderVisualState.Error
            ? "code-masking-tab-activated-from-error"
            : "code-masking-tab-activated";

        return new HeaderAnimationPlan(
            [
                CreateFrameSequenceStep(transitionDirectory),
                CreateStaticImageStep(
                    "States/application-started.png")
            ],
            HeaderVisualState.CodeMasking);
    }

    private static HeaderAnimationPlan CreateMaskingStartedPlan(
        HeaderVisualState currentState)
    {
        var transitionDirectory = currentState ==
                                  HeaderVisualState.Error
            ? "masking-started-from-error"
            : "masking-started";

        return new HeaderAnimationPlan(
            [
                CreateFrameSequenceStep(transitionDirectory),
                CreateStaticImageStep(
                    "States/masking-completed.png")
            ],
            HeaderVisualState.MaskingCompleted);
    }

    private static HeaderAnimationPlan CreateErrorPlan(
        HeaderVisualState currentState)
    {
        var transitionDirectory = currentState switch
        {
            HeaderVisualState.CodeMasking =>
                "error-occurred-from-application-started",

            HeaderVisualState.CodeRestore =>
                "error-occurred-from-code-restore-tab-activated",

            HeaderVisualState.MaskingCompleted =>
                "error-occurred-from-masking-started",

            HeaderVisualState.Error =>
                null,

            _ => throw new ArgumentOutOfRangeException(
                nameof(currentState),
                currentState,
                "Desteklenmeyen başlık görsel durumu.")
        };

        if (transitionDirectory is null)
        {
            return new HeaderAnimationPlan(
                [
                    CreateStaticImageStep(
                        "States/error-state.png")
                ],
                HeaderVisualState.Error);
        }

        return new HeaderAnimationPlan(
            [
                CreateFrameSequenceStep(transitionDirectory),
                CreateStaticImageStep(
                    "States/error-state.png")
            ],
            HeaderVisualState.Error);
    }

    private static HeaderAnimationStep CreateFrameSequenceStep(
        string directoryName)
    {
        return new HeaderAnimationStep(
            $"Transitions/{directoryName}",
            HeaderAnimationAssetType.PngFrameSequence,
            HeaderAnimationPlayback.Once,
            TransitionFrameRate);
    }

    private static HeaderAnimationStep CreateStaticImageStep(
        string assetPath)
    {
        return new HeaderAnimationStep(
            assetPath,
            HeaderAnimationAssetType.Image,
            HeaderAnimationPlayback.Static);
    }
}