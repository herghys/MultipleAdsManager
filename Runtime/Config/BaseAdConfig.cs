using System;
using UnityEngine;

namespace Herghys.MultipleAdsManager.Runtime
{
    public abstract class BaseAdConfig : ScriptableObject
    {
        [Header("Provider Settings")]
        [SerializeField] protected string networkName = "AdSource";
        [SerializeField] protected AdNetworkType networkType = AdNetworkType.GoogleAds;
        [SerializeField] protected bool enabled = true;

        [Header("App Information")]
        [SerializeField] protected string appName = "Default Apps";
        [SerializeField] protected string appStoreDetails = "com.DefaultCompany.DefaultApps";

        [Header("Platform Application IDs / SDK Keys")]
        [Tooltip("Application ID or SDK Key for Android.")]
        [SerializeField] protected string androidAppId = "";

        [Tooltip("Application ID or SDK Key for iOS.")]
        [SerializeField] protected string iosAppId = "";

        [Header("Mediation Weights (0 - 100)")]
        [Range(0, 100)] [SerializeField] protected int bannerWeight = 50;
        [Range(0, 100)] [SerializeField] protected int interstitialWeight = 50;
        [Range(0, 100)] [SerializeField] protected int rewardedWeight = 50;

        [Header("Test Ad IDs (Development & QA)")]
        [Tooltip("When enabled, requests for this provider will use test ad IDs instead of production ad IDs.")]
        [SerializeField] protected bool useTestAdIds = false;

        [Tooltip("Test Ad IDs configured per ad type.")]
        [SerializeField] protected System.Collections.Generic.List<TestAdIdEntry> testAdIds = new System.Collections.Generic.List<TestAdIdEntry>();

        protected virtual void OnEnable()
        {
            EnsureDefaultTestAdIds(false);
        }

        public string NetworkName { get => networkName; set => networkName = value; }
        public AdNetworkType NetworkType { get => networkType; set => networkType = value; }
        public bool Enabled { get => enabled; set => enabled = value; }

        public string AppName { get => appName; set => appName = value; }
        public string AppStoreDetails { get => appStoreDetails; set => appStoreDetails = value; }

        public bool UseTestAdIds { get => useTestAdIds; set => useTestAdIds = value; }
        public System.Collections.Generic.List<TestAdIdEntry> TestAdIds => testAdIds;

        public virtual bool IsTestAdIdActive(IAdsType type)
        {
            if (testAdIds == null) return false;
            var entry = testAdIds.Find(t => t != null && t.AdType == type);
            return entry != null && entry.Enabled && entry.HasValidTestId;
        }

        public virtual string GetTestAdId(IAdsType type)
        {
            if (testAdIds == null) return null;
            var entry = testAdIds.Find(t => t != null && t.AdType == type);
            if (entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.TestAdId))
            {
                return entry.TestAdId;
            }
            return null;
        }

        public string GetTestAdId(IAdsType type, bool isIos)
        {
            if (testAdIds == null) return null;
            var entry = testAdIds.Find(t => t != null && t.AdType == type);
            if (entry != null && entry.Enabled)
            {
                string id = isIos ? entry.IosTestAdId : entry.AndroidTestAdId;
                if (!string.IsNullOrWhiteSpace(id)) return id;
                return entry.TestAdId;
            }
            return null;
        }

        public virtual void EnsureDefaultTestAdIds(bool overwrite = false)
        {
            if (testAdIds == null) testAdIds = new System.Collections.Generic.List<TestAdIdEntry>();

            IAdsType[] allTypes = (IAdsType[])Enum.GetValues(typeof(IAdsType));
            foreach (var type in allTypes)
            {
                var existing = testAdIds.Find(t => t != null && t.AdType == type);
                if (existing == null)
                {
                    GetDefaultTestAdIdForType(type, out string androidId, out string iosId);
                    testAdIds.Add(new TestAdIdEntry(type, androidId, iosId, true));
                }
                else if (overwrite)
                {
                    GetDefaultTestAdIdForType(type, out string androidId, out string iosId);
                    existing.AndroidTestAdId = androidId;
                    existing.IosTestAdId = iosId;
                }
            }
        }

        public virtual void GetDefaultTestAdIdForType(IAdsType type, out string androidId, out string iosId)
        {
            switch (networkType)
            {
                case AdNetworkType.GoogleAds:
                    switch (type)
                    {
                        case IAdsType.Banner:
                            androidId = "ca-app-pub-3940256099942544/6300978111";
                            iosId = "ca-app-pub-3940256099942544/2934735716";
                            break;
                        case IAdsType.Interstitial:
                            androidId = "ca-app-pub-3940256099942544/1033173712";
                            iosId = "ca-app-pub-3940256099942544/4411468910";
                            break;
                        case IAdsType.Rewarded:
                            androidId = "ca-app-pub-3940256099942544/5224354917";
                            iosId = "ca-app-pub-3940256099942544/1712485313";
                            break;
                        case IAdsType.AppOpen:
                            androidId = "ca-app-pub-3940256099942544/9257390501";
                            iosId = "ca-app-pub-3940256099942544/5575463023";
                            break;
                        case IAdsType.RewardedInterstitial:
                            androidId = "ca-app-pub-3940256099942544/5354046379";
                            iosId = "ca-app-pub-3940256099942544/6978759866";
                            break;
                        default:
                            androidId = "ca-app-pub-3940256099942544/6300978111";
                            iosId = "ca-app-pub-3940256099942544/2934735716";
                            break;
                    }
                    break;

                case AdNetworkType.LevelPlay:
                    switch (type)
                    {
                        case IAdsType.Banner:
                            androidId = "DefaultBanner";
                            iosId = "DefaultBanner";
                            break;
                        case IAdsType.Interstitial:
                            androidId = "DefaultInterstitial";
                            iosId = "DefaultInterstitial";
                            break;
                        case IAdsType.Rewarded:
                            androidId = "DefaultRewardedVideo";
                            iosId = "DefaultRewardedVideo";
                            break;
                        default:
                            androidId = $"Default{type}";
                            iosId = $"Default{type}";
                            break;
                    }
                    break;

                case AdNetworkType.AppLovin:
                    androidId = $"max_test_{type.ToString().ToLowerInvariant()}";
                    iosId = androidId;
                    break;

                case AdNetworkType.Vungle:
                    androidId = $"DEFAULT-{type.ToString().ToUpperInvariant()}";
                    iosId = androidId;
                    break;

                default:
                    androidId = AdsTypeEntry.GetDefaultMockId(type);
                    iosId = androidId;
                    break;
            }
        }

        public virtual string AppId
        {
            get
            {
#if UNITY_IOS
                return !string.IsNullOrEmpty(iosAppId) ? iosAppId : androidAppId;
#elif UNITY_ANDROID
                return androidAppId;
#elif UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS
                    ? (!string.IsNullOrEmpty(iosAppId) ? iosAppId : androidAppId)
                    : androidAppId;
#else
                return androidAppId;
#endif
            }
            set => androidAppId = value;
        }

        public string AndroidAppId { get => androidAppId; set => androidAppId = value; }
        public string IosAppId { get => iosAppId; set => iosAppId = value; }

        public int BannerWeight { get => bannerWeight; set => bannerWeight = value; }
        public int InterstitialWeight { get => interstitialWeight; set => interstitialWeight = value; }
        public int RewardedWeight { get => rewardedWeight; set => rewardedWeight = value; }
    }
}
