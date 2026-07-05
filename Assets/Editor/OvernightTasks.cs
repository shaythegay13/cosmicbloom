// Cosmic Bloom — headless scene-mutation tasks for the overnight build run.
// Each method is invoked via: Unity.exe -batchmode -executeMethod CosmicBloom.EditorTools.OvernightTasks.<Method>
// Methods call EditorApplication.Exit themselves (0 = success), so -quit is not passed.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicBloom.EditorTools
{
    public static class OvernightTasks
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Phase0Cleanup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            int removed = 0;
            foreach (var name in new[] { "RenderTestSprite", "FarmPlotTestRunner" })
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                    removed++;
                    Debug.Log($"[Phase0] removed {name}");
                }
                else
                {
                    Debug.LogWarning($"[Phase0] {name} not found in scene");
                }
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Phase0] cleanup complete, removed={removed}/2");
            EditorApplication.Exit(removed == 2 ? 0 : 1);
        }

        public static void Phase1Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var gm = GameObject.Find("GameManager");
            if (gm == null)
            {
                Debug.LogError("[Phase1] GameManager not found");
                EditorApplication.Exit(1);
                return;
            }

            bool added = false;
            if (gm.GetComponent<Core.GameBootstrap>() == null)
            {
                gm.AddComponent<Core.GameBootstrap>();
                added = true;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Phase1] GameBootstrap on GameManager (added={added})");
            EditorApplication.Exit(0);
        }
    }
}
