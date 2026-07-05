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

        /// <summary>RegionWorldBuilder now creates all five region tilemaps at runtime
        /// from regions.json — the hand-baked StarlightVale grid is superseded.</summary>
        public static void Phase2RemoveBakedGrid()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var baked = GameObject.Find("StarlightVale");
            if (baked != null)
            {
                Object.DestroyImmediate(baked);
                Debug.Log("[Phase2] removed baked StarlightVale grid (runtime builder replaces it)");
            }
            else
            {
                Debug.LogWarning("[Phase2] baked StarlightVale not found (already removed?)");
            }
            EditorSceneManager.SaveScene(scene);
            EditorApplication.Exit(0);
        }

        /// <summary>FarmGrid now spawns plot views for every lush cell at runtime —
        /// the single proof-of-concept FarmPlot instance and prefab are superseded.</summary>
        public static void Phase3RemoveLonePlot()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var lone = GameObject.Find("FarmPlot");
            if (lone != null)
            {
                Object.DestroyImmediate(lone);
                Debug.Log("[Phase3] removed lone FarmPlot instance");
            }
            EditorSceneManager.SaveScene(scene);

            if (AssetDatabase.DeleteAsset("Assets/Prefabs/FarmPlot.prefab"))
                Debug.Log("[Phase3] deleted FarmPlot.prefab (runtime FarmPlot.Create replaces it)");
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        /// <summary>Phase-2 spec Phase 0: Android development build (IL2CPP per
        /// current project settings — not switched silently).</summary>
        // NOTE (2026-07-05): Unity 6000.5 removed Android x86_64 entirely, so the
        // x86_64 emulator can only run our ARM64 APK through binary translation.
        // Dev builds SIGILL there in profiler-thread init; the non-dev variant
        // below exists to smoke-test runtime behavior on the emulator anyway.
        public static void Phase0AndroidBuild()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/CosmicBloom-dev.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Phase0-Android] result={summary.result} sizeMB={summary.totalSize / (1024f * 1024f):F1} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"backend={PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)}");
            EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Non-development variant: skips the profiler init path that
        /// SIGILLs under the emulator's ARM→x86 binary translation.</summary>
        public static void Phase0AndroidBuildRelease()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/CosmicBloom-release.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Phase0-Android-Release] result={summary.result} sizeMB={summary.totalSize / (1024f * 1024f):F1} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings}");
            EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
