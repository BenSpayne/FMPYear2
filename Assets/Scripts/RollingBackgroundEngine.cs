using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Parallax scrolling background engine with tiling ground and multi-layer mountains.
/// 
/// KEY DESIGN:
///   - Each mountain layer owns a "spawn frontier" (nextSpawnX). Every frame that frontier
///     moves left at the same speed as the layer. When it falls inside the spawn window
///     (camRight + spawnAheadOfCamera) we plant a mountain and push the frontier right by
///     a random spacing. This replaces the fragile fixedMountainCount approach and can
///     never produce burst-spawns or gaps.
///   - Ground uses the same pattern: track the rightmost tile, extend while its right edge
///     is inside camRight + spawnBuffer. No maxPieces cap to cause cut-offs.
///   - Pooling is built-in (per-layer, per-prefab queues) so no external pool manager is needed.
/// 
/// SETUP:
///   1. Assign groundPrefab (any GameObject with a SpriteRenderer or Collider for width measurement).
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
    [Tooltip("Spawn tiles this many units ahead of the camera's right edge.")]
    [SerializeField] private float groundSpawnBuffer = 2f;
    [Tooltip("Despawn tiles this many units behind the camera's left edge.")]
    [SerializeField] private float groundDespawnBuffer = 4f;
    [Tooltip("Override tile width (world units). Set this when using a procedurally generated " +
             "ground mesh (e.g. ProceduralGroundGenerator) whose mesh doesn't exist on the prefab asset. " +
             "Leave at 0 to auto-detect from the prefab's SpriteRenderer or MeshFilter.")]
    [SerializeField] private float groundTileWidthOverride = 0f;

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

        [Header("Spacing")]
        public float minSpacing = 3f;
        public float maxSpacing = 8f;

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
    private float                   tileWidth;         // world-space width of one ground tile

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

        RefreshCameraBounds();
        InitialiseGround();
        InitialiseMountains();
    }

    void Update()
    {
        if (!isScrolling) return;

        RefreshCameraBounds();

        float dt    = Time.deltaTime;
        float speed = scrollSpeed * speedMultiplier * dt;   // units this frame

        TickGround(speed);
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

        // Use manual override if set, otherwise auto-detect from the prefab.
        tileWidth = groundTileWidthOverride > 0f
            ? groundTileWidthOverride
            : MeasurePrefabWidth(groundPrefab);

        if (tileWidth <= 0f)
        {
            Debug.LogError(
                "[RollingBG] Cannot measure ground tile width. " +
                "For 3D procedural ground (e.g. ProceduralGroundGenerator) the mesh is built at runtime " +
                "so it cannot be read from the prefab asset — set 'Ground Tile Width Override' manually.", this);
            tileWidth = 10f;
        }

        // Flood-fill from left buffer to right buffer.
        float startX = Mathf.Floor((camLeft - groundDespawnBuffer) / tileWidth) * tileWidth;
        float endX   = camRight + groundSpawnBuffer + tileWidth;

        for (float x = startX; x < endX; x += tileWidth)
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
        //    We loop because a large speed burst could require multiple tiles.
        float rightmost = RightmostGroundCentreX();
        while (rightmost + tileWidth * 0.5f < camRight + groundSpawnBuffer)
        {
            rightmost += tileWidth;
            SpawnGroundTileAt(rightmost);
        }

        // 3. Cull from the left.
        for (int i = activeGround.Count - 1; i >= 0; i--)
        {
            if (activeGround[i] == null) { activeGround.RemoveAt(i); continue; }

            float rightEdge = activeGround[i].transform.position.x + tileWidth * 0.5f;
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
        activeGround.Add(tile);
    }

    float RightmostGroundCentreX()
    {
        float max = camLeft - tileWidth;    // safe floor if list is empty
        foreach (var t in activeGround)
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

            // Place frontier just off the left of the fill zone, then fill forward.
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

            // 1. Move frontier and active mountains left at this layer's speed.
            nextSpawnX[li] -= layerSpeed;

            List<ActiveMountain> active = activeMountains[li];
            for (int i = 0; i < active.Count; i++)
            {
                ActiveMountain m = active[i];
                if (m.obj == null) continue;
                Vector3 p = m.obj.transform.position;
                p.x -= layerSpeed;
                m.obj.transform.position = p;
                active[i] = m;  // struct — must write back
            }

            // 2. Spawn new mountains while frontier is inside the spawn window.
            float spawnLimit = camRight + cfg.spawnAheadOfCamera;
            while (nextSpawnX[li] < spawnLimit)
                PlantMountain(li);

            // 3. Cull mountains that have fully exited camera left.
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
            // Push frontier anyway to avoid an infinite loop.
            nextSpawnX[li] += cfg.minSpacing;
            return;
        }

        int        pi  = PickPrefabIndex(li);
        GameObject obj = GetFromPool(mountainPools[li][pi], cfg.prefabs[pi]);

        // Get per-prefab Y offset
        float yOffset = GetPrefabYOffset(cfg, pi);
        float finalY = cfg.yPosition + yOffset;

        // Position
        float z = cfg.zPosition + Random.Range(-cfg.zVariation, cfg.zVariation);
        obj.transform.position = new Vector3(nextSpawnX[li], finalY, z);

        // Per-prefab rotation: read from prefabRotations[pi] if it exists, else identity.
        Vector3 eulerRot = (cfg.prefabRotations != null && pi < cfg.prefabRotations.Length)
            ? cfg.prefabRotations[pi]
            : Vector3.zero;
        obj.transform.rotation = Quaternion.Euler(eulerRot);

        // Scale: start from the prefab's own localScale, multiply per-axis by scaleMultiplier,
        // then optionally apply a uniform random variation on top.
        Vector3 prefabScale = cfg.prefabs[pi].transform.localScale;
        Vector3 baseScale   = Vector3.Scale(prefabScale, cfg.scaleMultiplier);
        float   randomMult  = cfg.randomScale ? Random.Range(cfg.minRandomScale, cfg.maxRandomScale) : 1f;
        obj.transform.localScale = baseScale * randomMult;

        // Apply visual settings based on toggle
        if (cfg.applyTint)
        {
            ApplyTint(obj, cfg.tint, cfg.sortingOrder);
        }
        else
        {
            // Only set sorting order without touching materials/tint
            foreach (SpriteRenderer sr in obj.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingOrder = cfg.sortingOrder;
            }
        }

        activeMountains[li].Add(new ActiveMountain { obj = obj, prefabIndex = pi });

        // Record for anti-repeat, then advance the frontier.
        RecordRecentPrefab(li, pi, cfg);
        nextSpawnX[li] += Random.Range(cfg.minSpacing, cfg.maxSpacing);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PER-PREFAB Y OFFSET HELPER
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the Y offset for a specific prefab index.
    /// If prefabYOffsets array exists and has an entry for this index, use it.
    /// Otherwise returns 0.
    /// </summary>
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

        // Build exclusion set from the tail of recent history.
        var excluded = new HashSet<int>();
        for (int i = Mathf.Max(0, recent.Count - windowSize); i < recent.Count; i++)
            excluded.Add(recent[i]);

        // Gather allowed candidates.
        var candidates = new List<int>(total);
        for (int i = 0; i < total; i++)
            if (!excluded.Contains(i)) candidates.Add(i);

        // Fallback: all excluded (only 1 unique prefab exists).
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
        // Pool empty: instantiate a new instance, parented for scene tidiness.
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

    /// <summary>
    /// Read width from a prefab asset without instantiating it.
    /// Works for 2D sprites and 3D meshes. Uses GetComponentInChildren so nested
    /// renderer hierarchies (e.g. model root → mesh child) are handled correctly.
    /// </summary>
    static float MeasurePrefabWidth(GameObject prefab)
    {
        // 2D — sprite bounds are in local space and valid on prefab assets.
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite.bounds.size.x * prefab.transform.localScale.x;

        // 3D static mesh — sharedMesh.bounds is local-space, valid on prefab assets.
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds.size.x * prefab.transform.localScale.x;

        // 3D skinned mesh (characters, animated props).
        SkinnedMeshRenderer smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
            return smr.sharedMesh.bounds.size.x * prefab.transform.localScale.x;

        // Nothing found — caller will warn and use override/fallback.
        return 0f;
    }

    /// <summary>
    /// Read world-space width from a live (active, in-scene) GameObject.
    /// Renderer.bounds is the most reliable source for both 2D and 3D objects
    /// because it reflects the actual rendered extents including scale.
    /// </summary>
    static float MeasureActiveWidth(GameObject obj)
    {
        // SpriteRenderer first so 2D objects don't accidentally hit a Collider.
        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.bounds.size.x;

        // Any 3D renderer (MeshRenderer, SkinnedMeshRenderer, etc.).
        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds.size.x;

        // Collider fallbacks — useful when the renderer is disabled or absent.
        Collider c3 = obj.GetComponentInChildren<Collider>();
        if (c3 != null) return c3.bounds.size.x;

        Collider2D c2 = obj.GetComponentInChildren<Collider2D>();
        if (c2 != null) return c2.bounds.size.x;

        return 1f;  // last resort
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  TINT HELPER
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a tint colour to every renderer in the hierarchy.
    ///   • SpriteRenderer  → sr.color  (2D)
    ///   • Any Renderer    → MaterialPropertyBlock with _Color + _BaseColor
    ///                       (_Color = Built-in / _BaseColor = URP / HDRP)
    /// sortingOrder is only applied to SpriteRenderers; for 3D objects depth is
    /// controlled by the Z position set in PlantMountain.
    /// </summary>
    static void ApplyTint(GameObject obj, Color tint, int sortingOrder)
    {
        // 2D sprites
        foreach (SpriteRenderer sr in obj.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.color        = tint;
            sr.sortingOrder = sortingOrder;
        }

        // 3D renderers — use a property block so we don't dirty the shared material.
        var mpb = new MaterialPropertyBlock();
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            // Skip sprite renderers already handled above.
            if (r is SpriteRenderer) continue;

            // Only apply if it's a material that supports these properties
            r.GetPropertyBlock(mpb);
            
            // Check if the material has the property before setting it
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
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────────

    public void SetScrollSpeed(float speed)       => scrollSpeed = speed;
    public void SetSpeedMultiplier(float mult)    => speedMultiplier = mult;
    public void PauseScrolling()                  => isScrolling = false;
    public void ResumeScrolling()                 => isScrolling = true;
    public float GetCurrentSpeed()               => scrollSpeed * speedMultiplier;

    /// <summary>
    /// Resets and re-initialises everything — useful after a scene restart or speed change.
    /// </summary>
    public void Rebuild()
    {
        // Return all active objects to their pools.
        foreach (var tile in activeGround) ReturnToPool(groundPool, tile);
        activeGround.Clear();

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

        RefreshCameraBounds();
        InitialiseGround();
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

        // Camera frustum
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(mainCamera.transform.position, new Vector3(w, h, 1f));

        // Ground Y line
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(cx - 60, groundY, 0), new Vector3(cx + 60, groundY, 0));

        Color[] colours = { Color.cyan, Color.magenta, Color.blue, Color.red, Color.white };

        for (int li = 0; li < layers.Length; li++)
        {
            MountainLayerConfig cfg = layers[li];
            Gizmos.color = colours[li % colours.Length];

            // Layer Y line
            Gizmos.DrawLine(
                new Vector3(cx - 60, cfg.yPosition, cfg.zPosition),
                new Vector3(cx + 60, cfg.yPosition, cfg.zPosition));

            // Spawn window (right)
            float spawnEdge = camRight + cfg.spawnAheadOfCamera;
            Gizmos.DrawLine(
                new Vector3(spawnEdge, cy - h * 0.5f, 0),
                new Vector3(spawnEdge, cy + h * 0.5f, 0));

            // Despawn window (left)
            float despawnEdge = camLeft - cfg.despawnBehindCamera;
            Gizmos.DrawLine(
                new Vector3(despawnEdge, cy - h * 0.5f, 0),
                new Vector3(despawnEdge, cy + h * 0.5f, 0));

            // Layer label
            UnityEditor.Handles.Label(
                new Vector3(spawnEdge + 0.2f, cfg.yPosition + 0.3f * li, cfg.zPosition),
                $"{cfg.name}  speed×{cfg.parallaxSpeed}");
        }
    }
#endif
}