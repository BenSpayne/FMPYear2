using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Parallax scrolling background engine with tiling ground, train tracks, and multi-layer mountains.
/// 
/// KEY DESIGN:
///   - Each mountain layer owns a "spawn frontier" (nextSpawnX). Every frame that frontier
///     moves left at the same speed as the layer. When it falls inside the spawn window
///     (camRight + spawnAheadOfCamera) we plant a mountain and push the frontier right by
///     a random spacing. This replaces the fragile fixedMountainCount approach and can
///     never produce burst-spawns or gaps.
///   - Ground and Tracks use the same pattern: track the rightmost tile, extend while its right edge
///     is inside camRight + spawnBuffer. No maxPieces cap to cause cut-offs.
///   - Pooling is built-in (per-layer, per-prefab queues) so no external pool manager is needed.
/// 
/// SETUP:
///   1. Assign groundPrefab and trackPrefab (any GameObject with a SpriteRenderer or Collider for width measurement).
///   2. Add one MountainLayerConfig per depth layer, assign prefabs arrays.
///   3. Tune parallaxSpeed, spacing, yPosition and tint per layer.
///   Nothing else is required.
/// </summary>
public class RollingBackgroundEngine : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("Scroll")]
    [Tooltip("World units per second at 1× multiplier")]
    [SerializeField] private float scrollSpeed = 5f;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private bool isScrolling = true;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    // ── Ground ───────────────────────────────────────────────────────────────────

    [Header("Ground")]
    [Tooltip("Prefab to tile. Must have a SpriteRenderer (or Collider) so width can be read.")]
    [SerializeField] private GameObject groundPrefab;
    [SerializeField] private float groundY = -4f;
    [SerializeField] private float groundZ = 0f;
    [Tooltip("Rotation for ground tiles (Euler angles in degrees).")]
    [SerializeField] private Vector3 groundRotation = Vector3.zero;
    [Tooltip("Spawn tiles this many units ahead of the camera's right edge.")]
    [SerializeField] private float groundSpawnBuffer = 2f;
    [Tooltip("Despawn tiles this many units behind the camera's left edge.")]
    [SerializeField] private float groundDespawnBuffer = 4f;
    [Tooltip("Override tile width (world units). Set this when using a procedurally generated " +
             "ground mesh (e.g. ProceduralGroundGenerator) whose mesh doesn't exist on the prefab asset. " +
             "Leave at 0 to auto-detect from the prefab's SpriteRenderer or MeshFilter.")]
    [SerializeField] private float groundTileWidthOverride = 0f;

    // ── Train Tracks ─────────────────────────────────────────────────────────────

    [Header("Train Tracks")]
    [Tooltip("Enable/disable the train track spawning system.")]
    [SerializeField] private bool enableTracks = true;
    [Tooltip("Prefab to tile for train tracks. Must have a SpriteRenderer (or Collider) so width can be read.")]
    [SerializeField] private GameObject trackPrefab;
    [SerializeField] private float trackY = -3f;
    [SerializeField] private float trackZ = 0.1f;
    [Tooltip("Rotation for track tiles (Euler angles in degrees).")]
    [SerializeField] private Vector3 trackRotation = Vector3.zero;
    [Tooltip("Spawn track tiles this many units ahead of the camera's right edge.")]
    [SerializeField] private float trackSpawnBuffer = 2f;
    [Tooltip("Despawn track tiles this many units behind the camera's left edge.")]
    [SerializeField] private float trackDespawnBuffer = 4f;
    [Tooltip("Override track tile width (world units). Leave at 0 to auto-detect.")]
    [SerializeField] private float trackTileWidthOverride = 0f;
    [Tooltip("Speed multiplier for tracks relative to ground. 1 = same speed, 0.9 = slightly slower (parallax).")]
    [Range(0f, 2f)]
    [SerializeField] private float trackSpeedMultiplier = 1f;

    // ── Mountain layers ───────────────────────────────────────────────────────────

    [Header("Mountain Layers")]
    [SerializeField] private MountainLayerConfig[] layers = new MountainLayerConfig[]
    {
        new MountainLayerConfig
        {
            name                = "Far Mountains",
            parallaxSpeed       = 0.3f,
            yPosition           = 0f,
            zPosition           = 10f,
            zVariation          = 1f,
            spawnAheadOfCamera  = 10f,
            despawnBehindCamera = 15f,
            useDensitySlider    = false,
            minSpacing          = 8f,
            maxSpacing          = 15f,
            scaleMultiplier     = Vector3.one,
            randomScale         = true,
            minRandomScale      = 1.2f,
            maxRandomScale      = 1.8f,
            applyTint           = false,
            tint                = new Color(0.6f, 0.65f, 0.7f),
            sortingOrder        = -20,
            minUniqueBeforeRepeat = 3
        },
        new MountainLayerConfig
        {
            name                = "Mid Mountains",
            parallaxSpeed       = 0.6f,
            yPosition           = 0f,
            zPosition           = 5f,
            zVariation          = 0.5f,
            spawnAheadOfCamera  = 6f,
            despawnBehindCamera = 10f,
            useDensitySlider    = false,
            minSpacing          = 5f,
            maxSpacing          = 10f,
            scaleMultiplier     = Vector3.one,
            randomScale         = true,
            minRandomScale      = 0.9f,
            maxRandomScale      = 1.3f,
            applyTint           = false,
            tint                = new Color(0.45f, 0.55f, 0.45f),
            sortingOrder        = -10,
            minUniqueBeforeRepeat = 2
        },
        new MountainLayerConfig
        {
            name                = "Near Mountains",
            parallaxSpeed       = 0.9f,
            yPosition           = 0f,
            zPosition           = 0f,
            zVariation          = 0.2f,
            spawnAheadOfCamera  = 4f,
            despawnBehindCamera = 7f,
            useDensitySlider    = false,
            minSpacing          = 3f,
            maxSpacing          = 7f,
            scaleMultiplier     = Vector3.one,
            randomScale         = true,
            minRandomScale      = 0.8f,
            maxRandomScale      = 1.1f,
            applyTint           = false,
            tint                = new Color(0.3f, 0.4f, 0.3f),
            sortingOrder        = 0,
            minUniqueBeforeRepeat = 2
        }
    };

    // ─────────────────────────────────────────────────────────────────────────────
    //  NESTED TYPES
    // ─────────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class MountainLayerConfig
    {
        public string name = "Layer";

        [Tooltip("Prefab variants for this layer. At least one required.")]
        public GameObject[] prefabs;

        [Tooltip("Per-prefab Y offset added to the layer's base yPosition.\n" +
                 "Use this when prefabs have different heights and some clip through the ground.\n" +
                 "Index matches the prefabs array above. Leave shorter than prefabs array to use 0 for the rest.\n" +
                 "Positive = raise the prefab, Negative = lower it.")]
        public float[] prefabYOffsets;

        [Tooltip("Per-prefab Euler rotation. Index matches the prefabs array above.\n" +
                 "Leave shorter than prefabs array (or empty) to use (0,0,0) for unspecified entries.\n" +
                 "Example: set Y=180 on a prefab that faces the wrong way.")]
        public Vector3[] prefabRotations;

        [Header("Movement")]
        [Range(0f, 1f)]
        [Tooltip("0 = stationary (sky), 1 = same speed as ground")]
        public float parallaxSpeed = 0.5f;

        [Header("Spawn Position")]
        [Tooltip("Base Y position for this layer. Individual prefabs can offset from this with prefabYOffsets.")]
        public float yPosition = 0f;
        public float zPosition = 0f;
        [Range(0f, 3f)] public float zVariation = 0f;

        [Header("Spawn Window")]
        [Tooltip("Spawn mountains this far ahead of the camera's right edge.")]
        public float spawnAheadOfCamera = 6f;
        [Tooltip("Remove mountains this far behind the camera's left edge.")]
        public float despawnBehindCamera = 8f;

        [Header("Spacing / Density")]
        [Tooltip("Enable to use the density slider for automatic spacing calculation.\n" +
                 "Disable to use the manual Min/Max Spacing fields below directly.")]
        public bool useDensitySlider = false;

        [Tooltip("How dense should this layer be?\n" +
                 "0.0 = extremely sparse (few objects, far apart)\n" +
                 "0.5 = normal/balanced\n" +
                 "1.0 = extremely dense (many objects, packed close)\n\n" +
                 "Only used when 'Use Density Slider' is enabled.")]
        [Range(0f, 1f)]
        public float spawnDensity = 0.5f;

        [Tooltip("Minimum spacing between objects (world units).")]
        public float minSpacing = 5f;
        [Tooltip("Maximum spacing between objects (world units).")]
        public float maxSpacing = 10f;

        [Header("Scale")]
        [Tooltip("Multiplies each axis of the prefab's own localScale.\n" +
                 "e.g. prefab is (1,1,1) and you set (3,3,3) -> spawned scale is (3,3,3).\n" +
                 "e.g. prefab is (2,1,2) and you set (3,3,3) -> spawned scale is (6,3,6).")]
        public Vector3 scaleMultiplier = Vector3.one;
        [Tooltip("Apply a random uniform multiplier on top of scaleMultiplier each spawn.")]
        public bool randomScale = true;
        [Tooltip("Minimum random multiplier (applied after scaleMultiplier).")]
        public float minRandomScale = 0.8f;
        [Tooltip("Maximum random multiplier (applied after scaleMultiplier).")]
        public float maxRandomScale = 1.5f;

        [Header("Visual")]
        [Tooltip("Enable this to apply tint color and sorting order. Disable to keep original materials.")]
        public bool applyTint = false;
        public Color tint = Color.white;
        public int sortingOrder = 0;

        [Header("Anti-Repeat")]
        [Min(1)]
        [Tooltip("How many different prefabs must appear before the same one can repeat.")]
        public int minUniqueBeforeRepeat = 2;

        // ── Internal spacing calculation ──────────────────────────────────────
        [HideInInspector] public float calculatedMinSpacing;
        [HideInInspector] public float calculatedMaxSpacing;
        [HideInInspector] public bool wasUsingDensitySlider;

        /// <summary>
        /// Returns the effective min spacing based on the current mode.
        /// If useDensitySlider is enabled, returns the auto-calculated value.
        /// If disabled, returns the manual minSpacing value.
        /// </summary>
        public float GetMinSpacing()
        {
            if (useDensitySlider)
                return calculatedMinSpacing;
            else
                return minSpacing;
        }

        /// <summary>
        /// Returns the effective max spacing based on the current mode.
        /// If useDensitySlider is enabled, returns the auto-calculated value.
        /// If disabled, returns the manual maxSpacing value.
        /// </summary>
        public float GetMaxSpacing()
        {
            if (useDensitySlider)
                return calculatedMaxSpacing;
            else
                return maxSpacing;
        }
    }

    private struct ActiveMountain
    {
        public GameObject obj;
        public int prefabIndex;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────────

    // Camera bounds (world space, updated each frame)
    private float camLeft, camRight, camHalfW, camHalfH;

    // Ground
    private Queue<GameObject>       groundPool   = new Queue<GameObject>();
    private List<GameObject>        activeGround = new List<GameObject>();
    private float                   groundTileWidth;

    // Train Tracks
    private Queue<GameObject>       trackPool   = new Queue<GameObject>();
    private List<GameObject>        activeTracks = new List<GameObject>();
    private float                   trackTileWidth;

    // Mountains — arrays indexed by layer
    private Queue<GameObject>[][]   mountainPools;     // [layer][prefabIndex]
    private List<ActiveMountain>[]  activeMountains;   // [layer]
    private float[]                 nextSpawnX;        // per-layer spawn frontier (world X)
    private List<int>[]             recentPrefabs;     // per-layer history for anti-repeat

    // ─────────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        CalculateAllLayerSpacings();

        RefreshCameraBounds();
        InitialiseGround();
        InitialiseTracks();
        InitialiseMountains();
    }

    void Update()
    {
        if (!isScrolling) return;

        RefreshCameraBounds();

        float dt    = Time.deltaTime;
        float speed = scrollSpeed * speedMultiplier * dt;

        TickGround(speed);
        TickTracks(speed);
        TickMountains(speed);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  CAMERA
    // ─────────────────────────────────────────────────────────────────────────────

    void RefreshCameraBounds()
    {
        camHalfH = mainCamera.orthographicSize;
        camHalfW = camHalfH * mainCamera.aspect;
        float cx = mainCamera.transform.position.x;
        camLeft  = cx - camHalfW;
        camRight = cx + camHalfW;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  GROUND — INITIALISE
    // ─────────────────────────────────────────────────────────────────────────────

    void InitialiseGround()
    {
        if (groundPrefab == null)
        {
            Debug.LogError("[RollingBG] groundPrefab is not assigned.", this);
            return;
        }

        groundTileWidth = groundTileWidthOverride > 0f
            ? groundTileWidthOverride
            : MeasurePrefabWidth(groundPrefab);

        if (groundTileWidth <= 0f)
        {
            Debug.LogError(
                "[RollingBG] Cannot measure ground tile width. " +
                "For 3D procedural ground (e.g. ProceduralGroundGenerator) the mesh is built at runtime " +
                "so it cannot be read from the prefab asset — set 'Ground Tile Width Override' manually.", this);
            groundTileWidth = 10f;
        }

        float startX = Mathf.Floor((camLeft - groundDespawnBuffer) / groundTileWidth) * groundTileWidth;
        float endX   = camRight + groundSpawnBuffer + groundTileWidth;

        for (float x = startX; x < endX; x += groundTileWidth)
            SpawnGroundTileAt(x);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  GROUND — TICK (move → spawn → despawn)
    // ─────────────────────────────────────────────────────────────────────────────

    void TickGround(float speed)
    {
        // 1. Move all tiles left.
        for (int i = 0; i < activeGround.Count; i++)
        {
            if (activeGround[i] == null) continue;
            Vector3 p = activeGround[i].transform.position;
            p.x -= speed;
            activeGround[i].transform.position = p;
        }

        // 2. Extend on the right while needed.
        float rightmost = RightmostGroundCentreX();
        while (rightmost + groundTileWidth * 0.5f < camRight + groundSpawnBuffer)
        {
            rightmost += groundTileWidth;
            SpawnGroundTileAt(rightmost);
        }

        // 3. Cull from the left.
        for (int i = activeGround.Count - 1; i >= 0; i--)
        {
            if (activeGround[i] == null) { activeGround.RemoveAt(i); continue; }

            float rightEdge = activeGround[i].transform.position.x + groundTileWidth * 0.5f;
            if (rightEdge < camLeft - groundDespawnBuffer)
            {
                ReturnToPool(groundPool, activeGround[i]);
                activeGround.RemoveAt(i);
            }
        }
    }

    void SpawnGroundTileAt(float centreX)
    {
        GameObject tile = GetFromPool(groundPool, groundPrefab);
        tile.transform.position = new Vector3(centreX, groundY, groundZ);
        tile.transform.rotation = Quaternion.Euler(groundRotation);
        activeGround.Add(tile);
    }

    float RightmostGroundCentreX()
    {
        float max = camLeft - groundTileWidth;
        foreach (var t in activeGround)
            if (t != null && t.transform.position.x > max)
                max = t.transform.position.x;
        return max;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  TRAIN TRACKS — INITIALISE
    // ─────────────────────────────────────────────────────────────────────────────

    void InitialiseTracks()
    {
        if (!enableTracks) return;

        if (trackPrefab == null)
        {
            Debug.LogWarning("[RollingBG] Track system is enabled but trackPrefab is not assigned.", this);
            return;
        }

        trackTileWidth = trackTileWidthOverride > 0f
            ? trackTileWidthOverride
            : MeasurePrefabWidth(trackPrefab);

        if (trackTileWidth <= 0f)
        {
            Debug.LogError(
                "[RollingBG] Cannot measure track tile width. " +
                "Set 'Track Tile Width Override' manually.", this);
            trackTileWidth = 10f;
        }

        float startX = Mathf.Floor((camLeft - trackDespawnBuffer) / trackTileWidth) * trackTileWidth;
        float endX   = camRight + trackSpawnBuffer + trackTileWidth;

        for (float x = startX; x < endX; x += trackTileWidth)
            SpawnTrackTileAt(x);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  TRAIN TRACKS — TICK (move → spawn → despawn)
    // ─────────────────────────────────────────────────────────────────────────────

    void TickTracks(float speed)
    {
        if (!enableTracks || trackPrefab == null) return;

        float trackSpeed = speed * trackSpeedMultiplier;

        // 1. Move all track tiles left.
        for (int i = 0; i < activeTracks.Count; i++)
        {
            if (activeTracks[i] == null) continue;
            Vector3 p = activeTracks[i].transform.position;
            p.x -= trackSpeed;
            activeTracks[i].transform.position = p;
        }

        // 2. Extend on the right while needed.
        float rightmost = RightmostTrackCentreX();
        while (rightmost + trackTileWidth * 0.5f < camRight + trackSpawnBuffer)
        {
            rightmost += trackTileWidth;
            SpawnTrackTileAt(rightmost);
        }

        // 3. Cull from the left.
        for (int i = activeTracks.Count - 1; i >= 0; i--)
        {
            if (activeTracks[i] == null) { activeTracks.RemoveAt(i); continue; }

            float rightEdge = activeTracks[i].transform.position.x + trackTileWidth * 0.5f;
            if (rightEdge < camLeft - trackDespawnBuffer)
            {
                ReturnToPool(trackPool, activeTracks[i]);
                activeTracks.RemoveAt(i);
            }
        }
    }

    void SpawnTrackTileAt(float centreX)
    {
        GameObject tile = GetFromPool(trackPool, trackPrefab);
        tile.transform.position = new Vector3(centreX, trackY, trackZ);
        tile.transform.rotation = Quaternion.Euler(trackRotation);
        activeTracks.Add(tile);
    }

    float RightmostTrackCentreX()
    {
        float max = camLeft - trackTileWidth;
        foreach (var t in activeTracks)
            if (t != null && t.transform.position.x > max)
                max = t.transform.position.x;
        return max;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  MOUNTAINS — INITIALISE
    // ─────────────────────────────────────────────────────────────────────────────

    void InitialiseMountains()
    {
        int n = layers.Length;
        mountainPools   = new Queue<GameObject>[n][];
        activeMountains = new List<ActiveMountain>[n];
        nextSpawnX      = new float[n];
        recentPrefabs   = new List<int>[n];

        for (int li = 0; li < n; li++)
        {
            MountainLayerConfig cfg = layers[li];
            int prefabCount = cfg.prefabs != null ? cfg.prefabs.Length : 0;

            mountainPools[li] = new Queue<GameObject>[prefabCount];
            for (int pi = 0; pi < prefabCount; pi++)
                mountainPools[li][pi] = new Queue<GameObject>();

            activeMountains[li] = new List<ActiveMountain>();
            recentPrefabs[li]   = new List<int>();

            nextSpawnX[li] = camLeft - cfg.despawnBehindCamera;

            float fillEnd = camRight + cfg.spawnAheadOfCamera;
            while (nextSpawnX[li] < fillEnd)
                PlantMountain(li);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  MOUNTAINS — TICK (move → spawn → despawn)
    // ─────────────────────────────────────────────────────────────────────────────

    void TickMountains(float speed)
    {
        for (int li = 0; li < layers.Length; li++)
        {
            MountainLayerConfig cfg        = layers[li];
            float               layerSpeed = speed * cfg.parallaxSpeed;

            nextSpawnX[li] -= layerSpeed;

            List<ActiveMountain> active = activeMountains[li];
            for (int i = 0; i < active.Count; i++)
            {
                ActiveMountain m = active[i];
                if (m.obj == null) continue;
                Vector3 p = m.obj.transform.position;
                p.x -= layerSpeed;
                m.obj.transform.position = p;
                active[i] = m;
            }

            float spawnLimit = camRight + cfg.spawnAheadOfCamera;
            while (nextSpawnX[li] < spawnLimit)
                PlantMountain(li);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveMountain m = active[i];
                if (m.obj == null) { active.RemoveAt(i); continue; }

                float rightEdge = m.obj.transform.position.x + MeasureActiveWidth(m.obj) * 0.5f;
                if (rightEdge < camLeft - cfg.despawnBehindCamera)
                {
                    ReturnToPool(mountainPools[li][m.prefabIndex], m.obj);
                    active.RemoveAt(i);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  MOUNTAINS — PLANT
    // ─────────────────────────────────────────────────────────────────────────────

    void PlantMountain(int li)
    {
        MountainLayerConfig cfg = layers[li];
        if (cfg.prefabs == null || cfg.prefabs.Length == 0)
        {
            nextSpawnX[li] += cfg.GetMinSpacing();
            return;
        }

        int        pi  = PickPrefabIndex(li);
        GameObject obj = GetFromPool(mountainPools[li][pi], cfg.prefabs[pi]);

        float yOffset = GetPrefabYOffset(cfg, pi);
        float finalY = cfg.yPosition + yOffset;

        float z = cfg.zPosition + Random.Range(-cfg.zVariation, cfg.zVariation);
        obj.transform.position = new Vector3(nextSpawnX[li], finalY, z);

        Vector3 eulerRot = (cfg.prefabRotations != null && pi < cfg.prefabRotations.Length)
            ? cfg.prefabRotations[pi]
            : Vector3.zero;
        obj.transform.rotation = Quaternion.Euler(eulerRot);

        Vector3 prefabScale = cfg.prefabs[pi].transform.localScale;
        Vector3 baseScale   = Vector3.Scale(prefabScale, cfg.scaleMultiplier);
        float   randomMult  = cfg.randomScale ? Random.Range(cfg.minRandomScale, cfg.maxRandomScale) : 1f;
        obj.transform.localScale = baseScale * randomMult;

        if (cfg.applyTint)
            ApplyTint(obj, cfg.tint, cfg.sortingOrder);
        else
        {
            foreach (SpriteRenderer sr in obj.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder = cfg.sortingOrder;
        }

        activeMountains[li].Add(new ActiveMountain { obj = obj, prefabIndex = pi });

        RecordRecentPrefab(li, pi, cfg);
        float minSpacing = cfg.GetMinSpacing();
        float maxSpacing = cfg.GetMaxSpacing();
        nextSpawnX[li] += Random.Range(minSpacing, maxSpacing);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PER-PREFAB Y OFFSET HELPER
    // ─────────────────────────────────────────────────────────────────────────────

    float GetPrefabYOffset(MountainLayerConfig cfg, int prefabIndex)
    {
        if (cfg.prefabYOffsets == null || cfg.prefabYOffsets.Length == 0)
            return 0f;

        if (prefabIndex < cfg.prefabYOffsets.Length)
            return cfg.prefabYOffsets[prefabIndex];

        return 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  ANTI-REPEAT PREFAB SELECTION
    // ─────────────────────────────────────────────────────────────────────────────

    int PickPrefabIndex(int li)
    {
        MountainLayerConfig cfg   = layers[li];
        int                 total = cfg.prefabs.Length;
        if (total == 1) return 0;

        List<int> recent     = recentPrefabs[li];
        int       windowSize = Mathf.Min(cfg.minUniqueBeforeRepeat, total - 1);

        var excluded = new HashSet<int>();
        for (int i = Mathf.Max(0, recent.Count - windowSize); i < recent.Count; i++)
            excluded.Add(recent[i]);

        var candidates = new List<int>(total);
        for (int i = 0; i < total; i++)
            if (!excluded.Contains(i)) candidates.Add(i);

        if (candidates.Count == 0) return Random.Range(0, total);

        return candidates[Random.Range(0, candidates.Count)];
    }

    void RecordRecentPrefab(int li, int pi, MountainLayerConfig cfg)
    {
        recentPrefabs[li].Add(pi);
        int maxHistory = Mathf.Max(cfg.minUniqueBeforeRepeat * 2, 4);
        if (recentPrefabs[li].Count > maxHistory)
            recentPrefabs[li].RemoveAt(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  POOLING
    // ─────────────────────────────────────────────────────────────────────────────

    GameObject GetFromPool(Queue<GameObject> pool, GameObject prefab)
    {
        while (pool.Count > 0)
        {
            GameObject pooled = pool.Dequeue();
            if (pooled != null)
            {
                pooled.SetActive(true);
                return pooled;
            }
        }
        return Instantiate(prefab, transform);
    }

    static void ReturnToPool(Queue<GameObject> pool, GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        pool.Enqueue(obj);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  WIDTH MEASUREMENT HELPERS
    // ─────────────────────────────────────────────────────────────────────────────

    static float MeasurePrefabWidth(GameObject prefab)
    {
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite.bounds.size.x * prefab.transform.localScale.x;

        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds.size.x * prefab.transform.localScale.x;

        SkinnedMeshRenderer smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
            return smr.sharedMesh.bounds.size.x * prefab.transform.localScale.x;

        return 0f;
    }

    static float MeasureActiveWidth(GameObject obj)
    {
        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.bounds.size.x;

        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds.size.x;

        Collider c3 = obj.GetComponentInChildren<Collider>();
        if (c3 != null) return c3.bounds.size.x;

        Collider2D c2 = obj.GetComponentInChildren<Collider2D>();
        if (c2 != null) return c2.bounds.size.x;

        return 1f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  TINT HELPER
    // ─────────────────────────────────────────────────────────────────────────────

    static void ApplyTint(GameObject obj, Color tint, int sortingOrder)
    {
        foreach (SpriteRenderer sr in obj.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.color        = tint;
            sr.sortingOrder = sortingOrder;
        }

        var mpb = new MaterialPropertyBlock();
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            if (r is SpriteRenderer) continue;

            r.GetPropertyBlock(mpb);
            
            Material mat = r.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty("_Color"))
                    mpb.SetColor("_Color", tint);
                if (mat.HasProperty("_BaseColor"))
                    mpb.SetColor("_BaseColor", tint);
            }
            
            r.SetPropertyBlock(mpb);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  SPACING CALCULATOR (based on spawnDensity)
    // ─────────────────────────────────────────────────────────────────────────────

    void CalculateAllLayerSpacings()
    {
        foreach (var cfg in layers)
        {
            if (cfg.useDensitySlider)
            {
                float invDensity = 1f - cfg.spawnDensity;
                
                float sparseMin = 20f;
                float sparseMax = 35f;
                float denseMin  = 1f;
                float denseMax  = 3f;

                float t = Mathf.Pow(invDensity, 1.5f);
                
                cfg.calculatedMinSpacing = Mathf.Lerp(denseMin, sparseMin, t);
                cfg.calculatedMaxSpacing = Mathf.Lerp(denseMax, sparseMax, t);
            }
            else
            {
                if (cfg.minSpacing <= 0f) cfg.minSpacing = 1f;
                if (cfg.maxSpacing <= 0f) cfg.maxSpacing = cfg.minSpacing + 2f;
                if (cfg.maxSpacing < cfg.minSpacing) cfg.maxSpacing = cfg.minSpacing;
                
                cfg.calculatedMinSpacing = cfg.minSpacing;
                cfg.calculatedMaxSpacing = cfg.maxSpacing;
            }
            
            cfg.wasUsingDensitySlider = cfg.useDensitySlider;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────────

    public void SetScrollSpeed(float speed)       => scrollSpeed = speed;
    public void SetSpeedMultiplier(float mult)    => speedMultiplier = mult;
    public void PauseScrolling()                  => isScrolling = false;
    public void ResumeScrolling()                 => isScrolling = true;
    public float GetCurrentSpeed()               => scrollSpeed * speedMultiplier;

    /// <summary>
    /// Enable or disable the train tracks at runtime.
    /// </summary>
    public void SetTracksEnabled(bool enabled)
    {
        enableTracks = enabled;
        if (!enabled)
        {
            foreach (var track in activeTracks)
                if (track != null)
                    ReturnToPool(trackPool, track);
            activeTracks.Clear();
        }
        else
        {
            InitialiseTracks();
        }
    }

    /// <summary>
    /// Resets and re-initialises everything — useful after a scene restart or speed change.
    /// </summary>
    public void Rebuild()
    {
        foreach (var tile in activeGround) ReturnToPool(groundPool, tile);
        activeGround.Clear();

        foreach (var track in activeTracks) ReturnToPool(trackPool, track);
        activeTracks.Clear();

        if (activeMountains != null)
        {
            for (int li = 0; li < activeMountains.Length; li++)
            {
                foreach (var m in activeMountains[li])
                    if (li < mountainPools.Length && m.prefabIndex < mountainPools[li].Length)
                        ReturnToPool(mountainPools[li][m.prefabIndex], m.obj);
                activeMountains[li].Clear();
            }
        }

        CalculateAllLayerSpacings();
        RefreshCameraBounds();
        InitialiseGround();
        InitialiseTracks();
        InitialiseMountains();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  EDITOR GIZMOS
    // ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (mainCamera == null) return;

        RefreshCameraBounds();
        float cx = mainCamera.transform.position.x;
        float cy = mainCamera.transform.position.y;
        float h  = camHalfH * 2f;
        float w  = camHalfW * 2f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(mainCamera.transform.position, new Vector3(w, h, 1f));

        // Ground Y line
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(cx - 60, groundY, 0), new Vector3(cx + 60, groundY, 0));

        // Track Y line
        if (enableTracks)
        {
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f); // Brown for tracks
            Gizmos.DrawLine(new Vector3(cx - 60, trackY, trackZ), new Vector3(cx + 60, trackY, trackZ));
        }

        Color[] colours = { Color.cyan, Color.magenta, Color.blue, Color.red, Color.white };

        for (int li = 0; li < layers.Length; li++)
        {
            MountainLayerConfig cfg = layers[li];
            Gizmos.color = colours[li % colours.Length];

            Gizmos.DrawLine(
                new Vector3(cx - 60, cfg.yPosition, cfg.zPosition),
                new Vector3(cx + 60, cfg.yPosition, cfg.zPosition));

            float spawnEdge = camRight + cfg.spawnAheadOfCamera;
            Gizmos.DrawLine(
                new Vector3(spawnEdge, cy - h * 0.5f, 0),
                new Vector3(spawnEdge, cy + h * 0.5f, 0));

            float despawnEdge = camLeft - cfg.despawnBehindCamera;
            Gizmos.DrawLine(
                new Vector3(despawnEdge, cy - h * 0.5f, 0),
                new Vector3(despawnEdge, cy + h * 0.5f, 0));

            string mode = cfg.useDensitySlider ? $"density:{cfg.spawnDensity:F2}" : $"spacing:{cfg.minSpacing}-{cfg.maxSpacing}";
            UnityEditor.Handles.Label(
                new Vector3(spawnEdge + 0.2f, cfg.yPosition + 0.3f * li, cfg.zPosition),
                $"{cfg.name}  speed×{cfg.parallaxSpeed}  {mode}");
        }
    }
#endif
}