using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    [CustomPropertyDrawer(typeof(AdsTypeEntry))]
    public class AdsTypeEntryDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Spacing = 2f;

        private static List<string> cachedCatalog = new List<string>();
        private static double lastCatalogCheckTime = 0;
        private static MultipleAdsConfig cachedMainConfig;

        private static int PlatformTab
        {
            get => EditorPrefs.GetInt("MultipleAds_PlatformTab", 0);
            set => EditorPrefs.SetInt("MultipleAds_PlatformTab", value);
        }

        private static readonly string[] PlatformTabs = new string[] { "Both (Android & iOS)", "Android", "iOS" };

        private static MultipleAdsConfig GetMainConfig()
        {
            if (EditorApplication.timeSinceStartup - lastCatalogCheckTime > 2.0 || cachedMainConfig == null)
            {
                cachedMainConfig = AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(MultipleAdsPaths.GetMainConfigAssetPath());
                if (cachedMainConfig == null)
                {
                    cachedMainConfig = Resources.Load<MultipleAdsConfig>(MultipleAdsPaths.MainConfigName);
                    if (cachedMainConfig == null)
                        cachedMainConfig = Resources.Load<MultipleAdsConfig>(MultipleAdsPaths.LegacyMainConfigName);
                }
                if (cachedMainConfig == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:MultipleAdsConfig");
                    if (guids.Length > 0)
                    {
                        cachedMainConfig = AssetDatabase.LoadAssetAtPath<MultipleAdsConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
            }
            return cachedMainConfig;
        }

        private static List<AdNetworkType> GetActiveNetworks()
        {
            var list = new List<AdNetworkType>();
            var cfg = GetMainConfig();
            if (cfg != null && cfg.ProviderConfigs != null)
            {
                foreach (var p in cfg.ProviderConfigs)
                {
                    if (p != null && p.NetworkType != AdNetworkType.Mock && !list.Contains(p.NetworkType))
                    {
                        list.Add(p.NetworkType);
                    }
                }
            }

            if (cfg != null && cfg.EnableMockInEditor)
            {
                if (!list.Contains(AdNetworkType.Mock)) list.Add(AdNetworkType.Mock);
            }

            // Fallback so the drawer is never completely empty
            if (list.Count == 0)
            {
                list.Add(AdNetworkType.GoogleAds);
            }

            return list;
        }

        private static List<string> GetCatalog()
        {
            if (EditorApplication.timeSinceStartup - lastCatalogCheckTime > 2.0 || cachedCatalog.Count == 0)
            {
                lastCatalogCheckTime = EditorApplication.timeSinceStartup;
                var mainConfig = GetMainConfig();
                if (mainConfig != null && mainConfig.AdPlacementsCatalog != null && mainConfig.AdPlacementsCatalog.Count > 0)
                {
                    cachedCatalog = new List<string>(mainConfig.AdPlacementsCatalog);
                }
                else
                {
                    cachedCatalog = new List<string>();
                }
            }
            return cachedCatalog;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var adNameProp = property.FindPropertyRelative("adName");
            var adTypeProp = property.FindPropertyRelative("adType");
            var adPlacementProp = property.FindPropertyRelative("adPlacement");
            var rewardItemNameProp = property.FindPropertyRelative("rewardItemName");
            var rewardAmountProp = property.FindPropertyRelative("rewardAmount");
            var providerAdIdsProp = property.FindPropertyRelative("providerAdIds");

            string typeName = adTypeProp != null ? ((IAdsType)adTypeProp.enumValueIndex).ToString() : "Ad";
            string placementStr = adPlacementProp != null && !string.IsNullOrEmpty(adPlacementProp.stringValue) ? adPlacementProp.stringValue : "Untitled";

            int androidCount = 0;
            int iosCount = 0;
            if (providerAdIdsProp != null && providerAdIdsProp.isArray)
            {
                for (int i = 0; i < providerAdIdsProp.arraySize; i++)
                {
                    var elem = providerAdIdsProp.GetArrayElementAtIndex(i);
                    var androidProp = elem.FindPropertyRelative("androidAdId");
                    var legacyProp = elem.FindPropertyRelative("adId");
                    var iosProp = elem.FindPropertyRelative("iosAdId");

                    string aId = androidProp != null && !string.IsNullOrWhiteSpace(androidProp.stringValue) 
                        ? androidProp.stringValue 
                        : (legacyProp != null ? legacyProp.stringValue : "");
                    string iId = iosProp != null ? iosProp.stringValue : "";

                    if (!string.IsNullOrWhiteSpace(aId)) androidCount++;
                    if (!string.IsNullOrWhiteSpace(iId)) iosCount++;
                }
            }

            string headerTitle = $"[{typeName}] {placementStr} (Android: {androidCount} | iOS: {iosCount})";

            Rect foldoutRect = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerTitle, true, EditorStyles.foldout);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + LineHeight + Spacing;

                var catalog = GetCatalog();
                List<string> options = new List<string>(catalog);
                options.Add("[Custom...]");

                int currentIndex = options.IndexOf(placementStr);
                bool isCustom = currentIndex < 0;
                int selectedIndex = isCustom ? options.Count - 1 : currentIndex;

                Rect placementPopupRect = new Rect(position.x, y, position.width, LineHeight);
                int newIndex = EditorGUI.Popup(placementPopupRect, "Placement (Catalog)", selectedIndex, options.ToArray());
                y += LineHeight + Spacing;

                if (newIndex >= 0 && newIndex < options.Count - 1)
                {
                    adPlacementProp.stringValue = options[newIndex];
                    if (string.IsNullOrEmpty(adNameProp.stringValue) || adNameProp.stringValue == "New Ad Entry")
                    {
                        adNameProp.stringValue = options[newIndex];
                    }
                }
                else if (newIndex == options.Count - 1 || isCustom)
                {
                    Rect customPlacementRect = new Rect(position.x, y, position.width, LineHeight);
                    EditorGUI.PropertyField(customPlacementRect, adPlacementProp, new GUIContent("Custom Placement"));
                    y += LineHeight + Spacing;
                }

                Rect nameRect = new Rect(position.x, y, position.width, LineHeight);
                EditorGUI.PropertyField(nameRect, adNameProp, new GUIContent("Display Name"));
                y += LineHeight + Spacing;

                Rect typeRect = new Rect(position.x, y, position.width, LineHeight);
                EditorGUI.PropertyField(typeRect, adTypeProp, new GUIContent("Ad Format"));
                y += LineHeight + Spacing;

                bool isRewardedType = adTypeProp != null && 
                    ((IAdsType)adTypeProp.enumValueIndex == IAdsType.Rewarded || (IAdsType)adTypeProp.enumValueIndex == IAdsType.RewardedInterstitial);

                if (isRewardedType)
                {
                    Rect rewardItemRect = new Rect(position.x, y, position.width, LineHeight);
                    if (rewardItemNameProp != null)
                    {
                        EditorGUI.PropertyField(rewardItemRect, rewardItemNameProp, new GUIContent("Reward item name", "The item name or currency granted to the user (e.g. Continue_Play, Coins)"));
                    }
                    y += LineHeight + Spacing;

                    Rect rewardAmountRect = new Rect(position.x, y, position.width, LineHeight);
                    if (rewardAmountProp != null)
                    {
                        EditorGUI.PropertyField(rewardAmountRect, rewardAmountProp, new GUIContent("Reward amount", "The amount of the reward item granted (e.g. 1)"));
                    }
                    y += LineHeight + Spacing;
                }

                Rect toolbarRect = new Rect(position.x, y, position.width, LineHeight);
                int currentTab = PlatformTab;
                int newTab = GUI.Toolbar(toolbarRect, currentTab, PlatformTabs, EditorStyles.miniButton);
                if (newTab != currentTab)
                {
                    PlatformTab = newTab;
                }
                y += LineHeight + Spacing + 2f;

                Rect providerHeaderRect = new Rect(position.x, y, position.width, LineHeight);
                string platformNotice = PlatformTab == 1 ? "Android IDs" : (PlatformTab == 2 ? "iOS IDs" : "Android & iOS IDs");
                EditorGUI.LabelField(providerHeaderRect, $"Provider Ad Unit IDs ({platformNotice} - Leave blank to ignore):", EditorStyles.boldLabel);
                y += LineHeight + Spacing;

                var supportedNetworks = GetActiveNetworks();

                foreach (var network in supportedNetworks)
                {
                    SerializedProperty matchedElem = null;
                    if (providerAdIdsProp != null)
                    {
                        for (int i = 0; i < providerAdIdsProp.arraySize; i++)
                        {
                            var elem = providerAdIdsProp.GetArrayElementAtIndex(i);
                            var netTypeProp = elem.FindPropertyRelative("networkType");
                            if (netTypeProp != null && (AdNetworkType)netTypeProp.enumValueIndex == network)
                            {
                                matchedElem = elem;
                                break;
                            }
                        }

                        if (matchedElem == null)
                        {
                            int newElemIdx = providerAdIdsProp.arraySize;
                            providerAdIdsProp.InsertArrayElementAtIndex(newElemIdx);
                            matchedElem = providerAdIdsProp.GetArrayElementAtIndex(newElemIdx);
                            matchedElem.FindPropertyRelative("networkType").enumValueIndex = (int)network;
                            matchedElem.FindPropertyRelative("androidAdId").stringValue = "";
                            matchedElem.FindPropertyRelative("iosAdId").stringValue = "";
                            matchedElem.FindPropertyRelative("adId").stringValue = "";
                        }
                    }

                    if (matchedElem != null)
                    {
                        var androidProp = matchedElem.FindPropertyRelative("androidAdId");
                        var legacyProp = matchedElem.FindPropertyRelative("adId");
                        var iosProp = matchedElem.FindPropertyRelative("iosAdId");
                        var useTestProp = matchedElem.FindPropertyRelative("useTestAdId");

                        if (network == AdNetworkType.Mock)
                        {
                            string mockId = AdsTypeEntry.GetDefaultMockId((IAdsType)adTypeProp.enumValueIndex);
                            if (androidProp != null) androidProp.stringValue = mockId;
                            if (iosProp != null) iosProp.stringValue = mockId;
                            if (legacyProp != null) legacyProp.stringValue = mockId;
                            if (useTestProp != null) useTestProp.boolValue = false;

                            Rect mRect = new Rect(position.x, y, position.width, LineHeight);
                            EditorGUI.LabelField(mRect, "Mock [Persistent ID]", mockId, EditorStyles.boldLabel);
                            y += LineHeight + Spacing;
                            continue;
                        }

                        if (androidProp != null && string.IsNullOrEmpty(androidProp.stringValue) && legacyProp != null && !string.IsNullOrEmpty(legacyProp.stringValue))
                        {
                            androidProp.stringValue = legacyProp.stringValue;
                        }

                        if (PlatformTab == 0 || PlatformTab == 1)
                        {
                            Rect aRect = new Rect(position.x, y, position.width, LineHeight);
                            EditorGUI.PropertyField(aRect, androidProp, new GUIContent($"{network} [Android ID]"));
                            if (legacyProp != null) legacyProp.stringValue = androidProp.stringValue;
                            y += LineHeight + Spacing;
                        }

                        if (PlatformTab == 0 || PlatformTab == 2)
                        {
                            Rect iRect = new Rect(position.x, y, position.width, LineHeight);
                            EditorGUI.PropertyField(iRect, iosProp, new GUIContent($"{network} [iOS ID]"));
                            y += LineHeight + Spacing;
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return LineHeight;
            }

            int lines = 6;

            var adTypeProp = property.FindPropertyRelative("adType");
            if (adTypeProp != null && ((IAdsType)adTypeProp.enumValueIndex == IAdsType.Rewarded || (IAdsType)adTypeProp.enumValueIndex == IAdsType.RewardedInterstitial))
            {
                lines += 2;
            }

            var adPlacementProp = property.FindPropertyRelative("adPlacement");
            string placementStr = adPlacementProp != null ? adPlacementProp.stringValue : "";
            if (!GetCatalog().Contains(placementStr))
            {
                lines++;
            }

            var activeNets = GetActiveNetworks();
            int tab = PlatformTab;
            foreach (var net in activeNets)
            {
                if (net == AdNetworkType.Mock) lines += 1;
                else lines += (tab == 0 ? 2 : 1);
            }

            return (LineHeight * lines) + (Spacing * lines) + 8f;
        }
    }
}
