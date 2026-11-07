using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    [CustomEditor(typeof(BaseAdConfig), true)]
    [CanEditMultipleObjects]
    public class BaseAdConfigCustomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var config = target as BaseAdConfig;
            string providerTitle = config != null ? config.NetworkName : "Ad Provider";

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"{providerTitle} Configuration\n" +
                "Manages platform credentials (App IDs / Keys) and mediation weights.\n" +
                "App Name and Package Identifier sync globally across PlayerSettings and all providers.",
                MessageType.Info
            );
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Provider Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("networkName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("networkType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enabled"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("App Information (Synced with Global)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("appName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("appStoreDetails"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Platform Application IDs / SDK Keys", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("androidAppId"), new GUIContent("Android App ID / Key"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("iosAppId"), new GUIContent("iOS App ID / Key"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mediation Weights (0 - 100)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bannerWeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interstitialWeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rewardedWeight"));

            DrawTestAdIdsSection(serializedObject, config);

            var androidKeyProp = serializedObject.FindProperty("androidAppKey");
            var iosKeyProp = serializedObject.FindProperty("iosAppKey");
            var androidIdProp = serializedObject.FindProperty("androidAppId");
            var iosIdProp = serializedObject.FindProperty("iosAppId");
            if (androidKeyProp != null && androidIdProp != null && androidKeyProp.stringValue != androidIdProp.stringValue)
            {
                androidKeyProp.stringValue = androidIdProp.stringValue;
            }
            if (iosKeyProp != null && iosIdProp != null && iosKeyProp.stringValue != iosIdProp.stringValue)
            {
                iosKeyProp.stringValue = iosIdProp.stringValue;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (config != null)
            {
                if (config.NetworkType == AdNetworkType.GoogleAds)
                {
                    if (GUILayout.Button("Sync App IDs to GoogleMobileAdsSettings Asset", GUILayout.Height(24)))
                    {
                        bool synced = MultipleAdsSdkConfigSynchronizer.SyncGoogleMobileAdsSettings(config.AndroidAppId, config.IosAppId);
                        if (synced)
                        {
                            EditorUtility.DisplayDialog("Sync Complete", "Google Mobile Ads App IDs were successfully synced into GoogleMobileAdsSettings.", "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Settings Not Found", "GoogleMobileAdsSettings asset was not found. It will be generated automatically once the Google Mobile Ads package is resolved.", "OK");
                        }
                    }
                }
                else if (config.NetworkType == AdNetworkType.LevelPlay)
                {
                    if (GUILayout.Button("Sync App Key to LevelPlay Settings Asset", GUILayout.Height(24)))
                    {
                        bool synced = MultipleAdsSdkConfigSynchronizer.SyncLevelPlayMediationSettings(config.AndroidAppId, config.IosAppId);
                        if (synced)
                        {
                            EditorUtility.DisplayDialog("Sync Complete", "LevelPlay App Key was successfully synced into LevelPlay / IronSource Mediation Settings.", "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Settings Not Found", "LevelPlay settings asset was not found. It will be generated automatically once the LevelPlay package is resolved.", "OK");
                        }
                    }
                }
                else if (config.NetworkType == AdNetworkType.AppLovin)
                {
                    if (GUILayout.Button("Sync SDK Key to AppLovin Settings Asset", GUILayout.Height(24)))
                    {
                        bool synced = MultipleAdsSdkConfigSynchronizer.SyncAppLovinSettings(config.AndroidAppId);
                        if (synced)
                        {
                            EditorUtility.DisplayDialog("Sync Complete", "AppLovin SDK Key was successfully synced into AppLovinSettings.", "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Settings Not Found", "AppLovinSettings asset was not found. It will be generated automatically once the AppLovin package is resolved.", "OK");
                        }
                    }
                }
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Open MultipleAds Setup Window", GUILayout.Height(28)))
            {
                MultipleAdsSetupWindow.Open();
            }
        }

        public static void DrawTestAdIdsSection(SerializedObject serializedObject, BaseAdConfig config)
        {
            if (config == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Format Test Ad IDs (Reference Library) [Editor Only]", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("These format-specific test IDs are strictly restricted to Unity Editor (#UNITY_EDITOR). They are never used in player builds.", EditorStyles.wordWrappedMiniLabel);

            var testAdIdsProp = serializedObject.FindProperty("testAdIds");
            config.EnsureDefaultTestAdIds(false);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (testAdIdsProp != null && testAdIdsProp.isArray)
            {
                for (int i = 0; i < testAdIdsProp.arraySize; i++)
                {
                    var entryProp = testAdIdsProp.GetArrayElementAtIndex(i);
                    if (entryProp == null) continue;

                    var adTypeProp = entryProp.FindPropertyRelative("adType");
                    var enabledProp = entryProp.FindPropertyRelative("enabled");
                    var androidIdProp = entryProp.FindPropertyRelative("androidTestAdId");
                    var iosIdProp = entryProp.FindPropertyRelative("iosTestAdId");

                    IAdsType adType = (IAdsType)adTypeProp.enumValueIndex;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    if (enabledProp != null)
                    {
                        enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
                    }
                    EditorGUILayout.LabelField(adType.ToString(), EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    if (enabledProp != null && enabledProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        if (androidIdProp != null) EditorGUILayout.PropertyField(androidIdProp, new GUIContent("Android Test ID"));
                        if (iosIdProp != null) EditorGUILayout.PropertyField(iosIdProp, new GUIContent("iOS Test ID"));
                        EditorGUI.indentLevel--;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(Test Ad ID disabled for this format)", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset to Official Default Test IDs", GUILayout.Height(20)))
                {
                    if (EditorUtility.DisplayDialog("Reset Test IDs", "Reset all test ad IDs to official provider defaults?", "Yes, Reset", "Cancel"))
                    {
                        config.EnsureDefaultTestAdIds(true);
                        serializedObject.Update();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }
}