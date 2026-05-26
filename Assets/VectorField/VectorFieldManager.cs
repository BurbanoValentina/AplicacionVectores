using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Header("Visibilidad (fallback sin prefab)")]
        [Tooltip("Si no hay arrowPrefab, se generan flechas simples con primitivas. Con conteos altos (ej. 2000) el escalado por cantidad puede volverlas demasiado pequeñas. Este multiplicador las hace visibles.")]
        [Min(1f)]
        public float simpleArrowScaleMultiplier = 8f;

        [Header("Configuracion del campo")]
        public int         vectorCount = 300;
        public float       fieldRadius = 20f;
        public float       arrowScale  = 1f;

        [Header("Zonas / persistencia")]
        [Tooltip("Si esta activo, al generar se reemplaza solo la zona actual y las otras zonas quedan en escena.")]
        public bool accumulateByZone = true;
        [Tooltip("Clave de zona actual (la setea el Panel). Si esta vacia, se calcula desde backSectors/backSectorIndex.")]
        public string activeZoneKey = "";

        [Header("Packing (densidad)")]
        [Tooltip("Si esta activo, el campo se empaqueta en un rectangulo mas pequeno para que las flechas queden mas juntas.")]
        public bool useDensePacking = true;
        [Min(0.05f)]
        public float desiredCellSize = 0.60f;
        [Tooltip("Si hay sectores atras, fuerza que cada zona sea cuadrada (profundidad = ancho del sector).")]
        public bool squareBackSectorZones = true;

        [Header("Altura de spawn")]
        public bool autoDetectWaterYFromOceanBounds = true;
        public float spawnYOffset = 0.15f;

        [Header("Forzado para demo")]
        public bool forceAtLeastNVectors = true;
        public int  forcedMinimumVectorCount = 2000;
        public bool onlyBackOfIsland = true;
        public float backEndOffset = 8f;

        [Header("Sectores atras (soloBack)")]
        public bool useBackSectors = true;
        [Min(1)] public int backSectors = 1;
        [Min(0)] public int backSectorIndex = 0;
        [Range(0.01f, 0.5f)] public float backSectorShrinkAt2000 = 0.15f;
        [Range(0f, 0.45f)] public float spawnJitterFactor = 0.02f;
        public bool useBackPackedRectangle = true;
        public float packedRectHorizontalPadding = 0f;
        public float packedRectBackPadding = 0f;
        public float packedRectFrontPadding = 0f;
        // Nota: para el escalado por cantidad, interpretamos:
        // - absoluteMaxArrowScale = escala cuando hay pocos vectores (100 aprox)
        // - absoluteMinArrowScale = escala cuando hay ~1000 vectores
        // y para 2000 se reduce aun mas (absoluteMinArrowScale * backSectorShrinkAt2000)
        public float absoluteMinArrowScale = 0.5f;
        public float absoluteMaxArrowScale = 1.0f;

        [Header("Multiplicadores X/Y (salida del vector)")]
        public float scaleX = 1f;
        public float scaleY = 1f;

        [Header("Zona activa")]
        public Vector2 zoneCenter = Vector2.zero;

        [Header("Isla (zona de exclusion)")]
        public Vector2 islandCenter          = Vector2.zero;
        public float   islandRadius          = 18f;
        public int     islandAvoidThreshold  = 1000;

        [Header("Filtro agua vs isla")]
        public float waterSurfaceY = 15f;
        public float landHeightThreshold = 0.35f;
        public float islandExclusionPadding = 25f;
        public bool  avoidUnderIslandWater = true;
        public bool  usePhysicsLandFilter = false;
        public bool  sampleAcrossEntireOcean = true;
        public bool  autoDetectOceanBounds = true;
        public string oceanNameHint = "Ocean";
        public float oceanSideInset = 0f;
        public float oceanFrontBackInset = 0f;
        public bool  excludeSolidObjectsOverWater = true;
        public bool  allowUnderBoats = true;

        [Header("Color temporal del campo")]
        public bool forceFieldColor = false;
        public Color forcedFieldColor = Color.black;

        [Header("Deteccion automatica de isla")]
        public bool autoDetectIslandFromScene = true;
        public string islandRootNameHint = "LandMass";

        [Header("Formula activa")]
        public FieldFormula formula    = FieldFormula.RadialOutward;

        [Header("Funciones f(x,y) (James Stewart)")]
        [Tooltip("Si está activo, el vector se evalúa como F(x,y)=<P(x,y),Q(x,y)> usando los textos ingresados en el panel.")]
        public bool useFunctionInputs = false;
        [Tooltip("Primera función P(x,y)")]
        public string functionP = "x";
        [Tooltip("Segunda función Q(x,y)")]
        public string functionQ = "y";

        [Header("Área fija detrás del agua")]
        [Tooltip("Si está activo, fuerza un solo campo rectangular detrás del océano, centrado en la parte trasera, del tamaño de la isla.")]
        public bool useSingleBackRectSameAsIsland = false;
        [Min(0f)]
        public float backRectEdgeInset = 0.5f;
        [Tooltip("Multiplica el tamaño del rectángulo detrás del mapa (1 = tamaño isla). Útil para 'un poco más grande'.")]
        [Min(0.5f)]
        public float backRectScaleMultiplier = 1.5f;

        [Header("Punto Objetivo (solo para TargetPoint)")]
        public bool    useTarget = false;
        public Vector3 target    = Vector3.zero;

        [Header("Centro del campo (marcador)")]
        public bool  showFieldCenterMarker = true;
        public float centerMarkerRadius = 0.6f;
        public Color centerMarkerColor = new Color(1f, 0.2f, 0.2f, 0.9f);

        [Header("Centro del campo (bolita)")]
        [Tooltip("Si se asigna, este transform define el centro del campo (origen de la formula).")]
        public Transform fieldCenterTransform;
        [Tooltip("Usar fieldCenterTransform para el origen de la formula y zona.")]
        public bool useFieldCenterTransformForOrigin = true;
        [Tooltip("Generar posiciones alrededor de la bolita (ignora el muestreo del oceano).")]
        public bool useFieldCenterTransformForPositions = true;
        [Tooltip("Si se generan posiciones alrededor de la bolita, ignorar filtros de isla y 'back of island'.")]
        public bool useFieldCenterTransformIgnoreFilters = true;
        [Tooltip("Usar fieldCenterTransform para el marcador/target del centro.")]
        public bool useFieldCenterTransformForMarker = true;
        [Tooltip("Si se usa fieldCenterTransform, ocultar la esfera automatica de centro.")]
        public bool hideAutoCenterMarkerWhenUsingTransform = true;

        [Header("Muestreo radial desde el centro del campo")]
        [Tooltip("Radio minimo desde la bolita centro hasta donde aparecen las flechas (evita el primer tramo comprimido).")]
        public float radialMinRadius = 4f;
        [Tooltip("Radio maximo desde la bolita centro hasta donde llegan las flechas (limite del rectangulo del campo).")]
        public float radialMaxRadius = 20f;
        [Tooltip("Angulo inicial del sector en grados (0 = hacia +Z / frente del mapa, 90 = +X / derecha, -90 = -X / izquierda, 180/ -180 = -Z / atras).")]
        public float radialArcStartDeg = -90f;
        [Tooltip("Angulo de barrido del sector en grados (180 = semicirculo, 270 = casi todo menos el centro-atras, 360 = circulo completo).")]
        public float radialArcSpanDeg = 270f;

        const string FieldCenterObjectName = "VFM FIELD CENTER";

        [Header("Animacion de movimiento")]
        public float animSpeed = 1.5f;
        public float animAmplitude = 0.3f;

        [Header("Magnitud (estilo Stewart)")]
        [Tooltip("Si está activo, la longitud de cada flecha es proporcional a |F(x,y)| (como en los diagramas del libro).")]
        public bool useVectorMagnitudeForLength = true;
        [Tooltip("Factor para convertir magnitud |F| en escala de longitud.")]
        public float magnitudeToLength = 0.18f;
        [Tooltip("Límite mínimo de longitud relativa.")]
        public float minLengthFactor = 0.55f;
        [Tooltip("Límite máximo de longitud relativa.")]
        public float maxLengthFactor = 2.80f;

        [Header("Parent de flechas")]
        [Tooltip("Si el manager está bajo un Canvas/Panel con escala (ej. WorldSpace UI), las flechas heredan esa escala y pueden volverse invisibles.\nSi está activo, se crea/usa un root en la raíz de la escena con escala 1 para instanciar las flechas.")]
        public bool spawnArrowsUnderSceneRoot = true;
        [Tooltip("Opcional: si se asigna, las flechas se instancian bajo este transform.")]
        public Transform arrowsParentOverride;
        private Vector2 GlobalEvalOrigin;

        struct ArrowEntry
        {
            public string zoneKey;
            public GameObject arrow;
            public ArrowAnimData anim;
        }

        readonly List<ArrowEntry> _entries = new List<ArrowEntry>();

        Transform _arrowsParentRuntime;
        GameObject _centerMarker;

        ExpressionCompiler.CompiledExpression _compiledP;
        ExpressionCompiler.CompiledExpression _compiledQ;
        string _compiledPLast;
        string _compiledQLast;

        // true si el prefab apunta en +Y (FlechaApp3 con Cone/Cylinder ProBuilder)
        bool _prefabPointsUpY = false;
        bool _hasOceanBounds = false;
        Bounds _oceanBounds;
        bool _hasIslandBounds = false;
        Bounds _islandBounds;

        struct ArrowAnimData
        {
            public Vector3 basePos;
            public Vector3 moveDir;
            public float offset;
        }

        string _generationZoneKey = "";

        void Update()
        {
            if (!Application.isPlaying) return;
            AnimateArrows();
        }

        void Awake()
        {
            // Forzar SIEMPRE el uso de la bolita [VFM Field Center] como centro
            ForceFieldCenterToVFMFieldCenter();
            // Puedes cambiar la fórmula aquí si quieres que apunten hacia la bolita:
            formula = FieldFormula.RadialOutward; // o FieldFormula.RadialInward
        }

        bool TryResolveFieldCenterTransform()
        {
            // SIEMPRE buscar y usar la bolita [VFM Field Center] como centro
            var go = GameObject.Find("[VFM Field Center]");
            if (go == null)
                go = GameObject.Find(FieldCenterObjectName);
            if (go == null)
                go = GameObject.Find("VFM Field Center");
            
            if (go == null)
            {
                // Crear automáticamente si no existe
                Debug.LogWarning("[VFM] Objeto [VFM Field Center] no encontrado en TryResolveFieldCenterTransform. Creando automáticamente...");
                go = new GameObject("[VFM Field Center]");
                go.transform.position = new Vector3(zoneCenter.x, waterSurfaceY + spawnYOffset, zoneCenter.y);
            }
            
            fieldCenterTransform = go.transform;
            useFieldCenterTransformForOrigin = true;
            useFieldCenterTransformForMarker = true;
            useFieldCenterTransformForPositions = true;
            useFieldCenterTransformIgnoreFilters = true;
            hideAutoCenterMarkerWhenUsingTransform = true;
            return true;
        }

        // Forzar SIEMPRE el uso de la bolita [VFM Field Center] como centro
        private void ForceFieldCenterToVFMFieldCenter()
        {
            var go = GameObject.Find("[VFM Field Center]");
            if (go == null)
                go = GameObject.Find(FieldCenterObjectName);
            if (go == null)
                go = GameObject.Find("VFM Field Center");
            
            if (go == null)
            {
                // Crear automáticamente el objeto si no existe
                Debug.LogWarning("[VFM] Objeto [VFM Field Center] no encontrado. Creando automáticamente...");
                
                // Calcular posición: centro del océano si existe, sino usar zona actual
                Vector3 spawnPos = Vector3.zero;
                if (_hasOceanBounds)
                {
                    spawnPos = _oceanBounds.center;
                    spawnPos.y = waterSurfaceY + spawnYOffset;
                }
                else
                {
                    spawnPos = new Vector3(zoneCenter.x, waterSurfaceY + spawnYOffset, zoneCenter.y);
                }
                
                go = new GameObject("[VFM Field Center]");
                go.transform.position = spawnPos;
                Debug.Log($"[VFM] Bolita creada en posición: {spawnPos}");
            }
            
            fieldCenterTransform = go.transform;
            useFieldCenterTransformForOrigin = true;
            useFieldCenterTransformForMarker = true;
            useFieldCenterTransformForPositions = true;
            useFieldCenterTransformIgnoreFilters = true;
            hideAutoCenterMarkerWhenUsingTransform = true;
        }

        public Vector3 GetFieldCenterWorld()
        {
            if (fieldCenterTransform != null)
                return fieldCenterTransform.position;

            return new Vector3(zoneCenter.x, transform.position.y, zoneCenter.y);
        }

        public bool TryGetOceanBounds(out Bounds bounds)
        {
            if (!_hasOceanBounds)
                RefreshOceanBoundsFromScene();

            if (_hasOceanBounds)
            {
                bounds = _oceanBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        public bool TryGetIslandBounds(out Bounds bounds)
        {
            if (!_hasIslandBounds)
                RefreshIslandBoundsFromScene();

            if (_hasIslandBounds)
            {
                bounds = _islandBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        public Vector2 GetFieldDirection(Vector3 worldPos)
        {
            TryResolveFieldCenterTransform();

            Vector2 origin = zoneCenter;
            if (useFieldCenterTransformForOrigin && fieldCenterTransform != null)
            {
                var center = fieldCenterTransform.position;
                origin = new Vector2(center.x, center.z);
            }

            return EvaluateFormula(new Vector2(worldPos.x, worldPos.z), origin);
        }

        public void GenerateField()
        {
            // Forzar SIEMPRE el uso de la bolita [VFM Field Center] como centro
            ForceFieldCenterToVFMFieldCenter();
            // Puedes cambiar la fórmula aquí si quieres que apunten hacia la bolita:
            formula = FieldFormula.RadialOutward; // o FieldFormula.RadialInward

            string zoneKey = GetZoneKey();
            _generationZoneKey = zoneKey;

            if (accumulateByZone)
                ClearZone(zoneKey);
            else
                ClearArrows();

            DetectPrefabOrientation();
            RefreshIslandBoundsFromScene();
            RefreshOceanBoundsFromScene();

            if (!_hasOceanBounds)
                Debug.LogWarning("[VFM] No se detectaron bounds del océano. Usando grid/fallback; ajusta oceanNameHint si hace falta.", this);

            if (useFunctionInputs)
            {
                if (!EnsureCompiledFunctions(out string err))
                {
                    Debug.LogError($"[VFM] Funciones inválidas: {err}");
                    return;
                }
            }

            int desiredCount = forceAtLeastNVectors
                ? Mathf.Max(vectorCount, forcedMinimumVectorCount)
                : vectorCount;

            if (arrowPrefab == null)
                Debug.Log("[VFM] arrowPrefab no asignado. Flechas simples.");

            bool avoidIsland = islandRadius > 0.01f;
            float exclusionRadius = avoidIsland ? islandRadius + islandExclusionPadding : 0f;

            List<Vector2> positions;
            Vector2 evalOrigin = zoneCenter;

            // SIEMPRE usar la bolita como centro - es obligatorio
            var center = fieldCenterTransform.position;
            evalOrigin = new Vector2(center.x, center.z);
            zoneCenter = evalOrigin;
            
            // Expandir todos los campos vectoriales en el rectángulo del agua
            if (_hasOceanBounds)
            {
                // Usar el rectángulo completo del océano para distribuir vectores
                positions = BuildDistributedOceanPointsFromCenter(desiredCount, _oceanBounds, evalOrigin);
            }
            else
            {
                // Fallback: grid centrado en la bolita sin filtros
                positions = BuildGrid2DNoFilters(desiredCount, fieldRadius, evalOrigin);
            }

            if (positions == null || positions.Count == 0)
            {
                Debug.LogError($"[VFM] No hay puntos para spawnear. desired={desiredCount} oceanBounds={_hasOceanBounds}", this);
                return;
            }

            UpdateFieldCenterMarker(positions);
            GlobalEvalOrigin = evalOrigin;

            int placed = 0;
            foreach (Vector2 p in positions)
            {
                Vector2 dir = EvaluateFormula(p, evalOrigin);
                Vector3 worldPos = new Vector3(p.x, GetSpawnY(p), p.y);
                PlaceArrow(worldPos, dir, desiredCount);
                placed++;
            }

            var parent = GetArrowsParent();
            Debug.Log($"[VFM] Parent de flechas: {(parent != null ? parent.name : "<null>")} / root={(parent != null ? parent.root.name : "<null>")}", this);
            if (positions.Count > 0)
            {
                var p0 = positions[0];
                float y0 = GetSpawnY(p0);
                Debug.Log($"[VFM] Ejemplo pos0=({p0.x:F2},{p0.y:F2}) ySpawn={y0:F2} oceanMaxY={(_hasOceanBounds ? _oceanBounds.max.y.ToString("F2") : "NA")} waterY={waterSurfaceY:F2}", this);
            }

            Debug.Log($"[VFM] Generado. placed={placed} desired={desiredCount} useFunctions={useFunctionInputs} oceanBounds={_hasOceanBounds}", this);
        }

        void UpdateFieldCenterMarker(List<Vector2> positions)
        {
            if (positions == null || positions.Count == 0)
                return;

            if (useFieldCenterTransformForMarker && fieldCenterTransform != null)
            {
                Vector3 centerFromTransform = fieldCenterTransform.position;
                target = centerFromTransform;

                if (hideAutoCenterMarkerWhenUsingTransform)
                {
                    if (_centerMarker != null)
                        _centerMarker.SetActive(false);
                    return;
                }

                if (_centerMarker == null)
                    CreateCenterMarker();

                _centerMarker.transform.position = centerFromTransform;
                _centerMarker.transform.localScale = Vector3.one * Mathf.Max(0.05f, centerMarkerRadius * 2f);
                _centerMarker.SetActive(true);
                return;
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 p = positions[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minZ) minZ = p.y;
                if (p.y > maxZ) maxZ = p.y;
            }

            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            float centerY = GetSpawnY(new Vector2(centerX, centerZ));
            Vector3 centerWorld = new Vector3(centerX, centerY, centerZ);

            // Guardar el centro como target para usos de formula.
            target = centerWorld;

            if (!showFieldCenterMarker)
            {
                if (_centerMarker != null)
                    _centerMarker.SetActive(false);
                return;
            }

            if (_centerMarker == null)
                CreateCenterMarker();

            _centerMarker.transform.position = centerWorld;
            _centerMarker.transform.localScale = Vector3.one * Mathf.Max(0.05f, centerMarkerRadius * 2f);
            _centerMarker.SetActive(true);
        }

        void CreateCenterMarker()
        {
            _centerMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _centerMarker.name = "[VFM Field Center]";
            var col = _centerMarker.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            var renderer = _centerMarker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Standard"));
                renderer.sharedMaterial.color = centerMarkerColor;
            }
        }

        bool EnsureCompiledFunctions(out string error)
        {
            error = null;

            string p = functionP ?? string.Empty;
            string q = functionQ ?? string.Empty;

            if (string.IsNullOrWhiteSpace(p) || string.IsNullOrWhiteSpace(q))
            {
                error = "Ambas funciones deben tener texto";
                return false;
            }

            if (_compiledP != null && _compiledQ != null && p == _compiledPLast && q == _compiledQLast)
                return true;

            if (!ExpressionCompiler.TryCompile(p, out _compiledP, out string errP))
            {
                error = $"Primera función: {errP}";
                return false;
            }

            if (!ExpressionCompiler.TryCompile(q, out _compiledQ, out string errQ))
            {
                error = $"Segunda función: {errQ}";
                return false;
            }

            _compiledPLast = p;
            _compiledQLast = q;
            return true;
        }

        public bool SetFunctions(string p, string q, out string error)
        {
            functionP = p ?? string.Empty;
            functionQ = q ?? string.Empty;
            useFunctionInputs = true;
            _compiledPLast = null;
            _compiledQLast = null;
            _compiledP = null;
            _compiledQ = null;
            return EnsureCompiledFunctions(out error);
        }

        static bool TryComputeBackRectSameAsIsland(Bounds oceanBounds, Vector2 islandCenter, float islandRadius, float edgeInset, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            // Rect del tamaño del diámetro de la isla.
            float oceanW = oceanBounds.size.x;
            float oceanD = oceanBounds.size.z;

            float half = Mathf.Max(0.01f, islandRadius);
            // Si el océano es más pequeño (por escalado del agua), ajustar para que quepa.
            half = Mathf.Min(half, oceanW * 0.5f);
            half = Mathf.Min(half, oceanD * 0.5f);

            // Centrar horizontalmente en el centro del océano ("centro del mapa").
            float centerX = oceanBounds.center.x;
            float inset = Mathf.Max(0f, edgeInset);

            // Elegir el borde "atrás" de forma robusta:
            // tomamos el lado del océano (minZ o maxZ) que queda MÁS LEJOS del centro de la isla.
            float distToMin = Mathf.Abs(islandCenter.y - oceanBounds.min.z);
            float distToMax = Mathf.Abs(oceanBounds.max.z - islandCenter.y);
            bool backIsMinZ = distToMin >= distToMax;

            float targetCenterZ = backIsMinZ
                ? (oceanBounds.min.z + inset + half)
                : (oceanBounds.max.z - inset - half);

            minX = centerX - half;
            maxX = centerX + half;
            minZ = targetCenterZ - half;
            maxZ = targetCenterZ + half;

            // Ajustar para que quede dentro del bounds del océano (solo shift, sin cambiar tamaño si es posible).
            ShiftRect1D(oceanBounds.min.x, oceanBounds.max.x, ref minX, ref maxX);
            ShiftRect1D(oceanBounds.min.z, oceanBounds.max.z, ref minZ, ref maxZ);

            return (maxX - minX) > 0.01f && (maxZ - minZ) > 0.01f;
        }

        Transform GetArrowsParent()
        {
            if (arrowsParentOverride != null)
                return arrowsParentOverride;

            if (!Application.isPlaying)
                return transform;

            if (!spawnArrowsUnderSceneRoot)
                return transform;

            // Siempre crear un contenedor visible en la raíz durante Play.
            if (_arrowsParentRuntime != null)
                return _arrowsParentRuntime;

            const string rootName = "[VectorFieldRuntimeRoot]";
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                root = new GameObject(rootName);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
            }

            var container = new GameObject($"[VFM Arrows] {gameObject.name}");
            container.transform.SetParent(root.transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale = Vector3.one;

            _arrowsParentRuntime = container.transform;
            return _arrowsParentRuntime;
        }

        static void ShiftRect1D(float boundsMin, float boundsMax, ref float rectMin, ref float rectMax)
        {
            float size = rectMax - rectMin;
            if (size <= 0.0001f)
                return;

            if (size > (boundsMax - boundsMin))
            {
                rectMin = boundsMin;
                rectMax = boundsMax;
                return;
            }

            if (rectMin < boundsMin)
            {
                float d = boundsMin - rectMin;
                rectMin += d;
                rectMax += d;
            }
            if (rectMax > boundsMax)
            {
                float d = rectMax - boundsMax;
                rectMin -= d;
                rectMax -= d;
            }
        }

        string GetZoneKey()
        {
            if (!string.IsNullOrEmpty(activeZoneKey))
                return activeZoneKey;

            if (useBackSectors && backSectors > 1)
                return $"Atras sector {Mathf.Clamp(backSectorIndex, 0, backSectors - 1) + 1}/{backSectors}";

            return "Zona";
        }

        float GetSpawnY(Vector2 p)
        {
            if (autoDetectWaterYFromOceanBounds && _hasOceanBounds)
            {
                // Si detectamos mal el océano, su bounds puede tener altura enorme y las flechas
                // terminan spawneando fuera de cámara. En ese caso, usamos waterSurfaceY.
                float oceanHeight = _oceanBounds.size.y;
                float oceanMaxY = _oceanBounds.max.y;
                if (oceanHeight > 8f || float.IsNaN(oceanMaxY) || float.IsInfinity(oceanMaxY))
                    return waterSurfaceY + spawnYOffset;

                // Usar la superficie (max.y) y asegurar mínimo en waterSurfaceY.
                float y = Mathf.Max(waterSurfaceY, oceanMaxY);
                return y + spawnYOffset;
            }

            return waterSurfaceY + spawnYOffset;
        }

        void RefreshOceanBoundsFromScene()
        {
            _hasOceanBounds = false;
            if (!autoDetectOceanBounds)
                return;

            if (!TryDetectOceanBounds(out var b))
            {
                // Fallback: inferir un "oceano" por heurística (gran área XZ y poco grosor Y).
                if (!TryDetectLargestFlatBounds(out b))
                    return;
            }

            _oceanBounds = b;
            _hasOceanBounds = true;
        }

        bool TryDetectOceanBounds(out Bounds bounds)
        {
            bounds = default;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
                return false;

            bool found = false;
            float bestArea = -1f;

            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                var transforms = root.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    var tr = transforms[j];
                    if (tr == null)
                        continue;

                    if (!tr.name.Contains(oceanNameHint, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var renderers = tr.GetComponentsInChildren<Renderer>(true);
                    for (int k = 0; k < renderers.Length; k++)
                    {
                        var r = renderers[k];
                        if (r == null)
                            continue;

                        var b = r.bounds;
                        float area = b.size.x * b.size.z;
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bounds = b;
                            found = true;
                        }
                    }

                    var colliders = tr.GetComponentsInChildren<Collider>(true);
                    for (int k = 0; k < colliders.Length; k++)
                    {
                        var c = colliders[k];
                        if (c == null)
                            continue;

                        var b = c.bounds;
                        float area = b.size.x * b.size.z;
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bounds = b;
                            found = true;
                        }
                    }
                }
            }

            return found && bestArea > 1f;
        }

        bool TryDetectLargestFlatBounds(out Bounds bounds)
        {
            bounds = default;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
                return false;

            bool found = false;
            float bestScore = -1f;

            // Queremos un objeto con área XZ grande y altura Y pequeña (tipo plano de agua).
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                var renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    var ren = renderers[r];
                    if (ren == null)
                        continue;

                    var b = ren.bounds;
                    float area = b.size.x * b.size.z;
                    if (area < 200f)
                        continue;

                    float height = Mathf.Max(0.0001f, b.size.y);
                    float score = area * (1f / height);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bounds = b;
                        found = true;
                    }
                }

                var colliders = root.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    var col = colliders[c];
                    if (col == null)
                        continue;

                    var b = col.bounds;
                    float area = b.size.x * b.size.z;
                    if (area < 200f)
                        continue;

                    float height = Mathf.Max(0.0001f, b.size.y);
                    float score = area * (1f / height);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bounds = b;
                        found = true;
                    }
                }
            }

            return found;
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
            scaleX      = 1f;
            scaleY      = 1f;
            zoneCenter  = Vector2.zero;
            sampleAcrossEntireOcean = true;
            GenerateField();
        }

        public void DeleteField() { ClearArrows(); }

        // Distribuye vectores en todo el rectángulo del océano centrado en la bolita
        List<Vector2> BuildDistributedOceanPointsFromCenter(int n, Bounds oceanBounds, Vector2 centerPoint)
        {
            float minX = oceanBounds.min.x;
            float maxX = oceanBounds.max.x;
            float minZ = oceanBounds.min.z;
            float maxZ = oceanBounds.max.z;

            float width = Mathf.Max(1f, maxX - minX);
            float depth = Mathf.Max(1f, maxZ - minZ);
            float aspect = width / depth;

            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * aspect)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / cols));

            float stepX = width / cols;
            float stepZ = depth / rows;
            float jitter = Mathf.Clamp(spawnJitterFactor, 0f, 0.45f);
            float jitterX = stepX * jitter;
            float jitterZ = stepZ * jitter;

            var pts = new List<Vector2>(n);
            for (int r = 0; r < rows && pts.Count < n; r++)
            {
                for (int c = 0; c < cols && pts.Count < n; c++)
                {
                    float baseX = minX + (c + 0.5f) * stepX;
                    float baseZ = minZ + (r + 0.5f) * stepZ;

                    float x = baseX + UnityEngine.Random.Range(-jitterX, jitterX);
                    float z = baseZ + UnityEngine.Random.Range(-jitterZ, jitterZ);
                    var pt = new Vector2(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(z, minZ, maxZ));

                    if (!IsPointValidForSpawn(pt))
                        continue;

                    pts.Add(pt);
                }
            }

            int attempts = 0;
            int maxAttempts = Mathf.Max(6000, n * 30);
            while (pts.Count < n && attempts < maxAttempts)
            {
                attempts++;
                float x = UnityEngine.Random.Range(minX, maxX);
                float z = UnityEngine.Random.Range(minZ, maxZ);
                var pt = new Vector2(x, z);

                if (!IsPointValidForSpawn(pt))
                    continue;

                pts.Add(pt);
            }

            return pts;
        }

        // inflate: genera mas candidatos para compensar los filtrados por la isla
        List<Vector2> BuildGrid2D(int n, float radius, Vector2 center,
                                  Vector2 islandCenter, float islandRadius)
        {
            float inflate = islandRadius > 0f ? 1.9f : 1.0f;
            if (avoidUnderIslandWater)
                inflate = Mathf.Max(inflate, 2.5f);

            int   target  = Mathf.RoundToInt(n * inflate);
            int   cols    = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(target)));
            int   rows    = Mathf.Max(1, Mathf.CeilToInt((float)target / cols));
            float stepX   = cols > 1 ? (2f * radius) / (cols - 1) : 0f;
            float stepZ   = rows > 1 ? (2f * radius) / (rows - 1) : 0f;

            var pts = new List<Vector2>(n);
            for (int r = 0; r < rows && pts.Count < n; r++)
                for (int c = 0; c < cols && pts.Count < n; c++)
                {
                    float x  = (cols > 1 ? -radius + c * stepX : 0f) + center.x;
                    float z  = (rows > 1 ? -radius + r * stepZ : 0f) + center.y;
                    var   pt = new Vector2(x, z);
                    if (islandRadius > 0f && Vector2.Distance(pt, islandCenter) < islandRadius)
                        continue;
                    if (onlyBackOfIsland && pt.y > (islandCenter.y - backEndOffset))
                        continue;
                    if (avoidUnderIslandWater && IsPointUnderIsland(pt))
                        continue;
                    if (!IsPointValidForSpawn(pt))
                        continue;
                    pts.Add(pt);
                }

            // Si el filtrado por isla/terreno deja pocos puntos, completar por muestreo aleatorio.
            int safety = 0;
            int maxAttempts = Mathf.Max(n * 40, 4000);
            while (pts.Count < n && safety < maxAttempts)
            {
                safety++;
                float x = UnityEngine.Random.Range(-radius, radius) + center.x;
                float z = UnityEngine.Random.Range(-radius, radius) + center.y;
                var pt = new Vector2(x, z);

                if (islandRadius > 0f && Vector2.Distance(pt, islandCenter) < islandRadius)
                    continue;
                if (onlyBackOfIsland && pt.y > (islandCenter.y - backEndOffset))
                    continue;
                if (avoidUnderIslandWater && IsPointUnderIsland(pt))
                    continue;
                if (!IsPointValidForSpawn(pt))
                    continue;

                pts.Add(pt);
            }

            return pts;
        }

        List<Vector2> BuildGrid2DNoFilters(int n, float radius, Vector2 center)
        {
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / cols));
            float stepX = cols > 1 ? (2f * radius) / (cols - 1) : 0f;
            float stepZ = rows > 1 ? (2f * radius) / (rows - 1) : 0f;

            var pts = new List<Vector2>(n);
            for (int r = 0; r < rows && pts.Count < n; r++)
                for (int c = 0; c < cols && pts.Count < n; c++)
                {
                    float x = (cols > 1 ? -radius + c * stepX : 0f) + center.x;
                    float z = (rows > 1 ? -radius + r * stepZ : 0f) + center.y;
                    var pt = new Vector2(x, z);
                    if (!IsPointValidForSpawn(pt))
                        continue;
                    pts.Add(pt);
                }

            int safety = 0;
            int maxAttempts = Mathf.Max(n * 40, 4000);
            while (pts.Count < n && safety < maxAttempts)
            {
                safety++;
                float x = UnityEngine.Random.Range(-radius, radius) + center.x;
                float z = UnityEngine.Random.Range(-radius, radius) + center.y;
                var pt = new Vector2(x, z);
                if (!IsPointValidForSpawn(pt))
                    continue;
                pts.Add(pt);
            }

            return pts;
        }

        List<Vector2> BuildRadialSectorPoints(int n, Vector2 center)
        {
            // Muestreo radial desde el centro del campo.
            // Las flechas se distribuyen en un sector circular alrededor de la bolita centro.
            // - No hay flechas en el anillo interior [0, radialMinRadius] (evita la zona comprimida junto a la bolita).
            // - Solo se generan puntos dentro del arco [radialArcStartDeg, radialArcStartDeg + radialArcSpanDeg]
            //   medido en sentido antihorario desde +Z=0: 0=+Z(frente), 90=+X(derecha), -90=-X(izquierda), 180/-Z=atrás.
            // - No se generan puntos más allá de radialMaxRadius (limite del rectángulo).
            // - Se descartan los puntos fuera de los límites de agua (IsPointValidForSpawn).

            float rmin = Mathf.Max(0.05f, radialMinRadius);
            float rmax = Mathf.Max(rmin + 0.1f, radialMaxRadius);
            float arcStart = radialArcStartDeg * Mathf.Deg2Rad;
            float arcSpan = radialArcSpanDeg * Mathf.Deg2Rad;

            var pts    = new List<Vector2>(n);
            int safety = 0;
            int maxAttempts = Mathf.Max(n * 80, 8000);

            while (pts.Count < n && safety < maxAttempts)
            {
                safety++;

                // Radio aleatorio en [rmin, rmax]
                float r = UnityEngine.Random.Range(rmin, rmax);
                // Ángulo aleatorio en el arco definido
                float angle = arcStart + UnityEngine.Random.Range(0f, 1f) * arcSpan;

                float x = center.x + r * Mathf.Sin(angle); // X: seno (eje derecho X+)
                float z = center.y + r * Mathf.Cos(angle); // Z: coseno (eje adelante Z+)

                var pt = new Vector2(x, z);
                if (!IsPointValidForSpawn(pt))
                    continue;

                pts.Add(pt);
            }

            return pts;
        }

        List<Vector2> BuildDistributedOceanPoints(int n, Bounds oceanBounds, Vector2 islandCenter, float islandRadius)
        {
            float minX = oceanBounds.min.x;
            float maxX = oceanBounds.max.x;
            float minZ = oceanBounds.min.z;
            float maxZ = oceanBounds.max.z;

            float sideInset = Mathf.Clamp(oceanSideInset, 0f, Mathf.Max(0f, (maxX - minX) * 0.49f));
            float frontBackInset = Mathf.Clamp(oceanFrontBackInset, 0f, Mathf.Max(0f, (maxZ - minZ) * 0.49f));
            minX += sideInset;
            maxX -= sideInset;
            minZ += frontBackInset;
            maxZ -= frontBackInset;

            if (onlyBackOfIsland)
                maxZ = Mathf.Min(maxZ, islandCenter.y - backEndOffset);

            // Modo solicitado: acomodar los vectores en un rectangulo compacto solo atras del mapa.
            if (useBackPackedRectangle)
            {
                float padX = Mathf.Clamp(packedRectHorizontalPadding, 0f, Mathf.Max(0f, (maxX - minX) * 0.49f));
                float padBack = Mathf.Clamp(packedRectBackPadding, 0f, Mathf.Max(0f, (maxZ - minZ) * 0.49f));
                float padFront = Mathf.Clamp(packedRectFrontPadding, 0f, Mathf.Max(0f, (maxZ - minZ) * 0.49f));

                minX += padX;
                maxX -= padX;
                minZ += padBack;
                maxZ -= padFront;

                ApplyBackSectorSlice(ref minX, ref maxX);

                if (squareBackSectorZones && useBackSectors && backSectors > 1)
                {
                    float sectorWidth = maxX - minX;
                    if (sectorWidth > 0.001f)
                    {
                        float squareMaxZ = minZ + sectorWidth;
                        maxZ = Mathf.Min(maxZ, squareMaxZ);
                    }
                }

                if (minX >= maxX || minZ >= maxZ)
                    return new List<Vector2>(0);

                if (useDensePacking)
                    return BuildDensePackedRectanglePoints(n, minX, maxX, minZ, maxZ);

                return BuildPackedRectanglePoints(n, minX, maxX, minZ, maxZ);
            }

            ApplyBackSectorSlice(ref minX, ref maxX);

            if (minX >= maxX || minZ >= maxZ)
                return new List<Vector2>(0);

            float width = Mathf.Max(1f, maxX - minX);
            float depth = Mathf.Max(1f, maxZ - minZ);
            float aspect = width / depth;

            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * aspect)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / cols));

            float stepX = width / cols;
            float stepZ = depth / rows;
            float jitter = Mathf.Clamp(spawnJitterFactor, 0f, 0.45f);
            float jitterX = stepX * jitter;
            float jitterZ = stepZ * jitter;

            var pts = new List<Vector2>(n);
            for (int r = 0; r < rows && pts.Count < n; r++)
            {
                for (int c = 0; c < cols && pts.Count < n; c++)
                {
                    float baseX = minX + (c + 0.5f) * stepX;
                    float baseZ = minZ + (r + 0.5f) * stepZ;

                    float x = baseX + UnityEngine.Random.Range(-jitterX, jitterX);
                    float z = baseZ + UnityEngine.Random.Range(-jitterZ, jitterZ);
                    var pt = new Vector2(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(z, minZ, maxZ));

                    if (islandRadius > 0f && Vector2.Distance(pt, islandCenter) < islandRadius)
                        continue;
                    if (onlyBackOfIsland && pt.y > (islandCenter.y - backEndOffset))
                        continue;
                    if (avoidUnderIslandWater && IsPointUnderIsland(pt))
                        continue;
                    if (!IsPointValidForSpawn(pt))
                        continue;

                    pts.Add(pt);
                }
            }

            int attempts = 0;
            int maxAttempts = Mathf.Max(6000, n * 30);
            while (pts.Count < n && attempts < maxAttempts)
            {
                attempts++;
                float x = UnityEngine.Random.Range(minX, maxX);
                float z = UnityEngine.Random.Range(minZ, maxZ);
                var pt = new Vector2(x, z);

                if (islandRadius > 0f && Vector2.Distance(pt, islandCenter) < islandRadius)
                    continue;
                if (onlyBackOfIsland && pt.y > (islandCenter.y - backEndOffset))
                    continue;
                if (avoidUnderIslandWater && IsPointUnderIsland(pt))
                    continue;
                if (!IsPointValidForSpawn(pt))
                    continue;

                pts.Add(pt);
            }

            return pts;
        }

        List<Vector2> BuildDensePackedRectanglePoints(int n, float minX, float maxX, float minZ, float maxZ)
        {
            if (n <= 0)
                return new List<Vector2>(0);

            float availableW = Mathf.Max(0.0001f, maxX - minX);
            float availableD = Mathf.Max(0.0001f, maxZ - minZ);

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / cols));

            float cell = Mathf.Max(0.05f, desiredCellSize);
            float reqW = cols * cell;
            float reqD = rows * cell;

            float usedW = Mathf.Min(availableW, reqW);
            float usedD = Mathf.Min(availableD, reqD);

            float centerX = (minX + maxX) * 0.5f;
            float denseMinX = Mathf.Clamp(centerX - usedW * 0.5f, minX, maxX - usedW);
            float denseMaxX = denseMinX + usedW;

            // Empaquetado denso centrado en profundidad: mantiene el campo "en el centro" del rectángulo atrás.
            float centerZ = (minZ + maxZ) * 0.5f;
            float denseMinZ = Mathf.Clamp(centerZ - usedD * 0.5f, minZ, maxZ - usedD);
            float denseMaxZ = denseMinZ + usedD;

            if (denseMinX >= denseMaxX || denseMinZ >= denseMaxZ)
                return new List<Vector2>(0);

            return BuildPackedRectanglePoints(n, denseMinX, denseMaxX, denseMinZ, denseMaxZ);
        }

        void ApplyBackSectorSlice(ref float minX, ref float maxX)
        {
            if (!useBackSectors)
                return;

            int sectors = Mathf.Max(1, backSectors);
            if (sectors <= 1)
            {
                backSectorIndex = 0;
                return;
            }

            float width = maxX - minX;
            if (width <= 0.001f)
                return;

            int idx = Mathf.Clamp(backSectorIndex, 0, sectors - 1);
            float sectorWidth = width / sectors;

            float sMinX = minX + sectorWidth * idx;
            float sMaxX = sMinX + sectorWidth;

            // Evitar degenerados por acumulacion de float.
            if (sMaxX <= sMinX + 0.001f)
                return;

            minX = sMinX;
            maxX = sMaxX;
        }

        List<Vector2> BuildPackedRectanglePoints(int n, float minX, float maxX, float minZ, float maxZ)
        {
            float width = Mathf.Max(0.0001f, maxX - minX);
            float depth = Mathf.Max(0.0001f, maxZ - minZ);
            float aspect = width / depth;

            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * aspect)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / cols));

            float stepX = width / cols;
            float stepZ = depth / rows;

            var pts = new List<Vector2>(n);
            for (int r = 0; r < rows && pts.Count < n; r++)
            {
                float z = minZ + (r + 0.5f) * stepZ;
                for (int c = 0; c < cols && pts.Count < n; c++)
                {
                    float x = minX + (c + 0.5f) * stepX;
                    pts.Add(new Vector2(x, z));
                }
            }

            return pts;
        }

        bool IsPointValidForSpawn(Vector2 p)
        {
            if (!excludeSolidObjectsOverWater)
                return true;

            var origin = new Vector3(p.x, waterSurfaceY + 200f, p.y);
            var hits = Physics.RaycastAll(origin, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return true;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                var go = h.collider != null ? h.collider.gameObject : null;
                if (go == null)
                    continue;

                string n = go.name.ToLowerInvariant();
                if (IsWaterLikeObjectName(n))
                    return true;

                if (allowUnderBoats && IsBoatLikeObjectName(n))
                    continue;

                return false;
            }

            return true;
        }

        bool IsPointUnderIsland(Vector2 p)
        {
            // Filtro estable: exclusion radial ampliada alrededor de la isla.
            float exclusionRadius = Mathf.Max(0f, islandRadius + islandExclusionPadding);
            if (exclusionRadius > 0.01f && Vector2.Distance(p, islandCenter) < exclusionRadius)
                return true;

            if (!usePhysicsLandFilter)
                return false;

            const float rayStartY = 500f;
            const float rayDistance = 1200f;

            var origin = new Vector3(p.x, rayStartY, p.y);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
                return false;

            return hit.point.y > (waterSurfaceY + landHeightThreshold);
        }

        void RefreshIslandBoundsFromScene()
        {
            _hasIslandBounds = false;
            if (!autoDetectIslandFromScene)
                return;

            if (!TryDetectIslandBounds(out Vector2 detectedCenter, out float detectedRadius, out Bounds detectedBounds))
                return;

            islandCenter = detectedCenter;
            islandRadius = detectedRadius;
            _islandBounds = detectedBounds;
            _hasIslandBounds = true;
        }

        bool TryDetectIslandBounds(out Vector2 center, out float radius, out Bounds bounds)
        {
            center = islandCenter;
            radius = islandRadius;
            bounds = default;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
                return false;

            bool found = false;
            Bounds localBounds = new Bounds();

            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                var candidates = root.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < candidates.Length; j++)
                {
                    var tr = candidates[j];
                    if (tr == null)
                        continue;

                    if (!tr.name.Contains(islandRootNameHint, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var renderers = tr.GetComponentsInChildren<Renderer>(true);
                    for (int k = 0; k < renderers.Length; k++)
                    {
                        var r = renderers[k];
                        if (r == null)
                            continue;

                        if (IsWaterLikeObjectName(r.gameObject.name) || r.bounds.max.y <= (waterSurfaceY + landHeightThreshold))
                            continue;

                        if (!found)
                        {
                            localBounds = r.bounds;
                            found = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(r.bounds);
                        }
                    }

                    var colliders = tr.GetComponentsInChildren<Collider>(true);
                    for (int k = 0; k < colliders.Length; k++)
                    {
                        var c = colliders[k];
                        if (c == null)
                            continue;

                        if (IsWaterLikeObjectName(c.gameObject.name) || c.bounds.max.y <= (waterSurfaceY + landHeightThreshold))
                            continue;

                        if (!found)
                        {
                            localBounds = c.bounds;
                            found = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(c.bounds);
                        }
                    }
                }
            }

            if (!found)
                return false;

            bounds = localBounds;
            center = new Vector2(localBounds.center.x, localBounds.center.z);
            radius = Mathf.Max(localBounds.extents.x, localBounds.extents.z);
            return radius > 0.01f;
        }

        static bool IsWaterLikeObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            string n = objectName.ToLowerInvariant();
            return n.Contains("ocean") || n.Contains("water") || n.Contains("river") || n.Contains("sea");
        }

        static bool IsBoatLikeObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            string n = objectName.ToLowerInvariant();
            return n.Contains("boat") || n.Contains("ship");
        }

        public Vector2 EvaluateFormula(Vector2 p, Vector2 localOrigin)
        {
            // Las formulas se calculan respecto al centro de la zona activa,
            // para que cada zona se comporte de forma coherente.
            // Siempre usar la bolita como centro real
            Vector2 center = localOrigin;
            Vector2 local = p - center;
            float x = local.x, y = local.y;
            float r2 = x * x + y * y;
            Vector2 result;

            if (useFunctionInputs && _compiledP != null && _compiledQ != null)
            {
                float px = _compiledP.Evaluate(x, y);
                float qy = _compiledQ.Evaluate(x, y);
                result = new Vector2(px * scaleX, qy * scaleY);
                return result;
            }

            switch (formula)
            {
                case FieldFormula.RadialOutward:
                    // Apunta hacia afuera desde la bolita
                    result = local;
                    break;
                case FieldFormula.RadialInward:
                    // Apunta hacia la bolita (centro)
                    result = -local;
                    break;
                case FieldFormula.RotationXY:
                    result = new Vector2(-y, x); break;
                case FieldFormula.Gravitational:
                    result = r2 < 0.01f ? Vector2.zero : (-local) / r2; break;
                case FieldFormula.Saddle:
                    result = new Vector2(x, -y); break;
                case FieldFormula.Constant:
                    result = new Vector2(1f, 0f); break;
                case FieldFormula.Whirlpool:
                    result = r2 < 0.01f ? Vector2.zero : new Vector2(-y, x) / r2; break;
                case FieldFormula.Spiral:
                    result = new Vector2(x - y, x + y); break;
                case FieldFormula.TargetPoint:
                    Vector2 diff = center - p;
                    result = diff.magnitude > 0.001f ? diff.normalized : Vector2.zero; break;
                default:
                    result = local;
                    break;
            }

            return new Vector2(result.x * scaleX, result.y * scaleY);
        }

        public Vector2 GetEvalOrigin()
        {
        return GlobalEvalOrigin;
        }
        void PlaceArrow(Vector3 worldPos, Vector2 dir2D, int totalCount)
        {
            if (dir2D.sqrMagnitude < 1e-6f) { PlaceDot(worldPos); return; }

            float mag     = dir2D.magnitude;
            // ── Escala por cantidad (pedido)
            // - 100  -> grande
            // - 1000 -> se achica progresivo (absoluteMinArrowScale)
            // - 2000 -> aumentado para que sea visible al expandirse en el océano
            int countClamped = Mathf.Clamp(totalCount, 100, 2000);
            float scaleAt1000 = Mathf.Max(0.0001f, absoluteMinArrowScale);
            // Para 2000 vectores expandidos: usar factor mayor para que sean más visibles
            float expandFactor = Mathf.Clamp(backSectorShrinkAt2000, 0.01f, 0.5f);
            float scaleAt2000 = Mathf.Max(0.0001f, scaleAt1000 * expandFactor * 1.8f); // Aumentado 1.8x
            float scale;
            if (countClamped <= 1000)
            {
                float t = Mathf.InverseLerp(100f, 1000f, countClamped);
                scale = Mathf.Lerp(absoluteMaxArrowScale, scaleAt1000, t);
            }
            else
            {
                float t = Mathf.InverseLerp(1000f, 2000f, countClamped);
                scale = Mathf.Lerp(scaleAt1000, scaleAt2000, t);
            }
            // Ajuste leve por magnitud del vector (vectores muy cortos se achican un poco)
            if (mag < 0.01f) scale *= 0.5f;
            float minAllowed = Mathf.Min(scaleAt2000, absoluteMaxArrowScale);
            float maxAllowed = Mathf.Max(scaleAt1000, absoluteMaxArrowScale);
            scale = Mathf.Clamp(scale, minAllowed, maxAllowed);

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
            Transform parent = GetArrowsParent();
            if (arrowPrefab != null)
                arrow = Instantiate(arrowPrefab, worldPos, rot, parent);
            else
                arrow = CreateSimpleArrow(worldPos, rot, parent);

            if (forceFieldColor)
                ApplyFieldColor(arrow, forcedFieldColor);

            float scaleMult = Mathf.Max(0.0001f, arrowScale);
            if (arrowPrefab == null)
                scaleMult *= Mathf.Max(1f, simpleArrowScaleMultiplier);

            // Longitud proporcional a la magnitud (estilo Stewart), con clamps.
            float lengthFactor = 1f;
            if (useVectorMagnitudeForLength)
            {
                float raw = mag * Mathf.Max(0.0001f, magnitudeToLength);
                lengthFactor = Mathf.Clamp(raw, Mathf.Max(0.05f, minLengthFactor), Mathf.Max(minLengthFactor, maxLengthFactor));
            }

            Vector3 s = Vector3.one * (scale * scaleMult);
            // El eje longitudinal depende del prefab: FlechaApp3 "vive" en Y; el prefab estándar/simple, en Z.
            if (_prefabPointsUpY)
                s.y *= lengthFactor;
            else
                s.z *= lengthFactor;

            arrow.transform.localScale = s;

            Vector3 moveDir3D = new Vector3(dir2D.x, 0f, dir2D.y).normalized;
            _entries.Add(new ArrowEntry
            {
                zoneKey = _generationZoneKey,
                arrow = arrow,
                anim = new ArrowAnimData
                {
                    basePos = worldPos,
                    moveDir = moveDir3D,
                    offset = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
                }
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

        static void ApplyFieldColor(GameObject arrow, Color color)
        {
            if (arrow == null)
                return;

            var renderers = arrow.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            var block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }

        GameObject CreateSimpleArrow(Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = new GameObject("Flecha");
            go.transform.SetParent(parent);
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
            var bc = body.GetComponent<Collider>(); if (bc) Destroy(bc);
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
            Transform parent = GetArrowsParent();
            dot.transform.SetParent(parent, true);
            dot.transform.position   = pos;

            float dotScale = 0.08f;
            if (arrowPrefab == null)
                dotScale *= Mathf.Max(1f, simpleArrowScaleMultiplier);
            dot.transform.localScale = Vector3.one * dotScale;
            var rend = dot.GetComponent<Renderer>();
            if (rend) rend.material = new Material(Shader.Find("Unlit/Color")) { color = Color.yellow };
            _entries.Add(new ArrowEntry
            {
                zoneKey = _generationZoneKey,
                arrow = dot,
                anim = new ArrowAnimData { basePos = pos, moveDir = Vector3.zero, offset = 0 }
            });
        }

        void ClearZone(string zoneKey)
        {
            if (string.IsNullOrEmpty(zoneKey))
                return;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].zoneKey != zoneKey)
                    continue;

                if (_entries[i].arrow != null)
                    Destroy(_entries[i].arrow);

                _entries.RemoveAt(i);
            }
        }

        void ClearArrows()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].arrow != null) Destroy(_entries[i].arrow);
            _entries.Clear();
            // Limpiar cache de materiales/mesh estaticos al salir de Play
            _matBody  = null;
            _matTip   = null;
            _coneMesh = null;
        }

        void AnimateArrows()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.arrow == null) continue;
                if (e.anim.moveDir.sqrMagnitude < 0.001f) continue;
                float t = Mathf.Sin(Time.time * animSpeed + e.anim.offset) * animAmplitude;
                e.arrow.transform.position = e.anim.basePos + e.anim.moveDir * t;
            }
        }
    }
}
