namespace Herghys.MultipleAdsManager.Runtime
{
    public interface IAdWeightable
    {
        int BannerWeight { get; set; }
        int InterstitialWeight { get; set; }
        int RewardedWeight { get; set; }

        int GetWeight(IAdsType adType);
        void SetWeight(IAdsType adType, int weight);

        int GetWeight(AdType adType);
        void SetWeight(AdType adType, int weight);
    }
}
