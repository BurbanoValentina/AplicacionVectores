using UnityEngine;
using UnityEngine.SceneManagement;

public static class AutoSceneInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSceneBootstrapper()
    {
        var activeScene = SceneManager.GetActiveScene();
        bool isUntitledScene = string.IsNullOrEmpty(activeScene.path);
        if (!isUntitledScene) return;

        if (Object.FindAnyObjectByType<SceneBootstrapper>() != null) return;

        var go = new GameObject("SceneController");
        go.AddComponent<SceneBootstrapper>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSingleAudioListener()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;
            if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";
        }

        var keep = cam.GetComponent<AudioListener>();
        if (keep == null) keep = cam.gameObject.AddComponent<AudioListener>();

        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var listener in listeners)
        {
            if (listener == null) continue;
            listener.enabled = listener == keep;
        }
    }
}
