using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class ModuleUIState
    {
        public AdsModuleDefinition Definition;
        public bool IsAdded;
        public bool HasDefine;
        public bool AutoDetected;
        public bool? SdkOverride;
        public bool IsEffectiveInstalled;
        public BaseAdConfig Config;
        public bool HasTestAdsActive;
        public bool Scanned;

        private static List<ModuleUIState> _cachedStates = null;

        public static List<ModuleUIState> GetStates(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown, bool forceRescan = false)
        {
            if (forceRescan || _cachedStates == null)
            {
                if (forceRescan)
                {
                    _cachedStates = ScanAll(targetGroup);
                }
                else
                {
                    _cachedStates = GetFastInitialStates(targetGroup);
                }
            }
            return _cachedStates;
        }

        public static List<ModuleUIState> GetFastInitialStates(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            var result = new List<ModuleUIState>();

            var defineSet = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup));

            var allModules = MultipleAdsModuleManager.GetAllModules();
            foreach (var mod in allModules)
            {
                if (mod == null) continue;

                bool isAdded = mod.IsDefault || Directory.Exists(MultipleAdsPaths.GetModuleDirectory(mod.Id));
                bool hasDefine = mod.IsDefault || (!string.IsNullOrEmpty(mod.DefineSymbol) && defineSet.Contains(mod.DefineSymbol));
                bool? sdkOverride = MultipleAdsPackageManager.GetSdkOverride(mod.Id);

                string configPath = MultipleAdsPaths.GetProviderConfigAssetPath($"{mod.Id}Config");
                var config = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(configPath);

                result.Add(new ModuleUIState
                {
                    Definition = mod,
                    IsAdded = isAdded,
                    HasDefine = hasDefine,
                    AutoDetected = mod.IsDefault,
                    SdkOverride = sdkOverride,
                    IsEffectiveInstalled = sdkOverride.HasValue ? sdkOverride.Value : (mod.IsDefault),
                    Config = config,
                    HasTestAdsActive = CheckIfNetworkUsesTestAds(mod.NetworkType),
                    Scanned = mod.IsDefault // Default mock module needs no scan
                });
            }

            return result;
        }

        public static List<ModuleUIState> ScanAll(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            var result = new List<ModuleUIState>();

            MultipleAdsPackageManager.Scan();

            var defineSet = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup));

            var allModules = MultipleAdsModuleManager.GetAllModules();
            foreach (var mod in allModules)
            {
                if (mod == null) continue;

                bool isAdded = MultipleAdsModuleManager.IsModuleAdded(mod.Id);
                bool hasDefine = !string.IsNullOrEmpty(mod.DefineSymbol) && defineSet.Contains(mod.DefineSymbol);
                bool isInstalled = mod.IsDefault || MultipleAdsPackageManager.IsSdkInstalledAuto(mod);
                
                MultipleAdsPackageManager.ClearSdkOverride(mod.Id);

                string configPath = MultipleAdsPaths.GetProviderConfigAssetPath($"{mod.Id}Config");
                var config = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(configPath);
                if (config == null)
                {
                    string[] guids = AssetDatabase.FindAssets($"{mod.Id}Config t:ScriptableObject");
                    if (guids.Length > 0)
                    {
                        config = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }

                bool testAdsActive = CheckIfNetworkUsesTestAds(mod.NetworkType);

                result.Add(new ModuleUIState
                {
                    Definition = mod,
                    IsAdded = isAdded,
                    HasDefine = hasDefine,
                    AutoDetected = isInstalled,
                    SdkOverride = null,
                    IsEffectiveInstalled = isInstalled,
                    Config = config,
                    HasTestAdsActive = testAdsActive,
                    Scanned = true
                });
            }

            MultipleAdsPackageManager.SyncSdkInstalledDefines(targetGroup);
            _cachedStates = result;
            return result;
        }

        private static bool CheckIfNetworkUsesTestAds(AdNetworkType network)
        {
            string mainPath = MultipleAdsPaths.GetMainConfigAssetPath();
            var mainConfig = AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(mainPath);
            if (mainConfig != null && mainConfig.AdEntries != null)
            {
                foreach (var entry in mainConfig.AdEntries)
                {
                    if (entry == null || entry.ProviderAdIds == null) continue;
                    var p = entry.GetProviderAdIdEntry(network);
                    if (p != null && p.UseTestAdId) return true;
                }
            }
            return false;
        }

        public void ScanThisModule(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            if (Definition == null) return;

            MultipleAdsPackageManager.Scan();

            var defineSet = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup));

            IsAdded = MultipleAdsModuleManager.IsModuleAdded(Definition.Id);
            HasDefine = !string.IsNullOrEmpty(Definition.DefineSymbol) && defineSet.Contains(Definition.DefineSymbol);
            bool isInstalled = Definition.IsDefault || MultipleAdsPackageManager.IsSdkInstalledAuto(Definition);

            MultipleAdsPackageManager.ClearSdkOverride(Definition.Id);
            SdkOverride = null;
            AutoDetected = isInstalled;
            IsEffectiveInstalled = isInstalled;
            MultipleAdsPackageManager.SyncSdkInstalledDefines(targetGroup);

            string configPath = MultipleAdsPaths.GetProviderConfigAssetPath($"{Definition.Id}Config");
            Config = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(configPath);
            if (Config == null)
            {
                string[] guids = AssetDatabase.FindAssets($"{Definition.Id}Config t:ScriptableObject");
                if (guids.Length > 0)
                {
                    Config = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            HasTestAdsActive = CheckIfNetworkUsesTestAds(Definition.NetworkType);
            Scanned = true;

            string statusMsg = IsEffectiveInstalled ? "Installed / Detected in Project" : "Not Detected in Packages or Assemblies";

            EditorUtility.DisplayDialog($"Scan Module: {Definition.DisplayName}",
                $"Module '{Definition.DisplayName}' scan complete:\n\n" +
                $"• Files Added: {(IsAdded ? "Yes" : "No")}\n" +
                $"• Scripting Define: {(HasDefine ? "Active" : "Inactive")}\n" +
                $"• SDK Package: {(string.IsNullOrEmpty(Definition.PackageId) ? "Built-in" : Definition.PackageId)}\n" +
                $"• SDK Installed: {(IsEffectiveInstalled ? "Yes" : "No")}\n" +
                $"• Status: {statusMsg}\n" +
                $"• Test Ad IDs: {(HasTestAdsActive ? "ACTIVE on Ad Unit(s)" : "Normal / Not active")}",
                "OK");
        }
    }
}
