using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public abstract class MultipleAdsEditorWindowBase : EditorWindow
    {
        protected MultipleAdsConfig mainConfig;
        protected SerializedObject serializedConfig;
        protected Vector2 scrollPos;

        protected virtual void OnEnable()
        {
            MultipleAdsConfigGenerator.LoadCheck(showDialog: false);
            LoadOrCreateConfig();
            minSize = new Vector2(480, 520);
        }

        protected virtual void OnFocus()
        {
            LoadOrCreateConfig();
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

        public virtual void RefreshAfterScan()
        {
            LoadOrCreateConfig();
            Repaint();
        }

        public void DrawSharedHeader(string activeTabName, string sectionTitle, string sectionDescription)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(sectionTitle, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Scan", "Scan installed SDK packages, active modules, and test ad configurations."), GUILayout.Height(22), GUILayout.Width(65)))
            {
                TriggerManualScan();
            }

            string regenLabel = mainConfig == null ? "Regenerate Config" : "Regenerate";
            string regenTooltip = mainConfig == null ? "Regenerate missing MultipleAdsManager Config asset with default settings." : "Regenerate and re-sync MultipleAdsManager Config asset and active providers.";
            if (GUILayout.Button(new GUIContent(regenLabel, regenTooltip), GUILayout.Height(22)))
            {
                mainConfig = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: true);
                LoadOrCreateConfig();
                Repaint();
            }

            if (GUILayout.Button(new GUIContent("Select Config", "Select and edit the MultipleAdsManager Config asset in Inspector."), GUILayout.Height(22)))
            {
                if (mainConfig == null)
                {
                    mainConfig = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: false);
                }
                LoadOrCreateConfig();
                if (mainConfig != null)
                {
                    EditorGUIUtility.PingObject(mainConfig);
                    Selection.activeObject = mainConfig;
                }
                else
                {
                    EditorUtility.DisplayDialog("Config Asset", "MultipleAdsManager Config asset was not found.", "OK");
                }
            }

            if (GUILayout.Button(new GUIContent("Setup Scene", "Create or select MultipleAdsManager in the active scene."), GUILayout.Height(22)))
            {
                MultipleAdsSceneSetup.SetupAdsManagerOnScene(showDialog: true);
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(sectionDescription))
            {
                EditorGUILayout.LabelField(sectionDescription, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(4);
            DrawNavigationToolbar(activeTabName);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        public void DrawSharedHeader(string sectionTitle, string sectionDescription)
        {
            DrawSharedHeader(sectionTitle, sectionTitle, sectionDescription);
        }

        private static readonly string[] NavTabs = new[]
        {
            "Global App",
            "Ads Placements",
            "Modules",
            "Mediation",
            "Optimization",
            "Full Setup"
        };

        private void DrawNavigationToolbar(string activeTabName)
        {
            int activeIndex = 0;
            if (activeTabName == "Ads Placements" || activeTabName == "Placements") activeIndex = 1;
            else if (activeTabName == "Modules") activeIndex = 2;
            else if (activeTabName == "Mediation") activeIndex = 3;
            else if (activeTabName == "Optimization") activeIndex = 4;
            else if (activeTabName == "Full Setup") activeIndex = 5;

            int newIndex = GUILayout.Toolbar(activeIndex, NavTabs, EditorStyles.miniButton, GUILayout.Height(20));
            if (newIndex != activeIndex)
            {
                switch (newIndex)
                {
                    case 0: MultipleAdsWindowRouter.OpenGlobalSettings(this); break;
                    case 1: MultipleAdsWindowRouter.OpenPlacements(this); break;
                    case 2: MultipleAdsWindowRouter.OpenModules(this); break;
                    case 3: MultipleAdsWindowRouter.OpenMediation(this); break;
                    case 4: MultipleAdsWindowRouter.OpenOptimization(this); break;
                    case 5: MultipleAdsWindowRouter.OpenSetupWindow(this); break;
                }
            }
        }

        public static void TriggerManualScan()
        {
            BuildTargetGroup target = MultipleAdsBuildTargetCompat.SelectedBuildTargetGroup;
            ModuleUIState.ScanAll(target, clearOverrides: true);
            MultipleAdsConfigGenerator.AutoScanAndCreateConfigs(forceRescan: true, showDialog: false);
            MultipleAdsPackageManager.Scan();
            ModuleUIState.ScanAll(target, clearOverrides: false);
            MultipleAdsConfigGenerator.RefreshAllOpenWindows();

            var config = MultipleAdsConfigGenerator.GetConfig();
            int activeTestCount = 0;
            if (config != null && config.AdEntries != null)
            {
                foreach (var entry in config.AdEntries)
                {
                    if (entry == null || entry.ProviderAdIds == null) continue;
                    foreach (var p in entry.ProviderAdIds)
                    {
                        if (p != null && p.UseTestAdId)
                        {
                            activeTestCount++;
                        }
                    }
                }
            }

            EditorUtility.DisplayDialog("MultipleAds Scan Complete",
                $"Manual scan completed:\n" +
                $"• Scanned Packages/packages-lock.json and manifest.json\n" +
                $"• Verified assembly types for active SDKs\n" +
                $"• Verified ad placements and ad unit mappings\n" +
                $"• Ad Unit Provider(s) using Test Ad IDs: {activeTestCount}", "OK");
        }

        protected static string DrawTextFieldWithPlaceholder(string label, string text, string placeholder, string tooltip = null)
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
    }

    public static class MultipleAdsAdUnitsUI
    {
        private static readonly HashSet<int> expandedEntries = new HashSet<int>();
        private static readonly HashSet<string> collapsedProviders = new HashSet<string>();

        private static GUIStyle cardFoldoutStyle;
        private static GUIStyle CardFoldoutStyle
        {
            get
            {
                if (cardFoldoutStyle == null)
                {
                    cardFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                    cardFoldoutStyle.focused.background = null;
                    cardFoldoutStyle.active.background = null;
                    cardFoldoutStyle.onFocused.background = null;
                    cardFoldoutStyle.onActive.background = null;
                    cardFoldoutStyle.focused.textColor = cardFoldoutStyle.normal.textColor;
                    cardFoldoutStyle.onFocused.textColor = cardFoldoutStyle.onNormal.textColor;
                }
                return cardFoldoutStyle;
            }
        }

        private static GUIStyle providerFoldoutStyle;
        private static GUIStyle ProviderFoldoutStyle
        {
            get
            {
                if (providerFoldoutStyle == null)
                {
                    providerFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 11
                    };
                    providerFoldoutStyle.focused.background = null;
                    providerFoldoutStyle.active.background = null;
                    providerFoldoutStyle.onFocused.background = null;
                    providerFoldoutStyle.onActive.background = null;
                    providerFoldoutStyle.focused.textColor = providerFoldoutStyle.normal.textColor;
                    providerFoldoutStyle.onFocused.textColor = providerFoldoutStyle.onNormal.textColor;
                }
                return providerFoldoutStyle;
            }
        }

        public static void DrawAdUnits(MultipleAdsConfig config, SerializedObject serializedConfig)
        {
            if (config == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            int count = config.AdEntries?.Count ?? 0;
            EditorGUILayout.LabelField($"Configured Central Ad Units ({count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Add Ad Unit", GUILayout.Width(110), GUILayout.Height(22)))
            {
                Undo.RecordObject(config, "Add Ad Unit");
                if (config.AdEntries == null)
                {
                    var adEntriesField = typeof(MultipleAdsConfig).GetField("adEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (adEntriesField != null)
                    {
                        adEntriesField.SetValue(config, new List<AdsTypeEntry>());
                    }
                }

                string defaultPlacement = config.AdPlacementsCatalog != null && config.AdPlacementsCatalog.Count > 0
                    ? config.AdPlacementsCatalog[0]
                    : "";

                var newEntry = new AdsTypeEntry("New Ad Unit", IAdsType.Banner, defaultPlacement);
                config.AdEntries.Add(newEntry);
                expandedEntries.Add(config.AdEntries.Count - 1);
                EditorUtility.SetDirty(config);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            if (config.AdEntries == null || config.AdEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No ad units configured yet. Click '+ Add Ad Unit' above to create one.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var activeNetworks = new List<AdNetworkType>();
            if (config.ProviderConfigs != null)
            {
                foreach (var p in config.ProviderConfigs)
                {
                    if (p != null && p.Enabled && !activeNetworks.Contains(p.NetworkType))
                    {
                        activeNetworks.Add(p.NetworkType);
                    }
                }
            }
            if (config.EnableMockInEditor && !activeNetworks.Contains(AdNetworkType.Mock))
            {
                activeNetworks.Add(AdNetworkType.Mock);
            }
            if (activeNetworks.Count == 0)
            {
                activeNetworks.Add(AdNetworkType.GoogleAds);
            }

            var catalog = config.AdPlacementsCatalog ?? new List<string>();

            for (int i = 0; i < config.AdEntries.Count; i++)
            {
                var entry = config.AdEntries[i];
                if (entry == null) continue;

                bool isExpanded = expandedEntries.Contains(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                string placementLabel = !string.IsNullOrEmpty(entry.AdPlacement) ? entry.AdPlacement : "No Placement";
                string title = $"[{entry.AdType}] {entry.AdName} ({placementLabel})";

                bool newExpanded = EditorGUILayout.Foldout(isExpanded, title, true, CardFoldoutStyle);
                if (newExpanded != isExpanded)
                {
                    GUI.FocusControl(null);
                    if (newExpanded) expandedEntries.Add(i);
                    else expandedEntries.Remove(i);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(18)))
                {
                    Undo.RecordObject(config, "Remove Ad Unit");
                    config.AdEntries.RemoveAt(i);
                    expandedEntries.Remove(i);
                    EditorUtility.SetDirty(config);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                if (newExpanded)
                {
                    EditorGUILayout.Space(4);
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();

                    string newName = EditorGUILayout.TextField("Display Name", entry.AdName);
                    IAdsType newType = (IAdsType)EditorGUILayout.EnumPopup("Ad Format", entry.AdType);

                    string newPlacement = entry.AdPlacement;
                    if (catalog.Count > 0)
                    {
                        var options = new List<string>(catalog);
                        options.Add("[Custom...]");

                        int curIdx = options.IndexOf(entry.AdPlacement);
                        bool isCustom = curIdx < 0 && !string.IsNullOrEmpty(entry.AdPlacement);
                        int selIdx = isCustom ? options.Count - 1 : (curIdx >= 0 ? curIdx : 0);

                        int chosen = EditorGUILayout.Popup("Placement", selIdx, options.ToArray());
                        if (chosen >= 0 && chosen < options.Count - 1)
                        {
                            newPlacement = options[chosen];
                        }
                        else if (chosen == options.Count - 1 || isCustom)
                        {
                            newPlacement = EditorGUILayout.TextField("Custom Placement", entry.AdPlacement);
                        }
                    }
                    else
                    {
                        newPlacement = EditorGUILayout.TextField("Placement", entry.AdPlacement);
                    }

                    string newRewardItem = entry.RewardItemName;
                    int newRewardAmount = entry.RewardAmount;
                    if (newType == IAdsType.Rewarded || newType == IAdsType.RewardedInterstitial)
                    {
                        newRewardItem = EditorGUILayout.TextField("Reward Item Name", entry.RewardItemName);
                        newRewardAmount = EditorGUILayout.IntField("Reward Amount", entry.RewardAmount);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(config, "Modify Ad Unit Details");
                        entry.AdName = newName;
                        entry.AdType = newType;
                        entry.AdPlacement = newPlacement;
                        entry.RewardItemName = newRewardItem;
                        entry.RewardAmount = newRewardAmount;
                        EditorUtility.SetDirty(config);
                    }

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Provider Ad Unit IDs:", EditorStyles.miniBoldLabel);

                    foreach (var net in activeNetworks)
                    {
                        string provKey = $"{i}_{net}";
                        bool isProvExpanded = !collapsedProviders.Contains(provKey);

                        var pEntry = entry.GetProviderAdIdEntry(net);
                        bool isMock = net == AdNetworkType.Mock;
                        string mockAdId = AdsTypeEntry.GetDefaultMockId(entry.AdType);

                        string statusBadge;
                        if (isMock)
                        {
                            statusBadge = $" [{mockAdId}]";
                        }
                        else
                        {
                            bool isTestAdsActive = MultipleAdsBuildTargetCompat.HasDefine(BuildTargetGroup.Unknown, MultipleAdsBuildTargetCompat.TestAdsDefine);
                            bool useTest = pEntry != null && pEntry.UseTestAdId;
                            if (useTest && isTestAdsActive)
                            {
                                statusBadge = " [Using Test Ad ID]";
                            }
                            else if (useTest && !isTestAdsActive)
                            {
                                statusBadge = pEntry != null && pEntry.HasValidId ? " [Configured (Real ID)]" : " [Real ID Needed]";
                            }
                            else
                            {
                                statusBadge = pEntry != null && pEntry.HasValidId ? " [Configured]" : " [Not Set]";
                            }
                        }

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        bool newProvExpanded = EditorGUILayout.Foldout(isProvExpanded, $"{net}{statusBadge}", true, ProviderFoldoutStyle);
                        if (newProvExpanded != isProvExpanded)
                        {
                            GUI.FocusControl(null);
                            if (newProvExpanded) collapsedProviders.Remove(provKey);
                            else collapsedProviders.Add(provKey);
                        }

                        if (newProvExpanded)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.Space(2);

                            if (isMock)
                            {
                                if (pEntry != null && (pEntry.AndroidAdId != mockAdId || pEntry.IosAdId != mockAdId || pEntry.UseTestAdId))
                                {
                                    entry.SetProviderEntry(net, mockAdId, mockAdId, false);
                                    EditorUtility.SetDirty(config);
                                }

                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.LabelField("Mock Ad Unit ID (Persistent):", mockAdId, EditorStyles.boldLabel);
                                EditorGUILayout.LabelField("Mock IDs always persist (e.g. mock_banner, mock_interstitial). No test ID configuration needed.", EditorStyles.wordWrappedMiniLabel);
                                EditorGUILayout.EndVertical();
                            }
                            else
                            {
                                EditorGUI.BeginChangeCheck();
                                bool isTestAdsActive = MultipleAdsBuildTargetCompat.HasDefine(BuildTargetGroup.Unknown, MultipleAdsBuildTargetCompat.TestAdsDefine);
                                bool curUseTest = pEntry != null && pEntry.UseTestAdId;
                                bool newUseTest = EditorGUILayout.ToggleLeft(
                                    new GUIContent("Use Test Ad ID (Editor Only)", $"When enabled and #TEST_ADS is active, uses the official {entry.AdType} test ad unit ID from {net} config in Unity Editor (#UNITY_EDITOR). If #TEST_ADS is disabled, real production IDs are used even in the Editor."),
                                    curUseTest
                                );
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(config, $"Toggle {net} Test Ad ID for {entry.AdName}");
                                    string existingAndroid = pEntry != null ? pEntry.AndroidAdId : "";
                                    string existingIos = pEntry != null ? pEntry.IosAdId : "";
                                    entry.SetProviderEntry(net, existingAndroid, existingIos, newUseTest);
                                    EditorUtility.SetDirty(config);
                                }

                                if (newUseTest && !isTestAdsActive)
                                {
                                    EditorGUILayout.HelpBox("Note: 'Use Test Ads On Editor' (#TEST_ADS) is currently disabled in Global Settings. Even though this ad is marked for test ads, real production IDs will be used in the Editor until #TEST_ADS is enabled.", MessageType.Info);
                                }

                                if (newUseTest)
                                {
                                    BaseAdConfig provConfig = config.GetProviderConfig(net);
                                    string testAndroid = provConfig != null ? provConfig.GetTestAdId(entry.AdType, false) : "";
                                    string testIos = provConfig != null ? provConfig.GetTestAdId(entry.AdType, true) : "";

                                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                    EditorGUILayout.LabelField($"Active Test Ad ID ({entry.AdType}) - [Used in Editor for this Ad]", EditorStyles.miniBoldLabel);
                                    EditorGUILayout.LabelField("Android Test ID:", string.IsNullOrEmpty(testAndroid) ? "(None configured in module)" : testAndroid, EditorStyles.wordWrappedMiniLabel);
                                    EditorGUILayout.LabelField("iOS Test ID:", string.IsNullOrEmpty(testIos) ? "(None configured in module)" : testIos, EditorStyles.wordWrappedMiniLabel);
                                    EditorGUILayout.EndVertical();
                                }

                                EditorGUI.BeginChangeCheck();
                                string curAndroid = pEntry != null ? pEntry.AndroidAdId : "";
                                string curIos = pEntry != null ? pEntry.IosAdId : "";

                                EditorGUILayout.LabelField(newUseTest ? "Production Ad Unit IDs (Enforced for Runtime / Builds):" : "Production Ad Unit IDs:", EditorStyles.miniBoldLabel);
                                string newAndroid = EditorGUILayout.TextField("Android Unit ID", curAndroid);
                                string newIos = EditorGUILayout.TextField("iOS Unit ID", curIos);

                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(config, $"Edit {net} Ad Unit IDs");
                                    entry.SetProviderEntry(net, newAndroid, newIos, newUseTest);
                                    EditorUtility.SetDirty(config);
                                }
                            }

                            EditorGUI.indentLevel--;
                            EditorGUILayout.Space(2);
                        }

                        EditorGUILayout.EndVertical();
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }
    }
}