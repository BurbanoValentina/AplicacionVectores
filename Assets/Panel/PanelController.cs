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

        // count 100-900: 8 zonas pequeñas que evitan la isla
        static readonly ZoneInfo[] ZONES_SMALL = {
            new ZoneInfo { label="Cuadrante NE",  centerFrac=new Vector2(+0.40f,+0.40f), radiusFrac=0.42f },
            new ZoneInfo { label="Cuadrante NO",  centerFrac=new Vector2(-0.40f,+0.40f), radiusFrac=0.42f },
            new ZoneInfo { label="Cuadrante SE",  centerFrac=new Vector2(+0.40f,-0.40f), radiusFrac=0.42f },
            new ZoneInfo { label="Cuadrante SO",  centerFrac=new Vector2(-0.40f,-0.40f), radiusFrac=0.42f },
            new ZoneInfo { label="Orilla Norte",  centerFrac=new Vector2( 0.00f,+0.72f), radiusFrac=0.24f },
            new ZoneInfo { label="Orilla Sur",    centerFrac=new Vector2( 0.00f,-0.72f), radiusFrac=0.24f },
            new ZoneInfo { label="Orilla Este",   centerFrac=new Vector2(+0.72f, 0.00f), radiusFrac=0.24f },
            new ZoneInfo { label="Orilla Oeste",  centerFrac=new Vector2(-0.72f, 0.00f), radiusFrac=0.24f },
        };

        // count 1000-1900: 2 hemisferios que cubren todo el agua
        static readonly ZoneInfo[] ZONES_MEDIUM = {
            new ZoneInfo { label="Hemisferio Norte", centerFrac=new Vector2(0f,+0.22f), radiusFrac=0.72f },
            new ZoneInfo { label="Hemisferio Sur",   centerFrac=new Vector2(0f,-0.22f), radiusFrac=0.72f },
        };

        // count 2000: todo el mapa, sin filtro de isla
        static readonly ZoneInfo[] ZONES_FULL = {
            new ZoneInfo { label="Todo el mapa", centerFrac=Vector2.zero, radiusFrac=1.00f },
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

        void Start()
        {
            if (fieldManager == null) return;
            Invoke(nameof(InitDropdowns), 0.05f);

            if (btnGenerate) btnGenerate.onClick.AddListener(OnGenerate);
            if (btnReset)    btnReset.onClick.AddListener(OnReset);
            if (btnDelete)   btnDelete.onClick.AddListener(OnDelete);
            if (dropFormula) dropFormula.onValueChanged.AddListener(OnFormulaChanged);
            if (dropCount)   dropCount.onValueChanged.AddListener(OnCountChanged);
            if (dropScaleX)  dropScaleX.onValueChanged.AddListener(_ => OnGenerate());
            if (dropScaleY)  dropScaleY.onValueChanged.AddListener(_ => OnGenerate());
            if (dropZone)    dropZone.onValueChanged.AddListener(OnZoneChanged);

            SetStatus("Listo. Selecciona formula.");
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
                } catch { }
            }

            if (dropFormula != null)
            {
                try {
                    dropFormula.ClearOptions();
                    dropFormula.AddOptions(new List<string>(FORMULA_LABELS));
                    dropFormula.value = 0;
                } catch { }
            }

            if (dropScaleX != null)
            {
                try {
                    dropScaleX.ClearOptions();
                    dropScaleX.AddOptions(new List<string>(SCALE_LABELS));
                    dropScaleX.value = DEFAULT_SCALE_IDX;
                } catch { }
            }

            if (dropScaleY != null)
            {
                try {
                    dropScaleY.ClearOptions();
                    dropScaleY.AddOptions(new List<string>(SCALE_LABELS));
                    dropScaleY.value = DEFAULT_SCALE_IDX;
                } catch { }
            }

            if (dropZone != null)
            {
                try {
                    dropZone.ClearOptions();
                    var opts = new List<string>();
                    foreach (var z in ZONES_SMALL) opts.Add(z.label);
                    dropZone.AddOptions(opts);
                    dropZone.value = 0;
                } catch { }
            }
        }

        void OnFormulaChanged(int idx)
        {
            if (statusLabel) statusLabel.text = "Formula: " + FORMULA_LABELS[idx];
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
            if (count >= 2000) return ZONES_FULL;
            if (count >= 1000) return ZONES_MEDIUM;
            return ZONES_SMALL;
        }

        void RefreshZoneDropdown()
        {
            if (!dropZone) return;
            int count = dropCount ? COUNTS[dropCount.value] : 100;
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

        ZoneInfo GetCurrentZone()
        {
            int count = dropCount ? COUNTS[dropCount.value] : 100;
            ZoneInfo[] zones = GetZonesForCount(count);
            int idx = dropZone ? Mathf.Clamp(dropZone.value, 0, zones.Length - 1) : 0;
            return zones[idx];
        }

        void OnGenerate()
        {
            if (!fieldManager) { SetStatus("ERROR: VFM no encontrado."); return; }

            ZoneInfo zone = GetCurrentZone();

            fieldManager.vectorCount = COUNTS[dropCount.value];
            fieldManager.formula     = (FieldFormula)dropFormula.value;
            fieldManager.useTarget   = false;
            fieldManager.target      = Vector3.zero;
            fieldManager.scaleX      = dropScaleX ? SCALE_VALUES[dropScaleX.value] : 1f;
            fieldManager.scaleY      = dropScaleY ? SCALE_VALUES[dropScaleY.value] : 1f;
            fieldManager.zoneCenter  = waterCenter + zone.centerFrac * waterRadius;
            fieldManager.fieldRadius = waterRadius * zone.radiusFrac;
            fieldManager.GenerateField();

            SetStatus("OK " + fieldManager.vectorCount + " vec -> " + FORMULA_LABELS[dropFormula.value]);
        }

        void OnReset()
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
                fieldManager.ResetField();
            }
            SetStatus("Campo reiniciado.");
        }

        void OnDelete()
        {
            if (fieldManager) fieldManager.DeleteField();
            SetStatus("Campo eliminado.");
        }

        void SetStatus(string msg) { if (statusLabel) statusLabel.text = msg; }
    }
}
