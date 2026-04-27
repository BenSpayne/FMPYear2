using UnityEngine;
using System.Collections.Generic;

public class RollingBackgroundEngine : MonoBehaviour
{
    [System.Serializable]
    public class MountainLayer
    {
        public string layerName = "MountainLayer";
        
        [Header("Movement")]
        [Range(0.1f, 1f)] public float parallaxSpeed = 0.5f;
        
        [Header("Scale")]
        public float minScale = 0.8f;
        public float maxScale = 1.5f;
        
        [Header("Spacing")]
        [Tooltip("Minimum distance between mountains in this layer")]
        public float minSpacing = 3f;
        [Tooltip("Maximum distance between mountains in this layer")]
        public float maxSpacing = 8f;
        
        [Header("Spawning")]
        [Range(0f, 1f)] public float spawnChance = 0.4f;
        [Tooltip("How far right of camera to spawn")]
        public float spawnMarginRight = 5f;
        [Tooltip("How far left of camera before despawning")]
        public float despawnMarginLeft = 10f;
        public int maxMountainsInLayer = 8;
        
        [Header("Visual")]
        public Color tintColor = Color.white;
        
        [Header("Z-Axis Layer Position")]
        [Tooltip("The Z position where this layer sits. All mountains in this layer will be on this Z plane.")]
        public float zPosition = 0f;
        [Tooltip("How much Z variation to add from the base Z position (creates depth within the layer)")]
        public float zVariation = 0f;
        
        [Header("Anti-Repetition")]
        [Tooltip("Minimum different mountains before same prefab can repeat")]
        public int minUniqueBeforeRepeat = 2;
        
        [Header("Rotation")]
        [Tooltip("Force this rotation on all mountains in this layer")]
        public Vector3 forcedRotation = new Vector3(0, 0, 0);
    }

    [Header("Movement Settings")]
    [SerializeField] private float baseScrollSpeed = 5f;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private bool isScrolling = true;

    [Header("Camera Setup")]
    [SerializeField] private Camera mainCamera;
    
    [Header("Ground Configuration")]
    [SerializeField] private float groundYPosition = -4f;
    [SerializeField] private int maxGroundPieces = 4;
    [SerializeField] private float groundSpawnBuffer = 2f;
    [SerializeField] private float groundDespawnBuffer = 5f;
    private List<GameObject> activeGroundPieces = new List<GameObject>();
    private float groundPieceWidth;
    private float lastGroundSpawnX;

    [Header("Mountain Layers")]
    [SerializeField] private MountainLayer[] mountainLayers = new MountainLayer[]
    {
        new MountainLayer { 
            layerName = "Far Mountains", 
            parallaxSpeed = 0.3f, 
            minScale = 1.2f, 
            maxScale = 1.8f, 
            minSpacing = 8f, 
            maxSpacing = 15f, 
            spawnChance = 0.2f, 
            zPosition = 10f,
            zVariation = 1f,
            spawnMarginRight = 8f,
            despawnMarginLeft = 15f,
            maxMountainsInLayer = 6,
            minUniqueBeforeRepeat = 3,
            forcedRotation = new Vector3(0, 0, 0),
            tintColor = new Color(0.6f, 0.65f, 0.7f) 
        },
        new MountainLayer { 
            layerName = "Mid Mountains", 
            parallaxSpeed = 0.6f, 
            minScale = 0.9f, 
            maxScale = 1.3f, 
            minSpacing = 5f, 
            maxSpacing = 10f, 
            spawnChance = 0.35f, 
            zPosition = 5f,
            zVariation = 0.5f,
            spawnMarginRight = 5f,
            despawnMarginLeft = 10f,
            maxMountainsInLayer = 8,
            minUniqueBeforeRepeat = 2,
            forcedRotation = new Vector3(0, 0, 0),
            tintColor = new Color(0.45f, 0.55f, 0.45f) 
        },
        new MountainLayer { 
            layerName = "Near Mountains", 
            parallaxSpeed = 0.9f, 
            minScale = 0.7f, 
            maxScale = 1.0f, 
            minSpacing = 3f, 
            maxSpacing = 7f, 
            spawnChance = 0.5f, 
            zPosition = 0f,
            zVariation = 0.2f,
            spawnMarginRight = 3f,
            despawnMarginLeft = 7f,
            maxMountainsInLayer = 10,
            minUniqueBeforeRepeat = 2,
            forcedRotation = new Vector3(0, 0, 0),
            tintColor = new Color(0.3f, 0.4f, 0.3f) 
        }
    };

    [Header("Global Y Position")]
    [Tooltip("All mountains will spawn at this Y position (0 by default)")]
    [SerializeField] private float globalMountainYPosition = 0f;

    [Header("Pool Manager")]
    [SerializeField] private BackgroundObjectPooling poolManager;
    
    [Header("Spawning Configuration")]
    [SerializeField] private float spawnCheckInterval = 0.3f;

    private List<ActiveMountain>[] activeMountainsByLayer;
    private List<int>[] recentMountainPrefabs;
    private float nextSpawnCheckTime;
    private float cameraLeftEdge;
    private float cameraRightEdge;
    private float cameraWidth;
    private float cameraHeight;

    private class ActiveMountain
    {
        public GameObject gameObject;
        public int prefabIndex;
        public int layerIndex;
        public Vector3 originalScale;

        public ActiveMountain(GameObject obj, int prefabIdx, int layer, Vector3 scale)
        {
            gameObject = obj;
            prefabIndex = prefabIdx;
            layerIndex = layer;
            originalScale = scale;
        }
    }

    void Start()
    {
        if (poolManager == null)
            poolManager = GetComponent<BackgroundObjectPooling>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Initialize arrays
        activeMountainsByLayer = new List<ActiveMountain>[mountainLayers.Length];
        recentMountainPrefabs = new List<int>[mountainLayers.Length];
        
        for (int i = 0; i < mountainLayers.Length; i++)
        {
            activeMountainsByLayer[i] = new List<ActiveMountain>();
            recentMountainPrefabs[i] = new List<int>();
        }

        UpdateCameraEdges();
        ValidateGroundSetup();
        InitializeGround();
        InitializeMountains();
    }

    void UpdateCameraEdges()
    {
        cameraHeight = 2f * mainCamera.orthographicSize;
        cameraWidth = cameraHeight * mainCamera.aspect;
        
        Vector3 cameraPos = mainCamera.transform.position;
        cameraRightEdge = cameraPos.x + cameraWidth / 2f;
        cameraLeftEdge = cameraPos.x - cameraWidth / 2f;
    }

    void ValidateGroundSetup()
    {
        GameObject tempGround = poolManager.GetGround();
        if (tempGround != null)
        {
            SpriteRenderer sr = tempGround.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                groundPieceWidth = sr.bounds.size.x;
            }
            else
            {
                Collider col = tempGround.GetComponent<Collider>();
                groundPieceWidth = col != null ? col.bounds.size.x : 10f;
            }
            poolManager.ReturnGround(tempGround);
        }
    }

    void InitializeGround()
    {
        float startX = cameraLeftEdge - (groundPieceWidth * 2) - groundSpawnBuffer;
        float endX = cameraRightEdge + (groundPieceWidth * 2) + groundSpawnBuffer;
        
        lastGroundSpawnX = startX;
        
        while (lastGroundSpawnX < endX)
        {
            SpawnGroundPiece(new Vector3(lastGroundSpawnX, groundYPosition, 0));
            lastGroundSpawnX += groundPieceWidth;
        }
    }

    void SpawnGroundPiece(Vector3 position)
    {
        if (activeGroundPieces.Count >= maxGroundPieces)
        {
            GameObject leftmost = GetLeftmostGround();
            if (leftmost != null)
            {
                poolManager.ReturnGround(leftmost);
                activeGroundPieces.Remove(leftmost);
            }
        }

        GameObject newGround = poolManager.GetGround();
        if (newGround != null)
        {
            newGround.transform.position = position;
            activeGroundPieces.Add(newGround);
        }
    }

    void InitializeMountains()
    {
        for (int layerIndex = 0; layerIndex < mountainLayers.Length; layerIndex++)
        {
            MountainLayer layer = mountainLayers[layerIndex];
            float startX = cameraLeftEdge - layer.despawnMarginLeft;
            float endX = cameraRightEdge + layer.spawnMarginRight;
            float currentX = startX;
            
            while (currentX < endX)
            {
                if (Random.value < layer.spawnChance * 1.5f)
                {
                    SpawnMountainAtPosition(layerIndex, currentX);
                }
                currentX += Random.Range(layer.minSpacing, layer.maxSpacing);
            }
        }
    }

    void Update()
    {
        if (!isScrolling) return;

        UpdateCameraEdges();
        
        float currentSpeed = baseScrollSpeed * speedMultiplier * Time.deltaTime;
        
        MoveGroundPieces(currentSpeed);
        MoveMountains(currentSpeed);
        ManageGroundSpawning();
        ManageMountainSpawning();
        CleanupOffscreenObjects();
    }

    void MoveGroundPieces(float speed)
    {
        for (int i = activeGroundPieces.Count - 1; i >= 0; i--)
        {
            if (activeGroundPieces[i] == null)
            {
                activeGroundPieces.RemoveAt(i);
                continue;
            }

            Vector3 pos = activeGroundPieces[i].transform.position;
            pos.x -= speed;
            activeGroundPieces[i].transform.position = pos;
        }
    }

    void MoveMountains(float speed)
    {
        for (int layerIndex = 0; layerIndex < mountainLayers.Length; layerIndex++)
        {
            float layerSpeed = speed * mountainLayers[layerIndex].parallaxSpeed;
            
            for (int i = activeMountainsByLayer[layerIndex].Count - 1; i >= 0; i--)
            {
                if (activeMountainsByLayer[layerIndex][i] == null || 
                    activeMountainsByLayer[layerIndex][i].gameObject == null)
                {
                    activeMountainsByLayer[layerIndex].RemoveAt(i);
                    continue;
                }

                Vector3 pos = activeMountainsByLayer[layerIndex][i].gameObject.transform.position;
                pos.x -= layerSpeed;
                activeMountainsByLayer[layerIndex][i].gameObject.transform.position = pos;
            }
        }
    }

    void ManageGroundSpawning()
    {
        GameObject rightmostGround = GetRightmostGround();
        if (rightmostGround == null) return;

        float rightEdgeOfRightmost = rightmostGround.transform.position.x + (groundPieceWidth / 2f);
        
        if (rightEdgeOfRightmost < cameraRightEdge + groundSpawnBuffer)
        {
            Vector3 newSpawnPos = new Vector3(
                rightmostGround.transform.position.x + groundPieceWidth,
                groundYPosition,
                0
            );
            
            SpawnGroundPiece(newSpawnPos);
        }
    }

    void ManageMountainSpawning()
    {
        if (Time.time < nextSpawnCheckTime) return;
        nextSpawnCheckTime = Time.time + spawnCheckInterval;

        for (int layerIndex = 0; layerIndex < mountainLayers.Length; layerIndex++)
        {
            MountainLayer layer = mountainLayers[layerIndex];
            
            if (activeMountainsByLayer[layerIndex].Count >= layer.maxMountainsInLayer) continue;
            
            if (Random.value < layer.spawnChance)
            {
                float rightmostX = cameraRightEdge;
                foreach (var mountain in activeMountainsByLayer[layerIndex])
                {
                    if (mountain != null && mountain.gameObject != null && 
                        mountain.gameObject.transform.position.x > rightmostX)
                    {
                        rightmostX = mountain.gameObject.transform.position.x;
                    }
                }
                
                float spacing = Random.Range(layer.minSpacing, layer.maxSpacing);
                float spawnX = rightmostX + spacing;
                
                if (spawnX < cameraRightEdge + layer.spawnMarginRight)
                {
                    SpawnMountainAtPosition(layerIndex, spawnX);
                }
            }
        }
    }

    void SpawnMountainAtPosition(int layerIndex, float xPosition)
    {
        MountainLayer layer = mountainLayers[layerIndex];
        
        int prefabIndexToAvoid = GetPrefabIndexToAvoid(layerIndex);
        GameObject newMountain = poolManager.GetMountainAvoiding(layerIndex, prefabIndexToAvoid);
        
        if (newMountain != null)
        {
            // Get the prefab index
            SpriteRenderer mountainSR = newMountain.GetComponent<SpriteRenderer>();
            int sourcePrefabIndex = -1;
            if (mountainSR != null && mountainSR.sprite != null)
            {
                sourcePrefabIndex = poolManager.GetPrefabIndexForMountain(layerIndex, mountainSR.sprite);
            }
            
            // Force rotation
            newMountain.transform.rotation = Quaternion.Euler(layer.forcedRotation);
            
            // Random scale
            float scale = Random.Range(layer.minScale, layer.maxScale);
            newMountain.transform.localScale = new Vector3(scale, scale, scale);
            
            // Calculate Z position with variation
            float zPos = layer.zPosition + Random.Range(-layer.zVariation, layer.zVariation);
            
            // All mountains spawn at the same Y position
            float yPos = globalMountainYPosition;
            
            // Set position
            newMountain.transform.position = new Vector3(xPosition, yPos, zPos);
            
            // Apply tint color
            SpriteRenderer sr = newMountain.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = layer.tintColor;
                // Sorting order based on Z depth (higher Z = further back = lower sorting order)
                sr.sortingOrder = Mathf.RoundToInt(-zPos * 10);
            }
            
            // Add to active mountains
            activeMountainsByLayer[layerIndex].Add(
                new ActiveMountain(newMountain, sourcePrefabIndex, layerIndex, newMountain.transform.localScale)
            );
            
            // Update recent prefabs history
            AddToRecentPrefabs(layerIndex, sourcePrefabIndex);
        }
    }

    private int GetPrefabIndexToAvoid(int layerIndex)
    {
        MountainLayer layer = mountainLayers[layerIndex];
        
        if (recentMountainPrefabs[layerIndex].Count < layer.minUniqueBeforeRepeat)
        {
            return -1;
        }
        
        if (recentMountainPrefabs[layerIndex].Count > 0)
        {
            int firstIndex = recentMountainPrefabs[layerIndex][0];
            bool allSame = true;
            
            foreach (int prefabIndex in recentMountainPrefabs[layerIndex])
            {
                if (prefabIndex != firstIndex)
                {
                    allSame = false;
                    break;
                }
            }
            
            if (allSame)
            {
                return firstIndex;
            }
        }
        
        int uniqueCount = CountUniquePrefabIndices(layerIndex);
        if (uniqueCount < layer.minUniqueBeforeRepeat && recentMountainPrefabs[layerIndex].Count > 0)
        {
            return recentMountainPrefabs[layerIndex][recentMountainPrefabs[layerIndex].Count - 1];
        }
        
        return -1;
    }

    private int CountUniquePrefabIndices(int layerIndex)
    {
        HashSet<int> unique = new HashSet<int>();
        foreach (int prefabIndex in recentMountainPrefabs[layerIndex])
        {
            if (prefabIndex >= 0)
                unique.Add(prefabIndex);
        }
        return unique.Count;
    }

    private void AddToRecentPrefabs(int layerIndex, int prefabIndex)
    {
        if (prefabIndex < 0) return;
        
        MountainLayer layer = mountainLayers[layerIndex];
        recentMountainPrefabs[layerIndex].Add(prefabIndex);
        
        int maxHistory = Mathf.Max(layer.minUniqueBeforeRepeat * 2, 4);
        while (recentMountainPrefabs[layerIndex].Count > maxHistory)
        {
            recentMountainPrefabs[layerIndex].RemoveAt(0);
        }
    }

    void CleanupOffscreenObjects()
    {
        for (int layerIndex = 0; layerIndex < mountainLayers.Length; layerIndex++)
        {
            MountainLayer layer = mountainLayers[layerIndex];
            
            for (int i = activeMountainsByLayer[layerIndex].Count - 1; i >= 0; i--)
            {
                if (activeMountainsByLayer[layerIndex][i] == null ||
                    activeMountainsByLayer[layerIndex][i].gameObject == null)
                {
                    activeMountainsByLayer[layerIndex].RemoveAt(i);
                    continue;
                }

                GameObject mountain = activeMountainsByLayer[layerIndex][i].gameObject;
                float mountainWidth = GetMountainWidth(mountain);
                float mountainRightEdge = mountain.transform.position.x + (mountainWidth / 2f);

                if (mountainRightEdge < cameraLeftEdge - layer.despawnMarginLeft)
                {
                    poolManager.ReturnMountain(mountain);
                    activeMountainsByLayer[layerIndex].RemoveAt(i);
                }
            }
        }

        for (int i = activeGroundPieces.Count - 1; i >= 0; i--)
        {
            if (activeGroundPieces[i] == null)
            {
                activeGroundPieces.RemoveAt(i);
                continue;
            }

            GameObject ground = activeGroundPieces[i];
            float rightEdge = ground.transform.position.x + (groundPieceWidth / 2f);

            if (rightEdge < cameraLeftEdge - groundDespawnBuffer)
            {
                poolManager.ReturnGround(ground);
                activeGroundPieces.RemoveAt(i);
            }
        }
    }

    float GetMountainWidth(GameObject mountain)
    {
        SpriteRenderer sr = mountain.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.size.x * mountain.transform.localScale.x;
        
        Collider col = mountain.GetComponent<Collider>();
        if (col != null) return col.bounds.size.x;
        
        return 2f;
    }

    float GetMountainHeight(GameObject mountain)
    {
        SpriteRenderer sr = mountain.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.size.y * mountain.transform.localScale.y;
        
        Collider col = mountain.GetComponent<Collider>();
        if (col != null) return col.bounds.size.y;
        
        return 2f;
    }

    GameObject GetRightmostGround()
    {
        GameObject rightmost = null;
        float highestX = float.MinValue;

        foreach (var ground in activeGroundPieces)
        {
            if (ground != null && ground.activeInHierarchy && ground.transform.position.x > highestX)
            {
                highestX = ground.transform.position.x;
                rightmost = ground;
            }
        }

        return rightmost;
    }

    GameObject GetLeftmostGround()
    {
        GameObject leftmost = null;
        float lowestX = float.MaxValue;

        foreach (var ground in activeGroundPieces)
        {
            if (ground != null && ground.activeInHierarchy && ground.transform.position.x < lowestX)
            {
                lowestX = ground.transform.position.x;
                leftmost = ground;
            }
        }

        return leftmost;
    }

    public void SetScrollSpeed(float speed) { baseScrollSpeed = speed; }
    public void SetSpeedMultiplier(float multiplier) { speedMultiplier = multiplier; }
    public void PauseScrolling() { isScrolling = false; }
    public void ResumeScrolling() { isScrolling = true; }

    public void ClearAllObjects()
    {
        for (int i = 0; i < activeMountainsByLayer.Length; i++)
        {
            foreach (var mountain in activeMountainsByLayer[i])
            {
                if (mountain != null && mountain.gameObject != null)
                    poolManager.ReturnMountain(mountain.gameObject);
            }
            activeMountainsByLayer[i].Clear();
            recentMountainPrefabs[i].Clear();
        }

        foreach (var ground in activeGroundPieces)
        {
            if (ground != null)
                poolManager.ReturnGround(ground);
        }
        activeGroundPieces.Clear();

        InitializeGround();
        InitializeMountains();
    }

    void OnDrawGizmosSelected()
    {
        if (mainCamera != null)
        {
            // Draw camera bounds
            Gizmos.color = Color.yellow;
            float height = 2f * mainCamera.orthographicSize;
            float width = height * mainCamera.aspect;
            Gizmos.DrawWireCube(mainCamera.transform.position, new Vector3(width, height, 10));
            
            // Draw global Y position line
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(-100, globalMountainYPosition, 0),
                new Vector3(100, globalMountainYPosition, 0)
            );
            
            // Draw ground line
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                new Vector3(-100, groundYPosition, 0),
                new Vector3(100, groundYPosition, 0)
            );
            
            // Draw spawn/despawn margins for each layer
            Color[] layerColors = { Color.magenta, Color.green, Color.blue };
            
            for (int layerIndex = 0; layerIndex < mountainLayers.Length; layerIndex++)
            {
                MountainLayer layer = mountainLayers[layerIndex];
                Gizmos.color = layerColors[layerIndex % layerColors.Length];
                
                // Spawn margin (right side)
                Vector3 rightEdge = mainCamera.transform.position + Vector3.right * (width / 2f);
                Gizmos.DrawWireCube(
                    rightEdge + Vector3.right * (layer.spawnMarginRight / 2f), 
                    new Vector3(layer.spawnMarginRight, height - (layerIndex * 2), 10)
                );
                
                // Despawn margin (left side)
                Vector3 leftEdge = mainCamera.transform.position + Vector3.left * (width / 2f);
                Gizmos.DrawWireCube(
                    leftEdge + Vector3.left * (layer.despawnMarginLeft / 2f), 
                    new Vector3(layer.despawnMarginLeft, height - (layerIndex * 2), 10)
                );
                
                // Draw Z position label
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    mainCamera.transform.position + Vector3.up * (4 - layerIndex) + Vector3.right * (width/2 + 3),
                    $"{layer.layerName}\nZ: {layer.zPosition}±{layer.zVariation}"
                );
                #endif
            }
        }
    }
}