// Adds Renderer2DData to PC_RPAsset and points Main Camera at it.
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public static class CosmicBloom2DRendererSetup
{
    const string RendererPath = "Assets/Settings/PC_2D_Renderer.asset";
    const string UrpPath = "Assets/Settings/PC_RPAsset.asset";

    [MenuItem("Cosmic Bloom/Setup 2D Renderer")]
    public static void Run()
    {
        var renderer2D = EnsureRenderer2DAsset();
        FixTilemapMaterial();

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
        CleanupDuplicateRenderers(urp, renderer2D);
        int rendererIndex = IndexOfRenderer(urp, renderer2D);
        if (rendererIndex < 0)
        {
            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            rendererIndex = list.arraySize;
            list.InsertArrayElementAtIndex(rendererIndex);
            list.GetArrayElementAtIndex(rendererIndex).objectReferenceValue = renderer2D;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            Debug.Log($"[CosmicBloom] Added PC_2D_Renderer to PC_RPAsset at index {rendererIndex}");
        }

        var camGo = GameObject.Find("Main Camera");
        var camData = camGo?.GetComponent<UniversalAdditionalCameraData>();
        if (camData != null)
        {
            Undo.RecordObject(camData, "Set 2D Renderer");
            camData.SetRenderer(rendererIndex);
            EditorUtility.SetDirty(camGo);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[CosmicBloom] Main Camera renderer index = {rendererIndex} ({renderer2D.name})");
    }

    static Renderer2DData EnsureRenderer2DAsset()
    {
        // Always recreate via URP editor factory so ResourceReloader populates shaders/materials.
        if (AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath) != null)
            AssetDatabase.DeleteAsset(RendererPath);

        var asm = Assembly.Load("Unity.RenderPipelines.Universal.Editor");
        var menusType = asm.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus");
        var method = menusType.GetMethod(
            "CreateRendererAsset",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(string), typeof(RendererType), typeof(bool), typeof(string) },
            null);

        var created = method.Invoke(null, new object[] { RendererPath, RendererType._2DRenderer, false, "Renderer" }) as Renderer2DData;
        created.name = "PC_2D_Renderer";
        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CosmicBloom] Created fully initialized Renderer2DData at {RendererPath}");
        return created;
    }

    static void FixTilemapMaterial()
    {
        var ground = GameObject.Find("StarlightVale")?.transform.Find("Ground");
        var tr = ground?.GetComponent<TilemapRenderer>();
        if (tr == null) return;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        tr.sharedMaterial = mat;
        EditorUtility.SetDirty(tr);
    }

    static void CleanupDuplicateRenderers(UniversalRenderPipelineAsset urp, Renderer2DData keep)
    {
        var list = new System.Collections.Generic.List<ScriptableRendererData>();
        bool kept2D = false;
        foreach (var rd in urp.rendererDataList)
        {
            if (rd is Renderer2DData)
            {
                if (!kept2D && rd == keep) { list.Add(rd); kept2D = true; }
            }
            else if (rd != null)
            {
                list.Add(rd);
            }
        }
        if (!kept2D) list.Add(keep);

        var so = new SerializedObject(urp);
        var prop = so.FindProperty("m_RendererDataList");
        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);
    }

    static int IndexOfRenderer(UniversalRenderPipelineAsset urp, ScriptableRendererData target)
    {
        for (int i = 0; i < urp.rendererDataList.Length; i++)
            if (urp.rendererDataList[i] == target) return i;
        return -1;
    }
}
