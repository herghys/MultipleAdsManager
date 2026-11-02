using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class AdsModuleDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DefineSymbol { get; set; }
        public AdNetworkType NetworkType { get; set; }
        public bool IsDefault { get; set; }
        public string ConfigFileName => $"{Id}Config.cs";
        public string ProviderFileName => $"{Id}Provider.cs";
        public string SdkInstalledSymbol { get; set; }
        public string SdkTypeCheck { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public ScopedRegistryDefinition ScopedRegistry { get; set; }
    }

    public static class MultipleAdsModuleManager
    {
        private const string RemoteUrlPrefKey = "MultipleAds_RemoteModulesUrlPattern";
        public const string DefaultRemoteUrlPattern = "https://raw.githubusercontent.com/HerghysStudio/MultipleAds/modules/{0}/{1}";

        public static readonly List<AdsModuleDefinition> AllModules = new List<AdsModuleDefinition>
        {
            new AdsModuleDefinition
            {
                Id = "MockAds",
                DisplayName = "Mock Ads (Editor Simulator)",
                Description = "Built-in simulated banner, interstitial, and rewarded ads for Unity Editor testing.",
                DefineSymbol = null,
                SdkInstalledSymbol = null,
                SdkTypeCheck = null,
                NetworkType = AdNetworkType.Mock,
                IsDefault = true,
                PackageId = null,
                PackageVersion = null,
                ScopedRegistry = null
            },
            new AdsModuleDefinition
            {
                Id = "GoogleAds",
                DisplayName = "Google Mobile Ads (AdMob)",
                Description = "Google AdMob mediation wrapper with banner, interstitial, and rewarded ad unit support.",
                DefineSymbol = "GOOGLE_ADS",
                SdkInstalledSymbol = "GOOGLE_ADS_SDK_INSTALLED",
                SdkTypeCheck = "GoogleMobileAds.Api.MobileAds",
                NetworkType = AdNetworkType.GoogleAds,
                IsDefault = false,
                PackageId = "com.google.ads.mobile",
                PackageVersion = "11.6.0",
                ScopedRegistry = new ScopedRegistryDefinition
                {
                    Name = "Herghys",
                    Url = "https://package.openupm.com",
                    Scopes = new List<string> { "com.google" }
                }
            },
            new AdsModuleDefinition
            {
                Id = "LevelPlay",
                DisplayName = "Unity LevelPlay (IronSource)",
                Description = "Unity LevelPlay mediation wrapper supporting App Key initialization and placements.",
                DefineSymbol = "LEVELPLAY_ADS",
                SdkInstalledSymbol = "LEVELPLAY_SDK_INSTALLED",
                SdkTypeCheck = "Unity.Services.LevelPlay.LevelPlay, com.unity3d.mediation.LevelPlay",
                NetworkType = AdNetworkType.LevelPlay,
                IsDefault = false,
                PackageId = "com.unity.services.levelplay",
                PackageVersion = "9.5.1",
                ScopedRegistry = null
            },
            new AdsModuleDefinition
            {
                Id = "AppLovin",
                DisplayName = "AppLovin MAX",
                Description = "AppLovin MAX monetization and mediation wrapper.",
                DefineSymbol = "APPLOVIN_ADS",
                SdkInstalledSymbol = "APPLOVIN_SDK_INSTALLED",
                SdkTypeCheck = "MaxSdk",
                NetworkType = AdNetworkType.AppLovin,
                IsDefault = false,
                PackageId = "com.applovin.mediation.ads",
                PackageVersion = "8.2.1",
                ScopedRegistry = new ScopedRegistryDefinition
                {
                    Name = "Herghys",
                    Url = "https://plugins.applovin.com/unity",
                    Scopes = new List<string> { "com.applovin" }
                }
            },
            new AdsModuleDefinition
            {
                Id = "Vungle",
                DisplayName = "Vungle / Liftoff",
                Description = "Vungle Liftoff monetization wrapper.",
                DefineSymbol = "VUNGLE_ADS",
                SdkInstalledSymbol = "VUNGLE_SDK_INSTALLED",
                SdkTypeCheck = "VungleAds",
                NetworkType = AdNetworkType.Vungle,
                IsDefault = false,
                PackageId = "io.liftoff.vungleads",
                PackageVersion = "7.4.4",
                ScopedRegistry = new ScopedRegistryDefinition
                {
                    Name = "Herghys",
                    Url = "https://registry.npmjs.com",
                    Scopes = new List<string> { "io.liftoff" }
                }
            }
        };

        public static List<AdsModuleDefinition> GetAllModules() => AllModules;

        public static AdsModuleDefinition GetModule(string moduleId)
        {
            return AllModules.Find(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetRemoteUrlPattern()
        {
            return EditorPrefs.GetString(RemoteUrlPrefKey, DefaultRemoteUrlPattern);
        }

        public static void SetRemoteUrlPattern(string url)
        {
            EditorPrefs.SetString(RemoteUrlPrefKey, url);
        }

        public static void EnsureDefaultModules()
        {
            // Mock module is built-in natively under Packages/com.herghys.multipleadsmanager/Runtime/Mock
            // and does not require generating files in Assets/MultipleAds/Modules/MockAds.
        }

        public static bool IsModuleAdded(string moduleId)
        {
            var def = GetModule(moduleId);
            if (def == null) return false;
            if (def.IsDefault) return true;

            string moduleDir = MultipleAdsPaths.GetModuleDirectory(moduleId);
            if (!Directory.Exists(moduleDir)) return false;

            string configPath = Path.Combine(moduleDir, def.ConfigFileName).Replace('\\', '/');
            string providerPath = Path.Combine(moduleDir, def.ProviderFileName).Replace('\\', '/');

            return File.Exists(configPath) && File.Exists(providerPath);
        }

        public static bool EnsureModuleFiles(string moduleId)
        {
            var def = GetModule(moduleId);
            return EnsureModuleFiles(def);
        }

        public static bool EnsureModuleFiles(AdsModuleDefinition def)
        {
            if (def == null || def.IsDefault) return false;

            string targetDir = MultipleAdsPaths.GetModuleDirectory(def.Id);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string dstConfig = Path.Combine(targetDir, def.ConfigFileName).Replace('\\', '/');
            string dstProvider = Path.Combine(targetDir, def.ProviderFileName).Replace('\\', '/');

            if (!File.Exists(dstConfig) || !File.Exists(dstProvider))
            {
                InstallFromLocalTemplates(def, targetDir);
                Debug.Log($"[MultipleAdsModuleManager] Auto-installed module template files for '{def.DisplayName}' into {targetDir}.");
                return true;
            }

            return false;
        }

        public static void AddModule(string moduleId, bool autoInstallSdk = true, bool downloadFromRemote = false, Action<bool> onComplete = null)
        {
            var def = GetModule(moduleId);
            if (def == null)
            {
                Debug.LogError($"[MultipleAdsModuleManager] Unknown module ID: {moduleId}");
                onComplete?.Invoke(false);
                return;
            }

            if (autoInstallSdk && !string.IsNullOrEmpty(def.PackageId))
            {
                bool packageAdded = MultipleAdsPackageManager.AddPackage(def.PackageId, def.PackageVersion, def.ScopedRegistry);
                if (packageAdded)
                {
                    Debug.Log($"[MultipleAdsModuleManager] Automatically added SDK '{def.PackageId}' ({def.PackageVersion}) for module '{def.DisplayName}'.");
                }
            }
            else if (!autoInstallSdk && !string.IsNullOrEmpty(def.PackageId))
            {
                Debug.Log($"[MultipleAdsModuleManager] Manual install selected for '{def.DisplayName}'. You can install {def.PackageId} via UPM, git, or .unitypackage at any time.");
            }

            string targetDir = MultipleAdsPaths.GetModuleDirectory(moduleId);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            BuildTargetGroup targetGroup = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            if (!string.IsNullOrEmpty(def.DefineSymbol))
            {
                MultipleAdsBuildTargetCompat.AddDefine(targetGroup, def.DefineSymbol);
            }
            if (autoInstallSdk && !string.IsNullOrEmpty(def.SdkInstalledSymbol) && MultipleAdsPackageManager.IsSdkInstalled(def))
            {
                MultipleAdsBuildTargetCompat.AddDefine(targetGroup, def.SdkInstalledSymbol);
            }

            if (downloadFromRemote)
            {
                DownloadFromRemote(def, targetDir, ok =>
                {
                    MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true);
                    onComplete?.Invoke(ok);
                });
            }
            else
            {
                InstallFromLocalTemplates(def, targetDir);
                AssetDatabase.Refresh();
                Debug.Log($"[MultipleAdsModuleManager] Successfully added module '{def.DisplayName}' into {targetDir}.");
                MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true);
                onComplete?.Invoke(true);
            }
        }

        public static void RemoveModule(string moduleId, BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            var def = GetModule(moduleId);
            if (def == null || def.IsDefault)
            {
                Debug.LogWarning($"[MultipleAdsModuleManager] Cannot remove default module or unknown module: {moduleId}");
                return;
            }

            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            var list = MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup);
            bool changed = false;
            if (!string.IsNullOrEmpty(def.DefineSymbol) && list.Remove(def.DefineSymbol))
            {
                changed = true;
                Debug.Log($"[MultipleAdsModuleManager] Removed define symbol {def.DefineSymbol} for {moduleId}.");
            }
            if (!string.IsNullOrEmpty(def.SdkInstalledSymbol) && list.Remove(def.SdkInstalledSymbol))
            {
                changed = true;
                Debug.Log($"[MultipleAdsModuleManager] Removed define symbol {def.SdkInstalledSymbol} for {moduleId}.");
            }
            if (changed)
            {
                MultipleAdsBuildTargetCompat.SetScriptingDefineSymbols(targetGroup, list);
            }

            string moduleDir = MultipleAdsPaths.GetModuleDirectory(moduleId);
            if (Directory.Exists(moduleDir))
            {
                AssetDatabase.DeleteAsset(moduleDir);
                Debug.Log($"[MultipleAdsModuleManager] Deleted module folder: {moduleDir}");
            }

            string configPath = MultipleAdsPaths.GetProviderConfigAssetPath($"{moduleId}Config");
            if (File.Exists(configPath))
            {
                AssetDatabase.DeleteAsset(configPath);
                Debug.Log($"[MultipleAdsModuleManager] Cleaned provider config asset: {configPath}");
            }

            if (!string.IsNullOrEmpty(def.PackageId))
            {
                MultipleAdsPackageManager.RemovePackage(def.PackageId);
            }

            AssetDatabase.Refresh();
        }

        private static void InstallFromLocalTemplates(AdsModuleDefinition def, string targetDir)
        {
            string templateDir = $"Packages/com.herghys.multipleadsmanager/Editor/ModuleTemplates/{def.Id}";
            if (!Directory.Exists(templateDir))
            {
                string fallbackDir = $"Assets/Plugins/Herghys/MultipleAds/Editor/ModuleTemplates/{def.Id}";
                if (Directory.Exists(fallbackDir))
                    templateDir = fallbackDir;
            }

            string configTemplate = Path.Combine(templateDir, $"{def.ConfigFileName}.txt").Replace('\\', '/');
            string providerTemplate = Path.Combine(templateDir, $"{def.ProviderFileName}.txt").Replace('\\', '/');

            string dstConfig = Path.Combine(targetDir, def.ConfigFileName).Replace('\\', '/');
            string dstProvider = Path.Combine(targetDir, def.ProviderFileName).Replace('\\', '/');

            if (File.Exists(configTemplate))
            {
                File.Copy(configTemplate, dstConfig, true);
            }

            if (File.Exists(providerTemplate))
            {
                File.Copy(providerTemplate, dstProvider, true);
            }
        }

        private static void DownloadFromRemote(AdsModuleDefinition def, string targetDir, Action<bool> onComplete)
        {
            string urlPattern = GetRemoteUrlPattern();
            string configUrl = string.Format(urlPattern, def.Id, def.ConfigFileName);
            string providerUrl = string.Format(urlPattern, def.Id, def.ProviderFileName);

            Debug.Log($"[MultipleAdsModuleManager] Downloading {def.Id} from remote branch: {configUrl}");

            var configReq = UnityWebRequest.Get(configUrl);
            var asyncOp = configReq.SendWebRequest();

            asyncOp.completed += op =>
            {
                if (configReq.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[MultipleAdsModuleManager] Failed to download {def.ConfigFileName} from remote: {configReq.error}. Falling back to local template.");
                    InstallFromLocalTemplates(def, targetDir);
                    AssetDatabase.Refresh();
                    onComplete?.Invoke(true);
                    return;
                }

                string configCode = configReq.downloadHandler.text;
                File.WriteAllText(Path.Combine(targetDir, def.ConfigFileName).Replace('\\', '/'), configCode);

                var providerReq = UnityWebRequest.Get(providerUrl);
                var providerAsync = providerReq.SendWebRequest();

                providerAsync.completed += pOp =>
                {
                    if (providerReq.result == UnityWebRequest.Result.Success)
                    {
                        string providerCode = providerReq.downloadHandler.text;
                        File.WriteAllText(Path.Combine(targetDir, def.ProviderFileName).Replace('\\', '/'), providerCode);
                        Debug.Log($"[MultipleAdsModuleManager] Successfully downloaded module '{def.DisplayName}' from remote branch.");
                    }
                    else
                    {
                        Debug.LogWarning($"[MultipleAdsModuleManager] Failed to download {def.ProviderFileName} from remote. Falling back to local template.");
                        InstallFromLocalTemplates(def, targetDir);
                    }

                    AssetDatabase.Refresh();
                    onComplete?.Invoke(true);
                };
            };
        }
    }
}
