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
        }

        void OnFormulaChanged(int idx)
        {
            if (statusLabel) statusLabel.text = "Formula: " + FORMULA_LABELS[idx];
            OnGenerate();
        }

        void OnGenerate()
        {
            if (!fieldManager) { SetStatus("ERROR: VFM no encontrado."); return; }

            fieldManager.vectorCount = COUNTS[dropCount.value];
            fieldManager.formula     = (FieldFormula)dropFormula.value;
            fieldManager.useTarget   = false;
            fieldManager.target      = Vector3.zero;
            fieldManager.GenerateField();

            SetStatus("OK " + fieldManager.vectorCount + " vec -> " + FORMULA_LABELS[dropFormula.value]);
        }

        void OnReset()
        {
            if (dropCount)   dropCount.value   = 0; // 100
            if (dropFormula) dropFormula.value  = 0;
            if (fieldManager)
            {
                fieldManager.formula     = FieldFormula.RadialOutward;
                fieldManager.useTarget   = false;
                fieldManager.vectorCount = 100;
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
