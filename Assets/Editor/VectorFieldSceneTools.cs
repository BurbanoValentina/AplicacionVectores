#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VectorFieldSceneTools
{
    const string SceneFolder = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/VectorField.unity";

    [MenuItem("Tools/VectorField/INICIAR PROYECTO (Crear y Abrir Escena)")]
    public static void QuickStart()
    {
        CreateOrOpenMainScene();
    }

    [MenuItem("Tools/VectorField/Crear u Abrir Escena Principal")]
    public static void CreateOrOpenMainScene()
    {
        Directory.CreateDirectory(SceneFolder);

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SetPlayModeStartScene();
            Debug.Log("[VectorField] Escena abierta: " + ScenePath);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var controller = new GameObject("SceneController");
        var bootstrapper = controller.AddComponent<SceneBootstrapper>();
        bootstrapper.arrowPrefab = FindArrowPrefab();
        bootstrapper.RebuildScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddSceneToBuildSettings();
        SetPlayModeStartScene();

        Selection.activeGameObject = controller;
        EditorGUIUtility.PingObject(controller);
        Debug.Log("[VectorField] Escena creada y guardada en " + ScenePath);
    }

    [MenuItem("Tools/VectorField/Usar Esta Escena al Dar Play")]
    public static void SetPlayModeStartScene()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorSceneManager.playModeStartScene = sceneAsset;

        if (sceneAsset != null)
        {
            Debug.Log("[VectorField] Play siempre iniciara desde: " + ScenePath);
        }
        else
        {
            Debug.LogWarning("[VectorField] Aun no existe la escena en " + ScenePath);
        }
    }

    static void AddSceneToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
        {
            if (s.path == ScenePath) return;
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++) updated[i] = scenes[i];
        updated[updated.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = updated;
    }

    static GameObject FindArrowPrefab()
    {
        const string directPath = "Assets/Flecha/Flecha.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directPath);
        if (prefab != null) return prefab;

        var guids = AssetDatabase.FindAssets("t:Prefab Flecha");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
        }

        return null;
    }
}
#endif
