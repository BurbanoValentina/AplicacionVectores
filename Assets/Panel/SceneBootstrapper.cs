using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using VectorField;
using VectorFieldUI;

/// <summary>
/// Construye la escena: suelo, campo vectorial, panel fisico World Space.
///
/// v3:
/// - La estructura de la escena (suelo, panel, VFM) se construye en Edit Mode
///   para que sea visible y editable. Las FLECHAS solo se generan en Play Mode.
/// - Panel es un Canvas WorldSpace (cartel flotante en el mundo).
/// - Camara con movimiento FPS (WASD + raton) en Play Mode.
/// - Click del raton interactua con el panel (dropdowns, botones, scroll).
/// - Sin inputs X/Y.
/// - Usa prefab Flecha de FlechaApp3.unitypackage.
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    [Header("== ARRASTRA AQUI EL PREFAB ==")]
    [Tooltip("Arrastra Assets/Flecha/Flecha.prefab aqui")]
    public GameObject arrowPrefab;

    [Header("Estado")]
    [SerializeField] bool _built = false;

    [Header("Generacion inicial")]
    public bool generateFieldOnBuild = false;
    public bool regenerateFieldOnPlay = true;

    [Header("Posicion del panel cartel")]
    public Vector3 panelWorldPosition = new Vector3(-8f, 3f, 0f);
    public Vector3 panelWorldRotation = new Vector3(0f, 90f, 0f);

    // ──────────────────────────────────────────────────────────────────────────
    void OnEnable()
    {
        if (!Application.isPlaying) return;

        generateFieldOnBuild = true;

        if (!_built || !IsSceneBuilt()) BuildScene();

        // Siempre garantizar FPS controller y worldCamera en Play Mode,
        // incluso si la escena ya estaba construida en Edit Mode.
        EnsureFPSController();
        EnsureCanvasWorldCamera();

        EnsureSingleAudioListenerFromMainCamera();
        SanitizeAllTmpTexts();

        if (regenerateFieldOnPlay)
            ForceGenerateFieldAtRuntime();
    }

    void EnsureFPSController()
    {
        Camera cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (cam == null) return;
        if (cam.GetComponent<PhysicsRaycaster>() == null)
            cam.gameObject.AddComponent<PhysicsRaycaster>();
        if (cam.GetComponent<FPSCameraController>() == null)
            cam.gameObject.AddComponent<FPSCameraController>();
    }

    void EnsureCanvasWorldCamera()
    {
        Camera cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (cam == null) return;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                canvas.worldCamera = cam;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        EnsureSingleAudioListenerFromMainCamera();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (arrowPrefab != null) return;
        // Priorizar flechaApp3
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab flechaApp3");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/Flecha/")) continue;
            arrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (arrowPrefab != null) return;
        }
        // Fallback: cualquier prefab en la carpeta Flecha
        guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab Flecha");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/Flecha/")) continue;
            arrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (arrowPrefab != null) break;
        }
    }
#endif

    [ContextMenu("Reconstruir Escena")]
    public void RebuildScene(bool allowInEditor = true)
    {
        _built = false;
        foreach (string n in new[] { "Main Camera", "DirLight", "Floor",
                                     "GridRoot", "VFM", "WorldPanel", "EventSystem" })
        {
            var go = GameObject.Find(n);
            if (go != null) DestroyImmediate(go);
        }
        var oldCanvas = GameObject.Find("UICanvas");
        if (oldCanvas != null) DestroyImmediate(oldCanvas);

        BuildScene(allowInEditor);
    }

    void BuildScene(bool allowInEditor = false)
    {
        if (!Application.isPlaying && !allowInEditor) return;
        if (_built && IsSceneBuilt()) return;
        _built = true;

        SetupCamera();
        SetupLighting();
        BuildFloor();
        var vfm = BuildVectorFieldManager();
        BuildWorldPanel(vfm);

        // Solo generar flechas en Play Mode
        if (Application.isPlaying && generateFieldOnBuild && vfm != null)
            vfm.GenerateField();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CAMARA con movimiento FPS
    // ──────────────────────────────────────────────────────────────────────────
    void SetupCamera()
    {
        var cam = Camera.main ?? FindAnyObjectByType<Camera>();

        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }

        if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";

        var camListener = cam.GetComponent<AudioListener>()
                         ?? cam.gameObject.AddComponent<AudioListener>();

        // Posicion inicial: vista cenital inclinada
        cam.transform.position = new Vector3(0f, 12f, -10f);
        cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        cam.backgroundColor    = new Color(0.05f, 0.05f, 0.08f);
        cam.clearFlags         = CameraClearFlags.SolidColor;
        cam.nearClipPlane      = 0.1f;

        EnsureSingleAudioListener(camListener);

        // PhysicsRaycaster necesario para que el click funcione con Canvas WorldSpace
        if (cam.GetComponent<PhysicsRaycaster>() == null)
            cam.gameObject.AddComponent<PhysicsRaycaster>();

        // Agregar controlador FPS solo en Play Mode
        if (Application.isPlaying)
        {
            if (cam.GetComponent<FPSCameraController>() == null)
                cam.gameObject.AddComponent<FPSCameraController>();
        }
    }

    static void EnsureSingleAudioListener(AudioListener keepListener)
    {
        if (keepListener == null) return;
        var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var l in listeners)
            if (l != null) l.enabled = (l == keepListener);
    }

    static void EnsureSingleAudioListenerFromMainCamera()
    {
        Camera cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (cam == null) return;
        if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";
        var keep = cam.GetComponent<AudioListener>()
                  ?? cam.gameObject.AddComponent<AudioListener>();
        EnsureSingleAudioListener(keep);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  ILUMINACION
    // ──────────────────────────────────────────────────────────────────────────
    void SetupLighting()
    {
        if (GameObject.Find("DirLight")) return;
        var go = new GameObject("DirLight");
        var l  = go.AddComponent<Light>();
        l.type      = LightType.Directional;
        l.intensity = 1.4f;
        l.color     = new Color(1f, 0.98f, 0.95f);
        go.transform.rotation       = Quaternion.Euler(55f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.28f, 0.28f, 0.32f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SUELO
    // ──────────────────────────────────────────────────────────────────────────
    void BuildFloor()
    {
        if (GameObject.Find("Floor")) return;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
        var mat = new Material(Shader.Find("Standard")) { color = new Color(0.06f, 0.06f, 0.09f) };
        floor.GetComponent<Renderer>().sharedMaterial = mat;

        var gridRoot = new GameObject("GridRoot");
        Color gc = new Color(0.15f, 0.15f, 0.22f);

        for (int i = -12; i <= 12; i++)
        {
            MakeLine(gridRoot.transform, "Gx" + i,
                     new Vector3(i, 0.01f, -12), new Vector3(i, 0.01f, 12), gc, 0.02f);
            MakeLine(gridRoot.transform, "Gz" + i,
                     new Vector3(-12, 0.01f, i), new Vector3(12, 0.01f, i), gc, 0.02f);
        }

        MakeLine(gridRoot.transform, "AxisX",
                 new Vector3(-12, 0.025f, 0), new Vector3(12, 0.025f, 0),
                 new Color(0.9f, 0.2f, 0.15f), 0.05f);
        MakeLine(gridRoot.transform, "AxisZ",
                 new Vector3(0, 0.025f, -12), new Vector3(0, 0.025f, 12),
                 new Color(0.2f, 0.4f, 0.95f), 0.05f);
    }

    static void MakeLine(Transform parent, string name, Vector3 a, Vector3 b, Color c, float w)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPositions(new[] { a, b });
        lr.startWidth = lr.endWidth = w;
        lr.material = new Material(Shader.Find("Unlit/Color")) { color = c };
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.useWorldSpace = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  VECTOR FIELD MANAGER
    // ──────────────────────────────────────────────────────────────────────────
    VectorFieldManager BuildVectorFieldManager()
    {
        var existing = GameObject.Find("VFM");
        VectorFieldManager vfm;

        if (existing)
        {
            vfm = existing.GetComponent<VectorFieldManager>();
            if (vfm == null) vfm = existing.AddComponent<VectorFieldManager>();
        }
        else
        {
            var go = new GameObject("VFM");
            vfm = go.AddComponent<VectorFieldManager>();
        }

        // Siempre aplicar los valores actuales (evita que queden los viejos al reconstruir)
        vfm.vectorCount = 100;
        vfm.fieldRadius = 12f;   // cubre toda la grilla (-12 a +12)
        vfm.arrowScale  = 2.5f;  // flechas grandes
        vfm.arrowPrefab = null;  // usar generacion procedural
        return vfm;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  PANEL FISICO WORLD SPACE
    // ──────────────────────────────────────────────────────────────────────────
    void BuildWorldPanel(VectorFieldManager vfm)
    {
        if (GameObject.Find("WorldPanel")) return;

        var panelRoot = new GameObject("WorldPanel");
        panelRoot.transform.position = panelWorldPosition;
        panelRoot.transform.rotation = Quaternion.Euler(panelWorldRotation);

        // Canvas World Space
        var canvasGO = new GameObject("PanelCanvas");
        canvasGO.transform.SetParent(panelRoot.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        canvas.worldCamera = Camera.main ?? FindAnyObjectByType<Camera>();

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(400, 500);
        canvasRT.localScale = new Vector3(0.008f, 0.008f, 0.008f);
        canvasRT.localPosition = Vector3.zero;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGO.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        // Fondo
        var bgImg = canvasGO.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.07f, 0.92f);

        // Borde
        var outline = canvasGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.6f, 1f, 0.6f);
        outline.effectDistance = new Vector2(3, 3);

        // ScrollRect
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(canvasGO.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(8, 8);
        scrollRT.offsetMax = new Vector2(-20, -8);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 40f;

        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0, 0, 0, 0.01f);
        scrollGO.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(12, 12, 14, 14);
        vlg.spacing              = 8f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;

        // Scrollbar
        var scrollbarGO = CreateScrollbar(canvasGO.transform);
        scrollRect.verticalScrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 2f;

        // ── Contenido ────────────────────────────────────────────────────────
        var content = contentGO.transform;

        MakeLabel(content, "CAMPO VECTORIAL", 22f,
                  new Color(0.35f, 0.85f, 1f), FontStyles.Bold, 36f);

        MakeLabel(content,
                  "Stewart - Calculo 8a Ed. 16.1\nSelecciona formula y cantidad.",
                  10f, new Color(0.6f, 0.6f, 0.65f), FontStyles.Italic, 38f);

        MakeSep(content);

        MakeLabel(content, "Cantidad de vectores", 13f,
                  new Color(0.85f, 0.85f, 0.9f), FontStyles.Bold, 22f);
        var ddCount = MakeDropdown(content, 36f);

        MakeSep(content);

        MakeLabel(content, "Formula del campo", 13f,
                  new Color(0.85f, 0.85f, 0.9f), FontStyles.Bold, 22f);
        var ddFormula = MakeDropdown(content, 36f);

        MakeSep(content);

        var btnGen = MakeButton(content, "GENERAR CAMPO",
                                new Color(0.08f, 0.55f, 0.22f), 42f);
        var btnRst = MakeButton(content, "REINICIAR",
                                new Color(0.20f, 0.35f, 0.60f), 34f);
        var btnDel = MakeButton(content, "ELIMINAR CAMPO",
                                new Color(0.55f, 0.12f, 0.10f), 34f);

        MakeSep(content);

        var statusLbl = MakeLabel(content, "Listo. Selecciona formula.",
                                  11f, new Color(0.4f, 1f, 0.4f), FontStyles.Italic, 30f);

        // Conectar PanelController
        var pc = canvasGO.AddComponent<PanelController>();
        pc.fieldManager = vfm;
        pc.dropCount    = ddCount;
        pc.dropFormula  = ddFormula;
        pc.btnGenerate  = btnGen;
        pc.btnReset     = btnRst;
        pc.btnDelete    = btnDel;
        pc.statusLabel  = statusLbl;
    }

    GameObject CreateScrollbar(Transform parent)
    {
        var go = new GameObject("Scrollbar");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 0.5f);
        rt.sizeDelta = new Vector2(12, 0);
        rt.anchoredPosition = new Vector2(-2, 0);

        var bgImg = go.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

        var scrollbar = go.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var slideArea = new GameObject("Sliding Area");
        slideArea.transform.SetParent(go.transform, false);
        var slideRT = slideArea.AddComponent<RectTransform>();
        slideRT.anchorMin = Vector2.zero;
        slideRT.anchorMax = Vector2.one;
        slideRT.offsetMin = Vector2.zero;
        slideRT.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle");
        handle.transform.SetParent(slideArea.transform, false);
        var handleRT = handle.AddComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;

        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.3f, 0.6f, 1f, 0.7f);

        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;

        return go;
    }

    bool IsSceneBuilt()
    {
        return GameObject.Find("VFM") != null
            && GameObject.Find("WorldPanel") != null
            && Camera.main != null;
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();

        Type nimt = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (nimt != null)
            es.AddComponent(nimt);
        else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ── Helpers UI ───────────────────────────────────────────────────────────
    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static TextMeshProUGUI MakeLabel(Transform parent, string text, float size,
                                     Color col, FontStyles style, float height)
    {
        var go = MakeRect("Lbl", parent);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = height; le.flexibleWidth = 1f;
        var t  = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.fontStyle = style; t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static TMP_Dropdown MakeDropdown(Transform parent, float height)
    {
        var res = new TMP_DefaultControls.Resources();
        var go  = TMP_DefaultControls.CreateDropdown(res);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = height;
        var img = go.GetComponent<Image>();
        if (img) img.color = new Color(0.12f, 0.12f, 0.18f);
        var dd  = go.GetComponent<TMP_Dropdown>();

        // Unity 6: TMP_Dropdown.m_AlphaTweenRunner se inicializa en Start(),
        // pero si el dropdown recibe un click antes de que Start() corra (mismo frame
        // de creacion) lanza NullReferenceException en AlphaFadeList.
        // Forzamos la inicializacion via reflection para que sea seguro de inmediato.
        ForceInitTweenRunner(dd);

        var cap = go.transform.Find("Label");
        if (cap) { var t = cap.GetComponent<TextMeshProUGUI>(); if (t) { t.fontSize = 11f; t.color = Color.white; } }
        return dd;
    }

    static void ForceInitTweenRunner(TMP_Dropdown dd)
    {
        if (dd == null) return;
        var fi = typeof(TMP_Dropdown).GetField("m_AlphaTweenRunner",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null || fi.GetValue(dd) != null) return;   // ya inicializado

        // TweenRunner<FloatTween> es internal — se crea via reflection pura.
        // TMP_Dropdown y TweenRunner estan en el mismo ensamblado (com.unity.ugui).
        var asm            = typeof(TMP_Dropdown).Assembly;
        var floatTweenT    = asm.GetType("UnityEngine.UI.CoroutineTween.FloatTween");
        var tweenRunnerOpenT = asm.GetType("UnityEngine.UI.CoroutineTween.TweenRunner`1");
        if (floatTweenT == null || tweenRunnerOpenT == null) return;

        var tweenRunnerT = tweenRunnerOpenT.MakeGenericType(floatTweenT);
        var runner = Activator.CreateInstance(tweenRunnerT);
        tweenRunnerT.GetMethod("Init")?.Invoke(runner, new object[] { dd });
        fi.SetValue(dd, runner);
    }

    static Button MakeButton(Transform parent, string label, Color color, float height)
    {
        var res = new TMP_DefaultControls.Resources();
        var go  = TMP_DefaultControls.CreateButton(res);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = height;
        var img = go.GetComponent<Image>();
        if (img) img.color = color;
        var btn = go.GetComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = color;
        cb.highlightedColor = color * 1.35f;
        cb.pressedColor     = color * 0.6f;
        btn.colors = cb;
        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt) { txt.text = label; txt.fontSize = 13f; txt.color = Color.white; txt.fontStyle = FontStyles.Bold; }
        return btn;
    }

    static void MakeSep(Transform parent)
    {
        var go = MakeRect("Sep", parent);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 1f;
        go.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
    }

    static void SanitizeAllTmpTexts()
    {
        var texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (var tmp in texts)
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;
            string s = tmp.text
                .Replace("\u25b6", "").Replace("\u21ba", "").Replace("\ud83d\uddd1", "")
                .Replace("\u2714", "OK").Replace("\u26a0", "WARN")
                .Replace("\u2192", "->").Replace("\u2197", "").Replace("\u2199", "")
                .Replace("\u00b3", "^3").Replace("\u00b2", "^2")
                .Replace("\u00a7", "").Replace("\u2013", "-");
            if (!string.Equals(tmp.text, s)) tmp.text = s;
        }
    }

    void ForceGenerateFieldAtRuntime()
    {
        var vfm = FindAnyObjectByType<VectorFieldManager>();
        if (vfm == null)
        {
            RebuildScene();
            vfm = FindAnyObjectByType<VectorFieldManager>();
        }
        if (vfm == null) return;

        // Siempre forzar los parametros correctos aunque el VFM ya existiera
        vfm.arrowPrefab = null;
        vfm.fieldRadius = 12f;   // cubre toda la grilla (-12 a +12)
        vfm.arrowScale  = 2.5f;

        vfm.GenerateField();
    }
}
