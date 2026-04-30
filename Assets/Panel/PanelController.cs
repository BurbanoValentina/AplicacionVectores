using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VectorField;

namespace VectorFieldUI
{
    /// <summary>
    /// Controla el panel fisico (cartel) en el mundo 3D.
    /// Sin campos X e Y. Dropdown de formula auto-genera el campo.
    /// </summary>
    [DisallowMultipleComponent]
    public class PanelController : MonoBehaviour
    {
        [Header("Referencia al manager del campo")]
        public VectorFieldManager fieldManager;

        [Header("Controles")]
        public TMP_Dropdown dropCount;
        public TMP_Dropdown dropFormula;

        [Header("Multiplicadores X/Y")]
        public TMP_Dropdown dropScaleX;
        public TMP_Dropdown dropScaleY;

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

        static readonly float[]  SCALE_VALUES = { -2f, -1f, 1f, 2f };
        static readonly string[] SCALE_LABELS = { "-2", "-1", "1", "2" };
        const int DEFAULT_SCALE_IDX = 2; // índice de "1"

        // ── Zonas ────────────────────────────────────────────────────────────
        struct ZoneInfo
        {
            public string  label;
            public Vector2 centerFrac; // centro = waterCenter + centerFrac * waterRadius
            public float   radiusFrac; // radio  = waterRadius  * radiusFrac
        }

        // Zonas fijas para usar mejor el agua en cualquier cantidad de vectores.
        static readonly ZoneInfo[] ZONES = {
            new ZoneInfo { label="Todo el oceano (sin isla)", centerFrac=Vector2.zero,       radiusFrac=1.00f },
            new ZoneInfo { label="Agua Norte",          centerFrac=new Vector2(0f, +0.35f), radiusFrac=0.68f },
            new ZoneInfo { label="Agua Sur",            centerFrac=new Vector2(0f, -0.35f), radiusFrac=0.68f },
            new ZoneInfo { label="Agua Este",           centerFrac=new Vector2(+0.35f, 0f), radiusFrac=0.68f },
            new ZoneInfo { label="Agua Oeste",          centerFrac=new Vector2(-0.35f, 0f), radiusFrac=0.68f },
            new ZoneInfo { label="Cuadrante NE",        centerFrac=new Vector2(+0.45f,+0.45f), radiusFrac=0.46f },
            new ZoneInfo { label="Cuadrante NO",        centerFrac=new Vector2(-0.45f,+0.45f), radiusFrac=0.46f },
            new ZoneInfo { label="Cuadrante SE",        centerFrac=new Vector2(+0.45f,-0.45f), radiusFrac=0.46f },
            new ZoneInfo { label="Cuadrante SO",        centerFrac=new Vector2(-0.45f,-0.45f), radiusFrac=0.46f },
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
            EnsureExtraDropdowns();
        }

        void Start()
        {
            if (fieldManager == null) 
            {
                SetStatus("ERROR: VectorFieldManager no encontrado");
                return;
            }

            Invoke(nameof(InitDropdowns), 0.05f);

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
                if (dropScaleX != null)  dropScaleX.onValueChanged.AddListener(_ => OnGenerate());
                if (dropScaleY != null)  dropScaleY.onValueChanged.AddListener(_ => OnGenerate());
                if (dropZone != null)    dropZone.onValueChanged.AddListener(OnZoneChanged);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al asignar listeners de dropdowns: {ex.Message}", this);
            }

            SetStatus("Listo. Selecciona formula.");
        }

        void EnsureExtraDropdowns()
        {
            TMP_Dropdown source = dropFormula ? dropFormula : dropCount;
            if (!source) return;

            Transform content = source.transform.parent;
            if (!content) return;

            if (!dropScaleX)
                dropScaleX = CreateRuntimeLabeledDropdown(content, source, "DropdownScaleX", "Multiplicador X");

            if (!dropScaleY)
                dropScaleY = CreateRuntimeLabeledDropdown(content, source, "DropdownScaleY", "Multiplicador Y");

            if (!dropZone)
                dropZone = CreateRuntimeLabeledDropdown(content, source, "DropdownZone", "Zona");

            // Fuerza el refresco de layout para que los nuevos controles sean visibles de inmediato.
            if (content is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
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

        void InitDropdowns()
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

            if (dropScaleX != null)
            {
                try {
                    dropScaleX.ClearOptions();
                    dropScaleX.AddOptions(new List<string>(SCALE_LABELS));
                    dropScaleX.value = DEFAULT_SCALE_IDX;
                } catch (System.Exception ex) { Debug.LogError($"Error init dropScaleX: {ex.Message}", this); }
            }

            if (dropScaleY != null)
            {
                try {
                    dropScaleY.ClearOptions();
                    dropScaleY.AddOptions(new List<string>(SCALE_LABELS));
                    dropScaleY.value = DEFAULT_SCALE_IDX;
                } catch (System.Exception ex) { Debug.LogError($"Error init dropScaleY: {ex.Message}", this); }
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

                fieldManager.vectorCount = COUNTS[countIndex];
                fieldManager.formula     = (FieldFormula)formulaIndex;
                fieldManager.useTarget   = false;
                fieldManager.target      = Vector3.zero;
                fieldManager.scaleX      = dropScaleX ? SCALE_VALUES[dropScaleX.value] : 1f;
                fieldManager.scaleY      = dropScaleY ? SCALE_VALUES[dropScaleY.value] : 1f;
                fieldManager.zoneCenter  = waterCenter + zone.centerFrac * waterRadius;
                fieldManager.fieldRadius = waterRadius * zone.radiusFrac;
                fieldManager.sampleAcrossEntireOcean = zone.label.StartsWith("Todo el oceano", System.StringComparison.OrdinalIgnoreCase);
                fieldManager.GenerateField();

                SetStatus("OK " + fieldManager.vectorCount + " vec -> " + FORMULA_LABELS[formulaIndex]);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al generar campo: {ex.Message}", this);
                SetStatus($"ERROR: {ex.Message}");
            }
        }

        void OnReset()
        {
            try
            {
                _refreshing = true;
                if (dropCount)   dropCount.value   = 0; // 100
                if (dropFormula) dropFormula.value  = 0;
                if (dropScaleX)  dropScaleX.value   = DEFAULT_SCALE_IDX; // 1
                if (dropScaleY)  dropScaleY.value   = DEFAULT_SCALE_IDX; // 1
                _refreshing = false;

                RefreshZoneDropdown();
                if (dropZone) dropZone.value = 0;

                if (fieldManager)
                {
                    ZoneInfo zone = GetCurrentZone();
                    fieldManager.formula     = FieldFormula.RadialOutward;
                    fieldManager.useTarget   = false;
                    fieldManager.vectorCount = 100;
                    fieldManager.scaleX      = 1f;
                    fieldManager.scaleY      = 1f;
                    fieldManager.zoneCenter  = waterCenter + zone.centerFrac * waterRadius;
                    fieldManager.fieldRadius = waterRadius * zone.radiusFrac;
                    fieldManager.sampleAcrossEntireOcean = zone.label.StartsWith("Todo el oceano", System.StringComparison.OrdinalIgnoreCase);
                    fieldManager.ResetField();
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
            if (statusLabel != null) 
                statusLabel.text = msg; 
        }
    }
}
