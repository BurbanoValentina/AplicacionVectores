using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [Header("Funciones f(x,y) (reusamos estos dropdowns)")]
        [Tooltip("Primera función P(x,y)")]
        public TMP_Dropdown dropScaleX;
        [Tooltip("Segunda función Q(x,y)")]
        public TMP_Dropdown dropScaleY;

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
        public Vector2 panelSize = new Vector2(480f, 580f);
        [Tooltip("Fuerza que el ScrollRect arranque arriba (para que se vea el título).")]
        public bool forceScrollTop = true;

        const int COUNTDOWN_SECONDS = 10;
        const string PanelDescriptionText =
            "Selecciona la primera y la segunda función. Luego pulsa Generar Campo.";

        static readonly string[] FunctionPOptions = { "X", "-X", "-Y", "Y", "-X-Y" };
        static readonly string[] FunctionQOptions = { "Y", "-Y", "X", "-X", "X-Y" };

        static readonly HashSet<string> HiddenScrollContentNames = new HashSet<string>
        {
            "FunctionsSectionLabel", "CountDesc", "CountFunctionLabel",
            "FormulaFunctionLabel", "FormulaDesc", "ZoneDesc", "DropdownZone_Label",
            "ScaleXDesc", "ScaleYDesc", "InputScaleX", "InputScaleY",
            "InputScaleX_Label", "InputScaleY_Label", "Dropdown", "DropdownZone",
            "dropScaleX_Label", "dropScaleY_Label", "ButtonsSpacer", "Sep",
        };

        Coroutine _countdown;
        string _pendingP;
        string _pendingQ;
        bool _uiInitialized;
        GraphicRaycaster _panelRaycaster;
        readonly List<RaycastResult> _raycastHits = new List<RaycastResult>();

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
            if (!Application.isPlaying && !applyStyleInEditMode)
                return;

            if (!Application.isPlaying)
                ApplyPanelVisualStyle();
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
            PurgeDuplicateButtons();
            ResolveButtonReferences();
            WireButtonListeners();
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

            if (duckSpawner == null)
                duckSpawner = FindFirstObjectByType<DuckFieldSpawner>();
            if (duckSpawner == null)
                duckSpawner = new GameObject("DuckFieldSpawner").AddComponent<DuckFieldSpawner>();

            SetDucksButtonEnabled(false);

            SetTimer(COUNTDOWN_SECONDS);
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            HandleMouseClickFallback();
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
            if (_uiInitialized && Application.isPlaying)
                return;

            if (dropScaleX != null) dropScaleX.gameObject.SetActive(true);
            if (dropScaleY != null) dropScaleY.gameObject.SetActive(true);

            HideLegacyPanelControls();
            EnforceScrollContentWhitelist();
            ImproveCanvasReadability();
            ResolveButtonReferences();
            PurgeDuplicateButtons();
            WireDropdownsFromPrefab();

            var titleLabel = FindTitleLabel();
            if (titleLabel != null)
            {
                titleLabel.gameObject.SetActive(true);
                titleLabel.text = "Campo Vectorial";
                titleLabel.fontSize = 42f;
                titleLabel.fontStyle = FontStyles.Bold;
                titleLabel.color = new Color(0.08f, 0.56f, 1f, 1f);
                titleLabel.alignment = TextAlignmentOptions.Center;
            }

            var desc = FindDescriptionLabel();
            if (desc != null)
            {
                desc.gameObject.SetActive(true);
                desc.text = PanelDescriptionText;
                desc.fontSize = 16f;
                desc.fontStyle = FontStyles.Normal;
                desc.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                desc.alignment = TextAlignmentOptions.Center;
            }

            DisableIfExists("dropScaleX_Label");
            DisableIfExists("dropScaleY_Label");

            SetLabelTextIfExists("DropdownScaleX_Label", "Primera función:");
            SetLabelTextIfExists("DropdownScaleY_Label", "Segunda función:");

            if (dropScaleX != null)
            {
                dropScaleX.gameObject.SetActive(true);
                ConfigureFunctionDropdown(dropScaleX, FunctionPOptions, "X");
            }

            if (dropScaleY != null)
            {
                dropScaleY.gameObject.SetActive(true);
                ConfigureFunctionDropdown(dropScaleY, FunctionQOptions, "Y");
            }

            if (statusLabel != null && statusLabel.name == "PanelTitleLabel")
                statusLabel = null;

            if (statusLabel == null)
                statusLabel = FindStatusLabelFallback();
            if (statusLabel != null && statusLabel.name != "PanelTitleLabel")
                statusLabel.gameObject.SetActive(false);

            DisableDecorativeRaycasts(titleLabel, desc);

            if (btnGenerate != null)
                btnGenerate.gameObject.SetActive(true);
            if (btnDelete != null)
                btnDelete.gameObject.SetActive(true);
            if (btnGenerateDucks != null)
                btnGenerateDucks.gameObject.SetActive(true);

            ConfigureButtonsLayout();
            ApplySimplifiedControlOrder();

            if (btnGenerate != null)
            {
                var buttonText = btnGenerate.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "GENERAR CAMPO";
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
                    buttonText.text = "ELIMINAR CAMPO";
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

            DisablePanelBackgroundRaycast();
            DisableScrollBlockingRaycasts();
            HideButtonsOutsideButtonsRow();

            if (Application.isPlaying)
            {
                EnsureUIInputWorks();
                WireButtonListeners();
                _uiInitialized = true;
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

        string GetDropdownValue(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
                return string.Empty;
            int idx = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            return NormalizeFunctionExpression(dropdown.options[idx].text);
        }

        static string NormalizeFunctionExpression(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;
            return raw.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        }

        void HideLegacyPanelControls()
        {
            if (dropCount) dropCount.gameObject.SetActive(false);
            if (dropFormula) dropFormula.gameObject.SetActive(false);
            if (dropZone) dropZone.gameObject.SetActive(false);
            if (btnReset) btnReset.gameObject.SetActive(false);

            DisableAllNamed(HiddenScrollContentNames);
        }

        Transform GetScrollContent()
        {
            if (dropScaleX != null && dropScaleX.transform.parent != null)
                return dropScaleX.transform.parent;
            if (dropCount != null && dropCount.transform.parent != null)
                return dropCount.transform.parent;

            var sr = GetComponentInChildren<ScrollRect>(true);
            return sr != null ? sr.content : null;
        }

        void EnforceScrollContentWhitelist()
        {
            var content = GetScrollContent();
            if (content == null)
                return;

            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child != null && HiddenScrollContentNames.Contains(child.name))
                    child.gameObject.SetActive(false);
            }
        }

        void EnsureUIInputWorks()
        {
            if (!Application.isPlaying)
                return;

            EnsureEventSystemExists();

            if (TryGetComponent<Canvas>(out var canvas) && canvas.renderMode == RenderMode.WorldSpace)
            {
                var cam = Camera.main;
                if (cam == null)
                    cam = FindFirstObjectByType<Camera>();

                if (cam != null)
                    canvas.worldCamera = cam;
            }

            _panelRaycaster = GetComponent<GraphicRaycaster>();
            if (_panelRaycaster == null)
                _panelRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            var scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
            {
                scroll.enabled = false;
                scroll.scrollSensitivity = 0f;
            }

            DisableScrollBlockingRaycasts();
        }

        void EnsureEventSystemExists()
        {
            if (EventSystem.current != null)
                return;

            var existing = FindFirstObjectByType<EventSystem>();
            if (existing != null)
                return;

            var go = new GameObject("PanelFallbackEventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        void DisableScrollBlockingRaycasts()
        {
            var scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll == null)
                return;

            void DisableImageRaycast(Transform tr)
            {
                if (tr == null)
                    return;

                var image = tr.GetComponent<Image>();
                if (image != null)
                    image.raycastTarget = false;
            }

            DisableImageRaycast(scroll.transform);
            DisableImageRaycast(scroll.viewport);

            var masks = scroll.GetComponentsInChildren<Mask>(true);
            for (int i = 0; i < masks.Length; i++)
            {
                if (masks[i] == null)
                    continue;

                var maskImage = masks[i].GetComponent<Image>();
                if (maskImage != null)
                    maskImage.raycastTarget = false;
            }
        }

        void HideButtonsOutsideButtonsRow()
        {
            var content = GetScrollContent();
            if (content == null)
                return;

            var buttonsRow = content.Find("ButtonsRow");
            var allButtons = content.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < allButtons.Length; i++)
            {
                var btn = allButtons[i];
                if (btn == null)
                    continue;

                if (buttonsRow != null && btn.transform.IsChildOf(buttonsRow))
                    continue;

                if (Application.isPlaying)
                    Destroy(btn.gameObject);
                else
                    btn.gameObject.SetActive(false);
            }
        }

        void DisablePanelBackgroundRaycast()
        {
            var bg = GetComponent<Image>();
            if (bg != null)
                bg.raycastTarget = false;
        }

        void HandleMouseClickFallback()
        {
            if (!Input.GetMouseButtonDown(0))
                return;

            if (_panelRaycaster == null)
                return;

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                return;

            if (canvas.worldCamera == null)
            {
                var cam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (cam != null)
                    canvas.worldCamera = cam;
            }

            if (canvas.worldCamera == null)
                return;

            EnsureEventSystemExists();
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            var pointer = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition,
            };

            _raycastHits.Clear();
            _panelRaycaster.Raycast(pointer, _raycastHits);
            if (_raycastHits.Count == 0)
                return;

            for (int i = 0; i < _raycastHits.Count; i++)
            {
                var go = _raycastHits[i].gameObject;
                if (go == null)
                    continue;

                if (TryHandleButtonClick(go))
                    return;

                if (TryHandleDropdownClick(go))
                    return;
            }
        }

        bool TryHandleButtonClick(GameObject go)
        {
            var btn = go.GetComponentInParent<Button>();
            if (btn == null || !btn.interactable || !btn.gameObject.activeInHierarchy)
                return false;

            if (btn == btnGenerate)
            {
                OnGenerate();
                return true;
            }
            if (btn == btnDelete)
            {
                OnDelete();
                return true;
            }
            if (btn == btnGenerateDucks)
            {
                OnGenerateDucks();
                return true;
            }

            btn.onClick.Invoke();
            return true;
        }

        bool TryHandleDropdownClick(GameObject go)
        {
            var toggle = go.GetComponentInParent<Toggle>();
            if (toggle != null && toggle.group != null)
            {
                toggle.isOn = true;
                return true;
            }

            var dropdown = go.GetComponentInParent<TMP_Dropdown>();
            if (dropdown == null || !dropdown.interactable || !dropdown.gameObject.activeInHierarchy)
                return false;

            // Cerrar el otro dropdown antes de abrir el seleccionado
            if (dropdown == dropScaleX && dropScaleY != null && dropScaleY.IsExpanded)
                dropScaleY.Hide();
            else if (dropdown == dropScaleY && dropScaleX != null && dropScaleX.IsExpanded)
                dropScaleX.Hide();

            dropdown.Show();
            return true;
        }

        void ImproveCanvasReadability()
        {
            var canvasRt = transform as RectTransform;
            if (canvasRt != null && canvasRt.localScale.x < 0.018f)
                canvasRt.localScale = Vector3.one * 0.018f;

            if (TryGetComponent<Canvas>(out var canvas))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = false;
            }

            if (TryGetComponent<CanvasScaler>(out var scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                if (scaler.dynamicPixelsPerUnit < 50f)
                    scaler.dynamicPixelsPerUnit = 80f;
            }
        }

        void ConfigureButtonsLayout()
        {
            var content = GetScrollContent();
            if (content == null)
                return;

            var buttonsRow = content.Find("ButtonsRow");
            if (buttonsRow != null)
                buttonsRow.gameObject.SetActive(true);

            void SetupButton(Button btn, float height)
            {
                if (btn == null)
                    return;

                btn.gameObject.SetActive(true);

                var le = btn.GetComponent<LayoutElement>();
                if (le == null)
                    le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = height;
                le.flexibleWidth = 1f;
                le.minHeight = height;
            }

            SetupButton(btnGenerate, 44f);
            SetupButton(btnDelete, 44f);
            SetupButton(btnGenerateDucks, 44f);

            if (buttonsRow != null)
            {
                var rowLe = buttonsRow.GetComponent<LayoutElement>();
                if (rowLe != null)
                    rowLe.preferredHeight = 140f;

                var vlg = buttonsRow.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = 6f;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                }
            }

            if (btnGenerate != null)
                btnGenerate.transform.SetSiblingIndex(0);
            if (btnDelete != null)
                btnDelete.transform.SetSiblingIndex(1);
            if (btnGenerateDucks != null)
                btnGenerateDucks.transform.SetSiblingIndex(2);
        }

        void ApplySimplifiedControlOrder()
        {
            var content = GetScrollContent();
            if (content == null)
                return;

            int idx = 0;
            void Place(Transform tr)
            {
                if (tr == null || !tr.gameObject.activeInHierarchy)
                    return;
                tr.SetSiblingIndex(idx++);
            }

            Place(FindTitleLabel()?.transform);
            Place(FindDescriptionLabel()?.transform);
            Place(content.Find("DropdownScaleX_Label"));
            Place(dropScaleX != null ? dropScaleX.transform : null);
            Place(content.Find("DropdownScaleY_Label"));
            Place(dropScaleY != null ? dropScaleY.transform : null);
            Place(content.Find("ButtonsRow"));

            if (content is RectTransform contentRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }

        void DisableIfExists(string name) => DisableAllNamed(name);

        void DisableAllNamed(params string[] names)
        {
            if (names == null || names.Length == 0)
                return;
            DisableAllNamed(new HashSet<string>(names));
        }

        void DisableAllNamed(HashSet<string> nameSet)
        {
            if (nameSet == null || nameSet.Count == 0)
                return;

            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var tr = all[i];
                if (tr != null && nameSet.Contains(tr.name))
                    tr.gameObject.SetActive(false);
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

        void ConfigureFunctionDropdown(TMP_Dropdown dropdown, string[] options, string fallbackSelected)
        {
            if (dropdown == null)
                return;

            dropdown.ClearOptions();
            var optList = new List<TMP_Dropdown.OptionData>();
            foreach (var o in options)
                optList.Add(new TMP_Dropdown.OptionData(o));
            dropdown.AddOptions(optList);

            int fallbackIdx = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], fallbackSelected, System.StringComparison.OrdinalIgnoreCase))
                {
                    fallbackIdx = i;
                    break;
                }
            }
            dropdown.SetValueWithoutNotify(fallbackIdx);

            FixDropdownTemplate(dropdown);
            dropdown.RefreshShownValue();
            dropdown.interactable = true;

            var dropdownImage = dropdown.GetComponent<Image>();
            if (dropdownImage != null)
                dropdownImage.raycastTarget = true;

            dropdown.onValueChanged.RemoveListener(OnFunctionDropdownChanged);
            dropdown.onValueChanged.AddListener(OnFunctionDropdownChanged);
        }

        void OnFunctionDropdownChanged(int _) { /* valores ya están en el dropdown, se leen en OnGenerate */ }

        void ConfigureFunctionInput(TMP_InputField input, string fallback)
        {
            if (input == null)
                return;

            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = 64;

            if (string.IsNullOrWhiteSpace(input.text))
                input.text = fallback;
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

        void ResolveButtonReferences()
        {
            if (btnGenerate == null) btnGenerate = FindButtonInPanel("btnGenerate");
            if (btnDelete == null) btnDelete = FindButtonInPanel("btnDelete");
            if (btnGenerateDucks == null) btnGenerateDucks = FindButtonInPanel("btnGenerateDucks");
        }

        void PurgeDuplicateButtons()
        {
            var groups = new Dictionary<string, List<Button>>();
            var allButtons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < allButtons.Length; i++)
            {
                var btn = allButtons[i];
                if (btn == null)
                    continue;

                if (!groups.TryGetValue(btn.name, out var list))
                {
                    list = new List<Button>();
                    groups[btn.name] = list;
                }
                list.Add(btn);
            }

            foreach (var pair in groups)
            {
                if (pair.Value.Count <= 1)
                    continue;

                Button keep = null;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var candidate = pair.Value[i];
                    if (candidate.transform.parent != null && candidate.transform.parent.name == "ButtonsRow")
                    {
                        keep = candidate;
                        break;
                    }
                }
                if (keep == null)
                    keep = pair.Value[0];

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var duplicate = pair.Value[i];
                    if (duplicate == keep)
                        continue;

                    if (Application.isPlaying)
                        Destroy(duplicate.gameObject);
                    else
                        duplicate.gameObject.SetActive(false);
                }

                if (pair.Key == "btnGenerate") btnGenerate = keep;
                if (pair.Key == "btnDelete") btnDelete = keep;
                if (pair.Key == "btnGenerateDucks") btnGenerateDucks = keep;
            }

            PurgeDuplicatePatosButtonsByLabel();
        }

        void PurgeDuplicatePatosButtonsByLabel()
        {
            var content = GetScrollContent();
            if (content == null)
                return;

            var buttonsRow = content.Find("ButtonsRow");
            var patosButtons = new List<Button>();
            var allButtons = content.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < allButtons.Length; i++)
            {
                var btn = allButtons[i];
                if (btn == null)
                    continue;

                var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null || label.text == null)
                    continue;

                if (label.text.IndexOf("GENERAR PATOS", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    patosButtons.Add(btn);
            }

            if (patosButtons.Count <= 1)
                return;

            Button keep = null;
            for (int i = 0; i < patosButtons.Count; i++)
            {
                if (buttonsRow != null && patosButtons[i].transform.IsChildOf(buttonsRow))
                {
                    keep = patosButtons[i];
                    break;
                }
            }
            if (keep == null)
                keep = patosButtons[0];

            for (int i = 0; i < patosButtons.Count; i++)
            {
                var duplicate = patosButtons[i];
                if (duplicate == keep)
                    continue;

                if (Application.isPlaying)
                    Destroy(duplicate.gameObject);
                else
                    duplicate.gameObject.SetActive(false);
            }

            btnGenerateDucks = keep;
        }

        Button FindButtonInPanel(string buttonName)
        {
            var content = GetScrollContent();
            if (content == null)
                return null;

            var buttonsRow = content.Find("ButtonsRow");
            if (buttonsRow != null)
            {
                var inRow = buttonsRow.Find(buttonName);
                if (inRow != null && inRow.TryGetComponent<Button>(out var rowBtn))
                    return rowBtn;
            }

            var onContent = content.Find(buttonName);
            if (onContent != null && onContent.TryGetComponent<Button>(out var contentBtn))
                return contentBtn;

            return null;
        }

        void DisableDecorativeRaycasts(params TextMeshProUGUI[] extras)
        {
            var labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var lbl = labels[i];
                if (lbl == null)
                    continue;

                if (lbl.name.Contains("Label") ||
                    lbl.name == "PanelTitleLabel" ||
                    lbl.name == "PanelDescriptionLabel")
                {
                    lbl.raycastTarget = false;
                }
            }

            if (extras == null)
                return;

            for (int i = 0; i < extras.Length; i++)
            {
                if (extras[i] != null)
                    extras[i].raycastTarget = false;
            }
        }

        void WireDropdownsFromPrefab()
        {
            if (dropScaleX == null)
                dropScaleX = FindDropdownInPanel("dropScaleX");
            if (dropScaleY == null)
                dropScaleY = FindDropdownInPanel("dropScaleY");
        }

        TMP_Dropdown FindDropdownInPanel(string dropdownName)
        {
            var all = GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == dropdownName)
                    return all[i];
            }
            return null;
        }

        void WireButtonListeners()
        {
            try
            {
                if (btnGenerate != null)
                {
                    btnGenerate.onClick.RemoveAllListeners();
                    btnGenerate.onClick.AddListener(OnGenerate);
                }
                if (btnDelete != null)
                {
                    btnDelete.onClick.RemoveAllListeners();
                    btnDelete.onClick.AddListener(OnDelete);
                }
                if (btnGenerateDucks != null)
                {
                    btnGenerateDucks.onClick.RemoveAllListeners();
                    btnGenerateDucks.onClick.AddListener(OnGenerateDucks);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al asignar listeners de botones: {ex.Message}", this);
            }
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

                var templateCanvas = dropdown.template.GetComponent<Canvas>();
                if (templateCanvas == null)
                    templateCanvas = dropdown.template.gameObject.AddComponent<Canvas>();
                templateCanvas.overrideSorting = true;
                templateCanvas.sortingOrder = 400;
                if (TryGetComponent<Canvas>(out var rootCanvas) && rootCanvas.worldCamera != null)
                    templateCanvas.worldCamera = rootCanvas.worldCamera;

                var templateRaycaster = dropdown.template.GetComponent<GraphicRaycaster>();
                if (templateRaycaster == null)
                    dropdown.template.gameObject.AddComponent<GraphicRaycaster>();

                dropdown.template.gameObject.SetActive(false);
            }

            var caption = dropdown.captionText;
            if (caption != null)
            {
                caption.fontSize = Mathf.Max(caption.fontSize, 20f);
                caption.color = Color.white;
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
                string p = dropScaleX != null ? GetDropdownValue(dropScaleX) : "x";
                string q = dropScaleY != null ? GetDropdownValue(dropScaleY) : "y";
                if (string.IsNullOrEmpty(p)) p = "x";
                if (string.IsNullOrEmpty(q)) q = "y";

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

        void OnDelete()
        {
            try
            {
                StopCountdown();

                if (dropScaleX) dropScaleX.SetValueWithoutNotify(0);
                if (dropScaleY) dropScaleY.SetValueWithoutNotify(0);

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
