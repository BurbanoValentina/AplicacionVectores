#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Limpia referencias de scripts faltantes antes de entrar a Play.
/// También deja un solo AudioListener activo para evitar spam en consola.
/// </summary>
[InitializeOnLoad]
public static class ScenePreflightCleaner
{
    static ScenePreflightCleaner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        RemoveMissingScriptsInOpenScenes();
        EnsureSingleAudioListenerInEditor();
    }

    static void RemoveMissingScriptsInOpenScenes()
    {
        bool changed = false;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                if (removed > 0) changed = true;

                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var tr in transforms)
                {
                    if (tr == null) continue;
                    removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
                    if (removed > 0) changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[Preflight] Se limpiaron componentes con scripts faltantes.");
        }
    }

    static void EnsureSingleAudioListenerInEditor()
    {
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (listeners == null || listeners.Length <= 1) return;

        AudioListener keep = null;
        if (Camera.main != null)
        {
            keep = Camera.main.GetComponent<AudioListener>();
        }

        if (keep == null)
        {
            var anyCamera = Object.FindAnyObjectByType<Camera>();
            if (anyCamera != null)
            {
                if (!anyCamera.CompareTag("MainCamera")) anyCamera.tag = "MainCamera";
                keep = anyCamera.GetComponent<AudioListener>();
                if (keep == null) keep = anyCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        if (keep == null) keep = listeners[0];

        foreach (var l in listeners)
        {
            if (l == null) continue;
            l.enabled = l == keep;
        }
    }
}
#endif
