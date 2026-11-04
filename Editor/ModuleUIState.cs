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
                _cachedStates = ScanAll(targetGroup, clearOverrides: false);
            }
            return _cachedStates;
        }

        public static List<ModuleUIState> GetFastInitialStates(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            return ScanAll(targetGroup, clearOverrides: false);
        }

        public static List<ModuleUIState> ScanAll(BuildTargetGroup targetGroup = BuildTargetGroup.Unknown)
        {
            return ScanAll(targetGroup, clearOverrides: false);
        }

        public static List<ModuleUIState> ScanAll(BuildTargetGroup targetGroup, bool clearOverrides)
        {
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            var result = new List<ModuleUIState>();

            MultipleAdsPackageManager.Scan();

            var allModules = MultipleAdsModuleManager.GetAllModules();
            if (clearOverrides)
            {
                foreach (var mod in allModules)
                {
                    if (mod != null)
                    {
                        MultipleAdsPackageManager.ClearSdkOverride(mod.Id);
                    }
                }
            }

            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                MultipleAdsPackageManager.SyncSdkInstalledDefines(targetGroup);
            }

            var defineSet = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup));

            foreach (var mod in allModules)
            {
                if (mod == null) continue;

                bool isAdded = MultipleAdsModuleManager.IsModuleAdded(mod.Id);
                bool hasDefine = mod.IsDefault || (!string.IsNullOrEmpty(mod.DefineSymbol) && defineSet.Contains(mod.DefineSymbol));
                bool isInstalled = mod.IsDefault || MultipleAdsPackageManager.IsSdkInstalledAuto(mod);
                bool? sdkOverride = MultipleAdsPackageManager.GetSdkOverride(mod.Id);
                bool isEffective = mod.IsDefault || (sdkOverride.HasValue ? sdkOverride.Value : isInstalled);

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
                if (config == null && (isEffective || isAdded || hasDefine || mod.IsDefault))
                {
                    if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    {
                        config = MultipleAdsConfigGenerator.EnsureProviderConfig(mod);
                    }
                    else
                    {
                        MultipleAdsConfigGenerator.RequestAutoRescan();
                    }
                }

                bool testAdsActive = CheckIfNetworkUsesTestAds(mod.NetworkType);

                result.Add(new ModuleUIState
                {
                    Definition = mod,
                    IsAdded = isAdded,
                    HasDefine = hasDefine,
                    AutoDetected = isInstalled,
                    SdkOverride = sdkOverride,
                    IsEffectiveInstalled = isEffective,
                    Config = config,
                    HasTestAdsActive = testAdsActive,
                    Scanned = true
                });
            }

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
            if (targetGroup == BuildTargetGroup.Unknown)
                targetGroup = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            MultipleAdsPackageManager.Scan();
            MultipleAdsPackageManager.ClearSdkOverride(Definition.Id);

            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                MultipleAdsPackageManager.SyncSdkInstalledDefines(targetGroup);
            }

            var defineSet = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(targetGroup));

            IsAdded = MultipleAdsModuleManager.IsModuleAdded(Definition.Id);
            HasDefine = Definition.IsDefault || (!string.IsNullOrEmpty(Definition.DefineSymbol) && defineSet.Contains(Definition.DefineSymbol));
            bool isInstalled = Definition.IsDefault || MultipleAdsPackageManager.IsSdkInstalledAuto(Definition);

            SdkOverride = null;
            AutoDetected = isInstalled;
            IsEffectiveInstalled = isInstalled;

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
            if (Config == null && (isInstalled || IsAdded || HasDefine || Definition.IsDefault))
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    Config = MultipleAdsConfigGenerator.EnsureProviderConfig(Definition);
                }
                else
                {
                    MultipleAdsConfigGenerator.RequestAutoRescan();
                }
            }

            HasTestAdsActive = CheckIfNetworkUsesTestAds(Definition.NetworkType);
            Scanned = true;

            MultipleAdsConfigGenerator.RefreshAllOpenWindows();

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
