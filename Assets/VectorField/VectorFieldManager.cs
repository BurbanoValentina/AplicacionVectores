using System;
using System.Collections.Generic;
using UnityEngine;

namespace VectorField
{
    public enum FieldFormula
    {
        RadialOutward   = 0,
        RadialInward    = 1,
        RotationXY      = 2,
        Gravitational   = 3,
        Saddle          = 4,
        Constant        = 5,
        Whirlpool       = 6,
        Spiral          = 7,
        TargetPoint     = 8
    }

    public class VectorFieldManager : MonoBehaviour
    {
        [Header("Prefab de la flecha")]
        public GameObject arrowPrefab;

        [Header("Configuracion del campo")]
        public int         vectorCount = 300;
        public float       fieldRadius = 30f;
        public float       arrowScale  = 12.0f;

        [Header("Formula activa")]
        public FieldFormula formula    = FieldFormula.RadialOutward;

        [Header("Punto Objetivo (solo para TargetPoint)")]
        public bool    useTarget = false;
        public Vector3 target    = Vector3.zero;

        [Header("Animacion de movimiento")]
        public float animSpeed = 1.5f;
        public float animAmplitude = 0.3f;

        readonly List<GameObject> _arrows = new List<GameObject>();
        readonly List<ArrowAnimData> _animData = new List<ArrowAnimData>();

        // true si el prefab apunta en +Y (FlechaApp3 con Cone/Cylinder ProBuilder)
        bool _prefabPointsUpY = false;

        struct ArrowAnimData
        {
            public Vector3 basePos;
            public Vector3 moveDir;
            public float offset;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            AnimateArrows();
        }

        public void GenerateField()
        {
            ClearArrows();
            DetectPrefabOrientation();

            if (arrowPrefab == null)
                Debug.Log("[VFM] arrowPrefab no asignado. Flechas simples.");

            var positions = BuildGrid2D(vectorCount, fieldRadius);
            foreach (Vector2 p in positions)
            {
                Vector2 dir = EvaluateFormula(p);
                Vector3 worldPos = new Vector3(p.x, 0.15f, p.y);
                PlaceArrow(worldPos, dir, positions.Count);
            }
        }

        /// <summary>
        /// Detecta si el prefab apunta en +Y (FlechaApp3 con Cone hijo en Y>0)
        /// o en +Z (generado por ArrowPrefabGenerator con Body en Z=0.5).
        /// </summary>
        void DetectPrefabOrientation()
        {
            _prefabPointsUpY = false;
            if (arrowPrefab == null) return;

            // FlechaApp3 tiene un hijo "Cone" posicionado en Y > 0
            Transform cone = arrowPrefab.transform.Find("Cone");
            if (cone != null && cone.localPosition.y > 0.5f)
            {
                _prefabPointsUpY = true;
                Debug.Log("[VFM] Prefab FlechaApp3 detectado (apunta en +Y).");
                return;
            }

            // Tambien verificar si el primer hijo esta en Y > 0 (otra variante)
            if (arrowPrefab.transform.childCount > 0)
            {
                bool allChildrenInY = true;
                for (int i = 0; i < arrowPrefab.transform.childCount; i++)
                {
                    var child = arrowPrefab.transform.GetChild(i);
                    if (child.localPosition.z > 0.1f)
                    {
                        allChildrenInY = false;
                        break;
                    }
                }
                if (allChildrenInY)
                {
                    // Check if any child is positioned along Y axis
                    for (int i = 0; i < arrowPrefab.transform.childCount; i++)
                    {
                        if (arrowPrefab.transform.GetChild(i).localPosition.y > 0.3f)
                        {
                            _prefabPointsUpY = true;
                            Debug.Log("[VFM] Prefab detectado apuntando en +Y.");
                            return;
                        }
                    }
                }
            }
        }

        public void ResetField()
        {
            formula     = FieldFormula.RadialOutward;
            useTarget   = false;
            target      = Vector3.zero;
            vectorCount = 100;
            GenerateField();
        }

        public void DeleteField() { ClearArrows(); }

        void AnimateArrows()
        {
            for (int i = 0; i < _arrows.Count && i < _animData.Count; i++)
            {
                if (_arrows[i] == null) continue;
                var d = _animData[i];
                if (d.moveDir.sqrMagnitude < 0.001f) continue;
                float t = Mathf.Sin(Time.time * animSpeed + d.offset) * animAmplitude;
                _arrows[i].transform.position = d.basePos + d.moveDir * t;
            }
        }

        static List<Vector2> BuildGrid2D(int n, float radius)
        {
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / cols));
            float halfSize = radius;
            float stepX = cols > 1 ? (2f * halfSize) / (cols - 1) : 0f;
            float stepZ = rows > 1 ? (2f * halfSize) / (rows - 1) : 0f;

            var pts = new List<Vector2>(rows * cols);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (pts.Count >= n) break;
                    float x = cols > 1 ? -halfSize + c * stepX : 0f;
                    float z = rows > 1 ? -halfSize + r * stepZ : 0f;
                    pts.Add(new Vector2(x, z));
                }
            return pts;
        }

        Vector2 EvaluateFormula(Vector2 p)
        {
            float x = p.x, y = p.y;
            float r2 = x * x + y * y;

            switch (formula)
            {
                case FieldFormula.RadialOutward:   return new Vector2(x, y);
                case FieldFormula.RadialInward:    return new Vector2(-x, -y);
                case FieldFormula.RotationXY:      return new Vector2(-y, x);
                case FieldFormula.Gravitational:
                    return r2 < 0.01f ? Vector2.zero : new Vector2(-x, -y) / r2;
                case FieldFormula.Saddle:          return new Vector2(x, -y);
                case FieldFormula.Constant:        return new Vector2(1f, 0f);
                case FieldFormula.Whirlpool:
                    return r2 < 0.01f ? Vector2.zero : new Vector2(-y, x) / r2;
                case FieldFormula.Spiral:          return new Vector2(x - y, x + y);
                case FieldFormula.TargetPoint:
                    Vector2 diff = new Vector2(target.x, target.z) - p;
                    return diff.magnitude > 0.001f ? diff.normalized : Vector2.zero;
                default: return new Vector2(x, y);
            }
        }

        void PlaceArrow(Vector3 worldPos, Vector2 dir2D, int totalCount)
        {
            if (dir2D.sqrMagnitude < 1e-6f) { PlaceDot(worldPos); return; }

            float mag     = dir2D.magnitude;
            float spacing = (fieldRadius * 2f) / Mathf.Max(1, Mathf.Sqrt(totalCount));
            float maxLen  = spacing * 0.90f;
            float scale   = arrowScale * Mathf.Min(1f, maxLen / (mag * arrowScale + 0.0001f));
            // minimo: 55% del espaciado para que las flechas sean siempre visibles
            float minScale = Mathf.Max(0.15f, maxLen * 0.55f);
            scale = Mathf.Clamp(scale, minScale, maxLen);

            float angle = Mathf.Atan2(dir2D.x, dir2D.y) * Mathf.Rad2Deg;
            Quaternion rot;

            if (_prefabPointsUpY)
            {
                // FlechaApp3: apunta en +Y local
                // Tumbar al plano XZ: rotar -90 en X -> ahora apunta en +Z
                // Luego rotar en Y segun la formula
                rot = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            }
            else
            {
                // Prefab generado: apunta en +Z local
                rot = Quaternion.Euler(0f, angle, 0f);
            }

            GameObject arrow;
            if (arrowPrefab != null)
                arrow = Instantiate(arrowPrefab, worldPos, rot, transform);
            else
                arrow = CreateSimpleArrow(worldPos, rot);

            arrow.transform.localScale = Vector3.one * scale;
            _arrows.Add(arrow);

            Vector3 moveDir3D = new Vector3(dir2D.x, 0f, dir2D.y).normalized;
            _animData.Add(new ArrowAnimData
            {
                basePos = worldPos,
                moveDir = moveDir3D,
                offset  = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            });
        }

        // Materiales compartidos para no crear uno por flecha
        static Material _matBody;
        static Material _matTip;
        static Mesh     _coneMesh;

        static Material GetBodyMat()
        {
            if (_matBody == null) _matBody = MakeArrowMaterial(new Color(0.20f, 0.55f, 1.00f));
            return _matBody;
        }
        static Material GetTipMat()
        {
            if (_matTip == null) _matTip = MakeArrowMaterial(new Color(1.00f, 0.50f, 0.08f));
            return _matTip;
        }
        static Mesh GetConeMesh()
        {
            if (_coneMesh == null) _coneMesh = CreateConeMesh(10, 1f, 1f);
            return _coneMesh;
        }

        GameObject CreateSimpleArrow(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Flecha");
            go.transform.SetParent(transform);
            go.transform.position = pos;
            go.transform.rotation = rot;

            // Cuerpo: cilindro tumbado a lo largo del eje Z
            // Dimensiones en espacio local unitario (se escalan con arrow.localScale)
            // El cilindro de Unity tiene su longitud en Y; rotamos 90 en X para que quede en Z
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cylinder";
            body.transform.SetParent(go.transform);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, 0f, 0.40f);
            body.transform.localScale    = new Vector3(0.18f, 0.40f, 0.18f);
            var bc = body.GetComponent<Collider>(); if (bc) DestroyImmediate(bc);
            body.GetComponent<Renderer>().sharedMaterial = GetBodyMat();

            // Punta: cono procedural apuntando en +Z
            var coneGO = new GameObject("Cone");
            coneGO.transform.SetParent(go.transform);
            coneGO.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            coneGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coneGO.transform.localScale    = new Vector3(0.32f, 0.45f, 0.32f);
            var coneFilter = coneGO.AddComponent<MeshFilter>();
            coneFilter.sharedMesh = GetConeMesh();
            var coneRend = coneGO.AddComponent<MeshRenderer>();
            coneRend.sharedMaterial = GetTipMat();

            return go;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Genera una malla de cono (apex en +Y, base centrada en -Y/2).
        // ──────────────────────────────────────────────────────────────────────
        static Mesh CreateConeMesh(int sides, float radius, float height)
        {
            int vCount = sides + 2;          // apex (0), base-center (1), base ring (2..sides+1)
            var verts   = new Vector3[vCount];
            var normals = new Vector3[vCount];
            var tris    = new int[sides * 6]; // sides triangles (lateral) + sides triangles (base)

            float halfH = height * 0.5f;

            // Apex
            verts[0]   = new Vector3(0f, halfH, 0f);
            normals[0] = Vector3.up;

            // Base center
            verts[1]   = new Vector3(0f, -halfH, 0f);
            normals[1] = Vector3.down;

            // Base ring
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts[i + 2]   = new Vector3(x, -halfH, z);
                normals[i + 2] = new Vector3(x, 0f, z).normalized;
            }

            int ti = 0;
            for (int i = 0; i < sides; i++)
            {
                int cur = i + 2;
                int nxt = (i + 1) % sides + 2;
                // Cara lateral
                tris[ti++] = 0; tris[ti++] = nxt; tris[ti++] = cur;
                // Base
                tris[ti++] = 1; tris[ti++] = cur; tris[ti++] = nxt;
            }

            var mesh = new Mesh { name = "ConeMesh" };
            mesh.vertices  = verts;
            mesh.normals   = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // Crea un material compatible con URP o Built-in pipeline.
        static Material MakeArrowMaterial(Color color)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Unlit/Color");
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);
            return mat;
        }

        void PlaceDot(Vector3 pos)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.SetParent(transform, false);
            dot.transform.position   = pos;
            dot.transform.localScale = Vector3.one * 0.08f;
            var rend = dot.GetComponent<Renderer>();
            if (rend) rend.material = new Material(Shader.Find("Unlit/Color")) { color = Color.yellow };
            _arrows.Add(dot);
            _animData.Add(new ArrowAnimData { basePos = pos, moveDir = Vector3.zero, offset = 0 });
        }

        void ClearArrows()
        {
            foreach (var a in _arrows) if (a != null) Destroy(a);
            _arrows.Clear();
            _animData.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            // Limpiar cache de materiales/mesh estaticos al salir de Play
            _matBody  = null;
            _matTip   = null;
            _coneMesh = null;
        }
    }
}
