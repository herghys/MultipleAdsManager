using System;
using System.Collections.Generic;
using UnityEngine;

namespace Herghys.MultipleAdsManager.Runtime
{
    [CreateAssetMenu(fileName = "MultipleAdsManager Config", menuName = "MultipleAds/MultipleAdsManager Config", order = 1)]
    public class MultipleAdsConfig : ScriptableObject
    {
        public const string DefaultBannerPlacement = "Banner";
        public const string DefaultInterstitialPlacement = "Interstitial";
        public const string DefaultRewardedPlacement = "Rewarded";

        public static string GetDefaultPlacement(IAdsType type)
        {
            switch (type)
            {
                case IAdsType.Banner: return DefaultBannerPlacement;
                case IAdsType.Interstitial: return DefaultInterstitialPlacement;
                case IAdsType.Rewarded:
                case IAdsType.RewardedInterstitial: return DefaultRewardedPlacement;
                default: return type.ToString();
            }
        }

        [Header("Global App Information")]
        [Tooltip("Global Application / Product Name for the game. Synchronizes to PlayerSettings and all provider configs.")]
        [SerializeField] private string globalAppName = "";

        [Tooltip("Global Package Name / Application Identifier (e.g. com.DefaultCompany.DefaultApps). Synchronizes to PlayerSettings and all provider configs.")]
        [SerializeField] private string globalPackageName = "";

        [Header("General Settings")]
        [Tooltip("List of test device IDs (hashed Android ID or GAID/IDFA).")]
        [SerializeField] private List<string> testDeviceIds = new List<string>();

        [Header("Privacy & COPPA Compliance")]
        [Tooltip("Tag ad requests for child-directed treatment / age-restricted users across all ad networks (e.g. AdMob TagForChildDirectedTreatment, LevelPlay is_child_directed, AppLovin setIsAgeRestrictedUser).")]
        [SerializeField] private bool targetAdsForChildren = false;

        [Header("Android Optimization - Manifest Stripping")]
        [Tooltip("Strip android.permission.ACCESS_FINE_LOCATION via tools:node=\"remove\" in AndroidManifest.xml.")]
        [SerializeField] private bool stripFineLocation = true;

        [Tooltip("Strip android.permission.ACCESS_COARSE_LOCATION via tools:node=\"remove\" in AndroidManifest.xml.")]
        [SerializeField] private bool stripCoarseLocation = true;

        [Tooltip("Strip android.permission.READ_PHONE_STATE via tools:node=\"remove\" in AndroidManifest.xml.")]
        [SerializeField] private bool stripPhoneState = true;

        [Tooltip("Strip android.permission.WRITE_EXTERNAL_STORAGE via tools:node=\"remove\" in AndroidManifest.xml.")]
        [SerializeField] private bool stripExternalStorage = true;

        [Tooltip("Strip com.google.android.gms.permission.AD_ID via tools:node=\"remove\" in AndroidManifest.xml (also automatically stripped when TargetAdsForChildren is enabled).")]
        [SerializeField] private bool stripAdvertisingId = false;

        [Header("Android Optimization - ProGuard / R8 Rules")]
        [Tooltip("Include MultipleAds core ProGuard rules (Annotations, SourceLineTable, IInterface methods).")]
        [SerializeField] private bool enableProguardCore = true;

        [Tooltip("Include Google Mobile Ads ProGuard rules when GOOGLE_ADS is enabled.")]
        [SerializeField] private bool enableProguardGoogleAds = true;

        [Tooltip("Include Unity LevelPlay (IronSource) ProGuard rules when LEVELPLAY_ADS is enabled.")]
        [SerializeField] private bool enableProguardLevelPlay = true;

        [Tooltip("Include AppLovin MAX ProGuard rules when APPLOVIN_ADS is enabled.")]
        [SerializeField] private bool enableProguardAppLovin = true;

        [Tooltip("Include Vungle / Liftoff ProGuard rules when VUNGLE_ADS is enabled.")]
        [SerializeField] private bool enableProguardVungle = true;

        [Tooltip("Include Unity Ads ProGuard rules when UNITY_ADS is enabled.")]
        [SerializeField] private bool enableProguardUnityAds = true;

        [Header("Editor Mock & Testing Settings")]
        [Tooltip("Enable simulated mock ads when running inside Unity Editor. Mock ads are strictly restricted to Unity Editor and never run in player builds.")]
        [SerializeField] private bool enableMockInEditor = true;

        [Tooltip("When enabled, requests in Unity Editor will use test ad IDs and test mode instead of production ad IDs. Test ads are strictly restricted to Unity Editor (#UNITY_EDITOR) and never run in player builds.")]
        [SerializeField] private bool useTestAdsOnEditor = true;

        [Header("Ad Placement Catalog")]
        [Tooltip("Predefined collection of ad placements / names across your game. Pick from this list to eliminate typos.")]
        [SerializeField] private List<string> adPlacementsCatalog = new List<string>
        {
            DefaultBannerPlacement,
            DefaultInterstitialPlacement,
            DefaultRewardedPlacement
        };

        [Header("Central Ad Entries")]
        [Tooltip("Centralized list of ad units in your game. Each entry maps an ad format and placement to ad unit IDs for each active provider. If an ad ID is null/empty, that provider will not be used for that entry.")]
        [SerializeField] private List<AdsTypeEntry> adEntries = new List<AdsTypeEntry>
        {
            new AdsTypeEntry("Banner", IAdsType.Banner, DefaultBannerPlacement),
            new AdsTypeEntry("Interstitial", IAdsType.Interstitial, DefaultInterstitialPlacement),
            new AdsTypeEntry("Rewarded", IAdsType.Rewarded, DefaultRewardedPlacement, "Reward", 1)
        };

        [Header("Mediation Provider Configurations")]
        [Tooltip("Individual ScriptableObject configs for each active ad network (GoogleAds, LevelPlay, AppLovin, Vungle, UnityAds).")]
        [SerializeField] private List<BaseAdConfig> providerConfigs = new List<BaseAdConfig>();

        public static MultipleAdsConfig ActiveConfig { get; internal set; }

        public string GlobalAppName { get => globalAppName; set => globalAppName = value; }
        public string GlobalPackageName { get => globalPackageName; set => globalPackageName = value; }

        public bool TestMode
        {
            get
            {
#if UNITY_EDITOR
                return useTestAdsOnEditor;
#else
                return false;
#endif
            }
            set => useTestAdsOnEditor = value;
        }
        public List<string> TestDeviceIds => testDeviceIds;
        public bool TargetAdsForChildren { get => targetAdsForChildren; set => targetAdsForChildren = value; }
        public bool EnableMockInEditor { get => enableMockInEditor; set => enableMockInEditor = value; }
        public bool UseTestAdsOnEditor { get => useTestAdsOnEditor; set => useTestAdsOnEditor = value; }

        public bool StripFineLocation { get => stripFineLocation; set => stripFineLocation = value; }
        public bool StripCoarseLocation { get => stripCoarseLocation; set => stripCoarseLocation = value; }
        public bool StripPhoneState { get => stripPhoneState; set => stripPhoneState = value; }
        public bool StripExternalStorage { get => stripExternalStorage; set => stripExternalStorage = value; }
        public bool StripAdvertisingId { get => stripAdvertisingId; set => stripAdvertisingId = value; }

        public bool EnableProguardCore { get => enableProguardCore; set => enableProguardCore = value; }
        public bool EnableProguardGoogleAds { get => enableProguardGoogleAds; set => enableProguardGoogleAds = value; }
        public bool EnableProguardLevelPlay { get => enableProguardLevelPlay; set => enableProguardLevelPlay = value; }
        public bool EnableProguardAppLovin { get => enableProguardAppLovin; set => enableProguardAppLovin = value; }
        public bool EnableProguardVungle { get => enableProguardVungle; set => enableProguardVungle = value; }
        public bool EnableProguardUnityAds { get => enableProguardUnityAds; set => enableProguardUnityAds = value; }

        public List<string> AdPlacementsCatalog => adPlacementsCatalog;
        public List<AdsTypeEntry> AdEntries => adEntries;
        public List<BaseAdConfig> ProviderConfigs => providerConfigs;

        public BaseAdConfig GetConfig(AdNetworkType type)
        {
#if !UNITY_EDITOR
            if (type == AdNetworkType.Mock) return null;
#endif
            return providerConfigs.Find(s => s != null && s.NetworkType == type && s.Enabled);
        }

        public BaseAdConfig GetProviderConfig(AdNetworkType type)
        {
#if !UNITY_EDITOR
            if (type == AdNetworkType.Mock) return null;
#endif
            if (providerConfigs == null) return null;
            return providerConfigs.Find(s => s != null && s.NetworkType == type);
        }

        public List<BaseAdConfig> GetActiveConfigs()
        {
            return providerConfigs.FindAll(s => s != null && s.Enabled && s.NetworkType != AdNetworkType.Mock);
        }

        public AdsTypeEntry GetAdEntry(IAdsType type, string placement = null)
        {
            if (adEntries == null || adEntries.Count == 0) return null;

            if (!string.IsNullOrEmpty(placement))
            {
                var match = adEntries.Find(e => e != null && e.AdType == type && 
                    (string.Equals(e.AdPlacement, placement, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.AdName, placement, StringComparison.OrdinalIgnoreCase)));
                if (match != null) return match;
            }

            return adEntries.Find(e => e != null && e.AdType == type);
        }

        public List<AdsTypeEntry> GetAdEntries(IAdsType type)
        {
            if (adEntries == null) return new List<AdsTypeEntry>();
            return adEntries.FindAll(e => e != null && e.AdType == type);
        }

        public string GetAdId(AdNetworkType network, IAdsType type, string placement = null)
        {
#if UNITY_EDITOR
            if (network == AdNetworkType.Mock)
            {
                return AdsTypeEntry.GetDefaultMockId(type);
            }
#else
            if (network == AdNetworkType.Mock)
            {
                return null;
            }
#endif

            var providerConfig = GetConfig(network);
            var entry = GetAdEntry(type, placement);
            return entry != null ? entry.GetAdIdForProvider(network, providerConfig) : null;
        }

        public void CleanDisabledProviders(bool hasGoogle, bool hasLevelPlay, bool hasAppLovin, bool hasVungle, bool hasUnityAds = false)
        {
            if (adEntries == null) return;

            foreach (var entry in adEntries)
            {
                if (entry == null) continue;
                if (!hasGoogle) entry.RemoveProvider(AdNetworkType.GoogleAds);
                if (!hasLevelPlay) entry.RemoveProvider(AdNetworkType.LevelPlay);
                if (!hasAppLovin) entry.RemoveProvider(AdNetworkType.AppLovin);
                if (!hasVungle) entry.RemoveProvider(AdNetworkType.Vungle);
                if (!hasUnityAds) entry.RemoveProvider(AdNetworkType.UnityAds);
            }
        }
    }
}