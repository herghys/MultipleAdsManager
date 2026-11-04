using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsOptimizationWindow : MultipleAdsEditorWindowBase
    {
        private const string SymbolGoogleAds = "GOOGLE_ADS";
        private const string SymbolLevelPlay = "LEVELPLAY_ADS";
        private const string SymbolAppLovin = "APPLOVIN_ADS";
        private const string SymbolVungle = "VUNGLE_ADS";
        private const string SymbolUnityAds = "UNITY_ADS";

        private void OnGUI()
        {
            if (mainConfig == null) LoadOrCreateConfig();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawSharedHeader("Optimization", "Android Optimization & ProGuard", 
                "Strip inactive mediation adapter DEX dependencies, inject modular ProGuard rules, and remove unwanted permissions.");

            DrawFullOptimizationUI(mainConfig, serializedConfig, isStandaloneWindow: true);

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        public static void DrawFullOptimizationUI(MultipleAdsConfig config, SerializedObject serializedConfig, bool isStandaloneWindow = false)
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("MultipleAdsManager Config asset is missing.", MessageType.Warning);
                if (GUILayout.Button("Regenerate MultipleAdsManager Config", GUILayout.Height(24)))
                {
                    MultipleAdsConfigGenerator.RegenerateConfig(showDialog: true);
                    var setupWin = Resources.FindObjectsOfTypeAll<MultipleAdsSetupWindow>().FirstOrDefault();
                    if (setupWin != null)
                    {
                        setupWin.LoadOrCreateConfig();
                        setupWin.Repaint();
                    }
                }
                return;
            }

            var defineList = new HashSet<string>(MultipleAdsBuildTargetCompat.GetDefinesList(MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup));

            bool hasGoogle = defineList.Contains(SymbolGoogleAds);
            bool hasLevelPlay = defineList.Contains(SymbolLevelPlay);
            bool hasAppLovin = defineList.Contains(SymbolAppLovin);
            bool hasVungle = defineList.Contains(SymbolVungle);
            bool hasUnityAds = defineList.Contains(SymbolUnityAds);

            // 1. Adapter DEX Status
            EditorGUILayout.LabelField("Android DEX & Adapter Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Mediation Adapter DEX Status (Strip inactive from build):", EditorStyles.miniBoldLabel);
            DrawAdapterStatus("LevelPlay AdMob Adapter", hasLevelPlay && hasGoogle);
            DrawAdapterStatus("LevelPlay AppLovin Adapter", hasLevelPlay && hasAppLovin);
            DrawAdapterStatus("LevelPlay UnityAds Adapter", hasLevelPlay && hasUnityAds);
            DrawAdapterStatus("Google Mobile Ads Standalone SDK", hasGoogle);
            DrawAdapterStatus("LevelPlay Core SDK", hasLevelPlay);
            DrawAdapterStatus("Unity Ads Standalone SDK", hasUnityAds);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);

            // 2. Android Manifest Stripping Toggles
            DrawManifestStrippingSection(config);

            EditorGUILayout.Space(6);

            // 3. ProGuard / R8 Rules Toggles
            DrawProguardRulesSection(config, hasGoogle, hasLevelPlay, hasAppLovin, hasVungle, hasUnityAds);

            EditorGUILayout.Space(6);

            // 4. Template Source & Asset Override Info
            DrawTemplatesSourceSection();

            EditorGUILayout.Space(8);

            // 5. Action Button
            if (GUILayout.Button("Optimize Android Dependencies, Manifest & ProGuard", GUILayout.Height(30)))
            {
                MultipleAdsAndroidOptimizer.OptimizeAll(showDialog: true);
            }
        }

        public static void DrawManifestStrippingSection(MultipleAdsConfig config)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Android Manifest Permission Stripping (tools:node=\"remove\")", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
            {
                Undo.RecordObject(config, "Select All Manifest Stripping");
                config.StripFineLocation = true;
                config.StripCoarseLocation = true;
                config.StripPhoneState = true;
                config.StripExternalStorage = true;
                config.StripAdvertisingId = true;
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButtonMid, GUILayout.Width(75)))
            {
                Undo.RecordObject(config, "Deselect All Manifest Stripping");
                config.StripFineLocation = false;
                config.StripCoarseLocation = false;
                config.StripPhoneState = false;
                config.StripExternalStorage = false;
                config.StripAdvertisingId = false;
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Default", EditorStyles.miniButtonRight, GUILayout.Width(55)))
            {
                Undo.RecordObject(config, "Reset Default Manifest Stripping");
                config.StripFineLocation = true;
                config.StripCoarseLocation = true;
                config.StripPhoneState = true;
                config.StripExternalStorage = true;
                config.StripAdvertisingId = false;
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Prevents third-party ad SDKs from injecting unwanted Android permissions into final APK/AAB:", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            bool fine = DrawPermissionToggle(
                "Strip ACCESS_FINE_LOCATION",
                "<uses-permission android:name=\"android.permission.ACCESS_FINE_LOCATION\" tools:node=\"remove\" />",
                config.StripFineLocation,
                "Removes precise GPS location permission injected by ad SDKs."
            );

            bool coarse = DrawPermissionToggle(
                "Strip ACCESS_COARSE_LOCATION",
                "<uses-permission android:name=\"android.permission.ACCESS_COARSE_LOCATION\" tools:node=\"remove\" />",
                config.StripCoarseLocation,
                "Removes network-based location permission injected by ad SDKs."
            );

            bool phone = DrawPermissionToggle(
                "Strip READ_PHONE_STATE",
                "<uses-permission android:name=\"android.permission.READ_PHONE_STATE\" tools:node=\"remove\" />",
                config.StripPhoneState,
                "Removes telephony state / device ID permission injected by ad SDKs."
            );

            bool storage = DrawPermissionToggle(
                "Strip WRITE_EXTERNAL_STORAGE",
                "<uses-permission android:name=\"android.permission.WRITE_EXTERNAL_STORAGE\" tools:node=\"remove\" />",
                config.StripExternalStorage,
                "Removes external storage write permission injected by legacy ad SDKs."
            );

            bool isChildTarget = config.TargetAdsForChildren;
            string adIdNote = isChildTarget 
                ? " (Auto-stripped: TargetAdsForChildren is enabled)" 
                : " (Google Play Families Policy / COPPA)";

            bool adId = DrawPermissionToggle(
                "Strip AD_ID (Google Advertising ID)" + adIdNote,
                "<uses-permission android:name=\"com.google.android.gms.permission.AD_ID\" tools:node=\"remove\" />",
                config.StripAdvertisingId || isChildTarget,
                "Removes Google Advertising ID permission for child-directed compliance or privacy."
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Modify Manifest Stripping Toggles");
                config.StripFineLocation = fine;
                config.StripCoarseLocation = coarse;
                config.StripPhoneState = phone;
                config.StripExternalStorage = storage;
                config.StripAdvertisingId = isChildTarget ? config.StripAdvertisingId : adId;
                EditorUtility.SetDirty(config);
            }

            EditorGUILayout.EndVertical();
        }

        public static void DrawProguardRulesSection(MultipleAdsConfig config, bool hasGoogle, bool hasLevelPlay, bool hasAppLovin, bool hasVungle, bool hasUnityAds = false)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ProGuard / R8 Rules Optimizer", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
            {
                Undo.RecordObject(config, "Select All ProGuard Rules");
                config.EnableProguardCore = true;
                config.EnableProguardGoogleAds = true;
                config.EnableProguardLevelPlay = true;
                config.EnableProguardAppLovin = true;
                config.EnableProguardVungle = true;
                config.EnableProguardUnityAds = true;
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButtonMid, GUILayout.Width(75)))
            {
                Undo.RecordObject(config, "Deselect All ProGuard Rules");
                config.EnableProguardCore = false;
                config.EnableProguardGoogleAds = false;
                config.EnableProguardLevelPlay = false;
                config.EnableProguardAppLovin = false;
                config.EnableProguardVungle = false;
                config.EnableProguardUnityAds = false;
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("Sync SDKs", EditorStyles.miniButtonRight, GUILayout.Width(70)))
            {
                Undo.RecordObject(config, "Sync ProGuard Rules to Active SDKs");
                config.EnableProguardCore = true;
                config.EnableProguardGoogleAds = hasGoogle;
                config.EnableProguardLevelPlay = hasLevelPlay;
                config.EnableProguardAppLovin = hasAppLovin;
                config.EnableProguardVungle = hasVungle;
                config.EnableProguardUnityAds = hasUnityAds;
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Controls which modular ProGuard / R8 keep rules to inject into Assets/Plugins/Android/proguard-user.txt:", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            bool core = DrawRuleToggle(
                "Core ProGuard Rules",
                "-keepattributes *Annotation*, SourceFile, LineNumberTable & IInterface methods",
                config.EnableProguardCore,
                null
            );

            bool google = DrawRuleToggle(
                "Google Mobile Ads Rules",
                "Keep com.google.unity.**, com.google.android.gms.ads.**",
                config.EnableProguardGoogleAds,
                hasGoogle ? "GOOGLE_ADS" : "Not Active"
            );

            bool levelPlay = DrawRuleToggle(
                "Unity LevelPlay Mediation Rules",
                "Keep IronSourceWebView$JSInterface, com.ironsource.mediationsdk.**",
                config.EnableProguardLevelPlay,
                hasLevelPlay ? "LEVELPLAY_ADS" : "Not Active"
            );

            bool appLovin = DrawRuleToggle(
                "AppLovin MAX Rules",
                "Keep & dontwarn com.applovin.**",
                config.EnableProguardAppLovin,
                hasAppLovin ? "APPLOVIN_ADS" : "Not Active"
            );

            bool vungle = DrawRuleToggle(
                "Vungle / Liftoff Rules",
                "Keep & dontwarn com.vungle.**",
                config.EnableProguardVungle,
                hasVungle ? "VUNGLE_ADS" : "Not Active"
            );

            bool unityAds = DrawRuleToggle(
                "Unity Ads Rules",
                "Keep & dontwarn com.unity3d.ads.**, com.unity3d.services.**",
                config.EnableProguardUnityAds,
                hasUnityAds ? "UNITY_ADS" : "Not Active"
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Modify ProGuard Rules Toggles");
                config.EnableProguardCore = core;
                config.EnableProguardGoogleAds = google;
                config.EnableProguardLevelPlay = levelPlay;
                config.EnableProguardAppLovin = appLovin;
                config.EnableProguardVungle = vungle;
                config.EnableProguardUnityAds = unityAds;
                EditorUtility.SetDirty(config);
            }

            EditorGUILayout.EndVertical();
        }

        public static void DrawTemplatesSourceSection()
        {
            EditorGUILayout.LabelField("Package Template Storage", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string packageTemplates = MultipleAdsPaths.TemplatesPath;
            bool exists = Directory.Exists(packageTemplates);

            EditorGUILayout.LabelField($"• Templates Location: {packageTemplates} {(exists ? "(Found)" : "(Missing)")}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Manifest & ProGuard templates are bundled directly inside the MultipleAds package.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Select Package Templates Folder", GUILayout.Height(22)))
            {
                var folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MultipleAdsPaths.TemplatesPath);
                if (folderAsset != null)
                {
                    EditorGUIUtility.PingObject(folderAsset);
                    Selection.activeObject = folderAsset;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static bool DrawPermissionToggle(string label, string xmlSnippet, bool currentValue, string tooltip)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            bool newValue = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), currentValue, EditorStyles.boldLabel);
            GUILayout.Label(newValue ? "Stripping Active" : "Retained", EditorStyles.miniBoldLabel, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(xmlSnippet, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
            return newValue;
        }

        private static bool DrawRuleToggle(string label, string description, bool currentValue, string defineBadge)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            bool newValue = EditorGUILayout.ToggleLeft(new GUIContent(label, description), currentValue, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(defineBadge))
            {
                GUILayout.Label($"[{defineBadge}]", EditorStyles.miniLabel, GUILayout.Width(100));
            }

            GUILayout.Label(newValue ? "Enabled" : "Skipped", EditorStyles.miniBoldLabel, GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
            return newValue;
        }

        private static void DrawAdapterStatus(string name, bool isIncluded)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(isIncluded ? "Included" : "Stripped", EditorStyles.boldLabel, GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();
        }
    }
}
