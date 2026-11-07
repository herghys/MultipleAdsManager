using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public static class MultipleAdsSdkConfigSynchronizer
    {
        private const string ManifestFilePath = "Assets/Plugins/Android/AndroidManifest.xml";

        public static bool SyncFromPlayerSettings(MultipleAdsConfig config)
        {
            if (config == null) return false;

            string playerAppName = PlayerSettings.productName;
            string playerPkg = MultipleAdsBuildTargetCompat.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (string.IsNullOrWhiteSpace(playerPkg))
            {
                playerPkg = MultipleAdsBuildTargetCompat.GetApplicationIdentifier(BuildTargetGroup.iOS);
            }
            if (string.IsNullOrWhiteSpace(playerPkg))
            {
                playerPkg = Application.identifier;
            }

            bool changed = false;
            if (!string.IsNullOrEmpty(playerAppName) && config.GlobalAppName != playerAppName)
            {
                config.GlobalAppName = playerAppName;
                changed = true;
            }

            if (!string.IsNullOrEmpty(playerPkg) && config.GlobalPackageName != playerPkg)
            {
                config.GlobalPackageName = playerPkg;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            SyncGlobalAppInfo(config);
            return changed;
        }

        public static void SyncGlobalAppInfo(MultipleAdsConfig config)
        {
            if (config == null) return;

            string appName = config.GlobalAppName;
            string packageName = config.GlobalPackageName;

            bool playerSettingsChanged = false;

            if (!string.IsNullOrWhiteSpace(appName) && PlayerSettings.productName != appName)
            {
                PlayerSettings.productName = appName;
                playerSettingsChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                try
                {
                    if (MultipleAdsBuildTargetCompat.GetApplicationIdentifier(BuildTargetGroup.Android) != packageName)
                    {
                        MultipleAdsBuildTargetCompat.SetApplicationIdentifier(BuildTargetGroup.Android, packageName);
                        playerSettingsChanged = true;
                    }

                    if (MultipleAdsBuildTargetCompat.GetApplicationIdentifier(BuildTargetGroup.iOS) != packageName)
                    {
                        MultipleAdsBuildTargetCompat.SetApplicationIdentifier(BuildTargetGroup.iOS, packageName);
                        playerSettingsChanged = true;
                    }

                    if (MultipleAdsBuildTargetCompat.GetApplicationIdentifier(BuildTargetGroup.Standalone) != packageName)
                    {
                        MultipleAdsBuildTargetCompat.SetApplicationIdentifier(BuildTargetGroup.Standalone, packageName);
                        playerSettingsChanged = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MultipleAdsSdkConfigSynchronizer] PlayerSettings.SetApplicationIdentifier warning: {ex.Message}");
                }
            }

            if (playerSettingsChanged)
            {
                Debug.Log($"[MultipleAdsSdkConfigSynchronizer] Updated PlayerSettings: Product Name = '{PlayerSettings.productName}', Package = '{packageName}'.");
            }

            string[] guids = AssetDatabase.FindAssets("t:BaseAdConfig");
            int configsUpdated = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var providerConfig = AssetDatabase.LoadAssetAtPath<BaseAdConfig>(path);
                if (providerConfig != null)
                {
                    bool changed = false;
                    if (!string.IsNullOrWhiteSpace(appName) && providerConfig.AppName != appName)
                    {
                        providerConfig.AppName = appName;
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(packageName) && providerConfig.AppStoreDetails != packageName)
                    {
                        providerConfig.AppStoreDetails = packageName;
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(providerConfig);
                        configsUpdated++;
                    }
                }
            }

            if (configsUpdated > 0)
            {
                Debug.Log($"[MultipleAdsSdkConfigSynchronizer] Synchronized global app info across {configsUpdated} provider config assets.");
            }

            SyncAndroidManifestPackage(packageName);

            EditorUtility.SetDirty(config);
        }

        private static void SyncAndroidManifestPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName) || !File.Exists(ManifestFilePath)) return;

            try
            {
                string content = File.ReadAllText(ManifestFilePath);
                var match = Regex.Match(content, @"package\s*=\s*""([^""]+)""");
                if (match.Success)
                {
                    string currentPkg = match.Groups[1].Value;
                    if (currentPkg != packageName && currentPkg != "com.unity3d.player")
                    {
                        content = Regex.Replace(content, @"package\s*=\s*""[^""]+""", $"package=\"{packageName}\"");
                        File.WriteAllText(ManifestFilePath, content);
                        Debug.Log($"[MultipleAdsSdkConfigSynchronizer] Updated AndroidManifest.xml package=\"{packageName}\".");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultipleAdsSdkConfigSynchronizer] AndroidManifest sync warning: {ex.Message}");
            }
        }

        public static int SyncAllSdkSettings(MultipleAdsConfig config)
        {
            int syncCount = 0;

            if (config == null) return syncCount;

            var googleConfig = config.GetConfig(AdNetworkType.GoogleAds) ??
                               AssetDatabase.LoadAssetAtPath<BaseAdConfig>(MultipleAdsPaths.GetProviderConfigAssetPath("GoogleAdsConfig"));
            if (googleConfig != null)
            {
                if (SyncGoogleMobileAdsSettings(googleConfig.AndroidAppId, googleConfig.IosAppId))
                {
                    syncCount++;
                }
            }

            var levelPlayConfig = config.GetConfig(AdNetworkType.LevelPlay) ??
                                  AssetDatabase.LoadAssetAtPath<BaseAdConfig>(MultipleAdsPaths.GetProviderConfigAssetPath("LevelPlayConfig"));
            if (levelPlayConfig != null)
            {
                if (SyncLevelPlayMediationSettings(levelPlayConfig.AndroidAppId, levelPlayConfig.IosAppId))
                {
                    syncCount++;
                }
            }

            var appLovinConfig = config.GetConfig(AdNetworkType.AppLovin) ??
                                 AssetDatabase.LoadAssetAtPath<BaseAdConfig>(MultipleAdsPaths.GetProviderConfigAssetPath("AppLovinConfig"));
            if (appLovinConfig != null)
            {
                string admobAndroid = googleConfig != null ? googleConfig.AndroidAppId : null;
                string admobIos = googleConfig != null ? googleConfig.IosAppId : null;
                if (SyncAppLovinSettings(appLovinConfig.AndroidAppId, admobAndroid, admobIos))
                {
                    syncCount++;
                }
            }

            return syncCount;
        }

        public static bool SyncGoogleMobileAdsSettings(string androidAppId, string iosAppId)
        {
            UnityEngine.Object settingsAsset = FindSdkSettingsAsset(
                "GoogleMobileAdsSettings",
                "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset"
            );

            if (settingsAsset == null)
            {
                // Attempt reflection instantiation if type is loaded
                Type type = FindTypeInAllAssemblies("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
                if (type != null)
                {
                    try
                    {
                        var prop = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (prop != null)
                        {
                            settingsAsset = prop.GetValue(null) as UnityEngine.Object;
                        }
                    }
                    catch { }
                }
            }

            if (settingsAsset == null) return false;

            var so = new SerializedObject(settingsAsset);
            so.Update();

            bool modified = false;

            if (!string.IsNullOrEmpty(androidAppId))
            {
                modified |= TrySetSerializedProperty(so, "admobAppIdAndroid", androidAppId);
                modified |= TrySetSerializedProperty(so, "m_GoogleMobileAdsAndroidAppId", androidAppId);
                modified |= TrySetSerializedProperty(so, "GoogleMobileAdsAndroidAppId", androidAppId);
            }

            if (!string.IsNullOrEmpty(iosAppId))
            {
                modified |= TrySetSerializedProperty(so, "admobAppIdIOS", iosAppId);
                modified |= TrySetSerializedProperty(so, "m_GoogleMobileAdsIOSAppId", iosAppId);
                modified |= TrySetSerializedProperty(so, "GoogleMobileAdsIOSAppId", iosAppId);
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settingsAsset);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MultipleAdsSdkConfigSynchronizer] Synchronized Google Mobile Ads Settings: Android ID='{androidAppId}', iOS ID='{iosAppId}'.");
                return true;
            }

            return false;
        }

        public static bool SyncLevelPlayMediationSettings(string androidKey, string iosKey)
        {
            UnityEngine.Object settingsAsset = FindSdkSettingsAsset(
                "LevelPlayMediationSettings",
                "Assets/LevelPlay/Resources/LevelPlayMediationSettings.asset"
            );

            if (settingsAsset == null)
            {
                settingsAsset = FindSdkSettingsAsset(
                    "IronSourceMediationSettings",
                    "Assets/IronSource/Resources/IronSourceMediationSettings.asset"
                );
            }

            if (settingsAsset == null) return false;

            var so = new SerializedObject(settingsAsset);
            so.Update();

            bool modified = false;

            if (!string.IsNullOrEmpty(androidKey))
            {
                modified |= TrySetSerializedProperty(so, "AndroidAppKey", androidKey);
                modified |= TrySetSerializedProperty(so, "androidAppKey", androidKey);
                modified |= TrySetSerializedProperty(so, "m_AndroidAppKey", androidKey);
            }

            if (!string.IsNullOrEmpty(iosKey))
            {
                modified |= TrySetSerializedProperty(so, "IOSAppKey", iosKey);
                modified |= TrySetSerializedProperty(so, "iosAppKey", iosKey);
                modified |= TrySetSerializedProperty(so, "m_IOSAppKey", iosKey);
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settingsAsset);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MultipleAdsSdkConfigSynchronizer] Synchronized LevelPlay Mediation Settings: Android Key='{androidKey}', iOS Key='{iosKey}'.");
                return true;
            }

            return false;
        }

        public static bool SyncAppLovinSettings(string sdkKey, string admobAndroidId = null, string admobIosId = null)
        {
            UnityEngine.Object settingsAsset = FindSdkSettingsAsset(
                "AppLovinSettings",
                "Assets/MaxSdk/Resources/AppLovinSettings.asset"
            );

            if (settingsAsset == null) return false;

            var so = new SerializedObject(settingsAsset);
            so.Update();

            bool modified = false;

            if (!string.IsNullOrEmpty(sdkKey))
            {
                modified |= TrySetSerializedProperty(so, "sdkKey", sdkKey);
                modified |= TrySetSerializedProperty(so, "SdkKey", sdkKey);
                modified |= TrySetSerializedProperty(so, "m_SdkKey", sdkKey);
            }

            if (!string.IsNullOrEmpty(admobAndroidId))
            {
                modified |= TrySetSerializedProperty(so, "adMobAndroidAppId", admobAndroidId);
                modified |= TrySetSerializedProperty(so, "m_AdMobAndroidAppId", admobAndroidId);
            }

            if (!string.IsNullOrEmpty(admobIosId))
            {
                modified |= TrySetSerializedProperty(so, "adMobIosAppId", admobIosId);
                modified |= TrySetSerializedProperty(so, "m_AdMobIosAppId", admobIosId);
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settingsAsset);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MultipleAdsSdkConfigSynchronizer] Synchronized AppLovin Settings: SDK Key='{sdkKey}'.");
                return true;
            }

            return false;
        }

        private static UnityEngine.Object FindSdkSettingsAsset(string assetNamePattern, string directPath)
        {
            if (File.Exists(directPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(directPath);
                if (asset != null) return asset;
            }

            string[] guids = AssetDatabase.FindAssets($"{assetNamePattern} t:ScriptableObject");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null) return asset;
            }

            return null;
        }

        private static bool TrySetSerializedProperty(SerializedObject so, string propertyName, string value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null && prop.propertyType == SerializedPropertyType.String)
            {
                if (prop.stringValue != value)
                {
                    prop.stringValue = value;
                    return true;
                }
            }
            return false;
        }

        private static Type FindTypeInAllAssemblies(string typeName)
        {
            Type direct = Type.GetType(typeName);
            if (direct != null) return direct;

#pragma warning disable UAC0005
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = assembly.GetType(typeName, false, true);
                    if (t != null) return t;
                }
                catch { }
            }
#pragma warning restore UAC0005

            return null;
        }
    }
}
