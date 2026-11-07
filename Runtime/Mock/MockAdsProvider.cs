using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Mock
{
    public class MockAdsProvider : IAdProvider
    {
        public string NetworkName => "MockAds";
        public AdNetworkType NetworkType => AdNetworkType.Mock;
        public bool IsInitialized => isInitialized;

        public int BannerWeight { get; set; } = 10;
        public int InterstitialWeight { get; set; } = 10;
        public int RewardedWeight { get; set; } = 10;

        public bool IsBannerSupported => true;
#if UNITY_EDITOR
        public bool IsInterstitialReady => isInterstitialLoaded;
        public bool IsRewardedReady => isRewardedLoaded;
#else
        public bool IsInterstitialReady => false;
        public bool IsRewardedReady => false;
#endif

        public event Action<IAdsType> OnAdLoaded;
        public event Action<IAdsType, string> OnAdFailedToLoad { add { } remove { } }
        public event Action<IAdsType> OnAdShown;
        public event Action<IAdsType> OnAdClosed;
        public event Action OnRewardEarned;
        public event Action<AdReward> OnRewardEarnedWithReward;

        private bool isInitialized = false;
        private bool isInterstitialLoaded = true;
        private bool isRewardedLoaded = true;
        private readonly Dictionary<BannerPosition, GameObject> activeBannerObjects = new Dictionary<BannerPosition, GameObject>();
        private readonly HashSet<BannerPosition> visibleBanners = new HashSet<BannerPosition>();

        public bool IsBannerVisible(BannerPosition position) => visibleBanners.Contains(position);

        public int GetWeight(IAdsType adType)
        {
            switch (adType)
            {
                case IAdsType.Banner: return BannerWeight;
                case IAdsType.Interstitial: return InterstitialWeight;
                case IAdsType.Rewarded: return RewardedWeight;
                default: return 10;
            }
        }

        public void SetWeight(IAdsType adType, int weight)
        {
            switch (adType)
            {
                case IAdsType.Banner: BannerWeight = weight; break;
                case IAdsType.Interstitial: InterstitialWeight = weight; break;
                case IAdsType.Rewarded: RewardedWeight = weight; break;
            }
        }

        public int GetWeight(AdType adType) => GetWeight((IAdsType)adType);
        public void SetWeight(AdType adType, int weight) => SetWeight((IAdsType)adType, weight);

        public void Initialize(BaseAdConfig config, List<string> testDeviceIds, bool targetAdsForChildren = false, Action<bool> onInitialized = null)
        {
#if !UNITY_EDITOR
            isInitialized = false;
            onInitialized?.Invoke(false);
            return;
#else
            isInitialized = true;
            if (config != null)
            {
                BannerWeight = config.BannerWeight;
                InterstitialWeight = config.InterstitialWeight;
                RewardedWeight = config.RewardedWeight;
            }
            Debug.Log($"[MockAdsProvider] Initialized successfully (TargetAdsForChildren: {targetAdsForChildren}).");
            onInitialized?.Invoke(true);
#endif
        }

        public void ShowBanner(BannerPosition position, string adUnitId = null, string placement = null)
        {
#if !UNITY_EDITOR
            return;
#else
            visibleBanners.Add(position);

            if (activeBannerObjects.TryGetValue(position, out GameObject existing) && existing != null)
            {
                existing.SetActive(true);
            }
            else
            {
                GameObject bannerObj = SpawnMockPrefab("MockBannerAd", position);
                if (bannerObj != null)
                {
                    activeBannerObjects[position] = bannerObj;
                    var view = bannerObj.GetComponent<MockAdView>();
                    if (view != null)
                    {
                        view.SetupBanner(position, () => HideBanner(position));
                    }
                }
            }

            Debug.Log($"[MockAdsProvider] Banner Shown ({position}, ID: {adUnitId}, Placement: {placement}).");
            OnAdShown?.Invoke(IAdsType.Banner);
#endif
        }

        public void HideBanner(BannerPosition position)
        {
            visibleBanners.Remove(position);
            if (activeBannerObjects.TryGetValue(position, out GameObject bannerObj) && bannerObj != null)
            {
                bannerObj.SetActive(false);
            }
            Debug.Log($"[MockAdsProvider] Banner Hidden ({position}).");
        }

        public void DestroyBanner(BannerPosition position)
        {
            visibleBanners.Remove(position);
            if (activeBannerObjects.TryGetValue(position, out GameObject bannerObj) && bannerObj != null)
            {
                UnityEngine.Object.Destroy(bannerObj);
                activeBannerObjects.Remove(position);
            }
            Debug.Log($"[MockAdsProvider] Banner Destroyed ({position}).");
        }

        public void LoadInterstitial(string adUnitId = null, string placement = null)
        {
            isInterstitialLoaded = true;
            OnAdLoaded?.Invoke(IAdsType.Interstitial);
        }

        public void ShowInterstitial(Action onOpened = null, Action onClosed = null)
        {
            ShowInterstitial(null, null, onOpened, onClosed);
        }

        public void ShowInterstitial(string adUnitId, string placement, Action onOpened = null, Action onClosed = null)
        {
#if !UNITY_EDITOR
            onClosed?.Invoke();
            return;
#else
            Debug.Log($"[MockAdsProvider] Showing Interstitial (ID: {adUnitId}, Placement: {placement}).");
            OnAdShown?.Invoke(IAdsType.Interstitial);
            onOpened?.Invoke();

            GameObject adObj = SpawnMockPrefab("MockInterstitialAd", BannerPosition.Center);
            if (adObj != null)
            {
                var view = adObj.GetComponent<MockAdView>();
                if (view != null)
                {
                    view.SetupInterstitial(placement, () =>
                    {
                        OnAdClosed?.Invoke(IAdsType.Interstitial);
                        onClosed?.Invoke();
                        LoadInterstitial(adUnitId, placement);
                    });
                    return;
                }
            }

            // Fallback if no prefab spawned
            OnAdClosed?.Invoke(IAdsType.Interstitial);
            onClosed?.Invoke();
            LoadInterstitial(adUnitId, placement);
#endif
        }

        public void LoadRewarded(string adUnitId = null, string placement = null)
        {
            isRewardedLoaded = true;
            OnAdLoaded?.Invoke(IAdsType.Rewarded);
        }

        public void ShowRewarded(Action onRewardEarned, Action onOpened = null, Action onClosed = null)
        {
            ShowRewarded(null, null, onRewardEarned, onOpened, onClosed);
        }

        public void ShowRewarded(string adUnitId, string placement, Action onRewardEarned, Action onOpened = null, Action onClosed = null)
        {
            ShowRewarded(adUnitId, placement, (reward) => onRewardEarned?.Invoke(), onOpened, onClosed);
        }

        public void ShowRewarded(string adUnitId, string placement, Action<AdReward> onRewardEarned, Action onOpened = null, Action onClosed = null)
        {
#if !UNITY_EDITOR
            onClosed?.Invoke();
            return;
#else
            var reward = new AdReward("Continue_Play", 1);
            Debug.Log($"[MockAdsProvider] Showing Rewarded Ad (ID: {adUnitId}, Placement: {placement} - Reward: {reward.ItemName} x{reward.Amount}).");
            OnAdShown?.Invoke(IAdsType.Rewarded);
            onOpened?.Invoke();

            GameObject adObj = SpawnMockPrefab("MockRewardedAd", BannerPosition.Center);
            if (adObj != null)
            {
                var view = adObj.GetComponent<MockAdView>();
                if (view != null)
                {
                    view.SetupRewarded(placement, () =>
                    {
                        OnRewardEarned?.Invoke();
                        OnRewardEarnedWithReward?.Invoke(reward);
                        onRewardEarned?.Invoke(reward);
                    }, () =>
                    {
                        OnAdClosed?.Invoke(IAdsType.Rewarded);
                        onClosed?.Invoke();
                        LoadRewarded(adUnitId, placement);
                    });
                    return;
                }
            }

            // Fallback if no prefab spawned
            OnRewardEarned?.Invoke();
            OnRewardEarnedWithReward?.Invoke(reward);
            onRewardEarned?.Invoke(reward);
            OnAdClosed?.Invoke(IAdsType.Rewarded);
            onClosed?.Invoke();
            LoadRewarded(adUnitId, placement);
#endif
        }

        private GameObject SpawnMockPrefab(string prefabName, BannerPosition position)
        {
            //EnsureEventSystem();
            GameObject prefab = null;

            // 1. Try Resources.Load from Resources/Mock
            prefab = Resources.Load<GameObject>($"Mock/{prefabName}");
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(prefabName);
            }

#if UNITY_EDITOR
            // 2. In Unity Editor, try direct asset path
            if (prefab == null)
            {
                string path = $"Packages/com.herghys.multipleadsmanager/Runtime/Mock/{prefabName}.prefab";
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    path = $"Packages/com.herghys.multipleadsmanager/Runtime/Resources/Mock/{prefabName}.prefab";
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                if (prefab == null)
                {
                    path = $"Assets/Plugins/Herghys/MultipleAds/Mock/{prefabName}.prefab";
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                if (prefab == null)
                {
                    path = $"Assets/Plugins/Herghys/MultipleAds/Resources/Mock/{prefabName}.prefab";
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
#endif

            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = $"[Mock] {prefabName}";
                instance.transform.localScale = Vector3.one;
                UnityEngine.Object.DontDestroyOnLoad(instance);
                return instance;
            }

            // 3. Fallback: create procedural UI overlay if prefab not found
            return CreateProceduralMockAd(prefabName, position);
        }

        private static void EnsureEventSystem()
        {
            // EventSystem creation is disabled to prevent conflicting with the project's input setup.
        }

        private GameObject CreateProceduralMockAd(string prefabName, BannerPosition position)
        {
            var root = new GameObject($"[Mock] {prefabName}");
            UnityEngine.Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29999;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            var view = root.AddComponent<MockAdView>();
            var font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 22);

            if (prefabName.Contains("Banner"))
            {
                var bannerPanel = new GameObject("BannerPanel");
                bannerPanel.transform.SetParent(root.transform, false);
                var img = bannerPanel.AddComponent<Image>();
                img.color = new Color(0.12f, 0.16f, 0.23f, 0.95f);

                var rt = bannerPanel.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(320, 50);

                var txtObj = new GameObject("Text");
                txtObj.transform.SetParent(bannerPanel.transform, false);
                var txt = txtObj.AddComponent<Text>();
                txt.font = font;
                txt.text = $"Mock Banner Ad (320x50)\nPosition: {position}";
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.fontSize = 14;
                var txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = Vector2.zero;

                view.SetupBanner(position);
            }
            else
            {
                // Fullscreen Interstitial / Rewarded procedural dialog
                bool isRewarded = prefabName.Contains("Rewarded");

                var backdrop = new GameObject("Backdrop");
                backdrop.transform.SetParent(root.transform, false);
                var bgImg = backdrop.AddComponent<Image>();
                bgImg.color = new Color(0, 0, 0, 0.8f);
                var bgRt = backdrop.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = Vector2.zero;

                var card = new GameObject("DialogCard");
                card.transform.SetParent(root.transform, false);
                var cardImg = card.AddComponent<Image>();
                cardImg.color = new Color(0.12f, 0.16f, 0.24f, 1f);
                var cardRt = card.GetComponent<RectTransform>();
                cardRt.sizeDelta = new Vector2(600, 800);

                var titleObj = new GameObject("Title");
                titleObj.transform.SetParent(card.transform, false);
                var titleTxt = titleObj.AddComponent<Text>();
                titleTxt.font = font;
                titleTxt.text = isRewarded ? "Mock Rewarded Ad" : "Mock Interstitial Ad";
                titleTxt.alignment = TextAnchor.MiddleCenter;
                titleTxt.color = isRewarded ? new Color(0.98f, 0.75f, 0.14f) : Color.white;
                titleTxt.fontSize = 28;
                titleTxt.fontStyle = FontStyle.Bold;
                var titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0, 1);
                titleRt.anchorMax = new Vector2(1, 1);
                titleRt.anchoredPosition = new Vector2(0, -60);
                titleRt.sizeDelta = new Vector2(-40, 60);

                var msgObj = new GameObject("Message");
                msgObj.transform.SetParent(card.transform, false);
                var msgTxt = msgObj.AddComponent<Text>();
                msgTxt.font = font;
                msgTxt.text = isRewarded 
                    ? "Simulated rewarded video ad.\nClick Claim Reward to grant bonus!"
                    : "Full-screen interstitial ad simulation.\nClick Close to return to the game.";
                msgTxt.alignment = TextAnchor.MiddleCenter;
                msgTxt.color = new Color(0.85f, 0.9f, 0.95f);
                msgTxt.fontSize = 20;
                var msgRt = msgObj.GetComponent<RectTransform>();
                msgRt.anchorMin = new Vector2(0.1f, 0.35f);
                msgRt.anchorMax = new Vector2(0.9f, 0.8f);
                msgRt.sizeDelta = Vector2.zero;

                if (isRewarded)
                {
                    // Claim Reward Button
                    var claimBtnObj = new GameObject("ClaimButton");
                    claimBtnObj.transform.SetParent(card.transform, false);
                    var claimImg = claimBtnObj.AddComponent<Image>();
                    claimImg.color = new Color(0.06f, 0.72f, 0.5f);
                    var claimBtn = claimBtnObj.AddComponent<Button>();
                    var claimRt = claimBtnObj.GetComponent<RectTransform>();
                    claimRt.anchorMin = new Vector2(0.5f, 0);
                    claimRt.anchorMax = new Vector2(0.5f, 0);
                    claimRt.anchoredPosition = new Vector2(0, 110);
                    claimRt.sizeDelta = new Vector2(280, 56);

                    var claimTxtObj = new GameObject("Text");
                    claimTxtObj.transform.SetParent(claimBtnObj.transform, false);
                    var claimTxt = claimTxtObj.AddComponent<Text>();
                    claimTxt.font = font;
                    claimTxt.text = "Claim Reward & Close";
                    claimTxt.alignment = TextAnchor.MiddleCenter;
                    claimTxt.color = Color.white;
                    claimTxt.fontSize = 20;
                    var claimTxtRt = claimTxtObj.GetComponent<RectTransform>();
                    claimTxtRt.anchorMin = Vector2.zero;
                    claimTxtRt.anchorMax = Vector2.one;
                    claimTxtRt.sizeDelta = Vector2.zero;

                    // Skip / Close Button
                    var closeBtnObj = new GameObject("CloseButton");
                    closeBtnObj.transform.SetParent(card.transform, false);
                    var closeImg = closeBtnObj.AddComponent<Image>();
                    closeImg.color = new Color(0.35f, 0.4f, 0.48f);
                    var closeBtn = closeBtnObj.AddComponent<Button>();
                    var closeRt = closeBtnObj.GetComponent<RectTransform>();
                    closeRt.anchorMin = new Vector2(0.5f, 0);
                    closeRt.anchorMax = new Vector2(0.5f, 0);
                    closeRt.anchoredPosition = new Vector2(0, 44);
                    closeRt.sizeDelta = new Vector2(180, 40);

                    var closeTxtObj = new GameObject("Text");
                    closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
                    var closeTxt = closeTxtObj.AddComponent<Text>();
                    closeTxt.font = font;
                    closeTxt.text = "Skip / Close";
                    closeTxt.alignment = TextAnchor.MiddleCenter;
                    closeTxt.color = Color.white;
                    closeTxt.fontSize = 16;
                    var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
                    closeTxtRt.anchorMin = Vector2.zero;
                    closeTxtRt.anchorMax = Vector2.one;
                    closeTxtRt.sizeDelta = Vector2.zero;

                    view.SetupRewarded(position.ToString(), () => { }, () => { });
                }
                else
                {
                    // Interstitial Close Button
                    var closeBtnObj = new GameObject("CloseButton");
                    closeBtnObj.transform.SetParent(card.transform, false);
                    var closeImg = closeBtnObj.AddComponent<Image>();
                    closeImg.color = new Color(0.85f, 0.25f, 0.25f);
                    var closeBtn = closeBtnObj.AddComponent<Button>();
                    var closeRt = closeBtnObj.GetComponent<RectTransform>();
                    closeRt.anchorMin = new Vector2(0.5f, 0);
                    closeRt.anchorMax = new Vector2(0.5f, 0);
                    closeRt.anchoredPosition = new Vector2(0, 60);
                    closeRt.sizeDelta = new Vector2(240, 56);

                    var closeTxtObj = new GameObject("Text");
                    closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
                    var closeTxt = closeTxtObj.AddComponent<Text>();
                    closeTxt.font = font;
                    closeTxt.text = "Close Ad";
                    closeTxt.alignment = TextAnchor.MiddleCenter;
                    closeTxt.color = Color.white;
                    closeTxt.fontSize = 20;
                    var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
                    closeTxtRt.anchorMin = Vector2.zero;
                    closeTxtRt.anchorMax = Vector2.one;
                    closeTxtRt.sizeDelta = Vector2.zero;

                    view.SetupInterstitial(position.ToString(), () => { });
                }
            }

            return root;
        }
    }
}

namespace Herghys.MultipleAdsManager.Modules
{
    // Alias for backwards compatibility
    public class MockAdsProvider : Herghys.MultipleAdsManager.Mock.MockAdsProvider { }
}