using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : MonoBehaviour
{
    // Singleton instance to access it easily
    public static PlayerSpawner Instance { get; private set; }

    [SerializeField] private BoxCollider spawnArea;

    private void Awake()
    {
        // Singleton pattern setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Returns a random position inside the BoxCollider bounds
    /// </summary>
    public Vector3 GetRandomPosition()
    {
        if (spawnArea == null)
        {
            Debug.LogWarning("Spawn Area Collider is not assigned!");
            return transform.position;
        }

        Bounds bounds = spawnArea.bounds;

        // Generate random X and Z within the bounds
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);
        
        // Keep the Y position (height) consistent or based on the collider's center
        float y = bounds.center.y; 

        return new Vector3(x, y, z);
    }
}