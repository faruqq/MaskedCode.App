namespace MaskedCode.App.Animations;

public static class HeaderAnimationSettings
{
    public static HeaderAnimationSeriesSelection SeriesSelection { get; } =
        HeaderAnimationSeriesSelection.Random;
}

public enum HeaderAnimationSeriesSelection
{
    Random,
    ClassicVideo,
    MaskState
}