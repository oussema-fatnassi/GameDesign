using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PowerCore script that handles game over when destroyed
/// Attach this to the PowerCore (Objective) GameObject
/// </summary>
[RequireComponent(typeof(Health))]
public class PowerCore : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showDebugLogs = true;
    
    private Health health;
    private bool isDestroyed = false;
    
    void Awake()
    {
        health = GetComponent<Health>();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to death event
        if (health != null)
        {
            health.OnDeath.AddListener(OnPowerCoreDestroyed);
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Unsubscribe
        if (health != null)
        {
            health.OnDeath.RemoveListener(OnPowerCoreDestroyed);
        }
    }
    
    /// <summary>
    /// Called when PowerCore health reaches 0
    /// This runs on all clients due to OnDeath event
    /// </summary>
    void OnPowerCoreDestroyed()
    {
        if (isDestroyed)
            return;
        
        isDestroyed = true;
        
        if (showDebugLogs)
            Debug.Log("=== POWER CORE DESTROYED! ===");
        
        // Trigger game over
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDefeat();
        }
        else
        {
            Debug.LogError("GameManager not found! Cannot trigger game over.");
        }
        
        // Only server handles wave system
        if (IsServer)
        {
            // Stop spawning enemies
            GameManager.Instance?.StopGame();
        }
    }
}