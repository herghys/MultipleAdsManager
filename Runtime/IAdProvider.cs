using System;
using System.Collections.Generic;

namespace Herghys.MultipleAdsManager.Runtime
{
    public interface IAdProvider : IAdWeightable
    {
        string NetworkName { get; }
        AdNetworkType NetworkType { get; }
        bool IsInitialized { get; }

        void Initialize(BaseAdConfig config, List<string> testDeviceIds, bool targetAdsForChildren = false, Action<bool> onInitialized = null);

        bool IsBannerSupported { get; }
        void ShowBanner(BannerPosition position, string adUnitId = null, string placement = null);
        void HideBanner(BannerPosition position);
        void DestroyBanner(BannerPosition position);

        bool IsInterstitialReady { get; }
        void LoadInterstitial(string adUnitId = null, string placement = null);
        void ShowInterstitial(Action onClosed = null);
        void ShowInterstitial(Action onOpened, Action onClosed);
        void ShowInterstitial(string adUnitId, string placement, Action onClosed = null);
        void ShowInterstitial(string adUnitId, string placement, Action onOpened, Action onClosed);

        bool IsRewardedReady { get; }
        void LoadRewarded(string adUnitId = null, string placement = null);
        void ShowRewarded(Action onRewardEarned, Action onClosed = null);
        void ShowRewarded(Action onRewardEarned, Action onOpened, Action onClosed);
        void ShowRewarded(string adUnitId, string placement, Action onRewardEarned, Action onClosed = null);
        void ShowRewarded(string adUnitId, string placement, Action onRewardEarned, Action onOpened, Action onClosed);
        void ShowRewarded(string adUnitId, string placement, Action<AdReward> onRewardEarned, Action onClosed = null);
        void ShowRewarded(string adUnitId, string placement, Action<AdReward> onRewardEarned, Action onOpened, Action onClosed);

        event Action<IAdsType> OnAdLoaded;
        event Action<IAdsType, string> OnAdFailedToLoad;
        event Action<IAdsType> OnAdShown;
        event Action<IAdsType> OnAdClosed;
        event Action OnRewardEarned;
        event Action<AdReward> OnRewardEarnedWithReward;
    }
}
