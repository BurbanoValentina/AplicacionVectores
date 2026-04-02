using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Genera el prefab Flecha SOLO si no existe.
/// Si ya existe (por ejemplo, importado de FlechaApp3.unitypackage),
/// NO lo sobreescribe.
/// </summary>
#if UNITY_EDITOR
public class ArrowPrefabGenerator : MonoBehaviour
{
    [InitializeOnLoadMethod]
    public static void GenerateArrowPrefabIfNeeded()
    {
        string prefabPath = "Assets/Flecha/Flecha.prefab";
        
        // Si ya existe, NO sobreescribir — respetar el prefab de FlechaApp3
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            return;

        Debug.Log("[ArrowPrefabGenerator] No existe Flecha.prefab. Creando uno basico...");
        Debug.Log("[ArrowPrefabGenerator] Para usar la flecha de FlechaApp3, importa el .unitypackage DESPUES.");

        GameObject arrowGO = new GameObject("Flecha");
        arrowGO.transform.position = Vector3.zero;

        // Cuerpo
        GameObject bodyGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bodyGO.name = "Body";
        bodyGO.transform.SetParent(arrowGO.transform);
        bodyGO.transform.localPosition = new Vector3(0, 0, 0.5f);
        bodyGO.transform.localRotation = Quaternion.identity;
        bodyGO.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);

        Renderer bodyRenderer = bodyGO.GetComponent<Renderer>();
        bodyRenderer.material = new Material(Shader.Find("Standard")) { color = Color.white };

        Collider bodyCollider = bodyGO.GetComponent<Collider>();
        if (bodyCollider != null) DestroyImmediate(bodyCollider);

        // Punta
        GameObject tipGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tipGO.name = "Tip";
        tipGO.transform.SetParent(arrowGO.transform);
        tipGO.transform.localPosition = new Vector3(0, 0, 1.0f);
        tipGO.transform.localRotation = Quaternion.Euler(0, 0, 0);
        tipGO.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        Renderer tipRenderer = tipGO.GetComponent<Renderer>();
        tipRenderer.material = new Material(Shader.Find("Standard")) { color = Color.red };

        Collider tipCollider = tipGO.GetComponent<Collider>();
        if (tipCollider != null) DestroyImmediate(tipCollider);

        string folderPath = "Assets/Flecha";
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets", "Flecha");

        PrefabUtility.SaveAsPrefabAsset(arrowGO, prefabPath);
        DestroyImmediate(arrowGO);

        AssetDatabase.Refresh();
        Debug.Log("[ArrowPrefabGenerator] Prefab basico creado en: " + prefabPath);
    }
}
#endif
