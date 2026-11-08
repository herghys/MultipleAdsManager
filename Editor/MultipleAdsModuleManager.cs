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
            },
            new AdsModuleDefinition
            {
                Id = "UnityAds",
                DisplayName = "Unity Ads",
                Description = "Unity Ads monetization wrapper supporting Game ID initialization, banner, interstitial, and rewarded ads.",
                DefineSymbol = "UNITY_ADS",
                SdkInstalledSymbol = "UNITY_ADS_SDK_INSTALLED",
                SdkTypeCheck = "UnityEngine.Advertisements.Advertisement",
                NetworkType = AdNetworkType.UnityAds,
                IsDefault = false,
                PackageId = "com.unity.ads",
                PackageVersion = "4.19.0",
                ScopedRegistry = null
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

            if (IsModuleScriptOutdated(def, targetDir))
            {
                if (PatchModule(def, targetDir))
                {
                    Debug.Log($"[MultipleAdsModuleManager] Auto-patched outdated module '{def.DisplayName}' files to match package version {MultipleAdsUpdateManager.CurrentVersion}.");
                    return true;
                }
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

            MultipleAdsPackageManager.ClearSdkOverride(def.Id);

            bool packageAdded = false;
            if (autoInstallSdk && !string.IsNullOrEmpty(def.PackageId))
            {
                packageAdded = MultipleAdsPackageManager.AddPackage(def.PackageId, def.PackageVersion, def.ScopedRegistry, resolveImmediately: false);
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

            MultipleAdsPackageManager.Scan();

            BuildTargetGroup targetGroup = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            var definesList = MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup);
            bool definesChanged = false;
            if (!string.IsNullOrEmpty(def.DefineSymbol) && !definesList.Contains(def.DefineSymbol))
            {
                definesList.Add(def.DefineSymbol);
                definesChanged = true;
            }
            if ((autoInstallSdk || MultipleAdsPackageManager.IsSdkInstalled(def)) && !string.IsNullOrEmpty(def.SdkInstalledSymbol) && !definesList.Contains(def.SdkInstalledSymbol))
            {
                if (MultipleAdsPackageManager.IsSdkInstalled(def))
                {
                    definesList.Add(def.SdkInstalledSymbol);
                    definesChanged = true;
                }
            }
            if (definesChanged)
            {
                MultipleAdsBuildTargetCompat.SetScriptingDefineSymbols(targetGroup, definesList);
            }

            MultipleAdsConfigGenerator.RequestAutoRescan();

            if (downloadFromRemote)
            {
                DownloadFromRemote(def, targetDir, ok =>
                {
                    MultipleAdsConfigGenerator.RequestAutoRescan();
                    if (packageAdded)
                    {
                        UnityEditor.PackageManager.Client.Resolve();
                    }
                    MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true);
                    ModuleUIState.ScanAll(targetGroup, clearOverrides: false);
                    MultipleAdsConfigGenerator.RefreshAllOpenWindows();
                    onComplete?.Invoke(ok);
                });
            }
            else
            {
                InstallFromLocalTemplates(def, targetDir);
                AssetDatabase.Refresh();
                if (packageAdded)
                {
                    UnityEditor.PackageManager.Client.Resolve();
                }
                Debug.Log($"[MultipleAdsModuleManager] Successfully added module '{def.DisplayName}' into {targetDir}.");
                MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true);
                ModuleUIState.ScanAll(targetGroup, clearOverrides: false);
                MultipleAdsConfigGenerator.RefreshAllOpenWindows();
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

            MultipleAdsPackageManager.ClearSdkOverride(def.Id);

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

            MultipleAdsConfigGenerator.RequestAutoRescan();
            AssetDatabase.Refresh();
            ModuleUIState.ScanAll(targetGroup, clearOverrides: false);
            MultipleAdsConfigGenerator.RefreshAllOpenWindows();
        }

        public static string GetModuleTemplateDirectory(string moduleId)
        {
            // 1. Try from resolved package path (handles Library/PackageCache and embedded Packages)
            string pkgRoot = MultipleAdsPaths.GetResolvedPackagePath();
            if (!string.IsNullOrEmpty(pkgRoot))
            {
                string p1 = Path.Combine(pkgRoot, "Editor", "ModuleTemplates", moduleId).Replace('\\', '/');
                if (Directory.Exists(p1)) return p1;

                string p2 = Path.Combine(pkgRoot, "ModuleTemplates", moduleId).Replace('\\', '/');
                if (Directory.Exists(p2)) return p2;
            }

            // 2. Direct package path
            string direct = $"Packages/com.herghys.multipleadsmanager/Editor/ModuleTemplates/{moduleId}";
            if (Directory.Exists(direct)) return direct;
            string fullDirect = Path.GetFullPath(direct).Replace('\\', '/');
            if (Directory.Exists(fullDirect)) return fullDirect;

            // 3. Fallback: Assets/Plugins legacy
            string fallbackDir = $"Assets/Plugins/Herghys/MultipleAds/Editor/ModuleTemplates/{moduleId}";
            if (Directory.Exists(fallbackDir)) return fallbackDir;

            // 4. Fallback: Search AssetDatabase for any module template folder
            try
            {
                string[] guids = AssetDatabase.FindAssets($"{moduleId}Provider t:TextAsset");
                if (guids != null && guids.Length > 0)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                    string fullDir = Path.GetFullPath(dir).Replace('\\', '/');
                    if (Directory.Exists(fullDir)) return fullDir;
                    if (Directory.Exists(dir)) return dir;
                }
            }
            catch { }

            return direct;
        }

        public static string GetModuleTemplateFile(string moduleId, string fileName)
        {
            string dir = GetModuleTemplateDirectory(moduleId);
            if (!string.IsNullOrEmpty(dir))
            {
                string path = Path.Combine(dir, fileName).Replace('\\', '/');
                if (File.Exists(path)) return path;
            }

            // Search AssetDatabase by file name if still not found
            try
            {
                string searchName = Path.GetFileNameWithoutExtension(fileName);
                string[] guids = AssetDatabase.FindAssets(searchName);
                foreach (var guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        string fullPath = Path.GetFullPath(assetPath).Replace('\\', '/');
                        if (File.Exists(fullPath)) return fullPath;
                        if (File.Exists(assetPath)) return assetPath;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            return code.Replace("\r\n", "\n").Trim();
        }

        private static void InstallFromLocalTemplates(AdsModuleDefinition def, string targetDir)
        {
            string configTemplate = GetModuleTemplateFile(def.Id, $"{def.ConfigFileName}.txt");
            string providerTemplate = GetModuleTemplateFile(def.Id, $"{def.ProviderFileName}.txt");

            string dstConfig = Path.Combine(targetDir, def.ConfigFileName).Replace('\\', '/');
            string dstProvider = Path.Combine(targetDir, def.ProviderFileName).Replace('\\', '/');

            if (!string.IsNullOrEmpty(configTemplate) && File.Exists(configTemplate))
            {
                File.Copy(configTemplate, dstConfig, true);
            }

            if (!string.IsNullOrEmpty(providerTemplate) && File.Exists(providerTemplate))
            {
                File.Copy(providerTemplate, dstProvider, true);
            }
        }

        private const string PrefInstalledModulesVersion = "MultipleAds_InstalledModulesVersion";
        public static string ModulesVersionFilePath => Path.Combine(MultipleAdsPaths.GetModulesDirectory(), ".version").Replace('\\', '/');

        public static string GetInstalledModulesVersion()
        {
            if (File.Exists(ModulesVersionFilePath))
            {
                try
                {
                    string ver = File.ReadAllText(ModulesVersionFilePath).Trim();
                    if (!string.IsNullOrEmpty(ver)) return ver;
                }
                catch { }
            }
            return EditorPrefs.GetString(PrefInstalledModulesVersion, "");
        }

        public static void SetInstalledModulesVersion(string version)
        {
            EditorPrefs.SetString(PrefInstalledModulesVersion, version ?? "");
            try
            {
                string dir = MultipleAdsPaths.GetModulesDirectory();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(ModulesVersionFilePath, version ?? "");
            }
            catch { }
        }

        public static bool IsModuleScriptOutdated(AdsModuleDefinition def, string targetDir)
        {
            if (def == null || def.IsDefault) return false;

            string dstProvider = Path.Combine(targetDir, def.ProviderFileName).Replace('\\', '/');
            if (!File.Exists(dstProvider)) return true;

            string providerCode = File.ReadAllText(dstProvider);

            // 1. Check if provider code is missing key interface methods
            if (!providerCode.Contains("ShowInterstitial(Action onClosed") ||
                !providerCode.Contains("ShowInterstitial(Action onOpened, Action onClosed)") ||
                !providerCode.Contains("ShowRewarded(Action onRewardEarned, Action onOpened, Action onClosed)"))
            {
                return true;
            }

            // 2. Compare against package template content
            string providerTemplate = GetModuleTemplateFile(def.Id, $"{def.ProviderFileName}.txt");
            if (!string.IsNullOrEmpty(providerTemplate) && File.Exists(providerTemplate))
            {
                string templateCode = File.ReadAllText(providerTemplate);
                if (NormalizeCode(providerCode) != NormalizeCode(templateCode))
                {
                    return true;
                }
            }

            // 3. Check package version comparison
            string currentPkgVersion = MultipleAdsUpdateManager.CurrentVersion;
            string recordedVersion = GetInstalledModulesVersion();
            if (!string.IsNullOrEmpty(recordedVersion) && MultipleAdsUpdateManager.CompareVersions(currentPkgVersion, recordedVersion) > 0)
            {
                return true;
            }

            return false;
        }

        public static bool PatchModule(AdsModuleDefinition def, string targetDir, bool createBackup = true, bool force = false)
        {
            if (def == null || def.IsDefault) return false;

            string providerTemplate = GetModuleTemplateFile(def.Id, $"{def.ProviderFileName}.txt");
            string configTemplate = GetModuleTemplateFile(def.Id, $"{def.ConfigFileName}.txt");

            if (string.IsNullOrEmpty(providerTemplate) || !File.Exists(providerTemplate))
            {
                Debug.LogWarning($"[MultipleAdsModuleManager] Provider template not found for module '{def.Id}'.");
                return false;
            }

            string dstProvider = Path.Combine(targetDir, def.ProviderFileName).Replace('\\', '/');
            string dstConfig = Path.Combine(targetDir, def.ConfigFileName).Replace('\\', '/');

            bool patched = false;

            string newProviderCode = File.ReadAllText(providerTemplate);
            string oldProviderCode = File.Exists(dstProvider) ? File.ReadAllText(dstProvider) : "";

            if (force || NormalizeCode(oldProviderCode) != NormalizeCode(newProviderCode))
            {
                if (createBackup && File.Exists(dstProvider))
                {
                    try
                    {
                        File.Copy(dstProvider, dstProvider + ".bak", true);
                    }
                    catch { }
                }

                File.WriteAllText(dstProvider, newProviderCode);
                patched = true;
            }

            if (!string.IsNullOrEmpty(configTemplate) && File.Exists(configTemplate))
            {
                string newConfigCode = File.ReadAllText(configTemplate);
                string oldConfigCode = File.Exists(dstConfig) ? File.ReadAllText(dstConfig) : "";

                if (force || NormalizeCode(oldConfigCode) != NormalizeCode(newConfigCode))
                {
                    if (createBackup && File.Exists(dstConfig))
                    {
                        try
                        {
                            File.Copy(dstConfig, dstConfig + ".bak", true);
                        }
                        catch { }
                    }

                    File.WriteAllText(dstConfig, newConfigCode);
                    patched = true;
                }
            }

            return patched;
        }

        public static int CheckAndPatchInstalledModules(bool force = false, bool verbose = true)
        {
            int patchedCount = 0;
            string currentPkgVersion = MultipleAdsUpdateManager.CurrentVersion;

            foreach (var module in AllModules)
            {
                if (module == null || module.IsDefault) continue;

                string targetDir = MultipleAdsPaths.GetModuleDirectory(module.Id);
                if (!Directory.Exists(targetDir)) continue;

                string dstProvider = Path.Combine(targetDir, module.ProviderFileName).Replace('\\', '/');
                if (!File.Exists(dstProvider) && !force) continue;

                bool needsPatch = force || IsModuleScriptOutdated(module, targetDir);
                if (needsPatch)
                {
                    if (PatchModule(module, targetDir, createBackup: true, force: force))
                    {
                        patchedCount++;
                        if (verbose)
                        {
                            Debug.Log($"[MultipleAdsModuleManager] Regenerated module '{module.DisplayName}' scripts from template to match package version {currentPkgVersion}.");
                        }
                    }
                }
            }

            if (patchedCount > 0 || force)
            {
                SetInstalledModulesVersion(currentPkgVersion);
                AssetDatabase.Refresh();
            }

            return patchedCount;
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
