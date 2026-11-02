using System;
using System.Collections.Generic;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MultipleAds/MultipleAdsManager")]
    public class MultipleAdsManager : MonoBehaviour
    {
        private static MultipleAdsManager _instance;
        public static MultipleAdsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MultipleAdsManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("MultipleAdsManager");
                        _instance = go.AddComponent<MultipleAdsManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
            ProviderTypeCache.Clear();
        }

        [SerializeField] private MultipleAdsConfig config;
        [SerializeField] private bool autoInitialize = true;

        [Header("Runtime Status (Read-Only)")]
        [SerializeField] private List<string> activeProviderNames = new List<string>();

        private readonly List<IAdProvider> registeredProviders = new List<IAdProvider>();
        private readonly Dictionary<BannerPosition, IAdProvider> activeBanners = new Dictionary<BannerPosition, IAdProvider>();
        private readonly Dictionary<BannerPosition, bool> wasBannerVisibleBeforeFullScreen = new Dictionary<BannerPosition, bool>();

        private static readonly Dictionary<AdNetworkType, Type> ProviderTypeCache = new Dictionary<AdNetworkType, Type>();

        public MultipleAdsConfig Config => config;
        public bool IsInitialized { get; private set; }
        public bool IsFullScreenAdShowing { get; private set; }

        public event Action OnFullScreenAdOpened;
        public event Action OnFullScreenAdClosed;

        public event Action<IAdsType> OnAdLoaded;
        public event Action<IAdsType, string> OnAdFailedToLoad;
        public event Action<IAdsType> OnAdShown;
        public event Action<IAdsType> OnAdClosed;
        public event Action OnRewardEarned;
        public event Action<AdReward> OnRewardEarnedWithReward;

        public void Configure(MultipleAdsConfig cfg)
        {
            config = cfg;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (autoInitialize && !IsInitialized)
            {
                Initialize();
            }
        }

        public void Initialize(MultipleAdsConfig customConfig = null)
        {
            if (IsInitialized) return;

            if (customConfig != null)
            {
                config = customConfig;
            }

            registeredProviders.Clear();
            activeProviderNames.Clear();
            activeBanners.Clear();

            if (config == null)
            {
#if UNITY_EDITOR
                string editorPath = MultipleAdsPaths.GetMainConfigAssetPath();
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(editorPath);
                if (config == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MultipleAdsConfig");
                    if (guids.Length > 0)
                    {
                        config = UnityEditor.AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
#endif
                if (config == null)
                {
                    config = Resources.Load<MultipleAdsConfig>(MultipleAdsPaths.MainConfigName);
                    if (config == null)
                        config = Resources.Load<MultipleAdsConfig>(MultipleAdsPaths.LegacyMainConfigName);
                }

                if (config == null)
                {
                    Debug.LogWarning("[MultipleAdsManager] No MultipleAdsManager Config assigned or found! Initializing fallback instance in memory.");
                    config = ScriptableObject.CreateInstance<MultipleAdsConfig>();
                }
            }

            var activeConfigs = config.GetActiveConfigs();
            foreach (var providerConfig in activeConfigs)
            {
                if (providerConfig == null) continue;

                IAdProvider provider = CreateProvider(providerConfig);
                if (provider != null)
                {
                    provider.Initialize(providerConfig, config.TestDeviceIds, config.TargetAdsForChildren);
                    registeredProviders.Add(provider);
                    activeProviderNames.Add(provider.NetworkName);
                }
            }

            // Mock provider registration:
            // 1. Used whenever no real providers are available ("Mock will be used if we don't have anything, can be used anytime")
            // 2. Used in Unity Editor when EnableMockInEditor is toggled on
            bool hasMock = registeredProviders.Exists(p => p.NetworkType == AdNetworkType.Mock);
            bool shouldAddMock = !hasMock && (registeredProviders.Count == 0
#if UNITY_EDITOR
                || (config != null && config.EnableMockInEditor)
#endif
            );

            if (shouldAddMock)
            {
                BaseAdConfig mockConfig = config != null ? config.GetConfig(AdNetworkType.Mock) : null;
                if (mockConfig == null)
                {
                    mockConfig = Resources.Load<BaseAdConfig>("Config/MockAppConfig");
                    if (mockConfig == null) mockConfig = Resources.Load<BaseAdConfig>("Config/MockAdsConfig");
                    if (mockConfig == null) mockConfig = Resources.Load<BaseAdConfig>("MockAdsConfig");
                }

                IAdProvider mockProvider = new Herghys.MultipleAdsManager.Mock.MockAdsProvider();
                mockProvider.Initialize(mockConfig, config != null ? config.TestDeviceIds : null, config != null && config.TargetAdsForChildren);
                registeredProviders.Add(mockProvider);
                activeProviderNames.Add(registeredProviders.Count == 1 ? "MockAds (Fallback Simulator)" : "MockAds (Simulator)");
            }

            foreach (var p in registeredProviders)
            {
                if (p == null) continue;
                p.OnAdLoaded += type => OnAdLoaded?.Invoke(type);
                p.OnAdFailedToLoad += (type, err) => OnAdFailedToLoad?.Invoke(type, err);
                p.OnAdShown += type => OnAdShown?.Invoke(type);
                p.OnAdClosed += type => OnAdClosed?.Invoke(type);
                p.OnRewardEarned += () => OnRewardEarned?.Invoke();
                p.OnRewardEarnedWithReward += r => OnRewardEarnedWithReward?.Invoke(r);
            }

            IsInitialized = true;
            Debug.Log($"[MultipleAdsManager] Initialized successfully with {registeredProviders.Count} active provider(s): {string.Join(", ", activeProviderNames)}");
        }

        private IAdProvider CreateProvider(BaseAdConfig adConfig)
        {
            if (adConfig == null) return null;

            if (ProviderTypeCache.TryGetValue(adConfig.NetworkType, out Type cachedType) && cachedType != null)
            {
                try
                {
                    return (IAdProvider)Activator.CreateInstance(cachedType);
                }
                catch { }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (typeof(IAdProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        {
                            try
                            {
                                var instance = (IAdProvider)Activator.CreateInstance(t);
                                if (instance.NetworkType == adConfig.NetworkType)
                                {
                                    ProviderTypeCache[adConfig.NetworkType] = t;
                                    return instance;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            Debug.LogWarning($"[MultipleAdsManager] No implementation found for provider '{adConfig.NetworkType}'. Ensure its module is added in Assets/MultipleAds/Modules/.");
            return null;
        }

        #region Banners (All Positions)

        public void PrepareBanner(string placement = null, BannerPosition position = BannerPosition.Bottom)
        {
            placement = ResolvePlacement(IAdsType.Banner, placement);
            var provider = GetCandidateProvider(IAdsType.Banner, placement);
            if (provider != null)
            {
                var entry = config != null ? config.GetAdEntry(IAdsType.Banner, placement) : null;
                string adId = null;
                var providerConfig = config != null ? config.GetConfig(provider.NetworkType) : null;
                if (providerConfig != null && providerConfig.IsTestAdIdActive(IAdsType.Banner))
                {
                    adId = providerConfig.GetTestAdId(IAdsType.Banner);
                }
                else if (entry != null)
                {
                    adId = entry.GetAdIdForProvider(provider.NetworkType);
                }
                provider.ShowBanner(position, adId, placement);
            }
        }

        public void ShowBanner(string placement = null, BannerPosition position = BannerPosition.Bottom)
        {
            ShowBanner(position, placement);
        }

        public void ShowBanner(BannerPosition position, string placement = null)
        {
            if (IsFullScreenAdShowing)
            {
                wasBannerVisibleBeforeFullScreen[position] = true;
                return;
            }

            placement = ResolvePlacement(IAdsType.Banner, placement);
            var provider = GetCandidateProvider(IAdsType.Banner, placement);
            if (provider == null)
            {
                // Fallback to Mock provider if available
                provider = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            }

            if (provider == null)
            {
                Debug.LogWarning($"[MultipleAdsManager] No banner provider available for position '{position}' (Placement: '{placement}').");
                return;
            }

            var entry = config != null ? config.GetAdEntry(IAdsType.Banner, placement) : null;
            string adId = null;
            var providerConfig = config != null ? config.GetConfig(provider.NetworkType) : null;
            if (providerConfig != null && providerConfig.IsTestAdIdActive(IAdsType.Banner))
            {
                adId = providerConfig.GetTestAdId(IAdsType.Banner);
            }
            else if (entry != null)
            {
                adId = entry.GetAdIdForProvider(provider.NetworkType);
            }

            if (provider.NetworkType == AdNetworkType.Mock)
            {
                adId = AdsTypeEntry.GetDefaultMockId(IAdsType.Banner);
            }

            foreach (var p in registeredProviders)
            {
                if (p != null && p != provider)
                {
                    p.HideBanner(position);
                    p.DestroyBanner(position);
                }
            }

            activeBanners[position] = provider;
            Debug.Log($"[MultipleAdsManager] Showing {position} Banner via {provider.NetworkName} (ID: {adId}, Placement: {placement})");
            provider.ShowBanner(position, adId, placement);
        }

        public void HideBanner(BannerPosition position = BannerPosition.Bottom)
        {
            if (activeBanners.TryGetValue(position, out var provider) && provider != null)
            {
                provider.HideBanner(position);
            }

            foreach (var p in registeredProviders)
            {
                p?.HideBanner(position);
            }

            wasBannerVisibleBeforeFullScreen[position] = false;
        }

        public void DestroyBanner(BannerPosition position = BannerPosition.Bottom)
        {
            if (activeBanners.TryGetValue(position, out var provider) && provider != null)
            {
                provider.DestroyBanner(position);
                activeBanners.Remove(position);
            }

            foreach (var p in registeredProviders)
            {
                p?.DestroyBanner(position);
            }

            wasBannerVisibleBeforeFullScreen[position] = false;
        }

        public void HideAllBanners()
        {
            foreach (BannerPosition pos in Enum.GetValues(typeof(BannerPosition)))
            {
                HideBanner(pos);
            }
        }

        public void DestroyAllBanners()
        {
            foreach (BannerPosition pos in Enum.GetValues(typeof(BannerPosition)))
            {
                DestroyBanner(pos);
            }
            activeBanners.Clear();
        }

        public bool IsBannerVisible(BannerPosition position)
        {
            return activeBanners.ContainsKey(position);
        }

        #endregion

        #region Interstitials

        public void PrepareInterstitial(string placement = null)
        {
            placement = ResolvePlacement(IAdsType.Interstitial, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Interstitial, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Interstitial);

            foreach (var (provider, adId) in waterfall)
            {
                if (provider != null && provider.IsInitialized && !string.IsNullOrEmpty(adId))
                {
                    provider.LoadInterstitial(adId, placement);
                }
            }
        }

        public void LoadInterstitial(string placement = null) => PrepareInterstitial(placement);

        public bool IsInterstitialReady(string placement = null)
        {
            placement = ResolvePlacement(IAdsType.Interstitial, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Interstitial, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Interstitial);

            foreach (var (provider, _) in waterfall)
            {
                if (provider != null && provider.IsInitialized && provider.IsInterstitialReady)
                {
                    return true;
                }
            }

            // Fallback: Mock provider is always ready
            var mock = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            return mock != null && mock.IsInitialized && mock.IsInterstitialReady;
        }

        public void ShowInterstitial(string placement = null, Action onClosed = null)
        {
            placement = ResolvePlacement(IAdsType.Interstitial, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Interstitial, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Interstitial);

            foreach (var (provider, adId) in waterfall)
            {
                if (provider != null && provider.IsInitialized && provider.IsInterstitialReady)
                {
                    NotifyFullScreenOpened();

                    provider.ShowInterstitial(adId, placement, () =>
                    {
                        NotifyFullScreenClosed();
                        onClosed?.Invoke();
                    });
                    return;
                }
            }

            // Fallback: If no real SDK ad was ready, but Mock provider is available, use Mock!
            var mockProvider = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            if (mockProvider != null && mockProvider.IsInitialized)
            {
                NotifyFullScreenOpened();
                mockProvider.ShowInterstitial(AdsTypeEntry.GetDefaultMockId(IAdsType.Interstitial), placement, () =>
                {
                    NotifyFullScreenClosed();
                    onClosed?.Invoke();
                });
                return;
            }

            Debug.LogWarning($"[MultipleAdsManager] No interstitial ad ready for placement '{placement}'. Requesting new preload.");
            PrepareInterstitial(placement);
            onClosed?.Invoke();
        }

        #endregion

        #region Rewarded Ads

        public void PrepareRewarded(string placement = null)
        {
            placement = ResolvePlacement(IAdsType.Rewarded, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Rewarded, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Rewarded);

            foreach (var (provider, adId) in waterfall)
            {
                if (provider != null && provider.IsInitialized && !string.IsNullOrEmpty(adId))
                {
                    provider.LoadRewarded(adId, placement);
                }
            }
        }

        public void LoadRewarded(string placement = null) => PrepareRewarded(placement);

        public bool IsRewardedReady(string placement = null)
        {
            placement = ResolvePlacement(IAdsType.Rewarded, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Rewarded, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Rewarded);

            foreach (var (provider, _) in waterfall)
            {
                if (provider != null && provider.IsInitialized && provider.IsRewardedReady)
                {
                    return true;
                }
            }

            // Fallback: Mock provider is always ready
            var mock = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            return mock != null && mock.IsInitialized && mock.IsRewardedReady;
        }

        public void ShowRewarded(string placement = null, Action onRewardEarned = null, Action onClosed = null)
        {
            ShowRewarded(placement, (reward) => onRewardEarned?.Invoke(), onClosed);
        }

        public void ShowRewarded(string placement, Action<AdReward> onRewardEarned, Action onClosed = null)
        {
            placement = ResolvePlacement(IAdsType.Rewarded, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Rewarded, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Rewarded);

            foreach (var (provider, adId) in waterfall)
            {
                if (provider != null && provider.IsInitialized && provider.IsRewardedReady)
                {
                    NotifyFullScreenOpened();

                    provider.ShowRewarded(adId, placement, (reward) =>
                    {
                        onRewardEarned?.Invoke(reward);
                    }, () =>
                    {
                        NotifyFullScreenClosed();
                        onClosed?.Invoke();
                    });
                    return;
                }
            }

            // Fallback: If no real SDK ad was ready, but Mock provider is available, use Mock!
            var mockProvider = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            if (mockProvider != null && mockProvider.IsInitialized)
            {
                NotifyFullScreenOpened();
                mockProvider.ShowRewarded(AdsTypeEntry.GetDefaultMockId(IAdsType.Rewarded), placement, (reward) =>
                {
                    onRewardEarned?.Invoke(reward);
                }, () =>
                {
                    NotifyFullScreenClosed();
                    onClosed?.Invoke();
                });
                return;
            }

            Debug.LogWarning($"[MultipleAdsManager] No rewarded ad ready for placement '{placement}'. Requesting new preload.");
            PrepareRewarded(placement);
            onClosed?.Invoke();
        }

        #endregion

        #region Full Screen Ad Lifecycle

        private void NotifyFullScreenOpened()
        {
            IsFullScreenAdShowing = true;
            wasBannerVisibleBeforeFullScreen.Clear();

            foreach (var kvp in activeBanners)
            {
                wasBannerVisibleBeforeFullScreen[kvp.Key] = true;
                kvp.Value?.HideBanner(kvp.Key);
            }

            OnFullScreenAdOpened?.Invoke();
        }

        private void NotifyFullScreenClosed()
        {
            IsFullScreenAdShowing = false;
            OnFullScreenAdClosed?.Invoke();

            foreach (var kvp in wasBannerVisibleBeforeFullScreen)
            {
                if (kvp.Value && activeBanners.TryGetValue(kvp.Key, out var provider) && provider != null)
                {
                    provider.ShowBanner(kvp.Key);
                }
            }
        }

        #endregion

        #region Weighted Waterfall Selection

        private string ResolvePlacement(IAdsType type, string placement)
        {
            if (!string.IsNullOrEmpty(placement)) return placement;
            var entry = config != null ? config.GetAdEntry(type, null) : null;
            if (entry != null && !string.IsNullOrEmpty(entry.AdPlacement))
            {
                return entry.AdPlacement;
            }
            return MultipleAdsConfig.GetDefaultPlacement(type);
        }

        private IAdProvider GetCandidateProvider(IAdsType type, string placement)
        {
            var entry = config != null ? config.GetAdEntry(type, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, type);

            foreach (var (provider, _) in waterfall)
            {
                if (provider != null && provider.IsInitialized)
                {
                    if (type == IAdsType.Banner && !provider.IsBannerSupported) continue;
                    return provider;
                }
            }

            return null;
        }

        private List<(IAdProvider provider, string adId)> GetWeightedWaterfallCandidates(AdsTypeEntry entry, IAdsType adType)
        {
            var list = new List<(IAdProvider, string)>();
            if (registeredProviders == null || registeredProviders.Count == 0) return list;

            var eligible = new List<(IAdProvider provider, string adId, int weight)>();

            foreach (var provider in registeredProviders)
            {
                if (provider == null || !provider.IsInitialized) continue;
                if (adType == IAdsType.Banner && !provider.IsBannerSupported) continue;

                string adId = null;
                var providerConfig = config != null ? config.GetConfig(provider.NetworkType) : null;
                if (providerConfig != null && providerConfig.IsTestAdIdActive(adType))
                {
                    adId = providerConfig.GetTestAdId(adType);
                }
                else if (entry != null)
                {
                    adId = entry.GetAdIdForProvider(provider.NetworkType);
                }

                if (provider.NetworkType == AdNetworkType.Mock)
                {
                    adId = AdsTypeEntry.GetDefaultMockId(adType);
                }

                if (!string.IsNullOrEmpty(adId))
                {
                    int weight = provider.GetWeight(adType);
                    eligible.Add((provider, adId, Mathf.Max(1, weight)));
                }
            }

            while (eligible.Count > 0)
            {
                int totalWeight = 0;
                foreach (var item in eligible) totalWeight += item.weight;

                int roll = UnityEngine.Random.Range(0, totalWeight);
                int cumulative = 0;
                int selectedIndex = 0;

                for (int i = 0; i < eligible.Count; i++)
                {
                    cumulative += eligible[i].weight;
                    if (roll < cumulative)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                list.Add((eligible[selectedIndex].provider, eligible[selectedIndex].adId));
                eligible.RemoveAt(selectedIndex);
            }

            return list;
        }

        #endregion
    }
}
