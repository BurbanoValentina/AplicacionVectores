#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DisableXriAnalyticsHooks
{
    static DisableXriAnalyticsHooks()
    {
        // Delay one tick so package hooks are already registered.
        EditorApplication.delayCall += RemoveXriAnalyticsPlayModeHooks;
    }

    private static void RemoveXriAnalyticsPlayModeHooks()
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

                if (!fullName.Contains("UnityEditor.XR.Interaction.Toolkit.Analytics.Hooks", StringComparison.Ordinal))
                    continue;

                if (callback is Action<PlayModeStateChange> playModeCallback)
                {
                    EditorApplication.playModeStateChanged -= playModeCallback;
                    removedCount++;
                }
            }

            if (removedCount > 0)
                Debug.Log($"[XRI Fix] Removed {removedCount} XR Interaction Toolkit analytics hook(s) from play mode events to avoid editor OOM.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[XRI Fix] Could not remove XR analytics hooks: {ex.Message}");
        }
    }
}
#endif
