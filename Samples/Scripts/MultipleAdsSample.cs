using System;
using UnityEngine;
using Herghys.MultipleAdsManager;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Samples
{
    public class MultipleAdsSample : MonoBehaviour
    {
        [Header("Banner Options")]
        [SerializeField] private BannerPosition defaultBannerPosition = BannerPosition.Bottom;
        [SerializeField] private string bannerPlacement = "Banner";

        [Header("Fullscreen Ad Placements")]
        [SerializeField] private string interstitialPlacement = "Interstitial";
        [SerializeField] private string rewardedPlacement = "Rewarded";

        [Header("Runtime Status")]
        [SerializeField] private string lastEventLog = "Waiting for events...";
        [SerializeField] private int rewardedPoints = 0;

        private void Awake()
        {
            // Ensure MultipleAdsManager is initialized early
            var manager = MultipleAdsManager.Instance;
            if (manager != null && !manager.IsInitialized)
            {
                manager.Initialize();
            }
        }

        private void Start()
        {
            // Subscribe to global ad events from MultipleAdsManager
            if (MultipleAdsManager.Instance != null)
            {
                MultipleAdsManager.Instance.OnAdLoaded += HandleAdLoaded;
                MultipleAdsManager.Instance.OnAdFailedToLoad += HandleAdFailedToLoad;
                MultipleAdsManager.Instance.OnAdShown += HandleAdShown;
                MultipleAdsManager.Instance.OnAdClosed += HandleAdClosed;
                MultipleAdsManager.Instance.OnRewardEarned += HandleRewardEarned;
                MultipleAdsManager.Instance.OnRewardEarnedWithReward += HandleRewardWithItem;
                MultipleAdsManager.Instance.OnFullScreenAdOpened += HandleFullScreenOpened;
                MultipleAdsManager.Instance.OnFullScreenAdClosed += HandleFullScreenClosed;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe when destroyed to avoid memory leaks
            if (MultipleAdsManager.Instance != null)
            {
                MultipleAdsManager.Instance.OnAdLoaded -= HandleAdLoaded;
                MultipleAdsManager.Instance.OnAdFailedToLoad -= HandleAdFailedToLoad;
                MultipleAdsManager.Instance.OnAdShown -= HandleAdShown;
                MultipleAdsManager.Instance.OnAdClosed -= HandleAdClosed;
                MultipleAdsManager.Instance.OnRewardEarned -= HandleRewardEarned;
                MultipleAdsManager.Instance.OnRewardEarnedWithReward -= HandleRewardWithItem;
                MultipleAdsManager.Instance.OnFullScreenAdOpened -= HandleFullScreenOpened;
                MultipleAdsManager.Instance.OnFullScreenAdClosed -= HandleFullScreenClosed;
            }
        }

        #region Public Ad Methods

        public void ShowBannerBottom()
        {
            if (MultipleAdsManager.Instance == null) return;
            LogEvent("Showing Bottom Banner");
            MultipleAdsManager.Instance.ShowBanner(BannerPosition.Bottom, bannerPlacement);
        }

        public void ShowBannerTop()
        {
            if (MultipleAdsManager.Instance == null) return;
            LogEvent("Showing Top Banner");
            MultipleAdsManager.Instance.ShowBanner(BannerPosition.Top, bannerPlacement);
        }

        public void HideBanner()
        {
            if (MultipleAdsManager.Instance == null) return;
            LogEvent($"Hiding Banner ({defaultBannerPosition})");
            MultipleAdsManager.Instance.HideBanner(defaultBannerPosition);
        }

        public void DestroyBanner()
        {
            if (MultipleAdsManager.Instance == null) return;
            LogEvent($"Destroying Banner ({defaultBannerPosition})");
            MultipleAdsManager.Instance.DestroyBanner(defaultBannerPosition);
        }

        public void LoadInterstitial()
        {
            if (MultipleAdsManager.Instance == null) return;
            LogEvent($"Loading Interstitial ({interstitialPlacement})");
            MultipleAdsManager.Instance.LoadInterstitial(interstitialPlacement);
        }

        public void ShowInterstitial()
        {
            if (MultipleAdsManager.Instance == null) return;

            LogEvent($"Requesting to Show Interstitial ({interstitialPlacement})");
            MultipleAdsManager.Instance.ShowInterstitial(interstitialPlacement, () =>
            {
                LogEvent("Interstitial ad was closed by user.");
            });
        }

        public void LoadRewarded()
        {
            if (MultipleAdsManager.Instance == null) return;
            LogEvent($"Loading Rewarded ({rewardedPlacement})");
            MultipleAdsManager.Instance.LoadRewarded(rewardedPlacement);
        }

        public void ShowRewarded()
        {
            if (MultipleAdsManager.Instance == null) return;

            LogEvent($"Requesting to Show Rewarded ({rewardedPlacement})");
            MultipleAdsManager.Instance.ShowRewarded(
                rewardedPlacement,
                onRewardEarned: () =>
                {
                    rewardedPoints += 50;
                    LogEvent($"Reward Earned! +50 points (Total: {rewardedPoints})");
                },
                onClosed: () =>
                {
                    LogEvent("Rewarded ad dialog closed.");
                }
            );
        }

        #endregion

        #region Event Callbacks

        private void HandleAdLoaded(IAdsType adType)
        {
            LogEvent($"Ad Loaded: {adType}");
        }

        private void HandleAdFailedToLoad(IAdsType adType, string error)
        {
            LogEvent($"Ad Failed to Load [{adType}]: {error}");
        }

        private void HandleAdShown(IAdsType adType)
        {
            LogEvent($"Ad Shown: {adType}");
        }

        private void HandleAdClosed(IAdsType adType)
        {
            LogEvent($"Ad Closed: {adType}");
        }

        private void HandleRewardEarned()
        {
            LogEvent("OnRewardEarned event triggered.");
        }

        private void HandleRewardWithItem(AdReward reward)
        {
            LogEvent($"Reward Received: {reward.ItemName} x{reward.Amount}");
        }

        private void HandleFullScreenOpened()
        {
            LogEvent("FullScreen Ad Opened (Game should pause / mute audio).");
        }

        private void HandleFullScreenClosed()
        {
            LogEvent("FullScreen Ad Closed (Game can resume).");
        }

        private void LogEvent(string message)
        {
            lastEventLog = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Debug.Log($"[MultipleAdsSample] {message}");
        }

        #endregion

        #region GUI For Quick Testing

        private void OnGUI()
        {
            int btnW = 210;
            int btnH = 34;
            int margin = 16;
            int y = margin;

            GUI.Box(new Rect(margin - 4, margin - 4, btnW + 16, 510), "MultipleAds Test Controls");
            y += 24;

            bool isManagerReady = MultipleAdsManager.Instance != null && MultipleAdsManager.Instance.IsInitialized;
            string statusStr = isManagerReady ? "<color=green>Ready</color>" : "<color=yellow>Initializing...</color>";
            GUI.Label(new Rect(margin, y, btnW, 20), $"Status: {statusStr}");
            y += 20;

            if (GUI.Button(new Rect(margin, y, btnW, btnH), "Show Bottom Banner"))
            {
                ShowBannerBottom();
            }
            y += btnH + 4;

            if (GUI.Button(new Rect(margin, y, btnW, btnH), "Show Top Banner"))
            {
                ShowBannerTop();
            }
            y += btnH + 4;

            if (GUI.Button(new Rect(margin, y, btnW, btnH), "Hide Banner"))
            {
                HideBanner();
            }
            y += btnH + 4;

            if (GUI.Button(new Rect(margin, y, btnW, btnH), "Destroy Banner"))
            {
                DestroyBanner();
            }
            y += btnH + 8;

            bool isInterReady = MultipleAdsManager.Instance != null && MultipleAdsManager.Instance.IsInterstitialReady(interstitialPlacement);
            if (GUI.Button(new Rect(margin, y, btnW, btnH), isInterReady ? "Show Interstitial (Ready)" : "Load & Show Interstitial"))
            {
                ShowInterstitial();
            }
            y += btnH + 4;

            if (GUI.Button(new Rect(margin, y, btnW, btnH), "Preload Interstitial"))
            {
                LoadInterstitial();
            }
            y += btnH + 8;

            bool isRewardReady = MultipleAdsManager.Instance != null && MultipleAdsManager.Instance.IsRewardedReady(rewardedPlacement);
            if (GUI.Button(new Rect(margin, y, btnW, btnH), isRewardReady ? "Show Rewarded (Ready)" : "Load & Show Rewarded"))
            {
                ShowRewarded();
            }
            y += btnH + 4;

            if (GUI.Button(new Rect(margin, y, btnW, btnH), "Preload Rewarded"))
            {
                LoadRewarded();
            }
            y += btnH + 12;

            // Points & Status
            GUI.Label(new Rect(margin, y, btnW, 20), $"Reward Points: {rewardedPoints}");
            y += 22;

            GUI.Label(new Rect(margin, y, 500, 32), lastEventLog);
        }

        #endregion
    }
}
