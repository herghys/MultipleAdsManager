using UnityEditor;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    [CustomEditor(typeof(MultipleAdsConfig))]
    public class MultipleAdsConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "MultipleAdsManager Configuration\n" +
                "Manages ad mediation sources, weighted distribution, and test device IDs.\n" +
                "Each provider has its own dedicated ScriptableObject under Assets/MultipleAds/AdsConfig/.",
                MessageType.Info
            );
            EditorGUILayout.Space();

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Open MultipleAds Setup Window", GUILayout.Height(30)))
            {
                MultipleAdsSetupWindow.Open();
            }
        }
    }
}
