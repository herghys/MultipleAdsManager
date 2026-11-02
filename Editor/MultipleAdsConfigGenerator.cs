using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public static class MultipleAdsConfigGenerator
    {
        private static bool isGenerating = false;

        public static MultipleAdsConfig GetConfig()
        {
            string configPath = MultipleAdsPaths.GetMainConfigAssetPath();
            var config = AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(configPath);
            if (config == null)
            {
                config = Resources.Load<MultipleAdsConfig>(MultipleAdsPaths.MainConfigName);
                if (config == null)
                    config = Resources.Load<MultipleAdsConfig>(MultipleAdsPaths.LegacyMainConfigName);
            }
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:MultipleAdsConfig");
                if (guids != null && guids.Length > 0)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(foundPath);
                }
            }
            return config;
        }

        public static MultipleAdsConfig GetOrCreateConfig(bool showDialog = false)
        {
            var config = GetConfig();
            if (config != null) return config;
            return RegenerateConfig(showDialog);
        }

        public static MultipleAdsConfig RegenerateConfig(bool showDialog = false)
        {
            if (isGenerating) return GetConfig();
            isGenerating = true;

            try
            {
                string dir = MultipleAdsPaths.GetMainConfigDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string targetPath = $"{dir}/{MultipleAdsPaths.MainConfigName}.asset";
                var config = AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(targetPath);
                bool isNew = false;

                if (config == null)
                {
                    config = GetConfig();
                }

                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<MultipleAdsConfig>();
                    AssetDatabase.CreateAsset(config, targetPath);
                    isNew = true;
                }

                // Ensure default Ad Placements Catalog
                if (config.AdPlacementsCatalog == null)
                {
                    var catalogField = typeof(MultipleAdsConfig).GetField("adPlacementsCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
                    catalogField?.SetValue(config, new List<string>());
                }

                if (config.AdPlacementsCatalog.Count == 0)
                {
                    config.AdPlacementsCatalog.Add(MultipleAdsConfig.DefaultBannerPlacement);
                    config.AdPlacementsCatalog.Add(MultipleAdsConfig.DefaultInterstitialPlacement);
                    config.AdPlacementsCatalog.Add(MultipleAdsConfig.DefaultRewardedPlacement);
                }

                // Ensure default Ad Entries
                if (config.AdEntries == null)
                {
                    var adEntriesField = typeof(MultipleAdsConfig).GetField("adEntries", BindingFlags.NonPublic | BindingFlags.Instance);
                    adEntriesField?.SetValue(config, new List<AdsTypeEntry>());
                }

                if (config.AdEntries.Count == 0)
                {
                    config.AdEntries.Add(new AdsTypeEntry("Banner", IAdsType.Banner, MultipleAdsConfig.DefaultBannerPlacement));
                    config.AdEntries.Add(new AdsTypeEntry("Interstitial", IAdsType.Interstitial, MultipleAdsConfig.DefaultInterstitialPlacement));
                    config.AdEntries.Add(new AdsTypeEntry("Rewarded", IAdsType.Rewarded, MultipleAdsConfig.DefaultRewardedPlacement, "Reward", 1));
                }

                // Sync from Unity PlayerSettings (App Name & Package Identifier)
                MultipleAdsSdkConfigSynchronizer.SyncFromPlayerSettings(config);

                // Synchronize provider configs according to active defines
                MultipleAdsSetupWindow.SyncConfigsWithActiveDefines(config, false);

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[MultipleAds] Successfully {(isNew ? "created" : "regenerated")} MultipleAdsManager Config asset at {AssetDatabase.GetAssetPath(config)}");

                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "MultipleAds Config",
                        $"MultipleAdsManager Config asset has been successfully {(isNew ? "created" : "regenerated")} and synchronized with active settings at:\n{AssetDatabase.GetAssetPath(config)}",
                        "OK"
                    );
                }

                return config;
            }
            finally
            {
                isGenerating = false;
            }
        }
    }

    [InitializeOnLoad]
    public static class MultipleAdsAutoConfigWatcher
    {
        private static bool isChecking = false;

        static MultipleAdsAutoConfigWatcher()
        {
            EditorApplication.delayCall += () => CheckAndAutoGenerate();
        }

        public static void CheckAndAutoGenerate()
        {
            if (isChecking) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (BuildPipeline.isBuildingPlayer) return;

            var config = MultipleAdsConfigGenerator.GetConfig();
            if (config == null)
            {
                isChecking = true;
                try
                {
                    Debug.Log("[MultipleAds] MultipleAdsManager Config asset is missing. Auto-generating default configuration asset...");
                    MultipleAdsConfigGenerator.RegenerateConfig(showDialog: false);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MultipleAds] Failed to auto-generate MultipleAdsManager Config: {ex.Message}");
                }
                finally
                {
                    isChecking = false;
                }
            }
        }
    }

    public class MultipleAdsConfigAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            EditorApplication.delayCall += () => MultipleAdsAutoConfigWatcher.CheckAndAutoGenerate();
        }
    }
}
