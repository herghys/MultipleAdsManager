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
                    _instance = FindAnyObjectByType<MultipleAdsManager>();
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

            MultipleAdsConfig.ActiveConfig = config;

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

#if UNITY_EDITOR
            // Mock provider registration:
            // Strictly restricted to Unity Editor when EnableMockInEditor is toggled on. Never runs at runtime or in player builds.
            bool hasMock = registeredProviders.Exists(p => p.NetworkType == AdNetworkType.Mock);
            bool shouldAddMock = !hasMock && (config != null && config.EnableMockInEditor);

            if (shouldAddMock)
            {
                BaseAdConfig mockConfig = config != null ? config.GetConfig(AdNetworkType.Mock) : null;
                if (mockConfig == null)
                {
                    string mockPath = MultipleAdsPaths.GetProviderConfigAssetPath("MockAdsConfig");
                    mockConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseAdConfig>(mockPath);
                    if (mockConfig == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("MockAdsConfig t:ScriptableObject");
                        if (guids != null && guids.Length > 0)
                        {
                            mockConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseAdConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                        }
                    }
                    if (mockConfig == null) mockConfig = Resources.Load<BaseAdConfig>("Config/MockAppConfig");
                    if (mockConfig == null) mockConfig = Resources.Load<BaseAdConfig>("Config/MockAdsConfig");
                    if (mockConfig == null) mockConfig = Resources.Load<BaseAdConfig>("MockAdsConfig");
                }

                IAdProvider mockProvider = new Herghys.MultipleAdsManager.Mock.MockAdsProvider();
                mockProvider.Initialize(mockConfig, config != null ? config.TestDeviceIds : null, config != null && config.TargetAdsForChildren);
                registeredProviders.Add(mockProvider);
                activeProviderNames.Add(registeredProviders.Count == 1 ? "MockAds (Fallback Simulator)" : "MockAds (Simulator)");
            }
#endif

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
#if !UNITY_EDITOR
            if (adConfig.NetworkType == AdNetworkType.Mock) return null;
#endif

            if (ProviderTypeCache.TryGetValue(adConfig.NetworkType, out Type cachedType) && cachedType != null)
            {
                try
                {
                    return (IAdProvider)Activator.CreateInstance(cachedType);
                }
                catch { }
            }

#if UNITY_EDITOR
            var derivedTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IAdProvider>();
            foreach (var t in derivedTypes)
            {
                if (!t.IsInterface && !t.IsAbstract)
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
#else
#pragma warning disable UAC0005
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
#pragma warning restore UAC0005
#endif

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
                string adId = GetAdIdForProvider(provider, IAdsType.Banner, placement);
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
#if UNITY_EDITOR
            if (provider == null)
            {
                // Fallback to Mock provider in Unity Editor only
                provider = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            }
#endif

            if (provider == null)
            {
                Debug.LogWarning($"[MultipleAdsManager] No banner provider available for position '{position}' (Placement: '{placement}').");
                return;
            }

            string adId = GetAdIdForProvider(provider, IAdsType.Banner, placement);

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

        public void ShowAllBanners()
        {
            ShowBanner(BannerPosition.Top);
            ShowBanner(BannerPosition.Bottom);
        }

        public void ShowTopBanner(string placement = null) => ShowBanner(BannerPosition.Top, placement);
        public void ShowBottomBanner(string placement = null) => ShowBanner(BannerPosition.Bottom, placement);
        public void HideTopBanner() => HideBanner(BannerPosition.Top);
        public void HideBottomBanner() => HideBanner(BannerPosition.Bottom);

        public bool AreBannersVisible => IsBannerVisible(BannerPosition.Top) || IsBannerVisible(BannerPosition.Bottom);

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

#if UNITY_EDITOR
            // Fallback: Mock provider is only available in Unity Editor
            var mock = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            return mock != null && mock.IsInitialized && mock.IsInterstitialReady;
#else
            return false;
#endif
        }

        public void ShowInterstitial(Action onClosed) => ShowInterstitial(null, null, onClosed);
        public void ShowInterstitial(Action onOpened, Action onClosed) => ShowInterstitial(null, onOpened, onClosed);
        public void ShowInterstitial(string placement, Action onClosed) => ShowInterstitial(placement, null, onClosed);

        public void ShowInterstitial(string placement = null, Action onOpened = null, Action onClosed = null)
        {
            placement = ResolvePlacement(IAdsType.Interstitial, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Interstitial, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Interstitial);

            foreach (var (provider, adId) in waterfall)
            {
                if (provider != null && provider.IsInitialized && provider.IsInterstitialReady)
                {
                    NotifyFullScreenOpened();

                    provider.ShowInterstitial(
                        adUnitId: adId,
                        placement: placement,
                        onOpened: () =>
                        {
                            onOpened?.Invoke();
                        },
                        onClosed: () =>
                        {
                            NotifyFullScreenClosed();
                            onClosed?.Invoke();
                        });
                    return;
                }
            }

#if UNITY_EDITOR
            // Fallback: If no real SDK ad was ready, but Mock provider is available, use Mock in Editor only
            var mockProvider = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            if (mockProvider != null && mockProvider.IsInitialized)
            {
                NotifyFullScreenOpened();
                mockProvider.ShowInterstitial(
                    adUnitId: AdsTypeEntry.GetDefaultMockId(IAdsType.Interstitial),
                    placement: placement,
                    onOpened: () =>
                    {
                        onOpened?.Invoke();
                    },
                    onClosed: () =>
                    {
                        NotifyFullScreenClosed();
                        onClosed?.Invoke();
                    });
                return;
            }
#endif

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

#if UNITY_EDITOR
            // Fallback: Mock provider is only available in Unity Editor
            var mock = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            return mock != null && mock.IsInitialized && mock.IsRewardedReady;
#else
            return false;
#endif
        }

        public void ShowRewarded(Action onRewardEarned, Action onClosed = null)
        {
            ShowRewarded(null, onRewardEarned, null, onClosed);
        }

        public void ShowRewarded(Action onRewardEarned, Action onOpened, Action onClosed)
        {
            ShowRewarded(null, onRewardEarned, onOpened, onClosed);
        }

        public void ShowRewarded(string placement, Action onRewardEarned, Action onClosed)
        {
            ShowRewarded(placement, onRewardEarned, null, onClosed);
        }

        public void ShowRewarded(string placement = null, Action onRewardEarned = null, Action onOpened = null, Action onClosed = null)
        {
            ShowRewarded(
                placement: placement,
                onRewardEarned: onRewardEarned != null ? (Action<AdReward>)((reward) => onRewardEarned.Invoke()) : null,
                onOpened: onOpened,
                onClosed: onClosed);
        }

        public void ShowRewarded(Action<AdReward> onRewardEarned, Action onClosed = null)
        {
            ShowRewarded(null, onRewardEarned, null, onClosed);
        }

        public void ShowRewarded(Action<AdReward> onRewardEarned, Action onOpened, Action onClosed)
        {
            ShowRewarded(null, onRewardEarned, onOpened, onClosed);
        }

        public void ShowRewarded(string placement, Action<AdReward> onRewardEarned)
        {
            ShowRewarded(placement, onRewardEarned, null, null);
        }

        public void ShowRewarded(string placement, Action<AdReward> onRewardEarned, Action onClosed)
        {
            ShowRewarded(placement, onRewardEarned, null, onClosed);
        }

        public void ShowRewarded(string placement, Action<AdReward> onRewardEarned, Action onOpened, Action onClosed)
        {
            placement = ResolvePlacement(IAdsType.Rewarded, placement);
            var entry = config != null ? config.GetAdEntry(IAdsType.Rewarded, placement) : null;
            var waterfall = GetWeightedWaterfallCandidates(entry, IAdsType.Rewarded);

            foreach (var (provider, adId) in waterfall)
            {
                if (provider != null && provider.IsInitialized && provider.IsRewardedReady)
                {
                    NotifyFullScreenOpened();

                    provider.ShowRewarded(
                        adUnitId: adId,
                        placement: placement,
                        onRewardEarned: (reward) =>
                        {
                            onRewardEarned?.Invoke(reward);
                        },
                        onOpened: () =>
                        {
                            onOpened?.Invoke();
                        },
                        onClosed: () =>
                        {
                            NotifyFullScreenClosed();
                            onClosed?.Invoke();
                        });
                    return;
                }
            }

#if UNITY_EDITOR
            // Fallback: If no real SDK ad was ready, but Mock provider is available, use Mock in Editor only
            var mockProvider = registeredProviders.Find(p => p.NetworkType == AdNetworkType.Mock);
            if (mockProvider != null && mockProvider.IsInitialized)
            {
                NotifyFullScreenOpened();
                mockProvider.ShowRewarded(
                    adUnitId: AdsTypeEntry.GetDefaultMockId(IAdsType.Rewarded),
                    placement: placement,
                    onRewardEarned: (reward) =>
                    {
                        onRewardEarned?.Invoke(reward);
                    },
                    onOpened: () =>
                    {
                        onOpened?.Invoke();
                    },
                    onClosed: () =>
                    {
                        NotifyFullScreenClosed();
                        onClosed?.Invoke();
                    });
                return;
            }
#endif

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
#if !UNITY_EDITOR
                if (provider.NetworkType == AdNetworkType.Mock) continue;
#endif
                if (adType == IAdsType.Banner && !provider.IsBannerSupported) continue;

                string adId = GetAdIdForProvider(provider, adType, entry?.AdPlacement ?? entry?.AdName);

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

        private string GetAdIdForProvider(IAdProvider provider, IAdsType adType, string placement)
        {
            if (provider == null) return null;

#if UNITY_EDITOR
            if (provider.NetworkType == AdNetworkType.Mock)
            {
                return AdsTypeEntry.GetDefaultMockId(adType);
            }
#else
            if (provider.NetworkType == AdNetworkType.Mock)
            {
                return null;
            }
#endif

            var entry = config != null ? config.GetAdEntry(adType, placement) : null;
            var providerConfig = config != null ? config.GetConfig(provider.NetworkType) : null;

            if (entry != null)
            {
                return entry.GetAdIdForProvider(provider.NetworkType, providerConfig);
            }

            return null;
        }

        #endregion
    }
}