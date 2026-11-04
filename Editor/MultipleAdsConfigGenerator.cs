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

                EnsureDefaultPlacementCatalogAndEntries(config);

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

        public static void EnsureDefaultPlacementCatalogAndEntries(MultipleAdsConfig config)
        {
            if (config == null) return;

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

            if (config.ProviderConfigs == null)
            {
                var providerConfigsField = typeof(MultipleAdsConfig).GetField("providerConfigs", BindingFlags.NonPublic | BindingFlags.Instance);
                providerConfigsField?.SetValue(config, new List<BaseAdConfig>());
            }
        }

        public static Type FindConfigType(string className)
        {
            if (string.IsNullOrEmpty(className)) return null;

            string[] candidateNames = new[]
            {
                $"Herghys.MultipleAdsManager.Modules.{className}",
                $"Herghys.MultipleAdsManager.Mock.{className}",
                $"Herghys.MultipleAdsManager.Runtime.{className}",
                className,
                $"MultipleAds.Packages.{className}"
            };

            foreach (var name in candidateNames)
            {
                Type direct = Type.GetType(name);
                if (direct != null && typeof(BaseAdConfig).IsAssignableFrom(direct)) return direct;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var name in candidateNames)
                {
                    try
                    {
                        var t = assembly.GetType(name, false, true);
                        if (t != null && typeof(BaseAdConfig).IsAssignableFrom(t)) return t;
                    }
                    catch { }
                }
            }

            // Fallback: search all types in all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase) && typeof(BaseAdConfig).IsAssignableFrom(t))
                        {
                            return t;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        public static BaseAdConfig EnsureProviderConfig(AdsModuleDefinition module, MultipleAdsConfig mainConfig = null, bool saveAssets = false)
        {
            if (module == null) return null;

            string assetName = $"{module.Id}Config";
            string path = MultipleAdsPaths.GetProviderConfigAssetPath(assetName);

            var existingAsset = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(path);
            if (existingAsset == null)
            {
                string[] guids = AssetDatabase.FindAssets($"{assetName} t:ScriptableObject");
                if (guids != null && guids.Length > 0)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    existingAsset = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(foundPath);
                }
            }

            if (existingAsset == null)
            {
                // Ensure module files are present in the project so script can be compiled if needed
                if (!module.IsDefault)
                {
                    MultipleAdsModuleManager.EnsureModuleFiles(module);
                }

                Type configType = FindConfigType(assetName);
                if (configType != null)
                {
                    existingAsset = (BaseAdConfig)ScriptableObject.CreateInstance(configType);

                    string dir = MultipleAdsPaths.GetAdsConfigDirectory();

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string targetAssetPath = $"{dir}/{assetName}.asset";
                    AssetDatabase.CreateAsset(existingAsset, targetAssetPath);
                    Debug.Log($"[MultipleAds] Auto-created {assetName} at {targetAssetPath}");
                }
                else
                {
                    Debug.LogWarning($"[MultipleAds] Could not auto-create {assetName}: C# type not yet found in assemblies. It will be created automatically once scripts compile.");
                }
            }

            if (existingAsset != null)
            {
                if (mainConfig == null) mainConfig = GetConfig();
                if (mainConfig != null)
                {
                    if (mainConfig.ProviderConfigs == null)
                    {
                        var field = typeof(MultipleAdsConfig).GetField("providerConfigs", BindingFlags.NonPublic | BindingFlags.Instance);
                        field?.SetValue(mainConfig, new List<BaseAdConfig>());
                    }

                    if (!mainConfig.ProviderConfigs.Contains(existingAsset))
                    {
                        mainConfig.ProviderConfigs.Add(existingAsset);
                        EditorUtility.SetDirty(mainConfig);
                        Debug.Log($"[MultipleAds] Linked {assetName} into MultipleAdsManager Config.");
                    }
                }

                EditorUtility.SetDirty(existingAsset);

                if (saveAssets)
                {
                    AssetDatabase.SaveAssets();
                }
            }

            return existingAsset;
        }

        public const string PendingAutoRescanSessionKey = "MultipleAds_PendingAutoRescan";

        public static void RequestAutoRescan()
        {
            SessionState.SetBool(PendingAutoRescanSessionKey, true);
            MultipleAdsAutoConfigWatcher.ScheduleRescanWhenReady();
        }

        public static void RefreshAllOpenWindows()
        {
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            var setupWindows = Resources.FindObjectsOfTypeAll<MultipleAdsSetupWindow>();
            if (setupWindows != null)
            {
                foreach (var win in setupWindows)
                {
                    if (win == null) continue;
                    win.RefreshAfterScan();
                }
            }

            var editorWindows = Resources.FindObjectsOfTypeAll<MultipleAdsEditorWindowBase>();
            if (editorWindows != null)
            {
                foreach (var win in editorWindows)
                {
                    if (win == null) continue;
                    win.RefreshAfterScan();
                }
            }
        }

        public static void LoadCheck(bool showDialog = false)
        {
            if (isGenerating) return;
            if (BuildPipeline.isBuildingPlayer) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                RequestAutoRescan();
                return;
            }

            isGenerating = true;
            try
            {
                // 1. Check MultipleAdsManager Config
                var config = GetConfig();
                bool createdMain = false;
                if (config == null)
                {
                    Debug.Log("[MultipleAds] MultipleAdsManager Config asset is missing. Auto-generating default configuration asset...");
                    config = RegenerateConfig(false);
                    createdMain = true;
                }
                else
                {
                    EnsureDefaultPlacementCatalogAndEntries(config);
                }

                if (config == null) return;

                // 2. Scan packages and assemblies
                MultipleAdsPackageManager.Scan();
                BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
                var defineList = MultipleAdsBuildTargetCompat.GetDefinesList(target);
                bool definesChanged = false;
                bool needsAssetRefresh = false;
                bool missingCompiledConfigType = false;

                // 3. LoadCheck for every provider config
                foreach (var module in MultipleAdsModuleManager.AllModules)
                {
                    if (module == null) continue;

                    bool sdkInstalled = MultipleAdsPackageManager.IsSdkInstalled(module);
                    bool moduleAdded = MultipleAdsModuleManager.IsModuleAdded(module.Id);
                    bool hasDefine = !string.IsNullOrEmpty(module.DefineSymbol) && defineList.Contains(module.DefineSymbol);

                    // If SDK is detected as installed, auto-install module templates and enable defines
                    if (sdkInstalled && !module.IsDefault)
                    {
                        if (!moduleAdded)
                        {
                            Debug.Log($"[MultipleAds] Installed SDK detected for '{module.DisplayName}'. Auto-installing module files...");
                            if (MultipleAdsModuleManager.EnsureModuleFiles(module))
                            {
                                needsAssetRefresh = true;
                                moduleAdded = true;
                            }
                        }

                        if (!string.IsNullOrEmpty(module.DefineSymbol) && !defineList.Contains(module.DefineSymbol))
                        {
                            defineList.Add(module.DefineSymbol);
                            definesChanged = true;
                            hasDefine = true;
                            Debug.Log($"[MultipleAds] Auto-enabled define symbol '{module.DefineSymbol}' for installed provider {module.DisplayName}.");
                        }

                        if (!string.IsNullOrEmpty(module.SdkInstalledSymbol) && !defineList.Contains(module.SdkInstalledSymbol))
                        {
                            defineList.Add(module.SdkInstalledSymbol);
                            definesChanged = true;
                            Debug.Log($"[MultipleAds] Auto-enabled define symbol '{module.SdkInstalledSymbol}' for active SDK {module.DisplayName}.");
                        }
                    }

                    // For active or installed providers, check if AdsConfig is missing and auto create
                    bool shouldHaveConfig = module.IsDefault || sdkInstalled || moduleAdded || hasDefine;
                    if (shouldHaveConfig)
                    {
                        string configPath = MultipleAdsPaths.GetProviderConfigAssetPath($"{module.Id}Config");
                        var existingConfig = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(configPath);

                        if (existingConfig == null)
                        {
                            string[] guids = AssetDatabase.FindAssets($"{module.Id}Config t:ScriptableObject");
                            if (guids != null && guids.Length > 0)
                            {
                                existingConfig = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                            }
                        }

                        if (existingConfig == null)
                        {
                            Debug.Log($"[MultipleAds] LoadCheck: Missing AdsConfig for {module.DisplayName}. Auto-creating...");
                            existingConfig = EnsureProviderConfig(module, config, false);
                            if (existingConfig == null)
                            {
                                missingCompiledConfigType = true;
                            }
                        }

                        if (existingConfig != null && config.ProviderConfigs != null && !config.ProviderConfigs.Contains(existingConfig))
                        {
                            if (module.IsDefault || hasDefine)
                            {
                                config.ProviderConfigs.Add(existingConfig);
                                EditorUtility.SetDirty(config);
                            }
                        }
                    }
                }

                if (definesChanged)
                {
                    MultipleAdsBuildTargetCompat.SetScriptingDefineSymbols(target, defineList);
                }

                // Sync PlayerSettings & SDK settings
                MultipleAdsSdkConfigSynchronizer.SyncFromPlayerSettings(config);
                MultipleAdsSdkConfigSynchronizer.SyncGlobalAppInfo(config);
                MultipleAdsSdkConfigSynchronizer.SyncAllSdkSettings(config);

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();

                if (needsAssetRefresh)
                {
                    AssetDatabase.Refresh();
                }

                if (definesChanged || needsAssetRefresh || missingCompiledConfigType)
                {
                    RequestAutoRescan();
                }

                ModuleUIState.ScanAll(target, clearOverrides: false);
                RefreshAllOpenWindows();

                if (showDialog)
                {
                    int providerCount = config.ProviderConfigs != null ? config.ProviderConfigs.Count : 0;
                    EditorUtility.DisplayDialog(
                        "MultipleAds LoadCheck",
                        $"LoadCheck completed successfully.\n\n" +
                        $"• MultipleAdsManager Config: OK\n" +
                        $"• Provider Configs Active: {providerCount}\n" +
                        $"• All missing configs auto-created and synchronized.",
                        "OK"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultipleAds] Error during LoadCheck: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                isGenerating = false;
            }
        }

        public static void AutoScanAndCreateConfigs(bool forceRescan = false, bool showDialog = false)
        {
            if (forceRescan)
            {
                MultipleAdsPackageManager.InvalidatePackageCache();
            }
            LoadCheck(showDialog);
        }
    }

    [InitializeOnLoad]
    public static class MultipleAdsAutoConfigWatcher
    {
        private static bool hasRunOnAppLoad = false;
        private static bool isPollingReady = false;

        static MultipleAdsAutoConfigWatcher()
        {
            UnityEditor.PackageManager.Events.registeredPackages -= OnPackagesRegistered;
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesRegistered;

            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            ScheduleRescanWhenReady();
        }

        private static void OnPackagesRegistered(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            if ((args.added != null && args.added.Count > 0) ||
                (args.removed != null && args.removed.Count > 0) ||
                (args.changedTo != null && args.changedTo.Count > 0))
            {
                MultipleAdsPackageManager.InvalidatePackageCache();
                MultipleAdsConfigGenerator.RequestAutoRescan();
            }
        }

        private static void OnAfterAssemblyReload()
        {
            ScheduleRescanWhenReady();
        }

        public static void ScheduleRescanWhenReady()
        {
            if (isPollingReady) return;
            isPollingReady = true;
            EditorApplication.delayCall += TryRunScheduledRescan;
        }

        private static void TryRunScheduledRescan()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                isPollingReady = false;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunScheduledRescan;
                return;
            }

            isPollingReady = false;

            bool pendingRescan = SessionState.GetBool(MultipleAdsConfigGenerator.PendingAutoRescanSessionKey, false);
            if (!hasRunOnAppLoad || pendingRescan)
            {
                hasRunOnAppLoad = true;
                SessionState.SetBool(MultipleAdsConfigGenerator.PendingAutoRescanSessionKey, false);
                MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: pendingRescan, showDialog: false);
            }
        }
    }
}
