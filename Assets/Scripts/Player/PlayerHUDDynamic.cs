using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Dynamic HUD that automatically finds and connects to the local player
/// Waits for the game to start before searching
/// </summary>
public class PlayerHUDDynamic : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reservesText;
    
    [Header("Settings")]
    [SerializeField] private bool showHealthPercentage = false;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private Color normalHealthColor = Color.white;
    [SerializeField] private float lowHealthThreshold = 0.3f;
    
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color normalAmmoColor = Color.white;
    [SerializeField] private float lowAmmoThreshold = 0.25f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Dynamic references
    private Health playerHealth;
    private WeaponControllerNetwork weaponController;
    private bool isConnected = false;
    private bool hasStartedSearching = false;
    
    void Update()
    {
        // Wait for NetworkManager to be active (game started)
        if (!hasStartedSearching && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            hasStartedSearching = true;
            
            if (showDebugLogs)
                Debug.Log("HUD: Game started, beginning player search...");
            
            StartCoroutine(FindLocalPlayer());
        }
        
        // Update display if connected
        if (isConnected && playerHealth != null && weaponController != null)
        {
            UpdateHealthDisplay();
            UpdateAmmoDisplay();
        }
    }
    
    /// <summary>
    /// Finds the local player in the scene
    /// </summary>
    IEnumerator FindLocalPlayer()
    {
        if (showDebugLogs)
            Debug.Log("HUD: Searching for local player...");
        
        int attemptCount = 0;
        const int maxAttempts = 20;
        
        while (!isConnected && attemptCount < maxAttempts)
        {
            attemptCount++;
            
            // Wait before checking
            yield return new WaitForSeconds(0.5f);
            
            // Find all player controllers
            PlayerControllerNetwork[] players = FindObjectsOfType<PlayerControllerNetwork>();
            
            if (showDebugLogs && attemptCount == 1)
                Debug.Log($"HUD: Found {players.Length} PlayerControllerNetwork(s) in scene");
            
            foreach (var player in players)
            {
                // Check if this is the local player
                if (player.IsOwner && player.IsSpawned)
                {
                    if (showDebugLogs)
                        Debug.Log($"HUD: Checking player '{player.gameObject.name}'");
                    
                    // Get components
                    playerHealth = player.GetComponent<Health>();
                    weaponController = player.GetComponent<WeaponControllerNetwork>();
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"HUD: Health component: {(playerHealth != null ? "✓ FOUND" : "✗ MISSING")}");
                        Debug.Log($"HUD: Weapon component: {(weaponController != null ? "✓ FOUND" : "✗ MISSING")}");
                    }
                    
                    // Need weapon at minimum (health might be on a child object)
                    if (weaponController != null)
                    {
                        // Try to find Health on the player or its children
                        if (playerHealth == null)
                        {
                            playerHealth = player.GetComponentInChildren<Health>();
                            
                            if (showDebugLogs && playerHealth != null)
                                Debug.Log($"HUD: Found Health component in children!");
                        }
                        
                        if (playerHealth != null)
                        {
                            isConnected = true;
                            Debug.Log($"✓✓✓ HUD CONNECTED to local player: {player.gameObject.name} ✓✓✓");
                            
                            // Force immediate update
                            UpdateHealthDisplay();
                            UpdateAmmoDisplay();
                            
                            yield break;
                        }
                        else
                        {
                            Debug.LogError($"HUD: Found player and weapon but NO HEALTH component! Add Health component to your player prefab.");
                        }
                    }
                }
            }
        }
        
        if (!isConnected)
        {
            Debug.LogError($"HUD: Failed to find local player after {attemptCount} attempts!");
            Debug.LogError("Make sure your player prefab has: PlayerControllerNetwork, Health, and WeaponControllerNetwork components.");
        }
    }
    
    void UpdateHealthDisplay()
    {
        if (healthText == null || playerHealth == null)
            return;
        
        float currentHealth = playerHealth.CurrentHealth;
        float maxHealth = playerHealth.MaxHealth;
        
        // Wait for NetworkVariable to sync
        if (maxHealth <= 0) return;
        if (currentHealth == 0 && maxHealth > 0)
        {
            currentHealth = maxHealth;
        }
        
        float healthPercent = currentHealth / maxHealth;
        
        // Update text
        if (showHealthPercentage)
        {
            healthText.text = $"Health: {Mathf.CeilToInt(healthPercent * 100)}%";
        }
        else
        {
            healthText.text = $"Health: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
        
        // Update color
        healthText.color = healthPercent <= lowHealthThreshold ? lowHealthColor : normalHealthColor;
    }
    
    void UpdateAmmoDisplay()
    {
        if (ammoText == null || reservesText == null || weaponController == null)
            return;
        
        int currentAmmo = weaponController.CurrentAmmo;
        int maxAmmo = weaponController.MaxAmmo;
        int reserves = weaponController.ReserveAmmo;
        
        // Update text
        ammoText.text = $"{currentAmmo}";
        reservesText.text = $"/ {reserves}";
        
        // Update color
        float ammoPercent = maxAmmo > 0 ? (float)currentAmmo / maxAmmo : 0f;
        Color ammoColor = ammoPercent <= lowAmmoThreshold ? lowAmmoColor : normalAmmoColor;
        ammoText.color = ammoColor;
        reservesText.color = ammoColor;
    }
    
    public void Reconnect()
    {
        isConnected = false;
        hasStartedSearching = false;
        playerHealth = null;
        weaponController = null;
        
        if (showDebugLogs)
            Debug.Log("HUD: Reconnecting...");
    }
    
    public bool IsConnected => isConnected;
}