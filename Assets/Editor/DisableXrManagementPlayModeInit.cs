#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DisableXrManagementPlayModeInit
{
    private static int cleanupAttempts;
    private const int MaxCleanupAttempts = 120;

    static DisableXrManagementPlayModeInit()
    {
        RemoveXrManagementPlayModeHooks();
        EditorApplication.delayCall += RemoveXrManagementPlayModeHooks;
        EditorApplication.update += RemoveXrManagementPlayModeHooksUntilStable;
    }

    private static void RemoveXrManagementPlayModeHooksUntilStable()
    {
        cleanupAttempts++;
        RemoveXrManagementPlayModeHooks();

        if (cleanupAttempts >= MaxCleanupAttempts)
            EditorApplication.update -= RemoveXrManagementPlayModeHooksUntilStable;
    }

    private static void RemoveXrManagementPlayModeHooks()
    {
        try
        {
            var field = typeof(EditorApplication).GetField("playModeStateChanged", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
                return;

            if (field.GetValue(null) is not Delegate callbacks)
                return;

            var removedCount = 0;
            foreach (var callback in callbacks.GetInvocationList())
            {
                var declaringType = callback.Method.DeclaringType;
                var fullName = declaringType?.FullName ?? string.Empty;

                if (!fullName.Contains("UnityEditor.XR.Management.XRPackageInitializationBootstrap", StringComparison.Ordinal))
                    continue;

                if (callback is Action<PlayModeStateChange> playModeCallback)
                {
                    EditorApplication.playModeStateChanged -= playModeCallback;
                    removedCount++;
                }
            }

            if (removedCount > 0)
                Debug.Log($"[XR Fix] Removed {removedCount} XR Management play mode hook(s) to avoid AssetDatabase OOM.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[XR Fix] Could not remove XR Management hook: {ex.Message}");
        }
    }
}
#endif
