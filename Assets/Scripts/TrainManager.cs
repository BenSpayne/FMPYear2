using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public class TrainManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spawnPoint; // Empty GameObject at front of train (0,0.42,-0.31)
    [SerializeField] private GameObject enginePrefab; // Engine prefab to spawn
    [SerializeField] private CarriageScriptableObject carriagesData;
    [SerializeField] private CameraController cameraController;
    
    [Header("Carriage Prefab Mapping")]
    [SerializeField] private List<CarriagePrefab> carriagePrefabs;
    
    [Header("Gap Settings")]
    [SerializeField] private float targetGap = 0.05f; // Gap between train parts
    [SerializeField] private float engineYOffset = 0.23f; // Additional Y offset for engine
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private bool autoRebuild = true;
    
    // Runtime tracking - Complete train including engine
    private List<GameObject> trainParts = new List<GameObject>(); // Index 0 = engine, then carriages
    private List<string> trainPartTags = new List<string>(); // "Engine", "Residential", etc.
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>(); // Cache prefabs by tag
    
    [System.Serializable]
    public class CarriagePrefab
    {
        public string tag;
        public GameObject prefab;
    }
    
    private void Start()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("Spawn Point is not assigned!");
            return;
        }
        
        if (enginePrefab == null)
        {
            Debug.LogError("Engine Prefab is not assigned!");
            return;
        }
        
        // Build prefab cache
        foreach (var mapping in carriagePrefabs)
        {
            if (!prefabCache.ContainsKey(mapping.tag))
                prefabCache[mapping.tag] = mapping.prefab;
        }
        
        BuildCompleteTrain();
    }
    
    [ContextMenu("Build Complete Train")]
    public void BuildCompleteTrain()
    {
        // Clear existing train
        foreach (var part in trainParts)
        {
            if (part != null)
                DestroyImmediate(part);
        }
        trainParts.Clear();
        trainPartTags.Clear();
        
        // 1. Spawn Engine at spawn point with Y offset
        SpawnEngine();
        
        // 2. Spawn all carriages from ScriptableObject
        foreach (var carriageCount in carriagesData.carriageCounts)
        {
            for (int i = 0; i < carriageCount.count; i++)
            {
                AddCarriageAtBack(carriageCount.carriageTag);
            }
        }
        
        // Update camera
        if (cameraController != null)
            cameraController.UpdateCameraPosition(trainParts.Count - 1); // Exclude engine
        
        Debug.Log($"Train built: Engine + {trainParts.Count - 1} carriages");
        DisplayTrainOrder();
    }
    
    private void SpawnEngine()
    {
        // Spawn engine at spawn point with Y offset
        Vector3 enginePosition = new Vector3(spawnPoint.position.x, spawnPoint.position.y + engineYOffset, spawnPoint.position.z);
        GameObject engine = Instantiate(enginePrefab, enginePosition, enginePrefab.transform.rotation);
        engine.transform.parent = transform;
        engine.tag = "Engine";
        
        trainParts.Add(engine);
        trainPartTags.Add("Engine");
        
        Debug.Log($"Engine spawned at position: {engine.transform.position} (Y offset: +{engineYOffset})");
        LogPartBounds(engine, "Engine");
    }
    
    private void AddCarriageAtBack(string tag)
    {
        GameObject prefab = GetPrefabByTag(tag);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for tag: {tag}");
            return;
        }
        
        // Calculate spawn position based on the last train part
        Vector3 spawnPos = CalculateSpawnPosition(prefab);
        
        // Spawn carriage
        GameObject newCarriage = Instantiate(prefab, spawnPos, prefab.transform.rotation);
        newCarriage.transform.parent = transform;
        newCarriage.tag = tag;
        
        // Add to list
        trainParts.Add(newCarriage);
        trainPartTags.Add(tag);
        
        // Now adjust position exactly to connect with previous part
        StartCoroutine(AdjustPositionNextFrame(newCarriage, trainParts.Count - 1));
    }
    
    private Vector3 CalculateSpawnPosition(GameObject prefab)
    {
        if (trainParts.Count == 0)
            return spawnPoint.position;
        
        // Get the last train part
        GameObject lastPart = trainParts[trainParts.Count - 1];
        BoxCollider lastCollider = lastPart.GetComponent<BoxCollider>();
        
        if (lastCollider == null)
        {
            Debug.LogError($"Last part {lastPart.tag} has no BoxCollider!");
            return lastPart.transform.position;
        }
        
        // Get the back edge of the last part (minimum X in world space)
        float lastPartBackEdge = lastCollider.bounds.min.x;
        
        // Get the length of the new carriage prefab
        float newCarriageLength = GetPrefabLength(prefab);
        
        // Calculate spawn position: back edge of last part - new carriage length - gap
        float spawnX = lastPartBackEdge - newCarriageLength - targetGap;
        
        // Keep Y and Z consistent with spawn point
        return new Vector3(spawnX, spawnPoint.position.y, spawnPoint.position.z);
    }
    
    private float GetPrefabLength(GameObject prefab)
    {
        BoxCollider prefabCollider = prefab.GetComponent<BoxCollider>();
        if (prefabCollider != null)
        {
            return prefabCollider.size.x;
        }
        return 2.52f; // Default fallback
    }
    
    private System.Collections.IEnumerator AdjustPositionNextFrame(GameObject carriage, int index)
    {
        yield return null; // Wait for collider to initialize
        
        BoxCollider carriageCollider = carriage.GetComponent<BoxCollider>();
        if (carriageCollider == null)
        {
            Debug.LogError($"Carriage {carriage.tag} has no BoxCollider!");
            yield break;
        }
        
        // Get previous train part (engine or previous carriage)
        GameObject previousPart = trainParts[index - 1];
        BoxCollider previousCollider = previousPart.GetComponent<BoxCollider>();
        
        if (previousCollider == null)
        {
            Debug.LogError($"Previous part {previousPart.tag} has no BoxCollider!");
            yield break;
        }
        
        // Calculate connection points
        float previousPartBackEdge = previousCollider.bounds.min.x; // Back edge of previous part
        float carriageBackEdge = carriageCollider.bounds.min.x;     // Back edge of new carriage
        
        // We want the carriage's BACK edge to be exactly at: previous part's BACK edge - carriage length - gap
        float carriageLength = carriageCollider.size.x;
        float targetBackEdge = previousPartBackEdge - carriageLength - targetGap;
        
        // Calculate adjustment needed
        float adjustment = targetBackEdge - carriageBackEdge;
        
        Debug.Log($"=== CONNECTING {carriage.tag} to {previousPart.tag} ===");
        Debug.Log($"Previous Part ({previousPart.tag}) Back Edge: {previousPartBackEdge:F3}");
        Debug.Log($"New Carriage ({carriage.tag}) Length: {carriageLength:F3}");
        Debug.Log($"New Carriage Current Back Edge: {carriageBackEdge:F3}");
        Debug.Log($"Target Back Edge (previous back - carriage length - {targetGap} gap): {targetBackEdge:F3}");
        Debug.Log($"Adjustment needed: {adjustment:F3}");
        
        // Apply adjustment
        Vector3 newPosition = carriage.transform.position;
        newPosition.x += adjustment;
        carriage.transform.position = newPosition;
        
        // Wait another frame for bounds to update
        yield return null;
        
        // Verify final position
        float finalBackEdge = carriageCollider.bounds.min.x;
        float finalFrontEdge = carriageCollider.bounds.max.x;
        float actualGap = previousPartBackEdge - finalFrontEdge; // Gap is space between previous back and new front
        
        Debug.Log($"Final Back Edge: {finalBackEdge:F3}");
        Debug.Log($"Final Front Edge: {finalFrontEdge:F3}");
        Debug.Log($"Actual Gap: {actualGap:F3} (Target: {targetGap:F3})");
        Debug.Log($"Final Carriage Position X: {carriage.transform.position.x:F3}");
        Debug.Log("=========================================");
        
        // If gap is not correct, do a second adjustment pass for connected carriages
        if (Mathf.Abs(actualGap - targetGap) > 0.01f)
        {
            Debug.Log($"Gap still off by {Mathf.Abs(actualGap - targetGap):F3}, doing second pass...");
            yield return StartCoroutine(AdjustPositionNextFrame(carriage, index));
        }
        
        // Update camera if this is the last carriage
        if (cameraController != null && index == trainParts.Count - 1)
            cameraController.UpdateCameraPosition(trainParts.Count - 1);
    }
    
    private void LogPartBounds(GameObject part, string name)
    {
        BoxCollider collider = part.GetComponent<BoxCollider>();
        if (collider != null)
        {
            Debug.Log($"=== {name} BOUNDS ===");
            Debug.Log($"Position: {part.transform.position}");
            Debug.Log($"BoxCollider Center (local): {collider.center}");
            Debug.Log($"BoxCollider Size: {collider.size}");
            Debug.Log($"World Bounds Min (Back): {collider.bounds.min}");
            Debug.Log($"World Bounds Max (Front): {collider.bounds.max}");
            Debug.Log($"Back Edge X: {collider.bounds.min.x:F3}");
            Debug.Log($"Front Edge X: {collider.bounds.max.x:F3}");
            Debug.Log($"Length: {collider.size.x:F3}");
            Debug.Log("==================");
        }
    }
    
    public void RemoveLastCarriage()
    {
        if (trainParts.Count > 1) // Keep at least engine
        {
            int lastIndex = trainParts.Count - 1;
            DestroyImmediate(trainParts[lastIndex]);
            trainParts.RemoveAt(lastIndex);
            trainPartTags.RemoveAt(lastIndex);
            
            SyncToScriptableObject();
            
            if (cameraController != null)
                cameraController.UpdateCameraPosition(trainParts.Count - 1);
            
            DisplayTrainOrder();
        }
    }
    
    public void RemoveCarriageByPosition(int position) // position 1 = first carriage behind engine
    {
        int index = position; // +1 because index 0 is engine
        if (index >= 1 && index < trainParts.Count)
        {
            DestroyImmediate(trainParts[index]);
            trainParts.RemoveAt(index);
            trainPartTags.RemoveAt(index);
            
            // Reposition all carriages after the removed one
            StartCoroutine(RepositionAllCarriagesFromIndex(index));
            
            SyncToScriptableObject();
            
            if (cameraController != null)
                cameraController.UpdateCameraPosition(trainParts.Count - 1);
            
            DisplayTrainOrder();
        }
    }
    
    private System.Collections.IEnumerator RepositionAllCarriagesFromIndex(int startIndex)
    {
        yield return null; // Wait one frame
        
        for (int i = startIndex; i < trainParts.Count; i++)
        {
            GameObject carriage = trainParts[i];
            BoxCollider carriageCollider = carriage.GetComponent<BoxCollider>();
            GameObject previousPart = trainParts[i - 1];
            BoxCollider previousCollider = previousPart.GetComponent<BoxCollider>();
            
            if (carriageCollider == null || previousCollider == null) continue;
            
            float previousPartBackEdge = previousCollider.bounds.min.x;
            float carriageLength = carriageCollider.size.x;
            float targetBackEdge = previousPartBackEdge - carriageLength - targetGap;
            float carriageBackEdge = carriageCollider.bounds.min.x;
            float adjustment = targetBackEdge - carriageBackEdge;
            
            Vector3 newPosition = carriage.transform.position;
            newPosition.x += adjustment;
            carriage.transform.position = newPosition;
            
            yield return null;
        }
        
        DisplayTrainOrder();
    }
    
    private void DisplayTrainOrder()
    {
        Debug.Log("\n╔════════════════════════════════════════╗");
        Debug.Log("║         COMPLETE TRAIN ORDER          ║");
        Debug.Log("╚════════════════════════════════════════╝");
        
        for (int i = 0; i < trainParts.Count; i++)
        {
            BoxCollider collider = trainParts[i].GetComponent<BoxCollider>();
            string position = i == 0 ? "ENGINE" : $"Carriage {i}";
            
            if (collider != null)
            {
                Debug.Log($"📍 {position}: {trainPartTags[i].ToUpper()}");
                Debug.Log($"   Back Edge: {collider.bounds.min.x:F3}");
                Debug.Log($"   Front Edge: {collider.bounds.max.x:F3}");
                Debug.Log($"   Length: {collider.size.x:F3}");
            }
        }
        
        // Show gaps
        Debug.Log("\n╔════════════════════════════════════════╗");
        Debug.Log("║           GAP VERIFICATION            ║");
        Debug.Log("╚════════════════════════════════════════╝");
        
        for (int i = 1; i < trainParts.Count; i++)
        {
            BoxCollider previousCollider = trainParts[i - 1].GetComponent<BoxCollider>();
            BoxCollider currentCollider = trainParts[i].GetComponent<BoxCollider>();
            
            if (previousCollider != null && currentCollider != null)
            {
                float previousBackEdge = previousCollider.bounds.min.x;
                float currentFrontEdge = currentCollider.bounds.max.x;
                float actualGap = previousBackEdge - currentFrontEdge;
                string status = Mathf.Abs(actualGap - targetGap) < 0.01f ? "✓ PERFECT" : "✗ NEEDS ADJUSTMENT";
                Debug.Log($"Gap between {trainPartTags[i - 1]} and {trainPartTags[i]}: {actualGap:F3} (Target: {targetGap:F3}) {status}");
            }
        }
        Debug.Log("========================================\n");
    }
    
    private GameObject GetPrefabByTag(string tag)
    {
        CarriagePrefab mapping = carriagePrefabs.Find(m => m.tag == tag);
        return mapping != null ? mapping.prefab : null;
    }
    
    private void SyncToScriptableObject()
    {
        // Count carriages (exclude engine)
        Dictionary<string, int> countDict = new Dictionary<string, int>();
        for (int i = 1; i < trainPartTags.Count; i++) // Start from 1 to skip engine
        {
            string tag = trainPartTags[i];
            if (countDict.ContainsKey(tag))
                countDict[tag]++;
            else
                countDict[tag] = 1;
        }
        
        // Update ScriptableObject
        carriagesData.carriageCounts.Clear();
        foreach (var kvp in countDict)
        {
            carriagesData.carriageCounts.Add(new CarriageScriptableObject.CarriageCount
            {
                carriageTag = kvp.Key,
                count = kvp.Value
            });
        }
        
        #if UNITY_EDITOR
        EditorUtility.SetDirty(carriagesData);
        #endif
    }
    
    // Auto-detect changes in ScriptableObject
    private List<CarriageScriptableObject.CarriageCount> lastKnownCounts = new List<CarriageScriptableObject.CarriageCount>();
    
    private void SaveCurrentCounts()
    {
        lastKnownCounts.Clear();
        foreach (var count in carriagesData.carriageCounts)
        {
            lastKnownCounts.Add(new CarriageScriptableObject.CarriageCount
            {
                carriageTag = count.carriageTag,
                count = count.count
            });
        }
    }
    
    private bool HasCountsChanged()
    {
        if (carriagesData.carriageCounts.Count != lastKnownCounts.Count)
            return true;
        
        foreach (var currentCount in carriagesData.carriageCounts)
        {
            var lastCount = lastKnownCounts.Find(l => l.carriageTag == currentCount.carriageTag);
            if (lastCount == null || lastCount.count != currentCount.count)
                return true;
        }
        
        return false;
    }
    
    private void Update()
    {
        if (!autoRebuild) return;
        
        if (HasCountsChanged())
        {
            Debug.Log("=== DETECTED CHANGE IN CARRIAGES SCRIPTABLEOBJECT ===");
            BuildCompleteTrain();
            SaveCurrentCounts();
        }
    }
    
    public int GetCarriageCount()
    {
        return trainParts.Count - 1; // Exclude engine
    }
    
    public List<string> GetCarriageOrder()
    {
        return new List<string>(trainPartTags);
    }

    // Add this public method to your TrainManager class
    public void AddCarriage(string carriageTag)
    {
        // Check if carriage type exists in prefab mapping
        GameObject prefab = GetPrefabByTag(carriageTag);
        if (prefab == null)
        {
            Debug.LogError($"Cannot add carriage: No prefab found for tag '{carriageTag}'!");
            return;
        }
        
        // Add the carriage to the back
        AddCarriageAtBack(carriageTag);
        
        // Sync to ScriptableObject
        SyncToScriptableObject();
        
        // Update camera
        if (cameraController != null)
            cameraController.UpdateCameraPosition(trainParts.Count - 1);
        
        Debug.Log($"Added {carriageTag} carriage. Total carriages: {trainParts.Count - 1}");
    }
        
    
    
    // Debug visualization
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals) return;

        if (spawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(spawnPoint.position, 0.15f);

            // Draw engine spawn position with offset
            Vector3 engineSpawnPos = new Vector3(spawnPoint.position.x, spawnPoint.position.y + engineYOffset, spawnPoint.position.z);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(engineSpawnPos, 0.15f);
        }

        foreach (var part in trainParts)
        {
            if (part != null)
            {
                BoxCollider collider = part.GetComponent<BoxCollider>();
                if (collider != null)
                {
                    Gizmos.color = part.tag == "Engine" ? Color.red : Color.green;
                    Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);

                    // Draw connection points
                    Vector3 backPoint = new Vector3(collider.bounds.min.x, collider.bounds.center.y, collider.bounds.center.z);
                    Vector3 frontPoint = new Vector3(collider.bounds.max.x, collider.bounds.center.y, collider.bounds.center.z);

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(backPoint, 0.08f);
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(frontPoint, 0.08f);
                }
            }
        }
    }
}