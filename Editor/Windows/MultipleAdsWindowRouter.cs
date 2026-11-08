using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public static class MultipleAdsWindowRouter
    {
        public static T OpenWindow<T>(EditorWindow currentWindow, string title) where T : EditorWindow
        {
            var existing = Resources.FindObjectsOfTypeAll<T>();
            if (existing != null && existing.Length > 0 && existing[0] != null)
            {
                existing[0].Focus();
                if (currentWindow != null && currentWindow != existing[0] && (currentWindow is MultipleAdsEditorWindowBase || currentWindow is MultipleAdsSetupWindow))
                {
                    currentWindow.Close();
                }
                return existing[0];
            }

            if (currentWindow != null && (currentWindow is MultipleAdsEditorWindowBase || currentWindow is MultipleAdsSetupWindow))
            {
                Rect rect = currentWindow.position;
                currentWindow.Close();
                var newWin = EditorWindow.GetWindow<T>(false, title, true);
                newWin.position = rect;
                newWin.Show();
                newWin.Focus();
                return newWin;
            }

            var win = EditorWindow.GetWindow<T>(false, title, true);
            win.Show();
            win.Focus();
            return win;
        }

        [MenuItem("Tools/MultipleAds/Full Setup", false, 0)]
        public static void OpenSetupWindow() => OpenSetupWindow(null);
        public static void OpenSetupWindow(EditorWindow from) => OpenWindow<MultipleAdsSetupWindow>(from, "Multiple Ads Full Setup");

        public static void SetupAdsManagerOnScene(bool showDialog = true) => MultipleAdsSceneSetup.SetupAdsManagerOnScene(showDialog);

        [MenuItem("Tools/MultipleAds/Global App Settings", false, 20)]
        public static void OpenGlobalSettings() => OpenGlobalSettings(null);
        public static void OpenGlobalSettings(EditorWindow from) => OpenWindow<MultipleAdsGlobalSettingsWindow>(from, "Global Settings");

        [MenuItem("Tools/MultipleAds/Ads Placements", false, 21)]
        public static void OpenPlacements() => OpenPlacements(null);
        public static void OpenPlacements(EditorWindow from) => OpenWindow<MultipleAdsPlacementsWindow>(from, "Ads Placements");

        [MenuItem("Tools/MultipleAds/Ad Network Modules", false, 22)]
        public static void OpenModules() => OpenModules(null);
        public static void OpenModules(EditorWindow from) => OpenWindow<MultipleAdsModulesWindow>(from, "Ad Modules");

        [MenuItem("Tools/MultipleAds/Active Mediation", false, 23)]
        public static void OpenMediation() => OpenMediation(null);
        public static void OpenMediation(EditorWindow from) => OpenWindow<MultipleAdsMediationWindow>(from, "Active Mediation");

        [MenuItem("Tools/MultipleAds/Optimization", false, 24)]
        public static void OpenOptimization() => OpenOptimization(null);
        public static void OpenOptimization(EditorWindow from) => OpenWindow<MultipleAdsOptimizationWindow>(from, "Optimization");

        [MenuItem("Tools/MultipleAds/Scan Packages & Modules", false, 40)]
        public static void ScanPackagesAndModules()
        {
            ModuleUIState.ScanAll(MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup);
            MultipleAdsEditorWindowBase.TriggerManualScan();
        }

        [MenuItem("Tools/MultipleAds/Append & Optimize Android ProGuard", false, 41)]
        public static void OptimizeAndroidFromMenu()
        {
            MultipleAdsAndroidOptimizer.OptimizeAll(showDialog: true);
        }

        [MenuItem("Tools/MultipleAds/Regenerate Config", false, 42)]
        public static void RegenerateConfigMenu()
        {
            var config = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: true);
            if (config != null)
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
            }
        }

        [MenuItem("Tools/MultipleAds/Select MultipleAdsManager Config Asset", false, 43)]
        public static void SelectConfigAsset()
        {
            var config = MultipleAdsConfigGenerator.GetConfig();
            if (config == null)
            {
                bool create = EditorUtility.DisplayDialog(
                    "Config Not Found", 
                    "MultipleAdsManager Config asset was not found.\n\nWould you like to auto-generate a new one now?", 
                    "Yes, Regenerate", 
                    "Cancel"
                );
                if (create)
                {
                    config = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: true);
                }
            }

            if (config != null)
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
            }
        }

        [MenuItem("Tools/MultipleAds/Check for Updates", false, 60)]
        public static void CheckForUpdatesMenu()
        {
            MultipleAdsUpdateManager.CheckForUpdates(silent: false);
        }

        [MenuItem("Tools/MultipleAds/Patch Installed Modules (Repair Scripts)", false, 61)]
        public static void PatchInstalledModulesMenu()
        {
            int patched = MultipleAdsModuleManager.CheckAndPatchInstalledModules(force: true, verbose: true);
            EditorUtility.DisplayDialog(
                "Module Script Patch",
                $"Module synchronization complete.\n\n" +
                $"• Package Version: v{MultipleAdsUpdateManager.CurrentVersion}\n" +
                $"• Patched/updated: {patched} module script(s) in Assets/MultipleAds/Modules/\n" +
                $"• Interface signatures are synchronized and up to date.",
                "OK"
            );
        }
    }
}
