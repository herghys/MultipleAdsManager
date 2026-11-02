namespace Herghys.MultipleAdsManager.Runtime
{
    public enum IAdsType
    {
        Banner = 0,
        Interstitial = 1,
        Rewarded = 2,
        AppOpen = 3,
        RewardedInterstitial = 4
    }

    public enum AdType
    {
        Banner = 0,
        Interstitial = 1,
        Rewarded = 2,
        AppOpen = 3,
        RewardedInterstitial = 4
    }

    public enum BannerPosition
    {
        Top = 0,
        Bottom = 1,
        TopLeft = 2,
        TopRight = 3,
        BottomLeft = 4,
        BottomRight = 5,
        Center = 6
    }

    public enum AdNetworkType
    {
        GoogleAds,
        LevelPlay,
        AppLovin,
        Vungle,
        Mock
    }

    [System.Serializable]
    public struct AdReward
    {
        public string ItemName;
        public double Amount;

        public AdReward(string itemName, double amount)
        {
            ItemName = itemName;
            Amount = amount;
        }

        public override string ToString() => $"{ItemName} x {Amount}";
    }
}
