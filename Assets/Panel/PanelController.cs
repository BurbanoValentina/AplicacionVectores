using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VectorField;

namespace VectorFieldUI
{
    /// <summary>
    /// Controla el panel físico (cartel) en el mundo 3D.
    /// Modo Stewart: permite ingresar dos funciones P(x,y) y Q(x,y) para generar un campo vectorial
    /// F(x,y)=<P(x,y),Q(x,y)> con un temporizador de 10s.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class PanelController : MonoBehaviour
    {
        [Header("Referencia al manager del campo")]
        public VectorFieldManager fieldManager;

        [Header("Controles legacy (se ocultan)")]
        public TMP_Dropdown dropCount;
        public TMP_Dropdown dropFormula;
        public TMP_Dropdown dropZone;
        public float        waterRadius = 45f;
        public Vector2      waterCenter = Vector2.zero;

        [Header("Selección de función (dropdown)")]
        [Tooltip("Primera función P(x,y)")]
        public TMP_Dropdown inputScaleX;
        [Tooltip("Segunda función Q(x,y)")]
        public TMP_Dropdown inputScaleY;

        [Header("Botones")]
        public Button btnGenerate;
        public Button btnReset;
        public Button btnDelete;
        public Button btnGenerateDucks;

        [Header("Patos")]
        public DuckFieldSpawner duckSpawner;
        public float ducksEnableDelaySeconds = 5f;

        [Header("Etiqueta (timer)")]
        public TextMeshProUGUI statusLabel;

        [Header("Layout (panel)")]
        [Tooltip("Si está activo, también aplica el estilo del panel cuando NO está en Play (para ocultar los controles legacy y mostrar el título).")]
        public bool applyStyleInEditMode = true;
        [Tooltip("Tamaño del panel (RectTransform) para evitar espacio vacío.")]
        public Vector2 panelSize = new Vector2(480f, 520f);
        [Tooltip("Fuerza que el ScrollRect arranque arriba (para que se vea el título).")]
        public bool forceScrollTop = true;

        const int COUNTDOWN_SECONDS = 10;
        Coroutine _countdown;
        string _pendingP;
        string _pendingQ;

        void Awake()
        {
            if (Application.isPlaying)
            {
                //RemoveConflictingTrackedRaycasters();
                EnsureExtraControls();
            }
        }

        void OnEnable()
        {
            if (Application.isPlaying)
                return;

            if (!applyStyleInEditMode)
                return;

            // En modo edición: ocultar panel legacy y asegurar título/scroll/layout.
            ApplyPanelVisualStyle();
            ApplyPanelLayoutTweaks();
        }

        void OnValidate()
        {
            // Evitar tocar RectTransform/layout desde OnValidate (genera warnings).
            // El estilo del panel se aplica en OnEnable (modo edición) y en Start (Play).
        }

        void Start()
        {
            if (!Application.isPlaying)
                return;

            ApplyPanelVisualStyle();
            ApplyPanelLayoutTweaks();

            if (fieldManager == null)
                fieldManager = GetComponentInChildren<VectorFieldManager>(true);
            if (fieldManager == null)
                fieldManager = FindFirstObjectByType<VectorFieldManager>();
            if (fieldManager == null)
            {
                SetStatus("ERROR: VectorFieldManager no encontrado");
                return;
            }

            Invoke(nameof(InitControls), 0.05f);
            if (forceScrollTop)
                Invoke(nameof(ForceScrollToTop), 0.08f);

            // Validar y agregar listeners a botones
            try
            {
                if (btnGenerate != null) btnGenerate.onClick.AddListener(OnGenerate);
                if (btnDelete != null)   btnDelete.onClick.AddListener(OnDelete);
                if (btnGenerateDucks != null) btnGenerateDucks.onClick.AddListener(OnGenerateDucks);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al asignar listeners de botones: {ex.Message}", this);
            }

            if (duckSpawner == null)
                duckSpawner = FindFirstObjectByType<DuckFieldSpawner>();
            if (duckSpawner == null)
                duckSpawner = new GameObject("DuckFieldSpawner").AddComponent<DuckFieldSpawner>();

            SetDucksButtonEnabled(false);

            SetTimer(COUNTDOWN_SECONDS);
        }

        void ApplyPanelLayoutTweaks()
        {
            // 1) Reducir tamaño del panel para evitar espacio vacío.
            if (TryGetComponent<RectTransform>(out var rt))
            {
                if (panelSize.x > 0f && panelSize.y > 0f)
                    rt.sizeDelta = panelSize;
            }

            // 2) Forzar scroll arriba para que el título quede visible.
            if (forceScrollTop && Application.isPlaying)
                ForceScrollToTop();
        }

        void ForceScrollToTop()
        {
            if (!Application.isPlaying)
                return;

            var sr = GetComponentInChildren<ScrollRect>(true);
            if (sr == null)
                return;

            // Rebuild previo ayuda a que el contenido calcule tamaño.
            Canvas.ForceUpdateCanvases();
            sr.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();

            if (sr.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
        }

        void ApplyPanelVisualStyle()
        {
            HideLegacyFunctionInputFields();

            // Ocultar controles legacy (formula/zona/reset). Conservamos SOLO el dropdown de cantidad.
            if (dropCount) dropCount.gameObject.SetActive(true);
            if (dropFormula) dropFormula.gameObject.SetActive(false);
            if (dropZone) dropZone.gameObject.SetActive(false);
            if (btnReset) btnReset.gameObject.SetActive(false);

            DisableIfExists("FunctionsSectionLabel");
            // NO ocultar CountFunctionLabel: es el label de la cantidad.
            DisableIfExists("CountDesc");
            DisableIfExists("FormulaFunctionLabel");
            DisableIfExists("FormulaDesc");
            DisableIfExists("ScaleXDesc");
            DisableIfExists("ScaleYDesc");
            DisableIfExists("ZoneDesc");
            DisableIfExists("DropdownZone_Label");

            // Asegurar texto del label de cantidad (si existe)
            SetLabelTextIfExists("CountFunctionLabel", "Cantidad de vectores:");

            var titleLabel = FindTitleLabel();
            if (titleLabel != null)
            {
                if (!titleLabel.gameObject.activeSelf)
                    titleLabel.gameObject.SetActive(true);

                titleLabel.text = "Campo Vectorial";
                titleLabel.fontSize = 48f;
                titleLabel.fontStyle = FontStyles.Bold;
                titleLabel.color = new Color(0.08f, 0.56f, 1f, 1f);
                titleLabel.alignment = TextAlignmentOptions.Center;
            }

            // Sin descripción (pedido)
            var desc = FindDescriptionLabel();
            if (desc != null)
            {
                desc.text = string.Empty;
                desc.gameObject.SetActive(false);
            }

            // Labels de los dropdowns
            SetLabelTextIfExists("InputScaleX_Label", "Primera funcion f(x,y):");
            SetLabelTextIfExists("InputScaleY_Label", "Segunda funcion f(x,y):");

            // Configurar dropdowns con las opciones de eje
            ConfigureFunctionDropdown(inputScaleX, new[] { "X", "-X", "-Y", "Y", "-X-Y", "Y^2" }, 0);
            ConfigureFunctionDropdown(inputScaleY, new[] { "Y", "-Y", "X", "-X", "X-Y", "0" }, 0);

            // Timer visible
            if (statusLabel == null)
                statusLabel = FindStatusLabelFallback();
            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(true);
                statusLabel.fontSize = 22f;
                statusLabel.fontStyle = FontStyles.Bold;
                statusLabel.color = new Color(1f, 1f, 1f, 1f);
                statusLabel.alignment = TextAlignmentOptions.Center;
            }

            // Asegurar orden: título arriba, luego timer.
            if (titleLabel != null && statusLabel != null)
            {
                var titleTr = titleLabel.transform;
                var timerTr = statusLabel.transform;
                if (titleTr.parent == timerTr.parent)
                {
                    titleTr.SetAsFirstSibling();
                    timerTr.SetSiblingIndex(Mathf.Min(1, timerTr.parent.childCount - 1));
                }
            }

            if (btnGenerate != null)
            {
                var buttonText = btnGenerate.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "COMENZAR CAMPO VECTORIAL";
                    buttonText.fontStyle = FontStyles.Bold;
                    buttonText.color = Color.white;
                }

                var image = btnGenerate.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.12f, 0.70f, 0.26f, 1f);

                var colors = btnGenerate.colors;
                colors.normalColor = new Color(0.12f, 0.70f, 0.26f, 1f);
                colors.highlightedColor = new Color(0.18f, 0.78f, 0.33f, 1f);
                colors.pressedColor = new Color(0.08f, 0.58f, 0.20f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.colorMultiplier = 1f;
                btnGenerate.colors = colors;
            }

            if (btnDelete != null)
            {
                var buttonText = btnDelete.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "ELIMINAR CAMPO VECTORIAL";
                    buttonText.fontStyle = FontStyles.Bold;
                    buttonText.color = Color.white;
                }
            }

            if (btnGenerateDucks != null)
            {
                var buttonText = btnGenerateDucks.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "GENERAR PATOS";
                    buttonText.fontStyle = FontStyles.Bold;
                    buttonText.color = Color.black;
                }

                var image = btnGenerateDucks.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.96f, 0.80f, 0.12f, 1f);

                var colors = btnGenerateDucks.colors;
                colors.normalColor = new Color(0.96f, 0.80f, 0.12f, 1f);
                colors.highlightedColor = new Color(1.00f, 0.86f, 0.22f, 1f);
                colors.pressedColor = new Color(0.86f, 0.70f, 0.05f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.colorMultiplier = 1f;
                btnGenerateDucks.colors = colors;
            }
        }

        void HideLegacyFunctionInputFields()
        {
            var inputs = GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (input == null)
                    continue;

                if (input.name == "InputScaleX" || input.name == "InputScaleY")
                    input.gameObject.SetActive(false);
            }
        }

        void InitControls()
        {
            // Dropdown de cantidad: 100..2000
            if (dropCount != null)
            {
                try
                {
                    dropCount.ClearOptions();
                    var options = new List<TMP_Dropdown.OptionData>();
                    for (int n = 100; n <= 2000; n += 100)
                        options.Add(new TMP_Dropdown.OptionData(n.ToString()));
                    dropCount.AddOptions(options);

                    int desired = fieldManager != null ? Mathf.Clamp(fieldManager.vectorCount, 100, 2000) : 1000;
                    int idx = Mathf.Clamp((desired - 100) / 100, 0, options.Count - 1);
                    dropCount.SetValueWithoutNotify(idx);

                    dropCount.onValueChanged.RemoveListener(OnCountDropdownChanged);
                    dropCount.onValueChanged.AddListener(OnCountDropdownChanged);

                    // Aplicar inmediatamente
                    OnCountDropdownChanged(dropCount.value);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Panel] Error al inicializar dropdown de cantidad: {ex.Message}", this);
                }
            }
        }

        void OnCountDropdownChanged(int _)
        {
            int count = GetSelectedCount();
            if (fieldManager != null)
                fieldManager.vectorCount = count;
        }

        int GetSelectedCount()
        {
            if (dropCount == null || dropCount.options == null || dropCount.options.Count == 0)
                return 1000;

            int idx = Mathf.Clamp(dropCount.value, 0, dropCount.options.Count - 1);
            string txt = dropCount.options[idx].text;
            if (int.TryParse(txt, out int n))
                return Mathf.Clamp(n, 100, 2000);

            return 1000;
        }

        void DisableIfExists(string name)
        {
            var tr = transform.Find(name);
            if (tr != null)
                tr.gameObject.SetActive(false);

            // También puede estar dentro del content del panel
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    all[i].gameObject.SetActive(false);
                    break;
                }
            }
        }

        void SetLabelTextIfExists(string labelName, string text)
        {
            var all = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var lbl = all[i];
                if (lbl == null) continue;
                if (lbl.name != labelName) continue;
                lbl.gameObject.SetActive(true);
                lbl.text = text;
                lbl.fontSize = 18f;
                lbl.fontStyle = FontStyles.Bold;
                lbl.color = new Color(0.92f, 0.92f, 0.92f, 1f);
                return;
            }
        }

        TextMeshProUGUI FindDescriptionLabel()
        {
            var labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null) continue;
                if (label.name == "PanelDescriptionLabel")
                    return label;
            }
            return null;
        }

        void ConfigureFunctionDropdown(TMP_Dropdown dropdown, string[] options, int defaultIndex)
        {
            if (dropdown == null)
                return;

            dropdown.ClearOptions();
            var opts = new List<TMP_Dropdown.OptionData>();
            for (int i = 0; i < options.Length; i++)
                opts.Add(new TMP_Dropdown.OptionData(options[i]));
            dropdown.AddOptions(opts);

            int safeIdx = Mathf.Clamp(defaultIndex, 0, opts.Count - 1);
            dropdown.SetValueWithoutNotify(safeIdx);
        }

        TextMeshProUGUI FindTitleLabel()
        {
            var labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                if (label.name == "PanelTitleLabel")
                    return label;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                if (label.text != null && label.text.Contains("CAMPO"))
                    return label;
            }

            return null;
        }

        TextMeshProUGUI FindStatusLabelFallback()
        {
            var labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                if (label.name == "TimerLabel" || label.name == "StatusLabel")
                    return label;
            }

            return null;
        }

        void EnsureExtraControls()
        {
            TMP_Dropdown source = dropFormula ? dropFormula : dropCount;
            if (!source) return;

            Transform content = source.transform.parent;
            if (!content) return;

            if (!inputScaleX)
                inputScaleX = CreateRuntimeAxisDropdown(content, source, "InputScaleX");

            if (!inputScaleY)
                inputScaleY = CreateRuntimeAxisDropdown(content, source, "InputScaleY");

            // Ya no usamos zonas / dropdowns en modo Stewart

            // Crear botones en runtime si no están cableados en el prefab
            if (!btnGenerate)
                btnGenerate = CreateRuntimeButton(content, "btnGenerate", "Generar Campo Vectorial", new Color(0.12f, 0.70f, 0.26f, 1f));

            if (!btnDelete)
                btnDelete = CreateRuntimeButton(content, "btnDelete", "Eliminar Campo Vectorial", new Color(0.75f, 0.15f, 0.15f, 1f));

            if (!btnGenerateDucks)
                btnGenerateDucks = CreateRuntimeButton(content, "btnGenerateDucks", "Generar Patos", new Color(0.96f, 0.80f, 0.12f, 1f));

            // Fuerza el refresco de layout para que los nuevos controles sean visibles de inmediato.
            if (content is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        Button CreateRuntimeButton(Transform parent, string objName, string label, Color bgColor)
        {
            // Reusar si ya existe con ese nombre
            var existing = parent.Find(objName);
            if (existing != null)
            {
                var existingBtn = existing.GetComponent<Button>();
                if (existingBtn != null) return existingBtn;
            }

            var go = new GameObject(objName, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 42f;
            le.flexibleWidth   = 1f;

            var img = go.GetComponent<Image>();
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = bgColor;
            colors.highlightedColor = bgColor * 1.15f;
            colors.pressedColor     = bgColor * 0.80f;
            colors.selectedColor    = bgColor * 1.15f;
            colors.colorMultiplier  = 1f;
            btn.colors = colors;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;

            var tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 16f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        TMP_Dropdown CreateRuntimeAxisDropdown(Transform parent, TMP_Dropdown templateSource, string objName)
        {
            // label
            var labelGO = new GameObject(objName + "_Label", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(parent, false);
            var ll = labelGO.GetComponent<LayoutElement>();
            ll.preferredHeight = 24f;
            var labelTextComp = labelGO.GetComponent<TextMeshProUGUI>();
            labelTextComp.text = objName == "InputScaleX" ? "Primera funcion f(x,y):" : "Segunda funcion f(x,y):";
            labelTextComp.fontSize = 18;
            labelTextComp.color = new Color(0.85f, 0.85f, 0.85f);
            labelTextComp.fontStyle = FontStyles.Bold;

            // dropdown
            var dropGO = new GameObject(objName, typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer), typeof(Image), typeof(TMP_Dropdown));
            dropGO.transform.SetParent(parent, false);
            var dle = dropGO.GetComponent<LayoutElement>();
            dle.preferredHeight = 36f;
            dle.flexibleWidth = 1f;

            var bg = dropGO.GetComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var dropdown = dropGO.GetComponent<TMP_Dropdown>();
            string[] firstFuncOpts  = { "X", "-X", "-Y", "Y", "-X-Y", "Y^2" };
            string[] secondFuncOpts = { "Y", "-Y", "X", "-X", "X-Y", "0" };
            string[] opts = objName == "InputScaleX" ? firstFuncOpts : secondFuncOpts;
            foreach (var o in opts) dropdown.options.Add(new TMP_Dropdown.OptionData(o));
            dropdown.SetValueWithoutNotify(0);

            // template required for TMP_Dropdown to open
            if (templateSource != null && templateSource.template != null)
            {
                var tmpl = Instantiate(templateSource.template, dropGO.transform);
                tmpl.name = "Template";
                tmpl.gameObject.SetActive(false);
                dropdown.template = tmpl as RectTransform;
                var cg = tmpl.GetComponent<CanvasGroup>();
                if (cg == null) tmpl.gameObject.AddComponent<CanvasGroup>();
            }

            return dropdown;
        }

        TMP_Dropdown CreateRuntimeLabeledDropdown(Transform parent, TMP_Dropdown source, string objName, string labelText)
        {
            var labelGO = new GameObject(objName + "_Label", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(parent, false);

            var labelLayout = labelGO.GetComponent<LayoutElement>();
            labelLayout.preferredHeight = 24f;

            var labelTextComp = labelGO.GetComponent<TextMeshProUGUI>();
            labelTextComp.text = labelText;
            labelTextComp.fontSize = 18;
            labelTextComp.color = new Color(0.85f, 0.85f, 0.85f);
            labelTextComp.fontStyle = FontStyles.Bold;

            var dropGO = Instantiate(source.gameObject, parent);
            dropGO.name = objName;

            var dropdown = dropGO.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.value = 0;
            FixDropdownTemplate(dropdown);

            return dropdown;
        }

        void FixDropdownTemplate(TMP_Dropdown dropdown)
        {
            if (!dropdown)
                return;

            if (dropdown.template == null)
            {
                var template = dropdown.transform.Find("Template") as RectTransform;
                if (template == null)
                {
                    var all = dropdown.GetComponentsInChildren<RectTransform>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] != null && all[i].name == "Template")
                        {
                            template = all[i];
                            break;
                        }
                    }
                }

                dropdown.template = template;
            }

            if (dropdown.template != null)
            {
                var cg = dropdown.template.GetComponent<CanvasGroup>();
                if (cg == null)
                    dropdown.template.gameObject.AddComponent<CanvasGroup>();

                dropdown.template.gameObject.SetActive(false);
            }
        }

        void RemoveConflictingTrackedRaycasters()
        {
            string[] fullTypeNames =
            {
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit",
                "UnityEngine.InputSystem.UI.TrackedDeviceRaycaster, Unity.InputSystem",
            };

            foreach (var fullTypeName in fullTypeNames)
            {
                var type = System.Type.GetType(fullTypeName);
                if (type == null) continue;

                var components = gameObject.GetComponentsInChildren(type, true);
                foreach (var component in components)
                {
                    if (component is Component c)
                        Destroy(c);
                }
            }
        }

        void OnGenerate()
        {
            if (!fieldManager) 
            { 
                SetStatus("ERROR: VectorFieldManager no encontrado.");
                return;
            }

            try
            {
                string p = GetSelectedExpression(inputScaleX);
                string q = GetSelectedExpression(inputScaleY);

                if (string.IsNullOrWhiteSpace(p) || string.IsNullOrWhiteSpace(q))
                {
                    SetStatus("Completa ambas funciones.");
                    return;
                }

                // Cantidad elegida
                fieldManager.vectorCount = GetSelectedCount();

                // Config del manager para el pedido
                fieldManager.accumulateByZone = false;
                fieldManager.activeZoneKey = string.Empty;
                fieldManager.forceAtLeastNVectors = false;
                fieldManager.forcedMinimumVectorCount = 0;
                fieldManager.onlyBackOfIsland = true;
                fieldManager.sampleAcrossEntireOcean = true;
                fieldManager.autoDetectOceanBounds = true;
                fieldManager.autoDetectIslandFromScene = true;
                fieldManager.useSingleBackRectSameAsIsland = false;
                fieldManager.useBackPackedRectangle = true;
                fieldManager.packedRectHorizontalPadding = 0f;
                fieldManager.packedRectBackPadding = 0f;
                fieldManager.packedRectFrontPadding = 0f;

                // Expandir todo el campo hacia atras hasta el borde de la isla.
                fieldManager.backEndOffset = 0f;

                // Cubrir el rectangulo completo (no centrar denso).
                fieldManager.useDensePacking = false;

                // Apuntar al centro calculado del campo.
                fieldManager.formula = FieldFormula.TargetPoint;

                // En esta escena el agua no siempre tiene collider; el filtro por raycast puede eliminar todos los puntos.
                fieldManager.excludeSolidObjectsOverWater = false;

                if (!fieldManager.SetFunctions(p, q, out string err))
                {
                    SetStatus(err);
                    return;
                }

                _pendingP = p;
                _pendingQ = q;
                RestartCountdown();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al generar campo: {ex.Message}", this);
                SetStatus($"ERROR: {ex.Message}");
            }
        }

        string GetSelectedExpression(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return "x";

            int idx = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            return dropdown.options[idx].text;
        }

        void OnDelete()
        {
            try
            {
                StopCountdown();

                if (inputScaleX != null) inputScaleX.SetValueWithoutNotify(0);
                if (inputScaleY != null) inputScaleY.SetValueWithoutNotify(0);

                if (fieldManager) fieldManager.DeleteField();
                SetDucksButtonEnabled(false);
                SetTimer(COUNTDOWN_SECONDS);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al eliminar campo: {ex.Message}", this);
                SetStatus($"ERROR al eliminar: {ex.Message}");
            }
        }

        void OnGenerateDucks()
        {
            if (duckSpawner == null)
            {
                SetStatus("ERROR: DuckFieldSpawner no encontrado.");
                return;
            }

            duckSpawner.fieldManager = fieldManager;
            duckSpawner.GenerateDucks();
        }

        void SetDucksButtonEnabled(bool enabled)
        {
            if (btnGenerateDucks != null)
                btnGenerateDucks.interactable = enabled;
        }

        void SetStatus(string msg)
        {
            Debug.Log($"[Panel] {msg}");
            if (statusLabel != null)
                statusLabel.text = msg;
        }

        void SetTimer(int seconds)
        {
            if (statusLabel != null)
                statusLabel.text = $"TIEMPO: {Mathf.Max(0, seconds)}";
        }

        void RestartCountdown()
        {
            StopCountdown();
            _countdown = StartCoroutine(CountdownThenGenerate());
        }

        void StopCountdown()
        {
            if (_countdown != null)
            {
                StopCoroutine(_countdown);
                _countdown = null;
            }
        }

        System.Collections.IEnumerator CountdownThenGenerate()
        {
            for (int t = COUNTDOWN_SECONDS; t >= 0; t--)
            {
                SetTimer(t);
                if (t == 0)
                    break;
                yield return new WaitForSeconds(1f);
            }

            // Generar al final usando las funciones capturadas
            if (fieldManager != null)
            {
                // Revalidar por si el usuario modificó mientras corría el timer
                string p = _pendingP;
                string q = _pendingQ;
                if (fieldManager.SetFunctions(p, q, out string err))
                    fieldManager.GenerateField();
                else
                    SetStatus(err);
            }

            if (duckSpawner != null)
                StartCoroutine(EnableDucksAfterDelay());

            _countdown = null;
        }

        System.Collections.IEnumerator EnableDucksAfterDelay()
        {
            SetDucksButtonEnabled(false);
            yield return new WaitForSeconds(Mathf.Max(0f, ducksEnableDelaySeconds));
            SetDucksButtonEnabled(true);
        }
    }
}

