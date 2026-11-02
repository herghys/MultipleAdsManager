using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    [CustomEditor(typeof(MultipleAdsManager))]
    public class MultipleAdsManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty configProp;
        private SerializedProperty autoInitializeProp;

        private void OnEnable()
        {
            configProp = serializedObject.FindProperty("config");
            autoInitializeProp = serializedObject.FindProperty("autoInitialize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (MultipleAdsManager)target;

            // Header Section
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("MultipleAdsManager", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Full Setup Window", "Open MultipleAds Full Setup configuration window."), EditorStyles.miniButton, GUILayout.Width(120)))
            {
                MultipleAdsSetupWindow.Open();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Coordinates multi-provider ad loading, mediation, fallbacks, and callbacks.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Warning if config is unassigned
            if (configProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("No MultipleAdsConfig assigned! MultipleAdsManager will attempt to load a fallback config at runtime, but assigning the asset is recommended.", MessageType.Warning);

                if (GUILayout.Button("Auto-Assign MultipleAdsConfig Asset", GUILayout.Height(24)))
                {
                    var cfg = MultipleAdsSceneSetup.GetOrCreateMainConfig();
                    if (cfg != null)
                    {
                        configProp.objectReferenceValue = cfg;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(manager);
                    }
                }
                EditorGUILayout.Space(4);
            }

            // Core Properties
            EditorGUILayout.PropertyField(configProp);
            EditorGUILayout.PropertyField(autoInitializeProp);

            serializedObject.ApplyModifiedProperties();

            // Quick Navigation Buttons
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Global Settings", GUILayout.Height(22)))
            {
                MultipleAdsWindowRouter.OpenGlobalSettings();
            }
            if (GUILayout.Button("Ad Placements", GUILayout.Height(22)))
            {
                MultipleAdsWindowRouter.OpenPlacements();
            }
            if (GUILayout.Button("Active Mediation", GUILayout.Height(22)))
            {
                MultipleAdsWindowRouter.OpenMediation();
            }
            EditorGUILayout.EndHorizontal();

            // Runtime Status
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Initialized: {(manager.IsInitialized ? "Yes" : "No")}");
                EditorGUILayout.LabelField($"Full-screen Ad Showing: {(manager.IsFullScreenAdShowing ? "Yes" : "No")}");
                EditorGUILayout.EndVertical();
            }
        }
    }
}
