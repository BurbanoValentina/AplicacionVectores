#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
internal static class PlayModeStartSceneSetter
{
    const string BigIslandScenePath = "Assets/Arquitectura/Gameplay/Entorno/RPG Tiny Fantasy Forest PBR/Scene/BigIsland.unity";

    static PlayModeStartSceneSetter()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BigIslandScenePath);
        if (sceneAsset != null)
            EditorSceneManager.playModeStartScene = sceneAsset;
    }
}
#endif
