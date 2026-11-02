using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsMediationWindow : MultipleAdsEditorWindowBase
    {
        private void OnGUI()
        {
            if (mainConfig == null) LoadOrCreateConfig();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawSharedHeader("Mediation", "Active Mediation & Weights", 
                "Manage active provider configs, set waterfall distribution weights (0 - 100), and sync external SDK settings assets.");

            if (mainConfig == null)
            {
                EditorGUILayout.HelpBox("MultipleAdsManager Config not found.", MessageType.Warning);
                if (GUILayout.Button("Regenerate MultipleAdsManager Config", GUILayout.Height(26)))
                {
                    mainConfig = MultipleAdsConfigGenerator.RegenerateConfig(showDialog: true);
                    LoadOrCreateConfig();
                    Repaint();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawProvidersSection();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawProvidersSection()
        {
            var cleanConfigs = mainConfig.ProviderConfigs != null
                ? mainConfig.ProviderConfigs.Where(c => c != null && c.NetworkType != AdNetworkType.Mock).ToList()
                : new List<BaseAdConfig>();

            EditorGUILayout.LabelField($"Active Mediation Providers ({cleanConfigs.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (cleanConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("No external ad providers active. Add and enable modules in Ad Network Modules.", MessageType.Info);
                if (GUILayout.Button("Go to Ad Modules Window", GUILayout.Height(26)))
                {
                    MultipleAdsWindowRouter.OpenModules(this);
                }
                EditorGUILayout.EndVertical();
                return;
            }

            foreach (var cfg in cleanConfigs)
            {
                DrawProviderCard(cfg);
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Mediation Weights Distribution (0 - 100)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawWeightGroup("Banner Ads Distribution", IAdsType.Banner, cleanConfigs);
            EditorGUILayout.Space(4);
            DrawWeightGroup("Interstitial Ads Distribution", IAdsType.Interstitial, cleanConfigs);
            EditorGUILayout.Space(4);
            DrawWeightGroup("Rewarded Video Ads Distribution", IAdsType.Rewarded, cleanConfigs);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Sync All External SDK Settings (AdMob / LevelPlay / AppLovin)", GUILayout.Height(26)))
            {
                int count = MultipleAdsSdkConfigSynchronizer.SyncAllSdkSettings(mainConfig);
                EditorUtility.DisplayDialog("External SDK Settings Synchronized",
                    $"Synchronized credentials to {count} external SDK configuration asset(s).", "OK");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProviderCard(BaseAdConfig cfg)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(cfg.NetworkName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {cfg.NetworkType} | App Name: '{cfg.AppName}' | Package: '{cfg.AppStoreDetails}'", EditorStyles.miniLabel);
            if (cfg.UseTestAdIds)
            {
                EditorGUILayout.LabelField("[Test Ad IDs Active]", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Open Inspector", GUILayout.Width(110), GUILayout.Height(22)))
            {
                EditorGUIUtility.PingObject(cfg);
                Selection.activeObject = cfg;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawWeightGroup(string groupTitle, IAdsType adType, List<BaseAdConfig> providers)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int totalWeight = 0;
            foreach (var cfg in providers)
            {
                totalWeight += Mathf.Max(0, GetWeightForType(cfg, adType));
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(groupTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(totalWeight > 0 ? $"Total Weight: {totalWeight}" : "No weight configured", EditorStyles.miniLabel, GUILayout.Width(130));
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
    }
}
