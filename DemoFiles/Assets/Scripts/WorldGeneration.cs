using UnityEngine;
using System.Collections.Generic;

public class WorldGeneration : MonoBehaviour
{
    [SerializeField] GameObject rockPrefab;
    [SerializeField] GameObject shroomPrefab;
    [SerializeField] Collider spawnCollider;
    [SerializeField] LayerMask spawnedObjectLayer; 
    [SerializeField] Transform obstacleParent;

    public List<GameObject> shrooms = new List<GameObject>();
    public List<GameObject> boulders = new List<GameObject>();

    // A list to keep track of all spawned objects
    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Start()
    {
        // Spawn a random number of rocks (10 to 20)
        SpawnObjects(rockPrefab, Random.Range(10, 21), boulders, 1.8f);

        // Spawn a random number of shrooms (5 to 10)
        SpawnObjects(shroomPrefab, Random.Range(5, 11), shrooms, 1f);

        GameManager.Instance.shrooms = shrooms;
        GameManager.Instance.boulders = boulders;
        spawnCollider.enabled = false;

        GameManager.Instance.shrooms = shrooms;
        GameManager.Instance.boulders = boulders;
    }


    /// Spawns a given number of a specific prefab within the spawnCollider without overlapping.
    void SpawnObjects(GameObject prefabToSpawn, int amountToSpawn, List<GameObject> objList, float height)
    {
        Bounds spawnBounds = spawnCollider.bounds;
        int spawnedCount = 0;
        int spawnAttempts = 0;
        int maxSpawnAttempts = amountToSpawn * 10; // To prevent an infinite loop

        while (spawnedCount < amountToSpawn && spawnAttempts < maxSpawnAttempts)
        {
            spawnAttempts++;

            // Generate a random position within the collider's bounds
            float randomX = Random.Range(spawnBounds.min.x, spawnBounds.max.x);
            float randomZ = Random.Range(spawnBounds.min.z, spawnBounds.max.z);
            Vector3 randomPosition = new Vector3(randomX, height, randomZ);

            // Ensure the random position is within the collider's volume
            if (!IsPointInsideCollider(spawnCollider, randomPosition))
            {
                continue;
            }

            // Get the size of the prefab for the overlap check
            Renderer prefabRenderer = prefabToSpawn.GetComponent<Renderer>();
            if (prefabRenderer == null)
            {
                Debug.LogError("Prefab does not have a Renderer component. Cannot determine size for overlap check.");
                return;
            }
            float spawnRadius = Mathf.Max(prefabRenderer.bounds.extents.x, prefabRenderer.bounds.extents.y, prefabRenderer.bounds.extents.z) + 2;

            // Check if the spawn area is clear of other objects on the specified layer
            if (!Physics.CheckSphere(randomPosition, spawnRadius, spawnedObjectLayer))
            {
                // Instantiate the object and add it to our list
                GameObject newObject = Instantiate(prefabToSpawn, randomPosition, Quaternion.identity);
                spawnedObjects.Add(newObject);
                objList.Add(newObject);
                newObject.transform.parent = obstacleParent;
                spawnedCount++;
            }
        }

        if (spawnedCount < amountToSpawn)
        {
            Debug.LogWarning($"Could not spawn all {amountToSpawn} of {prefabToSpawn.name}. The area might be too crowded.");
        }
    }

    /// Checks if a point is inside a collider.
    bool IsPointInsideCollider(Collider collider, Vector3 point)
    {
        return collider.ClosestPoint(point) == point;
    }
}