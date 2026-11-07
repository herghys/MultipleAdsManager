using System;
using System.Collections.Generic;
using UnityEngine;

namespace Herghys.MultipleAdsManager.Runtime
{
    [Serializable]
    public class ProviderAdIdEntry
    {
        [SerializeField] private AdNetworkType networkType = AdNetworkType.GoogleAds;
        [SerializeField] private string androidAdId = "";
        [SerializeField] private string iosAdId = "";
        [SerializeField] private string adId = ""; // fallback / backwards compatibility
        [SerializeField] private bool useTestAdId = false;

        public ProviderAdIdEntry() { }

        public ProviderAdIdEntry(AdNetworkType networkType, string androidAdId, string iosAdId = "", bool useTestAdId = false)
        {
            this.networkType = networkType;
            this.androidAdId = androidAdId;
            this.iosAdId = iosAdId;
            this.adId = androidAdId;
            this.useTestAdId = useTestAdId;
        }

        public AdNetworkType NetworkType
        {
            get => networkType;
            set => networkType = value;
        }

        public bool UseTestAdId
        {
            get => useTestAdId;
            set => useTestAdId = value;
        }

        public string AndroidAdId
        {
            get => !string.IsNullOrEmpty(androidAdId) ? androidAdId : adId;
            set
            {
                androidAdId = value;
                adId = value;
            }
        }

        public string IosAdId
        {
            get => iosAdId;
            set => iosAdId = value;
        }

        public string AdId
        {
            get
            {
#if UNITY_IOS
                return !string.IsNullOrEmpty(IosAdId) ? IosAdId : AndroidAdId;
#elif UNITY_ANDROID
                return AndroidAdId;
#elif UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS
                    ? (!string.IsNullOrEmpty(IosAdId) ? IosAdId : AndroidAdId)
                    : AndroidAdId;
#else
                return AndroidAdId;
#endif
            }
            set => AndroidAdId = value;
        }

        public bool HasValidId
        {
            get
            {
#if UNITY_EDITOR
                if (networkType == AdNetworkType.Mock || useTestAdId) return true;
#else
                if (networkType == AdNetworkType.Mock) return false;
#endif
                return !string.IsNullOrWhiteSpace(AndroidAdId) || !string.IsNullOrWhiteSpace(IosAdId);
            }
        }

        public bool HasValidIdForPlatform(bool isIos)
        {
#if UNITY_EDITOR
            if (networkType == AdNetworkType.Mock || useTestAdId) return true;
#else
            if (networkType == AdNetworkType.Mock) return false;
#endif
            return isIos ? !string.IsNullOrWhiteSpace(IosAdId) : !string.IsNullOrWhiteSpace(AndroidAdId);
        }

        public override string ToString()
        {
            return $"{networkType}: [UseTest: {useTestAdId}, Android: '{AndroidAdId}', iOS: '{IosAdId}']";
        }
    }

    [Serializable]
    public class AdsTypeEntry
    {
        [SerializeField] private string adName = "New Ad Entry";
        [SerializeField] private IAdsType adType = IAdsType.Banner;
        [SerializeField] private string adPlacement = "";
        [SerializeField] private string rewardItemName = "Reward";
        [SerializeField] private int rewardAmount = 1;
        [SerializeField] private List<ProviderAdIdEntry> providerAdIds = new List<ProviderAdIdEntry>();

        public AdsTypeEntry() { }

        public AdsTypeEntry(string adName, IAdsType adType, string adPlacement, string rewardItemName = "Reward", int rewardAmount = 1)
        {
            this.adName = adName;
            this.adType = adType;
            this.adPlacement = adPlacement;
            this.rewardItemName = rewardItemName;
            this.rewardAmount = rewardAmount;
        }

        public string AdName
        {
            get => adName;
            set => adName = value;
        }

        public IAdsType AdType
        {
            get => adType;
            set => adType = value;
        }

        public string AdPlacement
        {
            get => adPlacement;
            set => adPlacement = value;
        }

        public string RewardItemName
        {
            get => !string.IsNullOrEmpty(rewardItemName) ? rewardItemName : "Reward";
            set => rewardItemName = value;
        }

        public int RewardAmount
        {
            get => rewardAmount > 0 ? rewardAmount : 1;
            set => rewardAmount = value;
        }

        public List<ProviderAdIdEntry> ProviderAdIds => providerAdIds;

        public static string GetDefaultMockId(IAdsType type)
        {
            switch (type)
            {
                case IAdsType.Banner: return "mock_banner";
                case IAdsType.Interstitial: return "mock_interstitial";
                case IAdsType.Rewarded: return "mock_rewarded";
                case IAdsType.AppOpen: return "mock_app_open";
                case IAdsType.RewardedInterstitial: return "mock_rewarded_interstitial";
                default: return $"mock_{type.ToString().ToLowerInvariant()}";
            }
        }

        public ProviderAdIdEntry GetProviderAdIdEntry(AdNetworkType network)
        {
            if (providerAdIds == null) providerAdIds = new List<ProviderAdIdEntry>();
            var match = providerAdIds.Find(p => p != null && p.NetworkType == network);
            if (network == AdNetworkType.Mock)
            {
                string mockId = GetDefaultMockId(adType);
                if (match == null)
                {
                    match = new ProviderAdIdEntry(AdNetworkType.Mock, mockId, mockId, false);
                    providerAdIds.Add(match);
                }
                else
                {
                    match.UseTestAdId = false;
                    match.AndroidAdId = mockId;
                    match.IosAdId = mockId;
                }
            }
            return match;
        }

        public string GetAdIdForProvider(AdNetworkType network, BaseAdConfig providerConfig = null)
        {
#if UNITY_EDITOR
            if (network == AdNetworkType.Mock)
            {
                return GetDefaultMockId(adType);
            }
#else
            if (network == AdNetworkType.Mock)
            {
                return null;
            }
#endif

            if (providerAdIds == null) return null;
            var entry = providerAdIds.Find(p => p != null && p.NetworkType == network);
            if (entry == null) return null;

#if UNITY_EDITOR
            bool useTest = entry.UseTestAdId;
            if (MultipleAdsConfig.ActiveConfig != null && !MultipleAdsConfig.ActiveConfig.UseTestAdsOnEditor)
            {
                useTest = false;
            }

            if (useTest && providerConfig != null)
            {
                bool isIos = UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS;
                string testId = providerConfig.GetTestAdId(adType, isIos);
                if (!string.IsNullOrWhiteSpace(testId)) return testId;
            }
#endif

            return entry.HasValidId ? entry.AdId : null;
        }

        public string GetAdIdForProvider(AdNetworkType network, bool isIos, BaseAdConfig providerConfig = null)
        {
#if UNITY_EDITOR
            if (network == AdNetworkType.Mock)
            {
                return GetDefaultMockId(adType);
            }
#else
            if (network == AdNetworkType.Mock)
            {
                return null;
            }
#endif

            if (providerAdIds == null) return null;
            var entry = providerAdIds.Find(p => p != null && p.NetworkType == network);
            if (entry == null) return null;

#if UNITY_EDITOR
            bool useTest = entry.UseTestAdId;
            if (MultipleAdsConfig.ActiveConfig != null && !MultipleAdsConfig.ActiveConfig.UseTestAdsOnEditor)
            {
                useTest = false;
            }

            if (useTest && providerConfig != null)
            {
                string testId = providerConfig.GetTestAdId(adType, isIos);
                if (!string.IsNullOrWhiteSpace(testId)) return testId;
            }
#endif

            string id = isIos ? entry.IosAdId : entry.AndroidAdId;
            return !string.IsNullOrWhiteSpace(id) ? id : null;
        }

        public void SetAdIdForProvider(AdNetworkType network, string androidAdId, string iosAdId = "")
        {
            if (providerAdIds == null) providerAdIds = new List<ProviderAdIdEntry>();
            var entry = providerAdIds.Find(p => p != null && p.NetworkType == network);
            if (entry != null)
            {
                entry.AndroidAdId = androidAdId;
                entry.IosAdId = iosAdId;
            }
            else
            {
                providerAdIds.Add(new ProviderAdIdEntry(network, androidAdId, iosAdId));
            }
        }

        public void SetProviderEntry(AdNetworkType network, string androidAdId, string iosAdId, bool useTestId)
        {
            if (providerAdIds == null) providerAdIds = new List<ProviderAdIdEntry>();
            var entry = providerAdIds.Find(p => p != null && p.NetworkType == network);
            if (entry != null)
            {
                entry.AndroidAdId = androidAdId;
                entry.IosAdId = iosAdId;
                entry.UseTestAdId = useTestId;
            }
            else
            {
                providerAdIds.Add(new ProviderAdIdEntry(network, androidAdId, iosAdId, useTestId));
            }
        }

        public void RemoveProvider(AdNetworkType network)
        {
            if (providerAdIds != null)
            {
                providerAdIds.RemoveAll(p => p != null && p.NetworkType == network);
            }
        }

        public bool HasProvider(AdNetworkType network)
        {
            return !string.IsNullOrWhiteSpace(GetAdIdForProvider(network));
        }

        public bool HasProviderForPlatform(AdNetworkType network, bool isIos)
        {
            return !string.IsNullOrWhiteSpace(GetAdIdForProvider(network, isIos));
        }

        public override string ToString()
        {
            return $"[{adType}] {adName} (Placement: {adPlacement}, Providers: {providerAdIds?.Count ?? 0})";
        }
    }
}