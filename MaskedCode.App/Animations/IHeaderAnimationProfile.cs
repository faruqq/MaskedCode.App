namespace MaskedCode.App.Animations;

public interface IHeaderAnimationProfile
{
    string Id { get; }

    string AssetDirectoryName { get; }

    HeaderVisualState InitialState { get; }

    HeaderAnimationPlan CreatePlan(HeaderAnimationEvent animationEvent, HeaderVisualState currentState);
}