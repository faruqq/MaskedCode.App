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
        IHeaderAnimationProfile[] profiles =
        [
            new ClassicVideoHeaderAnimationProfile(),
            new MaskStateHeaderAnimationProfile()
        ];

        var selectedIndex = Random.Shared.Next(profiles.Length);

        return profiles[selectedIndex];
    }
}