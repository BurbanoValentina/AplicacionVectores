#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DisablePhotonEditorProjectScan
{
    private static int cleanupAttempts;
    private const int MaxCleanupAttempts = 120;

    static DisablePhotonEditorProjectScan()
    {
        RemovePhotonEditorCallbacks();
        EditorApplication.delayCall += RemovePhotonEditorCallbacks;
        EditorApplication.update += RemovePhotonEditorCallbacksUntilStable;
    }

    private static void RemovePhotonEditorCallbacksUntilStable()
    {
        cleanupAttempts++;
        RemovePhotonEditorCallbacks();

        if (cleanupAttempts >= MaxCleanupAttempts)
            EditorApplication.update -= RemovePhotonEditorCallbacksUntilStable;
    }

    private static void RemovePhotonEditorCallbacks()
    {
        try
        {
            RemoveFromEvent("delayCall", "Photon.Pun.PhotonEditor");
            RemoveFromEvent("projectChanged", "Photon.Pun.PhotonEditor");
            RemoveFromEvent("projectWindowChanged", "Photon.Pun.PhotonEditor");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Photon Fix] Could not remove Photon editor callbacks: {ex.Message}");
        }
    }

    private static void RemoveFromEvent(string fieldName, string typeName)
    {
        var field = typeof(EditorApplication).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null)
            return;

        if (field.GetValue(null) is not Delegate callbacks)
            return;

        var removedCount = 0;
        foreach (var callback in callbacks.GetInvocationList())
        {
            var declaringType = callback.Method.DeclaringType;
            var fullName = declaringType?.FullName ?? string.Empty;
            if (!string.Equals(fullName, typeName, StringComparison.Ordinal))
                continue;

            callbacks = Delegate.Remove(callbacks, callback);
            removedCount++;
        }

        if (removedCount > 0)
        {
            field.SetValue(null, callbacks);
            Debug.Log($"[Photon Fix] Removed {removedCount} Photon editor callback(s) from {fieldName} to avoid AssetDatabase OOM.");
        }
    }
}
#endif
