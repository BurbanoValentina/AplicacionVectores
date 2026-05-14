using System.Collections.Generic;
using UnityEngine;

namespace VectorField
{
    public enum DuckFieldMode
    {
        Outward,
        Inward,
        Rotational,
    }

    public class DuckFieldSpawner : MonoBehaviour
    {
        public VectorFieldManager fieldManager;
        public Transform fieldCenterOverride;
        public GameObject duckPrefab;
        public int minDuckCount = 5;
        public int maxDuckCount = 6;
        public float nearRadius = 2f;
        public float midRadius = 5f;
        public float farRadius = 10f;
        public float moveSpeed = 2f;
        public float rotateSpeed = 360f;
        public float sampleOffset = 1.2f;

        [Header("Escala")]
        [Min(0.01f)] public float movingDuckScale = 0.05f;
        [Min(0.01f)] public float staticAnimalScale = 0.05f;
        public bool moveOnlyLoveduck = true;

        [Header("Animales estaticos")]
        public bool spawnStaticAnimals = true;
        [Min(0)] public int staticAnimalsPerType = 1;
        [Range(0.05f, 1f)] public float staticSpawnRadiusMultiplier = 0.6f;
        public float staticSpawnHeightOffset = 0.02f;
        public float staticRaycastHeight = 200f;
        public LayerMask staticGroundMask = ~0;
        public List<GameObject> staticAnimalPrefabs = new List<GameObject>();
        public bool disableAnimatorOnStaticAnimals = true;

        readonly List<DuckFieldMover> _ducks = new List<DuckFieldMover>();
        readonly List<GameObject> _staticAnimals = new List<GameObject>();

        void Awake()
        {
            ResolveReferences();
        }

        void Start()
        {
            if (!Application.isPlaying)
                return;

            ResolveReferences();
            if (spawnStaticAnimals && _staticAnimals.Count == 0)
                SpawnStaticAnimals();
        }

        void OnValidate()
        {
            if (minDuckCount < 1) minDuckCount = 1;
            if (maxDuckCount < minDuckCount) maxDuckCount = minDuckCount;
            if (staticAnimalsPerType < 0) staticAnimalsPerType = 0;
            if (movingDuckScale < 0.01f) movingDuckScale = 0.01f;
            if (staticAnimalScale < 0.01f) staticAnimalScale = 0.01f;

#if UNITY_EDITOR
            TryAssignDuckPrefabFromEditor();
            TryAssignStaticAnimalsFromEditor();
#endif
        }

        public void GenerateDucks()
        {
            ResolveReferences();

            if (duckPrefab == null)
            {
                Debug.LogError("[DuckSpawner] duckPrefab no asignado.", this);
                return;
            }

            ClearDucks();

            Vector3 center = GetCenter();
            DuckFieldMode mode = DetectMode(center);

            int count = Random.Range(minDuckCount, maxDuckCount + 1);
            float radius = GetRadiusForMode(mode);
            float y = center.y;

            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f) * (i / Mathf.Max(1f, count));
                Vector3 pos = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    y,
                    center.z + Mathf.Sin(angle) * radius
                );

                var go = Instantiate(duckPrefab, pos, Quaternion.identity);
                bool shouldMove = ShouldMoveDuck(go);
                if (shouldMove)
                {
                    var mover = go.GetComponent<DuckFieldMover>();
                    if (mover == null)
                        mover = go.AddComponent<DuckFieldMover>();

                    mover.Initialize(fieldManager, center, mode, moveSpeed, rotateSpeed, y);
                    ApplyScale(go.transform, movingDuckScale);
                    _ducks.Add(mover);
                }
                else
                {
                    MakeStatic(go);
                    ApplyScale(go.transform, staticAnimalScale);
                    _staticAnimals.Add(go);
                }
            }

        }

        void ClearDucks()
        {
            for (int i = 0; i < _ducks.Count; i++)
            {
                if (_ducks[i] != null)
                    Destroy(_ducks[i].gameObject);
            }
            _ducks.Clear();
        }

        void ClearStaticAnimals()
        {
            for (int i = 0; i < _staticAnimals.Count; i++)
            {
                if (_staticAnimals[i] != null)
                    Destroy(_staticAnimals[i]);
            }
            _staticAnimals.Clear();
        }

        void ResolveReferences()
        {
            if (fieldManager == null)
                fieldManager = FindFirstObjectByType<VectorFieldManager>();

#if UNITY_EDITOR
            if (duckPrefab == null)
                TryAssignDuckPrefabFromEditor();
            TryAssignStaticAnimalsFromEditor();
#endif
        }

        void SpawnStaticAnimals()
        {
            if (staticAnimalPrefabs == null || staticAnimalPrefabs.Count == 0)
                return;
            if (staticAnimalsPerType <= 0)
                return;

            Vector2 islandCenter = fieldManager != null ? fieldManager.islandCenter : new Vector2(transform.position.x, transform.position.z);
            float baseY = fieldManager != null ? fieldManager.waterSurfaceY : transform.position.y;
            float spawnMinX = islandCenter.x - midRadius;
            float spawnMaxX = islandCenter.x + midRadius;
            float spawnMinZ = islandCenter.y - midRadius;
            float spawnMaxZ = islandCenter.y + midRadius;
            float rayStartY = baseY + staticRaycastHeight;

            if (fieldManager != null && fieldManager.TryGetIslandBounds(out Bounds islandBounds))
            {
                float shrink = Mathf.Clamp(staticSpawnRadiusMultiplier, 0.05f, 1f);
                Vector3 center = islandBounds.center;
                Vector3 extents = islandBounds.extents * shrink;
                islandCenter = new Vector2(center.x, center.z);
                spawnMinX = center.x - extents.x;
                spawnMaxX = center.x + extents.x;
                spawnMinZ = center.z - extents.z;
                spawnMaxZ = center.z + extents.z;
                rayStartY = islandBounds.max.y + staticRaycastHeight;
            }

            for (int p = 0; p < staticAnimalPrefabs.Count; p++)
            {
                var prefab = staticAnimalPrefabs[p];
                if (prefab == null)
                    continue;
                if (duckPrefab != null && prefab == duckPrefab)
                    continue;

                for (int i = 0; i < staticAnimalsPerType; i++)
                {
                    float x = UnityEngine.Random.Range(spawnMinX, spawnMaxX);
                    float z = UnityEngine.Random.Range(spawnMinZ, spawnMaxZ);
                    Vector3 pos = new Vector3(x, rayStartY, z);
                    if (TryGetGroundY(pos, out float y))
                        pos.y = y + staticSpawnHeightOffset;
                    else
                        pos.y = baseY + staticSpawnHeightOffset;

                    var go = Instantiate(prefab, pos, Quaternion.identity);
                    MakeStatic(go);
                    ApplyScale(go.transform, staticAnimalScale);
                    _staticAnimals.Add(go);
                }
            }
        }

        bool TryGetGroundY(Vector3 rayOrigin, out float y)
        {
            float maxDist = Mathf.Max(1f, staticRaycastHeight * 2f);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDist, staticGroundMask, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }

            y = 0f;
            return false;
        }

        bool ShouldMoveDuck(GameObject go)
        {
            if (!moveOnlyLoveduck)
                return true;

            string name = go != null ? go.name : string.Empty;
            return name.IndexOf("LOVEDUCK", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void MakeStatic(GameObject go)
        {
            if (go == null)
                return;

            var mover = go.GetComponent<DuckFieldMover>();
            if (mover != null)
                Destroy(mover);

            if (disableAnimatorOnStaticAnimals)
            {
                var animator = go.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    animator.enabled = false;
            }

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        static void ApplyScale(Transform target, float scale)
        {
            if (target == null)
                return;

            target.localScale = target.localScale * scale;
        }

        Vector3 GetCenter()
        {
            if (fieldCenterOverride != null)
                return fieldCenterOverride.position;

            if (fieldManager != null)
                return fieldManager.GetFieldCenterWorld();

            return transform.position;
        }

        DuckFieldMode DetectMode(Vector3 center)
        {
            if (fieldManager == null)
                return DuckFieldMode.Outward;

            Vector3 sample = center + new Vector3(sampleOffset, 0f, 0f);
            Vector2 dir = fieldManager.GetFieldDirection(sample);

            if (dir.sqrMagnitude < 0.0001f)
            {
                sample = center + new Vector3(0f, 0f, sampleOffset);
                dir = fieldManager.GetFieldDirection(sample);
            }

            if (dir.sqrMagnitude < 0.0001f)
                return DuckFieldMode.Outward;

            Vector2 radial = new Vector2(sample.x - center.x, sample.z - center.z).normalized;
            Vector2 dirNorm = dir.normalized;
            float dot = Vector2.Dot(radial, dirNorm);

            if (Mathf.Abs(dot) < 0.35f)
                return DuckFieldMode.Rotational;

            return dot > 0f ? DuckFieldMode.Outward : DuckFieldMode.Inward;
        }

        float GetRadiusForMode(DuckFieldMode mode)
        {
            switch (mode)
            {
                case DuckFieldMode.Inward:
                    return Mathf.Max(nearRadius, farRadius);
                case DuckFieldMode.Rotational:
                    return midRadius;
                default:
                    return nearRadius;
            }
        }

#if UNITY_EDITOR
        void TryAssignDuckPrefabFromEditor()
        {
            if (duckPrefab != null)
                return;

            string[] guids = UnityEditor.AssetDatabase.FindAssets("LOVEDUCK t:Prefab");
            if (guids == null || guids.Length == 0)
                return;

            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                duckPrefab = prefab;
        }

        void TryAssignStaticAnimalsFromEditor()
        {
            if (staticAnimalPrefabs == null)
                staticAnimalPrefabs = new List<GameObject>();

            if (staticAnimalPrefabs.Count > 0)
                return;

            string[] names = new[] { "Cat", "sheep", "penguin", "FLOWER", "mole_attack" };
            for (int i = 0; i < names.Length; i++)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"{names[i]} t:Prefab");
                if (guids == null || guids.Length == 0)
                    continue;

                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && !staticAnimalPrefabs.Contains(prefab))
                    staticAnimalPrefabs.Add(prefab);
            }
        }
#endif
    }

    public class DuckFieldMover : MonoBehaviour
    {
        VectorFieldManager _fieldManager;
        Vector3 _center;
        DuckFieldMode _mode;
        float _speed;
        float _rotateSpeed;
        float _fixedY;

        public void Initialize(VectorFieldManager fieldManager, Vector3 center, DuckFieldMode mode, float speed, float rotateSpeed, float fixedY)
        {
            _fieldManager = fieldManager;
            _center = center;
            _mode = mode;
            _speed = speed;
            _rotateSpeed = rotateSpeed;
            _fixedY = fixedY;
        }

        void Update()
        {
            if (_fieldManager == null)
                return;

            Vector2 dir2 = _fieldManager.GetFieldDirection(transform.position);
            Vector3 dir = new Vector3(dir2.x, 0f, dir2.y);

            if (_mode == DuckFieldMode.Rotational && dir.sqrMagnitude < 0.0001f)
            {
                Vector3 radial = transform.position - _center;
                radial.y = 0f;
                dir = new Vector3(-radial.z, 0f, radial.x);
            }

            if (dir.sqrMagnitude < 0.0001f)
                return;

            dir.Normalize();
            Vector3 nextPos = transform.position + dir * _speed * Time.deltaTime;

            if (_fieldManager.TryGetOceanBounds(out Bounds bounds))
            {
                float clampedX = Mathf.Clamp(nextPos.x, bounds.min.x, bounds.max.x);
                float clampedZ = Mathf.Clamp(nextPos.z, bounds.min.z, bounds.max.z);
                bool hitEdge = clampedX != nextPos.x || clampedZ != nextPos.z;
                nextPos = new Vector3(clampedX, _fixedY, clampedZ);
                transform.position = nextPos;

                if (hitEdge)
                    return;
            }
            else
            {
                transform.position = new Vector3(nextPos.x, _fixedY, nextPos.z);
            }

            if (_rotateSpeed > 0f)
            {
                Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _rotateSpeed * Time.deltaTime);
            }
        }
    }
}
