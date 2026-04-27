using System.Collections.Generic;
using UnityEngine;

public class ProceduralBackground : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public ObjectPool3D pool;
    public BoxCollider spawnZone;

    [Header("Prefabs")]
    public List<GameObject> backgroundPrefabs;
    public GameObject groundPrefab;

    [Header("Settings")]
    public float scrollSpeed = 5f;
    public float spawnOffset = 10f; // distance beyond right edge

    private List<(GameObject obj, GameObject prefab)> activeBackground = new();
    private List<(GameObject obj, GameObject prefab)> activeGround = new();

    private Plane[] frustumPlanes;

    void Update()
    {
        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

        MoveObjects();
        HandleBackground();
        HandleGround();
    }

    void MoveObjects()
    {
        foreach (var item in activeBackground)
            item.obj.transform.Translate(Vector3.left * scrollSpeed * Time.deltaTime, Space.World);

        foreach (var item in activeGround)
            item.obj.transform.Translate(Vector3.left * scrollSpeed * Time.deltaTime, Space.World);
    }

    // =========================
    // BACKGROUND
    // =========================
    void HandleBackground()
    {
        while (NeedsMoreBackground())
        {
            SpawnBackground();
        }

        for (int i = activeBackground.Count - 1; i >= 0; i--)
        {
            if (IsFullyOutsideView(activeBackground[i].obj))
            {
                pool.ReturnObject(activeBackground[i].prefab, activeBackground[i].obj);
                activeBackground.RemoveAt(i);
            }
        }
    }

    bool NeedsMoreBackground()
    {
        foreach (var item in activeBackground)
        {
            if (IsVisible(item.obj))
                return false;
        }
        return true;
    }

    void SpawnBackground()
    {
        GameObject prefab = backgroundPrefabs[Random.Range(0, backgroundPrefabs.Count)];

        Bounds zone = spawnZone.bounds;

        // Pick random Y and Z within zone
        float y = Random.Range(zone.min.y, zone.max.y);
        float z = Random.Range(zone.min.z, zone.max.z);

        // Spawn just outside right side of camera
        Vector3 camRight = mainCamera.transform.right;
        Vector3 spawnPos = mainCamera.transform.position
                         + camRight * spawnOffset;

        spawnPos.y = y;
        spawnPos.z = z;

        GameObject obj = pool.GetObject(prefab, spawnPos, Quaternion.identity);

        activeBackground.Add((obj, prefab));
    }

    // =========================
    // GROUND
    // =========================
    void HandleGround()
    {
        while (NeedsMoreGround())
        {
            SpawnGround();
        }

        for (int i = activeGround.Count - 1; i >= 0; i--)
        {
            if (IsFullyOutsideView(activeGround[i].obj))
            {
                pool.ReturnObject(activeGround[i].prefab, activeGround[i].obj);
                activeGround.RemoveAt(i);
            }
        }
    }

    bool NeedsMoreGround()
    {
        foreach (var item in activeGround)
        {
            if (IsVisible(item.obj))
                return false;
        }
        return true;
    }

    void SpawnGround()
    {
        Bounds zone = spawnZone.bounds;

        float y = zone.min.y;
        float z = 0f;

        Vector3 spawnPos = mainCamera.transform.position
                         + mainCamera.transform.right * spawnOffset;

        spawnPos.y = y;
        spawnPos.z = z;

        GameObject obj = pool.GetObject(groundPrefab, spawnPos, Quaternion.identity);

        // Snap next to last ground
        if (activeGround.Count > 0)
        {
            float prevRight = GetRightEdge(activeGround[^1].obj);
            float width = GetWidth(obj);

            spawnPos.x = prevRight + width / 2f;
            obj.transform.position = spawnPos;
        }

        activeGround.Add((obj, groundPrefab));
    }

    // =========================
    // VISIBILITY CHECKS
    // =========================
    bool IsVisible(GameObject obj)
    {
        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r == null) return false;

        return GeometryUtility.TestPlanesAABB(frustumPlanes, r.bounds);
    }

    bool IsFullyOutsideView(GameObject obj)
    {
        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r == null) return true;

        return !GeometryUtility.TestPlanesAABB(frustumPlanes, r.bounds);
    }

    float GetRightEdge(GameObject obj)
    {
        Renderer r = obj.GetComponentInChildren<Renderer>();
        return r.bounds.max.x;
    }

    float GetWidth(GameObject obj)
    {
        Renderer r = obj.GetComponentInChildren<Renderer>();
        return r.bounds.size.x;
    }
}