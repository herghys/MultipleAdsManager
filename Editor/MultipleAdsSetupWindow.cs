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
    public class MultipleAdsSetupWindow : EditorWindow
    {
        public const string SymbolGoogleAds = "GOOGLE_ADS";
        public const string SymbolLevelPlay = "LEVELPLAY_ADS";
        public const string SymbolAppLovin = "APPLOVIN_ADS";
        public const string SymbolVungle = "VUNGLE_ADS";
        public const string SymbolUnityAds = "UNITY_ADS";

        private Vector2 scrollPos;
        private MultipleAdsConfig mainConfig;
        private SerializedObject serializedConfig;

        private static GUIStyle rightBoldLabel;
        private static GUIStyle RightBoldLabel
        {
            get
            {
                if (rightBoldLabel == null)
                {
                    rightBoldLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleRight
                    };
                }
                return rightBoldLabel;
            }
        }

        [MenuItem("Window/MultipleAds Full Setup", false, 100)]
        public static void Open()
        {
            var window = GetWindow<MultipleAdsSetupWindow>("Multiple Ads Full Setup");
            window.minSize = new Vector2(540, 680);
            window.Show();
            window.Focus();
        }

        private readonly Dictionary<string, BaseAdConfig> moduleConfigCache = new Dictionary<string, BaseAdConfig>();
        private List<ModuleUIState> moduleStates = null;
        private string newPlacementName = "";

        private const string PrefFoldoutGlobalApp = "MultipleAds_Setup_Foldout_GlobalApp";
        private const string PrefFoldoutPlacements = "MultipleAds_Setup_Foldout_Placements";
        private const string PrefFoldoutAdUnits = "MultipleAds_Setup_Foldout_AdUnits";
        private const string PrefFoldoutModules = "MultipleAds_Setup_Foldout_Modules";
        private const string PrefFoldoutMediation = "MultipleAds_Setup_Foldout_Mediation";
        private const string PrefFoldoutOptimization = "MultipleAds_Setup_Foldout_Optimization";

        private bool foldoutGlobalApp = true;
        private bool foldoutPlacements = true;
        private bool foldoutAdUnits = true;
        private bool foldoutModules = true;
        private bool foldoutMediation = true;
        private bool foldoutOptimization = true;

        private void OnEnable()
        {
            MultipleAdsConfigGenerator.LoadCheck(showDialog: false);
            MultipleAdsUpdateManager.CheckForUpdates(silent: true);
            foldoutGlobalApp = EditorPrefs.GetBool(PrefFoldoutGlobalApp, true);
            foldoutPlacements = EditorPrefs.GetBool(PrefFoldoutPlacements, true);
            foldoutAdUnits = EditorPrefs.GetBool(PrefFoldoutAdUnits, true);
            foldoutModules = EditorPrefs.GetBool(PrefFoldoutModules, true);
            foldoutMediation = EditorPrefs.GetBool(PrefFoldoutMediation, true);
            foldoutOptimization = EditorPrefs.GetBool(PrefFoldoutOptimization, true);

            MultipleAdsModuleManager.EnsureDefaultModules();
            LoadOrCreateConfig();
            SyncConfigsWithActiveDefines(mainConfig, false);
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            moduleStates = ModuleUIState.GetStates(target, forceRescan: false);
        }

        private void SetAllFoldouts(bool state)
        {
            foldoutGlobalApp = state;
            foldoutPlacements = state;
            foldoutAdUnits = state;
            foldoutModules = state;
            foldoutMediation = state;
            foldoutOptimization = state;

            EditorPrefs.SetBool(PrefFoldoutGlobalApp, state);
            EditorPrefs.SetBool(PrefFoldoutPlacements, state);
            EditorPrefs.SetBool(PrefFoldoutAdUnits, state);
            EditorPrefs.SetBool(PrefFoldoutModules, state);
            EditorPrefs.SetBool(PrefFoldoutMediation, state);
            EditorPrefs.SetBool(PrefFoldoutOptimization, state);
        }

        private static GUIStyle sectionHeaderFoldoutStyle;
        private static GUIStyle SectionHeaderFoldoutStyle
        {
            get
            {
                if (sectionHeaderFoldoutStyle == null)
                {
                    sectionHeaderFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 12
                    };
                    sectionHeaderFoldoutStyle.focused.background = null;
                    sectionHeaderFoldoutStyle.active.background = null;
                    sectionHeaderFoldoutStyle.onFocused.background = null;
                    sectionHeaderFoldoutStyle.onActive.background = null;
                    sectionHeaderFoldoutStyle.focused.textColor = sectionHeaderFoldoutStyle.normal.textColor;
                    sectionHeaderFoldoutStyle.onFocused.textColor = sectionHeaderFoldoutStyle.onNormal.textColor;
                    sectionHeaderFoldoutStyle.active.textColor = sectionHeaderFoldoutStyle.normal.textColor;
                    sectionHeaderFoldoutStyle.onActive.textColor = sectionHeaderFoldoutStyle.onNormal.textColor;
                }
                return sectionHeaderFoldoutStyle;
            }
        }

        private bool DrawSectionFoldout(string title, bool currentState, string prefKey)
        {
            EditorGUILayout.Space(6);
            bool newState = EditorGUILayout.Foldout(currentState, title, true, SectionHeaderFoldoutStyle);

            if (newState != currentState)
            {
                GUI.FocusControl(null);
                EditorPrefs.SetBool(prefKey, newState);
            }
            return newState;
        }

        public void RefreshAfterScan()
        {
            moduleConfigCache.Clear();
            LoadOrCreateConfig();
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            moduleStates = ModuleUIState.GetStates(target, forceRescan: false);
            Repaint();
        }

        public void RescanModules(bool clearOverrides = false)
        {
            moduleConfigCache.Clear();
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            if (clearOverrides)
            {
                ModuleUIState.ScanAll(target, clearOverrides: true);
            }
            MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true);
            moduleStates = ModuleUIState.ScanAll(target, clearOverrides: false);
            LoadOrCreateConfig();
            Repaint();
        }

        public void LoadOrCreateConfig()
        {
            mainConfig = MultipleAdsConfigGenerator.GetConfig();

            if (mainConfig != null)
            {
                serializedConfig = new SerializedObject(mainConfig);
            }
            else
            {
                serializedConfig = null;
            }
        }

        public void RegenerateConfig()
        {
            mainConfig = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: false);
            if (mainConfig != null)
            {
                serializedConfig = new SerializedObject(mainConfig);
                SyncConfigsWithActiveDefines(mainConfig, false);
            }
            Repaint();
        }

        private void OnGUI()
        {
            if (mainConfig == null)
            {
                LoadOrCreateConfig();
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();

            if (mainConfig == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("MultipleAdsManager Config asset is missing. Click 'Regenerate Config' to automatically create and initialize a new configuration asset.", MessageType.Warning);
                if (GUILayout.Button("Regenerate Config", GUILayout.Height(26)))
                {
                    RegenerateConfig();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);
            DrawGlobalAppInfoSection();

            EditorGUILayout.Space(4);
            DrawAdPlacementSection();

            EditorGUILayout.Space(4);
            DrawAdUnitsSection();

            EditorGUILayout.Space(4);
            DrawDefineSection();

            EditorGUILayout.Space(4);
            DrawActiveProvidersSection();

            EditorGUILayout.Space(4);
            DrawAndroidOptimizationSection();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            MultipleAdsEditorWindowBase.DrawUpdateBanner();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("MultipleAds Full Setup", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Scan", "Scan installed SDK packages, active modules, and test ad configurations."), GUILayout.Height(22), GUILayout.Width(65)))
            {
                TriggerManualScan();
            }

            string regenLabel = mainConfig == null ? "Regenerate Config" : "Regenerate";
            string regenTooltip = mainConfig == null ? "Regenerate missing MultipleAdsManager Config asset with default settings." : "Regenerate and re-sync MultipleAdsManager Config asset and active providers.";
            if (GUILayout.Button(new GUIContent(regenLabel, regenTooltip), GUILayout.Height(22)))
            {
                RegenerateConfig();
            }

            if (GUILayout.Button(new GUIContent("Select Config", "Select and edit the MultipleAdsManager Config asset in Inspector."), GUILayout.Height(22)))
            {
                if (mainConfig == null)
                {
                    RegenerateConfig();
                }
                else
                {
                    LoadOrCreateConfig();
                }

                if (mainConfig != null)
                {
                    EditorGUIUtility.PingObject(mainConfig);
                    Selection.activeObject = mainConfig;
                }
            }

            if (GUILayout.Button(new GUIContent("Setup Scene", "Create or select MultipleAdsManager in the active scene."), GUILayout.Height(22)))
            {
                MultipleAdsSceneSetup.SetupAdsManagerOnScene(showDialog: true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                "All-in-One Setup: Click any section header to expand or collapse it.",
                EditorStyles.wordWrappedMiniLabel
            );
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand All", EditorStyles.miniButtonLeft, GUILayout.Width(75), GUILayout.Height(17)))
            {
                SetAllFoldouts(true);
            }
            if (GUILayout.Button("Collapse All", EditorStyles.miniButtonRight, GUILayout.Width(75), GUILayout.Height(17)))
            {
                SetAllFoldouts(false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        public void TriggerManualScan()
        {
            MultipleAdsPackageManager.Scan();
            moduleConfigCache.Clear();
            RescanModules(clearOverrides: true);

            int testCount = 0;
            if (mainConfig != null && mainConfig.AdEntries != null)
            {
                foreach (var entry in mainConfig.AdEntries)
                {
                    if (entry == null || entry.ProviderAdIds == null) continue;
                    foreach (var p in entry.ProviderAdIds)
                    {
                        if (p != null && p.UseTestAdId)
                        {
                            testCount++;
                        }
                    }
                }
            }

            EditorUtility.DisplayDialog("MultipleAds Scan Complete",
                $"Manual scan completed:\n" +
                $"• Scanned Packages/packages-lock.json and manifest.json\n" +
                $"• Verified assembly types for active SDKs\n" +
                $"• Verified ad placements and ad unit mappings\n" +
                $"• Ad Unit Provider(s) using Test Ad IDs: {testCount}", "OK");
        }

        private void DrawGlobalAppInfoSection()
        {
            if (mainConfig == null) return;

            foldoutGlobalApp = DrawSectionFoldout("Global App Information & Privacy", foldoutGlobalApp, PrefFoldoutGlobalApp);
            if (!foldoutGlobalApp) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Application Information (PlayerSettings & Providers)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 110f;

            EditorGUI.BeginChangeCheck();
            string newAppName = DrawTextFieldWithPlaceholder(
                "App Name",
                mainConfig.GlobalAppName,
                "Default Apps",
                "Product / App Name synchronized to PlayerSettings.productName and all individual provider configs."
            );
            string newPkg = DrawTextFieldWithPlaceholder(
                "Package Name",
                mainConfig.GlobalPackageName,
                "com.DefaultCompany.DefaultApps",
                "Bundle ID / Package Name (e.g. com.DefaultCompany.DefaultApps) synchronized to PlayerSettings and all individual provider configs."
            );

            EditorGUIUtility.labelWidth = prevLabelWidth;

            if (EditorGUI.EndChangeCheck())
            {
                mainConfig.GlobalAppName = newAppName;
                mainConfig.GlobalPackageName = newPkg;
                EditorUtility.SetDirty(mainConfig);
                MultipleAdsSdkConfigSynchronizer.SyncGlobalAppInfo(mainConfig);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Sync from Player Settings", "Read current Product Name and Application Identifier from Unity PlayerSettings."), GUILayout.Height(24)))
            {
                MultipleAdsSdkConfigSynchronizer.SyncFromPlayerSettings(mainConfig);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Push to Player Settings", "Push App Name and Package Name to Unity PlayerSettings and all provider configs."), GUILayout.Height(24)))
            {
                MultipleAdsSdkConfigSynchronizer.SyncGlobalAppInfo(mainConfig);
                EditorUtility.DisplayDialog("Global App Info Synchronized", 
                    $"Pushed:\n• App Name: '{mainConfig.GlobalAppName}'\n• Package Name: '{mainConfig.GlobalPackageName}'\n\nAcross PlayerSettings (Android, iOS, Standalone) and all Provider Configs.", "OK");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(1);
            if (GUILayout.Button(new GUIContent("Sync All External SDK Settings (AdMob / LevelPlay / AppLovin)", "Push credentials to third-party SDK configuration assets in Resources."), GUILayout.Height(22)))
            {
                int count = MultipleAdsSdkConfigSynchronizer.SyncAllSdkSettings(mainConfig);
                EditorUtility.DisplayDialog("External SDK Settings Synchronized", 
                    $"Synchronized provider credentials to {count} external SDK configuration asset(s) (GoogleMobileAdsSettings, LevelPlayMediationSettings, AppLovinSettings).", "OK");
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Privacy, COPPA & Mode Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            bool targetChildren = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    " Target ads for children (COPPA)",
                    "Tags ad requests as child-directed across AdMob, LevelPlay, and other networks."
                ),
                mainConfig.TargetAdsForChildren,
                EditorStyles.boldLabel
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mainConfig, "Toggle Target Ads for Children");
                mainConfig.TargetAdsForChildren = targetChildren;
                EditorUtility.SetDirty(mainConfig);
            }

            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            bool mockInEditor = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    " Enable simulated Mock Ads in Editor",
                    "Simulates banners, interstitials, and rewarded ads inside Unity Editor without external SDK calls."
                ),
                mainConfig.EnableMockInEditor
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mainConfig, "Toggle Mock Ads in Editor");
                mainConfig.EnableMockInEditor = mockInEditor;
                EditorUtility.SetDirty(mainConfig);
            }

            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            bool isTestAdsDefined = MultipleAdsBuildTargetCompat.HasDefine(BuildTargetGroup.Unknown, MultipleAdsBuildTargetCompat.TestAdsDefine);
            bool testAdsInEditor = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    " Use Test Ads On Editor (#TEST_ADS)",
                    "When enabled, defines the #TEST_ADS scripting symbol in PlayerSettings so selected ad units use test ad IDs inside the Unity Editor (#UNITY_EDITOR). When disabled, #TEST_ADS is removed and real production ad IDs are used in the Editor."
                ),
                isTestAdsDefined
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mainConfig, "Toggle Test Ads in Editor");
                mainConfig.RawUseTestAdsOnEditor = testAdsInEditor;
                EditorUtility.SetDirty(mainConfig);
                MultipleAdsBuildTargetCompat.SetTestAdsDefine(testAdsInEditor);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Test Device IDs", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (serializedConfig != null)
            {
                serializedConfig.Update();
                var testDeviceIdsProp = serializedConfig.FindProperty("testDeviceIds");
                if (testDeviceIdsProp != null)
                {
                    EditorGUILayout.PropertyField(testDeviceIdsProp, new GUIContent("Hashed Device IDs (GAID / IDFA)"), true);
                }
                serializedConfig.ApplyModifiedProperties();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawAdPlacementSection()
        {
            if (mainConfig == null) return;

            foldoutPlacements = DrawSectionFoldout("Ad Placement", foldoutPlacements, PrefFoldoutPlacements);
            if (!foldoutPlacements) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Predefined Ad Placements Catalog", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var catalog = mainConfig.AdPlacementsCatalog;
            if (catalog == null || catalog.Count == 0)
            {
                EditorGUILayout.HelpBox("Catalog is empty. Add placement names below (e.g. Banner, Interstitial, Rewarded).", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Available Placements ({catalog.Count}):", EditorStyles.miniBoldLabel);

                for (int i = 0; i < catalog.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"• {catalog[i]}", EditorStyles.boldLabel);

                    if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(24), GUILayout.Height(18)))
                    {
                        Undo.RecordObject(mainConfig, "Remove Ad Placement");
                        catalog.RemoveAt(i);
                        EditorUtility.SetDirty(mainConfig);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            newPlacementName = EditorGUILayout.TextField(newPlacementName, GUILayout.Height(20));
            if (GUILayout.Button("+ Add Placement", GUILayout.Width(130), GUILayout.Height(20)))
            {
                if (!string.IsNullOrWhiteSpace(newPlacementName))
                {
                    Undo.RecordObject(mainConfig, "Add Ad Placement");
                    if (catalog == null)
                    {
                        var catalogField = typeof(MultipleAdsConfig).GetField("adPlacementsCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (catalogField != null)
                        {
                            catalogField.SetValue(mainConfig, new List<string>());
                        }
                    }
                    if (!mainConfig.AdPlacementsCatalog.Contains(newPlacementName.Trim()))
                    {
                        mainConfig.AdPlacementsCatalog.Add(newPlacementName.Trim());
                        EditorUtility.SetDirty(mainConfig);
                    }
                    newPlacementName = "";
                    GUIUtility.keyboardControl = 0;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawAdUnitsSection()
        {
            if (mainConfig == null) return;

            foldoutAdUnits = DrawSectionFoldout("Ad Units", foldoutAdUnits, PrefFoldoutAdUnits);
            if (!foldoutAdUnits) return;

            MultipleAdsAdUnitsUI.DrawAdUnits(mainConfig, serializedConfig);
        }

        private static string DrawTextFieldWithPlaceholder(string label, string text, string placeholder, string tooltip = null)
        {
            string controlName = "PlaceholderField_" + label.Replace(" ", "");
            var content = new GUIContent(label, tooltip);
            Rect position = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            Rect fieldRect = EditorGUI.PrefixLabel(position, content);

            bool hasText = !string.IsNullOrEmpty(text);
            Rect inputRect = hasText ? new Rect(fieldRect.x, fieldRect.y, fieldRect.width - 24, fieldRect.height) : fieldRect;
            Rect clearBtnRect = new Rect(fieldRect.xMax - 22, fieldRect.y, 22, fieldRect.height);

            GUI.SetNextControlName(controlName);
            string result = EditorGUI.TextField(inputRect, text);

            if (hasText)
            {
                if (GUI.Button(clearBtnRect, new GUIContent("x", "Clear to empty"), EditorStyles.miniButton))
                {
                    result = "";
                    GUI.FocusControl(null);
                }
            }

            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            if (string.IsNullOrEmpty(result) && !isFocused && !string.IsNullOrEmpty(placeholder))
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var placeholderStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.45f) },
                        fontStyle = FontStyle.Italic,
                        alignment = TextAnchor.MiddleLeft,
                        contentOffset = new Vector2(4, 0)
                    };
                    placeholderStyle.Draw(inputRect, new GUIContent(placeholder), false, false, false, false);
                }
            }

            return result;
        }

        private void DrawDefineSection()
        {
            foldoutModules = DrawSectionFoldout("Ad Network Modules (Assets/MultipleAds/Modules)", foldoutModules, PrefFoldoutModules);
            if (!foldoutModules) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Available Ad Networks", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Scan All Modules", "Scan all packages and assemblies for every module"), EditorStyles.miniButton, GUILayout.Width(130), GUILayout.Height(18)))
            {
                RescanModules(clearOverrides: true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            moduleStates = ModuleUIState.GetStates(target, forceRescan: false);

            if (moduleStates != null)
            {
                foreach (var state in moduleStates)
                {
                    DrawModuleRow(target, state);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawModuleRow(BuildTargetGroup target, ModuleUIState state)
        {
            if (state == null || state.Definition == null) return;
            var module = state.Definition;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(module.DisplayName, EditorStyles.boldLabel);
            string symbolStr = string.IsNullOrEmpty(module.DefineSymbol) ? "Default Built-In" : module.DefineSymbol;
            EditorGUILayout.LabelField($"{module.Description} [{symbolStr}]", EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrEmpty(module.PackageId))
            {
                string statusText = !state.Scanned
                    ? "Not Scanned"
                    : (state.IsEffectiveInstalled ? "Installed" : "Not Installed");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"SDK: {module.PackageId} [{statusText}]", EditorStyles.miniLabel, GUILayout.Width(270));

                if (GUILayout.Button(new GUIContent("Scan", $"Scan package & assembly presence for {module.DisplayName}"), EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(17)))
                {
                    state.ScanThisModule(target);
                    GUIUtility.ExitGUI();
                }

                EditorGUI.BeginChangeCheck();
                bool newToggleVal = EditorGUILayout.ToggleLeft(
                    new GUIContent("SDK Installed", "Manually toggle whether this SDK is considered installed. Allows forcing real SDK code or testing in safe Stub mode."),
                    state.IsEffectiveInstalled,
                    GUILayout.Width(105)
                );
                if (EditorGUI.EndChangeCheck())
                {
                    MultipleAdsPackageManager.SetSdkOverride(module.Id, newToggleVal);
                    MultipleAdsPackageManager.SyncSdkInstalledDefines(target);
                    state.SdkOverride = newToggleVal;
                    state.IsEffectiveInstalled = newToggleVal;
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Built-in Simulator (No external SDK required)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(6);

            EditorGUILayout.BeginVertical(GUILayout.Width(200));

            const float actionBtnWidth = 94f;
            const float singleRowWidth = 192f; // 94 + 4 + 94

            if (module.IsDefault)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Default Module", RightBoldLabel, GUILayout.Height(20));
                GUILayout.Space(6);
                if (state.Config != null)
                {
                    if (GUILayout.Button("Open Config", EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        OpenConfig(state.Config);
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("Add Config", $"Create missing AdsConfig for {module.DisplayName}"), EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        state.Config = MultipleAdsConfigGenerator.EnsureProviderConfig(module, mainConfig, saveAssets: true);
                        if (state.Config != null)
                        {
                            OpenConfig(state.Config);
                        }
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (!state.IsAdded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Not Added", RightBoldLabel, GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add", GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    PromptAddModule(module, downloadFromRemote: false);
                    GUIUtility.ExitGUI();
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Branch ▾", GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add from Local Template"), false, () =>
                    {
                        PromptAddModule(module, downloadFromRemote: false);
                    });
                    menu.AddItem(new GUIContent("Download from Remote Branch..."), false, () =>
                    {
                        PromptAddModule(module, downloadFromRemote: true);
                    });
                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(state.HasDefine ? "Enabled" : "Disabled", RightBoldLabel, GUILayout.Height(20));
                GUILayout.Space(6);

                if (state.Config != null)
                {
                    if (GUILayout.Button("Open Config", EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        OpenConfig(state.Config);
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("Add Config", $"Create missing AdsConfig for {module.DisplayName}"), EditorStyles.miniButton, GUILayout.Width(actionBtnWidth), GUILayout.Height(20)))
                    {
                        state.Config = MultipleAdsConfigGenerator.EnsureProviderConfig(module, mainConfig, saveAssets: true);
                        if (state.Config != null)
                        {
                            OpenConfig(state.Config);
                        }
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                string btnText = state.HasDefine ? "Disable" : "Enable";
                if (GUILayout.Button(btnText, GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    if (state.HasDefine)
                    {
                        RemoveDefine(target, module.DefineSymbol);
                        state.HasDefine = false;
                    }
                    else
                    {
                        bool isSdkInstalled = state.IsEffectiveInstalled;
                        if (!string.IsNullOrEmpty(module.PackageId) && !isSdkInstalled)
                        {
                            int choice = EditorUtility.DisplayDialogComplex(
                                $"Enable {module.DisplayName}",
                                $"The SDK for {module.DisplayName} ({module.PackageId}) is not yet detected in your project.\n\n" +
                                $"You can still enable {module.DisplayName} to configure its ad units and weights in mediation—it will safely run in Stub mode until the SDK is installed.\n\n" +
                                $"Would you like to auto-install the SDK via UPM now, or enable anyway in Stub mode?",
                                "Auto Install SDK & Enable",
                                "Enable in Stub Mode",
                                "Cancel"
                            );

                            if (choice == 0) // Auto install SDK & enable
                            {
                                MultipleAdsPackageManager.InstallSdkForModule(module, target);
                                state.HasDefine = true;
                            }
                            else if (choice == 1) // Enable in Stub Mode
                            {
                                AddDefine(target, module.DefineSymbol);
                                state.HasDefine = true;
                            }
                            else
                            {
                                return; // Cancel
                            }
                        }
                        else
                        {
                            AddDefine(target, module.DefineSymbol);
                            state.HasDefine = true;
                        }
                    }

                    SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                    GUIUtility.ExitGUI();
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Remove", GUILayout.Width(actionBtnWidth), GUILayout.Height(22)))
                {
                    string sdkWarning = !string.IsNullOrEmpty(module.PackageId) ? $"\nIts SDK package ({module.PackageId}) will also be removed from Packages/manifest.json." : "";
                    if (EditorUtility.DisplayDialog($"Remove {module.DisplayName} Module",
                        $"Are you sure you want to remove {module.DisplayName} from Assets/MultipleAds/Modules/?\nIts scripting define and active config will also be removed.{sdkWarning}",
                        "Remove", "Cancel"))
                    {
                        MultipleAdsModuleManager.RemoveModule(module.Id, target);
                        SyncConfigsWithActiveDefines(mainConfig, true);
                        RescanModules();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(module.PackageId) && !state.IsEffectiveInstalled)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Install SDK", $"Install package {module.PackageId} via UPM"), EditorStyles.miniButton, GUILayout.Width(singleRowWidth), GUILayout.Height(20)))
                    {
                        MultipleAdsPackageManager.InstallSdkForModule(module, target);
                        RescanModules();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void OpenConfig(BaseAdConfig config)
        {
            if (config != null)
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
            }
        }

        private void PromptAddModule(AdsModuleDefinition module, bool downloadFromRemote)
        {
            if (module == null) return;

            if (string.IsNullOrEmpty(module.PackageId))
            {
                MultipleAdsModuleManager.AddModule(module.Id, autoInstallSdk: false, downloadFromRemote, success =>
                {
                    SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                });
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                $"Add {module.DisplayName} Module",
                $"How would you like to install the SDK for {module.DisplayName} ({module.PackageId})?\n\n" +
                $"• Auto Install (UPM): Configures 'Herghys' Scoped Registry & adds '{module.PackageId}': '{module.PackageVersion}' to Packages/manifest.json via UPM.\n\n" +
                $"• Manual Install: Adds module template only. You will import the SDK manually (.unitypackage, git URL, or Asset Store).\n\n" +
                $"The module will run in safe stub mode until the SDK is detected in the project.",
                "Auto Install (UPM)",
                "Manual Install",
                "Cancel"
            );

            if (choice == 0)
            {
                MultipleAdsModuleManager.AddModule(module.Id, autoInstallSdk: true, downloadFromRemote, success =>
                {
                    SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                });
            }
            else if (choice == 1)
            {
                MultipleAdsModuleManager.AddModule(module.Id, autoInstallSdk: false, downloadFromRemote, success =>
                {
                    SyncConfigsWithActiveDefines(mainConfig, true);
                    RescanModules();
                });
            }
        }

        private BaseAdConfig GetModuleConfig(AdsModuleDefinition module)
        {
            if (module == null) return null;

            if (moduleConfigCache.TryGetValue(module.Id, out var cached) && cached != null)
            {
                return cached;
            }

            string path = MultipleAdsPaths.GetProviderConfigAssetPath($"{module.Id}Config");
            var asset = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(path);
            if (asset == null)
            {
                string[] guids = AssetDatabase.FindAssets($"{module.Id}Config t:ScriptableObject");
                if (guids.Length > 0)
                {
                    asset = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (asset != null)
            {
                moduleConfigCache[module.Id] = asset;
            }

            return asset;
        }

        private void OpenModuleConfig(AdsModuleDefinition module)
        {
            if (module == null) return;

            var asset = GetModuleConfig(module);

            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            else
            {
                Debug.LogWarning($"[MultipleAds] Config asset for {module.DisplayName} not found.");
            }
        }

        private void DrawActiveProvidersSection()
        {
            foldoutMediation = DrawSectionFoldout("Active Mediation Providers & Weights", foldoutMediation, PrefFoldoutMediation);
            if (!foldoutMediation) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (mainConfig == null)
            {
                EditorGUILayout.HelpBox("Assign or create MultipleAdsManager Config to manage providers.", MessageType.Warning);
                if (GUILayout.Button("Regenerate MultipleAdsManager Config", GUILayout.Height(24)))
                {
                    RegenerateConfig();
                }
                EditorGUILayout.EndVertical();
                return;
            }

            var cleanConfigs = mainConfig.ProviderConfigs != null
                ? mainConfig.ProviderConfigs.Where(c => c != null && c.NetworkType != AdNetworkType.Mock).ToList()
                : new List<BaseAdConfig>();

            if (cleanConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No ad networks are currently enabled. Click 'Enable' above to activate Google Mobile Ads or LevelPlay.",
                    MessageType.Info
                );
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Active Networks ({cleanConfigs.Count}):", EditorStyles.miniBoldLabel, GUILayout.Width(130));
            string activeNames = string.Join(", ", cleanConfigs.Select(c => c.NetworkName));
            EditorGUILayout.LabelField(activeNames, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            DrawAdTypeWeightGroup("Banner Ads", cleanConfigs, IAdsType.Banner);
            EditorGUILayout.Space(4);
            DrawAdTypeWeightGroup("Interstitial Ads", cleanConfigs, IAdsType.Interstitial);
            EditorGUILayout.Space(4);
            DrawAdTypeWeightGroup("Rewarded Ads", cleanConfigs, IAdsType.Rewarded);

            EditorGUILayout.EndVertical();
        }

        private void DrawAdTypeWeightGroup(string groupTitle, List<BaseAdConfig> providers, IAdsType adType)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int totalWeight = 0;
            foreach (var cfg in providers)
            {
                int w = GetWeightForType(cfg, adType);
                totalWeight += Mathf.Max(0, w);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(groupTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(totalWeight > 0 ? $"Total Weight: {totalWeight}" : "No weight configured", EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            foreach (var cfg in providers)
            {
                int currentWeight = GetWeightForType(cfg, adType);
                float pct = totalWeight > 0 ? (float)currentWeight / totalWeight * 100f : 0f;

                EditorGUILayout.BeginHorizontal();

                string labelText = totalWeight > 0 
                    ? $"-- {cfg.NetworkName} ({pct:0.#}%)" 
                    : $"-- {cfg.NetworkName}";
                EditorGUILayout.LabelField(labelText, EditorStyles.boldLabel, GUILayout.Width(160));

                EditorGUI.BeginChangeCheck();
                int newWeight = EditorGUILayout.IntSlider(currentWeight, 0, 100);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(cfg, $"Change {adType} Weight for {cfg.NetworkName}");
                    SetWeightForType(cfg, adType, newWeight);
                    EditorUtility.SetDirty(cfg);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private static int GetWeightForType(BaseAdConfig cfg, IAdsType adType)
        {
            switch (adType)
            {
                case IAdsType.Banner: return cfg.BannerWeight;
                case IAdsType.Interstitial: return cfg.InterstitialWeight;
                case IAdsType.Rewarded: return cfg.RewardedWeight;
                default: return 50;
            }
        }

        private static void SetWeightForType(BaseAdConfig cfg, IAdsType adType, int weight)
        {
            switch (adType)
            {
                case IAdsType.Banner: cfg.BannerWeight = weight; break;
                case IAdsType.Interstitial: cfg.InterstitialWeight = weight; break;
                case IAdsType.Rewarded: cfg.RewardedWeight = weight; break;
            }
        }

        private void DrawAndroidOptimizationSection()
        {
            foldoutOptimization = DrawSectionFoldout("Android Size, Manifest & ProGuard Optimization", foldoutOptimization, PrefFoldoutOptimization);
            if (!foldoutOptimization) return;

            MultipleAdsOptimizationWindow.DrawFullOptimizationUI(mainConfig, serializedConfig, isStandaloneWindow: false);
        }

        private void DrawAdapterStatus(string name, bool isIncluded)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(isIncluded ? "Included" : "Stripped", EditorStyles.boldLabel, GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();
        }

        public static void SetupStandardConfigs()
        {
            MultipleAdsConfigGenerator.RegenerateConfig(showDialog: false);
        }

        public static void SyncConfigsWithActiveDefines(MultipleAdsConfig config, bool saveAndRefresh)
        {
            if (config == null) return;

            string mainDir = MultipleAdsPaths.GetMainConfigDirectory();
            if (!Directory.Exists(mainDir))
            {
                Directory.CreateDirectory(mainDir);
            }

            string adsDir = MultipleAdsPaths.GetAdsConfigDirectory();
            if (!Directory.Exists(adsDir))
            {
                Directory.CreateDirectory(adsDir);
            }

            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            bool hasGoogle = HasDefine(target, SymbolGoogleAds);
            bool hasLevelPlay = HasDefine(target, SymbolLevelPlay);
            bool hasAppLovin = HasDefine(target, SymbolAppLovin);
            bool hasVungle = HasDefine(target, SymbolVungle);
            bool hasUnityAds = HasDefine(target, SymbolUnityAds);

            foreach (var module in MultipleAdsModuleManager.AllModules)
            {
                if (module.IsDefault) continue;
                bool isEnabled = !string.IsNullOrEmpty(module.DefineSymbol) && HasDefine(target, module.DefineSymbol);

                SyncSingleProvider(config, isEnabled, $"{module.Id}Config", $"{module.Id}Config", null, module.NetworkType);
            }

            config.CleanDisabledProviders(hasGoogle, hasLevelPlay, hasAppLovin, hasVungle, hasUnityAds);

            EditorUtility.SetDirty(config);

            MultipleAdsAndroidOptimizer.OptimizeAll(showDialog: false);
            MultipleAdsPackageManager.SyncSdkInstalledDefines(target);
            MultipleAdsSdkConfigSynchronizer.SyncGlobalAppInfo(config);
            MultipleAdsSdkConfigSynchronizer.SyncAllSdkSettings(config);

            if (saveAndRefresh)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void SyncSingleProvider(MultipleAdsConfig config, bool isEnabled, string className, string assetName, Action<BaseAdConfig> onInit, AdNetworkType networkType)
        {
            var module = MultipleAdsModuleManager.AllModules.Find(m => string.Equals($"{m.Id}Config", className, StringComparison.OrdinalIgnoreCase) || m.NetworkType == networkType);
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

            if (isEnabled)
            {
                if (existingAsset == null)
                {
                    if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    {
                        if (module != null)
                        {
                            existingAsset = MultipleAdsConfigGenerator.EnsureProviderConfig(module, config, false);
                        }
                        else
                        {
                            Type configType = MultipleAdsConfigGenerator.FindConfigType(className);
                            if (configType != null)
                            {
                                existingAsset = (BaseAdConfig)ScriptableObject.CreateInstance(configType);
                                string dir = MultipleAdsPaths.GetAdsConfigDirectory();
                                if (!Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }
                                AssetDatabase.CreateAsset(existingAsset, path);
                                Debug.Log($"[MultipleAdsSetupWindow] Created {assetName} at {path}");
                            }
                        }
                    }

                    if (existingAsset == null)
                    {
                        MultipleAdsConfigGenerator.RequestAutoRescan();
                    }
                }

                if (existingAsset != null)
                {
                    onInit?.Invoke(existingAsset);
                    if (config.ProviderConfigs != null && !config.ProviderConfigs.Contains(existingAsset))
                    {
                        config.ProviderConfigs.Add(existingAsset);
                        Debug.Log($"[MultipleAdsSetupWindow] Added {assetName} to MultipleAdsManager Config.");
                    }
                    EditorUtility.SetDirty(existingAsset);
                }
            }
            else
            {
                if (existingAsset != null && config.ProviderConfigs != null && config.ProviderConfigs.Contains(existingAsset))
                {
                    config.ProviderConfigs.Remove(existingAsset);
                    Debug.Log($"[MultipleAdsSetupWindow] Removed {assetName} from MultipleAdsManager Config because define is not enabled.");
                }
                config.ProviderConfigs?.RemoveAll(c => c == null || c.NetworkType == networkType);
            }
        }

        private static Type FindTypeInAllAssemblies(string typeName)
        {
            return MultipleAdsConfigGenerator.FindConfigType(typeName);
        }

        private static void DeleteConfigAssetIfDisabled(string assetName)
        {
            string path = MultipleAdsPaths.GetProviderConfigAssetPath(assetName);
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[MultipleAdsSetupWindow] Removed unneeded config asset: {path}");
            }
        }

        private static bool HasDefine(BuildTargetGroup target, string symbol)
        {
            return MultipleAdsBuildTargetCompat.HasDefine(target, symbol);
        }

        private static void AddDefine(BuildTargetGroup target, string symbol)
        {
            if (MultipleAdsBuildTargetCompat.AddDefine(target, symbol))
            {
                Debug.Log($"[MultipleAdsSetupWindow] Added define symbol '{symbol}' for target '{target}'.");
            }
        }

        private static void RemoveDefine(BuildTargetGroup target, string symbol)
        {
            if (MultipleAdsBuildTargetCompat.RemoveDefine(target, symbol))
            {
                Debug.Log($"[MultipleAdsSetupWindow] Removed define symbol '{symbol}' for target '{target}'.");
            }
        }
    }
}