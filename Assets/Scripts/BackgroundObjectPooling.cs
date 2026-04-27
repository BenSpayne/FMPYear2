using UnityEngine;
using System.Collections.Generic;

public class BackgroundObjectPooling : MonoBehaviour
{
    [System.Serializable]
    public class MountainLayerPool
    {
        public string layerName = "MountainLayer";
        public GameObject[] mountainPrefabs; // Mountains specific to this layer
        public int poolSize = 10;
    }

    [Header("Mountain Layer Pools")]
    [SerializeField] private MountainLayerPool[] mountainLayerPools = new MountainLayerPool[]
    {
        new MountainLayerPool { layerName = "Near Mountains", poolSize = 10 },
        new MountainLayerPool { layerName = "Mid Mountains", poolSize = 8 },
        new MountainLayerPool { layerName = "Far Mountains", poolSize = 6 }
    };
    
    [Header("Ground")]
    [SerializeField] private GameObject groundPrefab;
    [SerializeField] private int groundPoolSize = 5;

    private List<List<GameObject>> mountainPoolsByLayer;
    private List<GameObject> groundPool = new List<GameObject>();
    private Transform poolParent;

    void Awake()
    {
        poolParent = new GameObject("PooledObjects").transform;
        poolParent.SetParent(transform);
        InitializePools();
    }

    void InitializePools()
    {
        mountainPoolsByLayer = new List<List<GameObject>>();

        // Initialize mountain pools for each layer
        for (int layerIndex = 0; layerIndex < mountainLayerPools.Length; layerIndex++)
        {
            MountainLayerPool layerPool = mountainLayerPools[layerIndex];
            List<GameObject> pool = new List<GameObject>();
            
            GameObject layerFolder = new GameObject(layerPool.layerName + "_Pool");
            layerFolder.transform.SetParent(poolParent);

            if (layerPool.mountainPrefabs == null || layerPool.mountainPrefabs.Length == 0)
            {
                Debug.LogError($"No mountain prefabs assigned to layer: {layerPool.layerName}");
                mountainPoolsByLayer.Add(pool);
                continue;
            }

            for (int i = 0; i < layerPool.poolSize; i++)
            {
                // Randomly pick from this layer's mountain variations
                GameObject prefab = layerPool.mountainPrefabs[Random.Range(0, layerPool.mountainPrefabs.Length)];
                GameObject mountain = Instantiate(prefab, layerFolder.transform);
                mountain.name = $"{layerPool.layerName}_Mountain_{i}";
                mountain.SetActive(false);
                pool.Add(mountain);
            }

            mountainPoolsByLayer.Add(pool);
        }

        // Initialize ground pool
        GameObject groundFolder = new GameObject("Ground_Pool");
        groundFolder.transform.SetParent(poolParent);
        
        for (int i = 0; i < groundPoolSize; i++)
        {
            GameObject ground = Instantiate(groundPrefab, groundFolder.transform);
            ground.name = "Ground_" + i;
            ground.SetActive(false);
            groundPool.Add(ground);
        }
    }

    // Get mountain from specific layer
    public GameObject GetMountain(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= mountainPoolsByLayer.Count)
        {
            Debug.LogError($"Invalid layer index: {layerIndex}");
            return null;
        }

        List<GameObject> pool = mountainPoolsByLayer[layerIndex];

        // Find any inactive mountain
        foreach (GameObject mountain in pool)
        {
            if (!mountain.activeInHierarchy)
            {
                mountain.SetActive(true);
                return mountain;
            }
        }

        // Expand pool with a random prefab
        MountainLayerPool layerPool = mountainLayerPools[layerIndex];
        GameObject selectedPrefab = layerPool.mountainPrefabs[Random.Range(0, layerPool.mountainPrefabs.Length)];
        
        GameObject newMountain = Instantiate(selectedPrefab, pool[0].transform.parent);
        newMountain.name = $"{layerPool.layerName}_Mountain_{pool.Count}";
        pool.Add(newMountain);
        newMountain.SetActive(true);
        return newMountain;
    }

    // Get mountain from specific layer, avoiding a specific prefab index
    public GameObject GetMountainAvoiding(int layerIndex, int prefabIndexToAvoid)
    {
        if (layerIndex < 0 || layerIndex >= mountainPoolsByLayer.Count)
        {
            Debug.LogError($"Invalid layer index: {layerIndex}");
            return null;
        }

        MountainLayerPool layerPool = mountainLayerPools[layerIndex];
        List<GameObject> pool = mountainPoolsByLayer[layerIndex];

        // If no need to avoid, just get any mountain
        if (prefabIndexToAvoid < 0 || prefabIndexToAvoid >= layerPool.mountainPrefabs.Length)
        {
            GameObject mountain = GetMountain(layerIndex);
            if (mountain != null)
            {
                mountain.transform.rotation = Quaternion.identity; // Reset rotation
            }
            return mountain;
        }

        GameObject prefabToAvoid = layerPool.mountainPrefabs[prefabIndexToAvoid];
        Sprite avoidSprite = null;
        SpriteRenderer avoidSR = prefabToAvoid.GetComponent<SpriteRenderer>();
        if (avoidSR != null)
        {
            avoidSprite = avoidSR.sprite;
        }

        // Try to find an inactive mountain that doesn't match the avoided prefab
        foreach (GameObject mountain in pool)
        {
            if (!mountain.activeInHierarchy)
            {
                SpriteRenderer mountainSR = mountain.GetComponent<SpriteRenderer>();
                if (mountainSR != null && avoidSprite != null && mountainSR.sprite != avoidSprite)
                {
                    mountain.SetActive(true);
                    mountain.transform.rotation = Quaternion.identity; // Reset rotation
                    return mountain;
                }
                else if (avoidSprite == null)
                {
                    mountain.SetActive(true);
                    mountain.transform.rotation = Quaternion.identity; // Reset rotation
                    return mountain;
                }
            }
        }

        // Create new one from different prefab
        List<GameObject> availablePrefabs = new List<GameObject>();
        for (int i = 0; i < layerPool.mountainPrefabs.Length; i++)
        {
            if (i != prefabIndexToAvoid)
            {
                availablePrefabs.Add(layerPool.mountainPrefabs[i]);
            }
        }

        if (availablePrefabs.Count == 0)
        {
            availablePrefabs.AddRange(layerPool.mountainPrefabs);
        }

        GameObject selectedPrefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
        GameObject newMountain = Instantiate(selectedPrefab, pool[0].transform.parent);
        newMountain.name = $"{layerPool.layerName}_Mountain_{pool.Count}";
        newMountain.transform.rotation = Quaternion.identity; // Set rotation
        pool.Add(newMountain);
        newMountain.SetActive(true);
        return newMountain;
    }

    // Get the prefab index for a mountain based on its sprite
    public int GetPrefabIndexForMountain(int layerIndex, Sprite mountainSprite)
    {
        if (layerIndex < 0 || layerIndex >= mountainLayerPools.Length)
            return -1;
        
        MountainLayerPool layerPool = mountainLayerPools[layerIndex];
        
        for (int i = 0; i < layerPool.mountainPrefabs.Length; i++)
        {
            GameObject prefab = layerPool.mountainPrefabs[i];
            SpriteRenderer prefabSR = prefab.GetComponent<SpriteRenderer>();
            
            if (prefabSR != null && prefabSR.sprite == mountainSprite)
            {
                return i;
            }
        }
        
        return -1;
    }

    public void ReturnMountain(GameObject mountain)
    {
        if (mountain != null)
            mountain.SetActive(false);
    }

    public GameObject GetGround()
    {
        foreach (GameObject ground in groundPool)
        {
            if (!ground.activeInHierarchy)
            {
                ground.SetActive(true);
                return ground;
            }
        }

        // Expand ground pool
        GameObject newGround = Instantiate(groundPrefab, groundPool[0].transform.parent);
        newGround.name = "Ground_" + groundPool.Count;
        groundPool.Add(newGround);
        newGround.SetActive(true);
        return newGround;
    }

    public void ReturnGround(GameObject ground)
    {
        if (ground != null)
            ground.SetActive(false);
    }

    // Get number of mountain layers
    public int GetLayerCount()
    {
        return mountainLayerPools.Length;
    }
}