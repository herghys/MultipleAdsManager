using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public class MultipleAdsPlacementsWindow : MultipleAdsEditorWindowBase
    {
        private string newPlacementName = "";

        private void OnGUI()
        {
            if (mainConfig == null) LoadOrCreateConfig();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawSharedHeader("Ads Placements", 
                "Configure your game's ad placements, format mappings, reward items, and provider ad unit IDs.");

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

            DrawPlacementCatalogSection();

            EditorGUILayout.Space(8);
            DrawAdUnitsSection();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPlacementCatalogSection()
        {
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
        }

        private void DrawAdUnitsSection()
        {
            MultipleAdsAdUnitsUI.DrawAdUnits(mainConfig, serializedConfig);
        }
    }
}
