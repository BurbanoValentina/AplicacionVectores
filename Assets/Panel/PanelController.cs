using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VectorField;

namespace VectorFieldUI
{
    /// <summary>
    /// Controla el panel físico (cartel) en el mundo 3D.
    /// Soporta campos de entrada para X e Y. Dropdown de formula auto-genera el campo.
    /// </summary>
    [DisallowMultipleComponent]
    public class PanelController : MonoBehaviour
    {
        [Header("Referencia al manager del campo")]
        public VectorFieldManager fieldManager;

        [Header("Controles principales")]
        public TMP_Dropdown dropCount;
        public TMP_Dropdown dropFormula;

        [Header("Multiplicadores X/Y (inputs de texto)")]
        public TMP_InputField inputScaleX;
        public TMP_InputField inputScaleY;

        [Header("Zona de visualizacion")]
        public TMP_Dropdown dropZone;
        public float        waterRadius = 45f;
        public Vector2      waterCenter = Vector2.zero;

        [Header("Botones")]
        public Button btnGenerate;
        public Button btnReset;
        public Button btnDelete;

        [Header("Etiqueta de estado")]
        public TextMeshProUGUI statusLabel;

        static readonly int[] COUNTS = {
            100, 200, 300, 400, 500, 600, 700, 800, 900, 1000,
            1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000
        };

        const float DEFAULT_SCALE_X = 1f;
        const float DEFAULT_SCALE_Y = 1f;

        // ── Zonas ────────────────────────────────────────────────────────────
        struct ZoneInfo
        {
            public string  label;
            public Vector2 centerFrac; // centro = waterCenter + centerFrac * waterRadius
            public float   radiusFrac; // radio  = waterRadius  * radiusFrac
            public bool    useOceanRect;   // usa bounds del oceano (rectangulo)
            public int     backSectors;    // 1 = sin sectores
            public int     backSectorIndex;
        }

        // Zonas fijas para usar mejor el agua en cualquier cantidad de vectores.
        static readonly ZoneInfo[] ZONES = {
            new ZoneInfo { label="Atras (todo)",            centerFrac=new Vector2(0f, -0.55f), radiusFrac=0.90f, useOceanRect=true, backSectors=1, backSectorIndex=0 },
            new ZoneInfo { label="Atras sector 1/4",        centerFrac=new Vector2(0f, -0.55f), radiusFrac=0.90f, useOceanRect=true, backSectors=4, backSectorIndex=0 },
            new ZoneInfo { label="Atras sector 2/4",        centerFrac=new Vector2(0f, -0.55f), radiusFrac=0.90f, useOceanRect=true, backSectors=4, backSectorIndex=1 },
            new ZoneInfo { label="Atras sector 3/4",        centerFrac=new Vector2(0f, -0.55f), radiusFrac=0.90f, useOceanRect=true, backSectors=4, backSectorIndex=2 },
            new ZoneInfo { label="Atras sector 4/4",        centerFrac=new Vector2(0f, -0.55f), radiusFrac=0.90f, useOceanRect=true, backSectors=4, backSectorIndex=3 },
            new ZoneInfo { label="Todo el oceano (sin isla)",centerFrac=Vector2.zero,            radiusFrac=1.00f, useOceanRect=true, backSectors=1, backSectorIndex=0 },
        };
        // ─────────────────────────────────────────────────────────────────────

        bool _refreshing = false;

        static readonly string[] FORMULA_LABELS = {
            "Radial Out  F=(x,y)",
            "Radial In   F=(-x,-y)",
            "Rotacion XY  F=(-y,x)",
            "Gravitacional  F=-r/|r|^2",
            "Silla  F=(x,-y)",
            "Constante  F=(1,0)",
            "Torbellino  F=(-y,x)/(x^2+y^2)",
            "Espiral  F=(x-y,x+y)"
        };

        void Awake()
        {
            RemoveConflictingTrackedRaycasters();
            EnsureExtraControls();
        }

        void Start()
        {
            ApplyPanelVisualStyle();

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

            // Validar y agregar listeners a botones
            try
            {
                if (btnGenerate != null) btnGenerate.onClick.AddListener(OnGenerate);
                if (btnReset != null)    btnReset.onClick.AddListener(OnReset);
                if (btnDelete != null)   btnDelete.onClick.AddListener(OnDelete);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al asignar listeners de botones: {ex.Message}", this);
            }

            // Validar y agregar listeners a dropdowns
            try
            {
                if (dropFormula != null) dropFormula.onValueChanged.AddListener(OnFormulaChanged);
                if (dropCount != null)   dropCount.onValueChanged.AddListener(OnCountChanged);
                if (dropZone != null)    dropZone.onValueChanged.AddListener(OnZoneChanged);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al asignar listeners de dropdowns: {ex.Message}", this);
            }

            // Validar y agregar listeners a inputs de X/Y
            try
            {
                if (inputScaleX != null) inputScaleX.onEndEdit.AddListener(_ => OnScaleChanged());
                if (inputScaleY != null) inputScaleY.onEndEdit.AddListener(_ => OnScaleChanged());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al asignar listeners de inputs: {ex.Message}", this);
            }

            SetStatus("Listo. Selecciona formula.");
        }

        void ApplyPanelVisualStyle()
        {
            var labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var lbl in labels)
            {
                if (lbl == null) continue;
                if (lbl.name == "PanelDescriptionLabel" || (lbl.name != "PanelTitleLabel" && lbl == statusLabel))
                {
                    lbl.text = string.Empty;
                    lbl.gameObject.SetActive(false);
                }
            }

            var titleLabel = FindTitleLabel();
            if (titleLabel != null)
            {
                if (!titleLabel.gameObject.activeSelf)
                    titleLabel.gameObject.SetActive(true);

                titleLabel.text = "CAMPO VECTORIAL";
                titleLabel.fontSize = 42f;
                titleLabel.fontStyle = FontStyles.Bold;
                titleLabel.color = new Color(0.08f, 0.56f, 1f, 1f);
            }

            if (btnGenerate != null)
            {
                var buttonText = btnGenerate.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "Generar Campo Vectorial";
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

                if (label.name == "PanelDescriptionLabel")
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
                inputScaleX = CreateRuntimeLabeledInputField(content, source, "InputScaleX", "Multiplicador X");

            if (!inputScaleY)
                inputScaleY = CreateRuntimeLabeledInputField(content, source, "InputScaleY", "Multiplicador Y");

            if (!dropZone)
                dropZone = CreateRuntimeLabeledDropdown(content, source, "DropdownZone", "Zona");

            // Crear botones en runtime si no están cableados en el prefab
            if (!btnGenerate)
                btnGenerate = CreateRuntimeButton(content, "btnGenerate", "Generar Campo Vectorial", new Color(0.12f, 0.70f, 0.26f, 1f));

            if (!btnReset)
                btnReset = CreateRuntimeButton(content, "btnReset", "Reiniciar Campo", new Color(0.85f, 0.60f, 0.10f, 1f));

            if (!btnDelete)
                btnDelete = CreateRuntimeButton(content, "btnDelete", "Eliminar Campo Vectorial", new Color(0.75f, 0.15f, 0.15f, 1f));

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

        TMP_InputField CreateRuntimeLabeledInputField(Transform parent, TMP_Dropdown source, string objName, string labelText)
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

            // Crear InputField basado en el Dropdown como referencia
            var inputGO = new GameObject(objName, typeof(RectTransform), typeof(LayoutElement), typeof(TMP_InputField));
            inputGO.transform.SetParent(parent, false);

            var inputLayout = inputGO.GetComponent<LayoutElement>();
            inputLayout.preferredHeight = 36f;

            var inputField = inputGO.GetComponent<TMP_InputField>();
            inputField.text = "1";
            inputField.characterLimit = 10;
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;

            // Crear visual del input field
            var image = inputGO.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(inputGO.transform, false);
            var textComp = textGO.GetComponent<TextMeshProUGUI>();
            textComp.text = "1";
            textComp.fontSize = 20;
            textComp.color = Color.white;

            inputField.textComponent = textComp;
            inputField.targetGraphic = image;

            return inputField;
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

            return dropdown;
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

        void InitControls()
        {
            if (dropCount != null)
            {
                try {
                    dropCount.ClearOptions();
                    var opts = new List<string>();
                    foreach (int v in COUNTS) opts.Add(v.ToString());
                    dropCount.AddOptions(opts);
                    dropCount.value = 0; // 100 vectores por defecto
                } catch (System.Exception ex) { Debug.LogError($"Error init dropCount: {ex.Message}", this); }
            }

            if (dropFormula != null)
            {
                try {
                    dropFormula.ClearOptions();
                    dropFormula.AddOptions(new List<string>(FORMULA_LABELS));
                    dropFormula.value = 0;
                } catch (System.Exception ex) { Debug.LogError($"Error init dropFormula: {ex.Message}", this); }
            }

            // Inicializar inputs de X/Y con valores por defecto
            if (inputScaleX != null)
            {
                try {
                    inputScaleX.text = DEFAULT_SCALE_X.ToString();
                } catch (System.Exception ex) { Debug.LogError($"Error init inputScaleX: {ex.Message}", this); }
            }

            if (inputScaleY != null)
            {
                try {
                    inputScaleY.text = DEFAULT_SCALE_Y.ToString();
                } catch (System.Exception ex) { Debug.LogError($"Error init inputScaleY: {ex.Message}", this); }
            }

            if (dropZone != null)
            {
                try {
                    dropZone.ClearOptions();
                    var opts = new List<string>();
                    foreach (var z in ZONES) opts.Add(z.label);
                    dropZone.AddOptions(opts);
                    dropZone.value = 0;
                } catch (System.Exception ex) { Debug.LogError($"Error init dropZone: {ex.Message}", this); }
            }
        }

        void OnFormulaChanged(int idx)
        {
            if (statusLabel != null && idx >= 0 && idx < FORMULA_LABELS.Length)
                statusLabel.text = "Formula: " + FORMULA_LABELS[idx];
            OnGenerate();
        }

        void OnCountChanged(int idx)
        {
            if (_refreshing) return;
            RefreshZoneDropdown();
            OnGenerate();
        }

        void OnScaleChanged()
        {
            OnGenerate();
        }

        void OnZoneChanged(int idx)
        {
            if (_refreshing) return;
            OnGenerate();
        }

        ZoneInfo[] GetZonesForCount(int count)
        {
            return ZONES;
        }

        void RefreshZoneDropdown()
        {
            if (!dropZone) return;
            if (!dropCount) return;
            
            try
            {
                int count = COUNTS[Mathf.Clamp(dropCount.value, 0, COUNTS.Length - 1)];
                ZoneInfo[] zones = GetZonesForCount(count);

                int prevIdx = Mathf.Min(dropZone.value, zones.Length - 1);
                bool prev = _refreshing;
                _refreshing = true;
                
                dropZone.ClearOptions();
                var opts = new List<string>();
                foreach (var z in zones) opts.Add(z.label);
                dropZone.AddOptions(opts);
                dropZone.value = prevIdx;
                
                _refreshing = prev;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al actualizar zona dropdown: {ex.Message}", this);
            }
        }

        ZoneInfo GetCurrentZone()
        {
            int count = dropCount ? COUNTS[Mathf.Clamp(dropCount.value, 0, COUNTS.Length - 1)] : 100;
            ZoneInfo[] zones = GetZonesForCount(count);
            int idx = dropZone ? Mathf.Clamp(dropZone.value, 0, zones.Length - 1) : 0;
            return zones[idx];
        }

        void OnGenerate()
        {
            if (!fieldManager) 
            { 
                SetStatus("ERROR: VectorFieldManager no encontrado.");
                return;
            }

            if (!dropCount || !dropFormula)
            {
                SetStatus("ERROR: Dropdowns no configurados.");
                return;
            }

            try
            {
                ZoneInfo zone = GetCurrentZone();

                int countIndex = Mathf.Clamp(dropCount.value, 0, COUNTS.Length - 1);
                int formulaIndex = Mathf.Clamp(dropFormula.value, 0, FORMULA_LABELS.Length - 1);

                // Parsear valores de X/Y desde inputs
                float scaleXValue = DEFAULT_SCALE_X;
                float scaleYValue = DEFAULT_SCALE_Y;

                if (inputScaleX != null && !string.IsNullOrEmpty(inputScaleX.text))
                    scaleXValue = TryParseFloatLoose(inputScaleX.text, DEFAULT_SCALE_X);

                if (inputScaleY != null && !string.IsNullOrEmpty(inputScaleY.text))
                    scaleYValue = TryParseFloatLoose(inputScaleY.text, DEFAULT_SCALE_Y);

                fieldManager.vectorCount = COUNTS[countIndex];
                fieldManager.formula     = (FieldFormula)formulaIndex;
                fieldManager.useTarget   = false;
                fieldManager.target      = Vector3.zero;
                fieldManager.scaleX      = scaleXValue;
                fieldManager.scaleY      = scaleYValue;
                fieldManager.zoneCenter  = waterCenter + zone.centerFrac * waterRadius;
                fieldManager.fieldRadius = waterRadius * zone.radiusFrac;
                // Respetar el conteo seleccionado (sin forzar minimo 2000)
                fieldManager.forceAtLeastNVectors = false;
                fieldManager.forcedMinimumVectorCount = 0;

                // Mantener el campo en el agua de atras del mapa
                fieldManager.onlyBackOfIsland = true;

                // Zonas: modo rectangulo por bounds del oceano vs radio
                fieldManager.sampleAcrossEntireOcean = zone.useOceanRect;
                fieldManager.useBackPackedRectangle = zone.useOceanRect;
                fieldManager.useBackSectors = zone.backSectors > 1;
                fieldManager.backSectors = Mathf.Max(1, zone.backSectors);
                fieldManager.backSectorIndex = Mathf.Clamp(zone.backSectorIndex, 0, fieldManager.backSectors - 1);
                fieldManager.GenerateField();

                SetStatus("OK " + fieldManager.vectorCount + " vec -> " + FORMULA_LABELS[formulaIndex]);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al generar campo: {ex.Message}", this);
                SetStatus($"ERROR: {ex.Message}");
            }
        }

        static float TryParseFloatLoose(string text, float fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            string s = text.Trim();

            // Soportar coma o punto como separador decimal.
            // 1) Intentar cultura actual
            if (float.TryParse(s, out float v))
                return v;

            // 2) Intentar normalizando separador
            string sDot = s.Replace(',', '.');
            if (float.TryParse(sDot, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;

            string sComma = s.Replace('.', ',');
            if (float.TryParse(sComma, out v))
                return v;

            return fallback;
        }

        void OnReset()
        {
            try
            {
                _refreshing = true;
                if (dropCount)   dropCount.value   = 0; // 100
                if (dropFormula) dropFormula.value  = 0;
                if (inputScaleX) inputScaleX.text = DEFAULT_SCALE_X.ToString();
                if (inputScaleY) inputScaleY.text = DEFAULT_SCALE_Y.ToString();
                _refreshing = false;

                RefreshZoneDropdown();
                if (dropZone) dropZone.value = 0;

                if (fieldManager)
                {
                    ZoneInfo zone = GetCurrentZone();
                    fieldManager.formula     = FieldFormula.RadialOutward;
                    fieldManager.useTarget   = false;
                    fieldManager.target      = Vector3.zero;
                    fieldManager.vectorCount = 100;
                    fieldManager.scaleX      = DEFAULT_SCALE_X;
                    fieldManager.scaleY      = DEFAULT_SCALE_Y;
                    fieldManager.zoneCenter  = waterCenter + zone.centerFrac * waterRadius;
                    fieldManager.fieldRadius = waterRadius * zone.radiusFrac;

                    // Consistente con OnGenerate
                    fieldManager.forceAtLeastNVectors = false;
                    fieldManager.forcedMinimumVectorCount = 0;
                    fieldManager.onlyBackOfIsland = true;

                    fieldManager.sampleAcrossEntireOcean = zone.useOceanRect;
                    fieldManager.useBackPackedRectangle = zone.useOceanRect;
                    fieldManager.useBackSectors = zone.backSectors > 1;
                    fieldManager.backSectors = Mathf.Max(1, zone.backSectors);
                    fieldManager.backSectorIndex = Mathf.Clamp(zone.backSectorIndex, 0, fieldManager.backSectors - 1);

                    fieldManager.GenerateField();
                }
                SetStatus("Campo reiniciado.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al reiniciar campo: {ex.Message}", this);
                SetStatus($"ERROR al reiniciar: {ex.Message}");
            }
        }

        void OnDelete()
        {
            try
            {
                if (fieldManager) fieldManager.DeleteField();
                SetStatus("Campo eliminado.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al eliminar campo: {ex.Message}", this);
                SetStatus($"ERROR al eliminar: {ex.Message}");
            }
        }

        void SetStatus(string msg)
        {
            Debug.Log($"[Panel] {msg}");
        }
    }
}
