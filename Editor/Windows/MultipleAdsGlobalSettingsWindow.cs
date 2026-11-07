using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsGlobalSettingsWindow : MultipleAdsEditorWindowBase
    {
        private void OnGUI()
        {
            if (mainConfig == null) LoadOrCreateConfig();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawSharedHeader("Global App", "Global App Settings & Privacy", 
                "Configure your application name, package identifier, COPPA child-directed compliance, and mock simulator.");

            if (mainConfig == null)
            {
                EditorGUILayout.HelpBox("MultipleAdsManager Config not found. Create or assign one to proceed.", MessageType.Warning);
                if (GUILayout.Button("Regenerate MultipleAdsManager Config", GUILayout.Height(26)))
                {
                    mainConfig = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: true);
                    LoadOrCreateConfig();
                    Repaint();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawAppInfoSection();

            EditorGUILayout.Space(8);
            DrawPrivacyAndComplianceSection();

            EditorGUILayout.Space(8);
            DrawTestDevicesSection();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAppInfoSection()
        {
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

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Sync from Player Settings", "Pull current Product Name and Application Identifier from Unity PlayerSettings."), GUILayout.Height(24)))
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

            EditorGUILayout.Space(2);
            if (GUILayout.Button(new GUIContent("Sync All External SDK Settings (AdMob / LevelPlay / AppLovin)", "Push credentials to third-party SDK configuration assets in Resources."), GUILayout.Height(22)))
            {
                int count = MultipleAdsSdkConfigSynchronizer.SyncAllSdkSettings(mainConfig);
                EditorUtility.DisplayDialog("External SDK Settings Synchronized", 
                    $"Synchronized provider credentials to {count} external SDK configuration asset(s) (GoogleMobileAdsSettings, LevelPlayMediationSettings, AppLovinSettings).", "OK");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPrivacyAndComplianceSection()
        {
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
            bool enableMock = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    " Enable simulated Mock Ads in Editor",
                    "Allows seeing interactive mock banner, interstitial, and rewarded dialogues when playing in Editor."
                ),
                mainConfig.EnableMockInEditor
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mainConfig, "Toggle Mock Ads in Editor");
                mainConfig.EnableMockInEditor = enableMock;
                EditorUtility.SetDirty(mainConfig);
            }

            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            bool testAdsInEditor = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    " Use Test Ads On Editor",
                    "When running inside Unity Editor (#UNITY_EDITOR), ad providers use official test ad IDs and test mode instead of production ads. Test ads are strictly restricted to Unity Editor and never run in player builds."
                ),
                mainConfig.UseTestAdsOnEditor
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mainConfig, "Toggle Test Ads in Editor");
                mainConfig.UseTestAdsOnEditor = testAdsInEditor;
                EditorUtility.SetDirty(mainConfig);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTestDevicesSection()
        {
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
        }
    }
}