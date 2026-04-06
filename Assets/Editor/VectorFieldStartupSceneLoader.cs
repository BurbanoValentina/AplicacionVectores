#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Abre la escena principal al iniciar el Editor.
/// Construye la estructura (suelo, panel, VFM) en Edit Mode para que sea VISIBLE.
/// Las flechas SOLO se generan en Play Mode.
/// </summary>
[InitializeOnLoad]
public static class VectorFieldStartupSceneLoader
{
    const string SceneFolder = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/VectorField.unity";

    static VectorFieldStartupSceneLoader()
    {
        EditorApplication.delayCall += OpenMainSceneIfNeeded;
    }

    static void OpenMainSceneIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        EnsureMainSceneExists();
        if (!File.Exists(ScenePath)) return;

        AddSceneToBuildSettings();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

        var active = EditorSceneManager.GetActiveScene();

        if (active.path == ScenePath)
        {
            ValidateAndRepairOpenedScene(active);
            return;
        }

        if (active.isDirty) return;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateAndRepairOpenedScene(opened);
            Debug.Log("[VectorField] Escena cargada: " + ScenePath);
        }
    }

    static void EnsureMainSceneExists()
    {
        if (File.Exists(ScenePath)) return;

        Directory.CreateDirectory(SceneFolder);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var controller = new GameObject("SceneController");
        var bootstrapper = controller.AddComponent<SceneBootstrapper>();
        bootstrapper.arrowPrefab = FindArrowPrefab();
        
        // Construir estructura visible (suelo, panel, VFM) pero sin flechas
        bootstrapper.generateFieldOnBuild = false;
        bootstrapper.RebuildScene(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddSceneToBuildSettings();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("[VectorField] Escena creada en: " + ScenePath);
    }

    static void ValidateAndRepairOpenedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        var controller = GameObject.Find("SceneController");
        SceneBootstrapper bootstrapper = null;

        if (controller != null)
            bootstrapper = controller.GetComponent<SceneBootstrapper>();

        if (bootstrapper == null)
        {
            if (controller == null) controller = new GameObject("SceneController");
            bootstrapper = controller.AddComponent<SceneBootstrapper>();
        }

        if (bootstrapper.arrowPrefab == null)
            bootstrapper.arrowPrefab = FindArrowPrefab();

        if (!Application.isPlaying)
        {
            // Limpiar flechas huerfanas en Edit Mode
            var vfmGo = GameObject.Find("VFM");
            if (vfmGo != null && vfmGo.transform.childCount > 0)
            {
                for (int i = vfmGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(vfmGo.transform.GetChild(i).gameObject);
                Debug.Log("[VectorField] Flechas limpiadas de Edit Mode.");
            }

            // Verificar que la estructura este completa
            var panelGo = GameObject.Find("WorldPanel") ?? GameObject.Find("UICanvas");
            var floorGo = GameObject.Find("Floor");

            if (vfmGo == null || panelGo == null || floorGo == null)
            {
                bootstrapper.generateFieldOnBuild = false;
                bootstrapper.RebuildScene(true);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            return;
        }

        // Play Mode
        var vfmPlay = GameObject.Find("VFM");
        var uiPlay = GameObject.Find("WorldPanel");
        var floorPlay = GameObject.Find("Floor");
        bool hasArrows = vfmPlay != null && vfmPlay.transform.childCount > 0;
        bool needsRebuild = vfmPlay == null || uiPlay == null || floorPlay == null || !hasArrows;
        if (!needsRebuild) return;

        bootstrapper.generateFieldOnBuild = true;
        bootstrapper.RebuildScene(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void AddSceneToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == ScenePath) return;

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
