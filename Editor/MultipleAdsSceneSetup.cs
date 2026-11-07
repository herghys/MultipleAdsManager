using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Herghys.MultipleAdsManager.Runtime;

namespace Herghys.MultipleAdsManager.Editor
{
    public static class MultipleAdsSceneSetup
    {
        [MenuItem("Tools/MultipleAds/Setup AdsManager on Scene", false, 2)]
        public static void SetupAdsManagerFromToolsMenu()
        {
            SetupAdsManagerOnScene(showDialog: true);
        }

        [MenuItem("GameObject/MultipleAds/Setup AdsManager on Scene", false, 10)]
        public static void SetupAdsManagerFromHierarchy(MenuCommand menuCommand)
        {
            SetupAdsManagerOnScene(showDialog: false);
        }

        public static MultipleAdsManager SetupAdsManagerOnScene(bool showDialog = false)
        {
            // 1. Check for any existing instances in the scene (including inactive GameObjects)
            MultipleAdsManager[] allManagers = Object.FindObjectsByType<MultipleAdsManager>(
                FindObjectsInactive.Include
            );

            if (allManagers != null && allManagers.Length > 0)
            {
                MultipleAdsManager primary = allManagers[0];

                // If multiple instances already exist, clean up duplicates to eliminate scene redundancy
                if (allManagers.Length > 1)
                {
                    int removedCount = 0;
                    for (int i = 1; i < allManagers.Length; i++)
                    {
                        if (allManagers[i] != null && allManagers[i].gameObject != null)
                        {
                            Undo.DestroyObjectImmediate(allManagers[i].gameObject);
                            removedCount++;
                        }
                    }
                    Debug.LogWarning($"[MultipleAds] Prevented redundancy: found {allManagers.Length} MultipleAdsManager instances. Removed {removedCount} duplicate(s).");
                }

                // Ensure the primary instance is active and at scene root
                if (!primary.gameObject.activeSelf)
                {
                    Undo.RecordObject(primary.gameObject, "Activate MultipleAdsManager");
                    primary.gameObject.SetActive(true);
                }

                if (primary.transform.parent != null)
                {
                    Undo.SetTransformParent(primary.transform, null, "Move MultipleAdsManager to Root");
                }

                // Ensure config is assigned if it was missing
                if (primary.Config == null)
                {
                    MultipleAdsConfig cfg = GetOrCreateMainConfig();
                    if (cfg != null)
                    {
                        Undo.RecordObject(primary, "Assign Config to MultipleAdsManager");
                        primary.Configure(cfg);

                        var serialized = new SerializedObject(primary);
                        var prop = serialized.FindProperty("config");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = cfg;
                            serialized.ApplyModifiedProperties();
                        }

                        EditorUtility.SetDirty(primary);
                        EditorSceneManager.MarkSceneDirty(primary.gameObject.scene);
                    }
                }

                Selection.activeGameObject = primary.gameObject;
                EditorGUIUtility.PingObject(primary.gameObject);

                Debug.Log($"[MultipleAds] MultipleAdsManager already exists on GameObject '{primary.gameObject.name}'. Redundant creation prevented.", primary.gameObject);

                if (showDialog)
                {
                    string extraNote = allManagers.Length > 1
                        ? $"\n\n(Prevented redundancy: {allManagers.Length - 1} duplicate instance(s) were cleaned up.)"
                        : "";

                    EditorUtility.DisplayDialog(
                        "MultipleAdsManager Already Exists",
                        $"MultipleAdsManager is already present in this scene on GameObject '{primary.gameObject.name}'.\n\nThe existing instance has been selected to prevent redundancy.{extraNote}",
                        "OK"
                    );
                }

                return primary;
            }

            // 2. Prevent duplicate GameObjects: check if a GameObject named "MultipleAdsManager" already exists
            GameObject existingGo = GameObject.Find("MultipleAdsManager");
            if (existingGo == null)
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    var rootObjects = activeScene.GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        if (root.name == "MultipleAdsManager")
                        {
                            existingGo = root;
                            break;
                        }
                    }
                }
            }

            GameObject go;
            MultipleAdsManager manager;

            if (existingGo != null)
            {
                go = existingGo;
                manager = Undo.AddComponent<MultipleAdsManager>(go);
                Debug.Log($"[MultipleAds] Reused existing GameObject '{go.name}' to prevent redundancy.", go);
            }
            else
            {
                go = new GameObject("MultipleAdsManager");
                Undo.RegisterCreatedObjectUndo(go, "Setup MultipleAdsManager on Scene");

                // Must be at root level because DontDestroyOnLoad requires root GameObjects
                go.transform.SetParent(null);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                manager = Undo.AddComponent<MultipleAdsManager>(go);
            }

            // 3. Find or create config and link it
            MultipleAdsConfig config = GetOrCreateMainConfig();
            if (config != null)
            {
                manager.Configure(config);

                var serializedManager = new SerializedObject(manager);
                var configProp = serializedManager.FindProperty("config");
                if (configProp != null)
                {
                    configProp.objectReferenceValue = config;
                    serializedManager.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(manager);
            }

            // 4. Mark scene dirty to ensure saving
            EditorSceneManager.MarkSceneDirty(go.scene);

            // 5. Select and ping newly created GameObject
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            Debug.Log("[MultipleAds] MultipleAdsManager successfully set up on the current scene.", go);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Setup AdsManager on Scene",
                    $"MultipleAdsManager has been successfully set up on your scene!\n\n• GameObject: '{go.name}'\n• Config: '{(config != null ? config.name : "None")}'\n• Auto Initialize: Enabled",
                    "OK"
                );
            }

            return manager;
        }

        public static MultipleAdsConfig GetOrCreateMainConfig()
        {
            return MultipleAdsConfigGenerator.GetOrCreateConfig(showDialog: false);
        }
    }
}
