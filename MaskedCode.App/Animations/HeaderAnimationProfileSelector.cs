namespace MaskedCode.App.Animations;

public static class HeaderAnimationProfileSelector
{
    public static IHeaderAnimationProfile Select()
    {
        return HeaderAnimationSettings.SeriesSelection switch
        {
            HeaderAnimationSeriesSelection.Random =>
                SelectRandom(),

            HeaderAnimationSeriesSelection.ClassicVideo =>
                new ClassicVideoHeaderAnimationProfile(),

            HeaderAnimationSeriesSelection.MaskState =>
                new MaskStateHeaderAnimationProfile(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(HeaderAnimationSettings.SeriesSelection),
                HeaderAnimationSettings.SeriesSelection,
                "Desteklenmeyen başlık animasyonu serisi seçimi.")
        };
    }

    private static IHeaderAnimationProfile SelectRandom()
    {
        const int maskStateProbabilityPercent = 10;

        var shouldUseMaskState =
            Random.Shared.Next(100) <
            maskStateProbabilityPercent;

        return shouldUseMaskState
            ? new MaskStateHeaderAnimationProfile()
            : new ClassicVideoHeaderAnimationProfile();
    }
}