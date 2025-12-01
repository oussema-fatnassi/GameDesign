using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Dynamic HUD that automatically finds and connects to the local player and PowerCore
/// Waits for the GAME to start (GameManager.IsStarted), not just network connection
/// Shows game over message when PowerCore is destroyed
/// FIXED: Better handling of NetworkVariable sync for PowerCore
/// </summary>
public class PlayerHUDDynamic : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reservesText;
    [SerializeField] private TextMeshProUGUI powerCoreText;
    [SerializeField] private GameObject gameOverPanel; // Optional: Full game over screen
    [SerializeField] private TextMeshProUGUI gameOverText; // Optional: Game over message
    
    [Header("Settings")]
    [SerializeField] private bool showHealthPercentage = false;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private Color normalHealthColor = Color.white;
    [SerializeField] private float lowHealthThreshold = 0.3f;
    
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color normalAmmoColor = Color.white;
    [SerializeField] private float lowAmmoThreshold = 0.25f;
    
    [SerializeField] private Color criticalPowerCoreColor = Color.red;
    [SerializeField] private Color warningPowerCoreColor = new Color(1f, 0.6f, 0f); // Orange
    [SerializeField] private Color normalPowerCoreColor = Color.cyan;
    [SerializeField] private float criticalPowerCoreThreshold = 0.25f; // 25%
    [SerializeField] private float warningPowerCoreThreshold = 0.5f; // 50%
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Dynamic references
    private Health playerHealth;
    private WeaponControllerNetwork weaponController;
    private Health powerCoreHealth;
    private NetworkObject powerCoreNetworkObject; // Store for network checks
    
    private bool isConnectedToPlayer = false;
    private bool isConnectedToPowerCore = false;
    private bool hasStartedSearching = false;
    private bool powerCoreDestroyed = false;
    
    // Network sync tracking
    private bool powerCoreNetworkReady = false;
    private float powerCoreLastHealth = -1f; // Track last known health
    
    void Update()
    {
        // Wait for GAME to start (not just network connection)
        if (!hasStartedSearching && GameManager.Instance != null && GameManager.Instance.IsStarted)
        {
            hasStartedSearching = true;
            
            if (showDebugLogs)
                Debug.Log("HUD: Game started (IsStarted = true), beginning search...");
            
            StartCoroutine(FindLocalPlayer());
            StartCoroutine(FindPowerCore());
        }
        
        // Update displays if connected
        if (isConnectedToPlayer && playerHealth != null && weaponController != null)
        {
            UpdateHealthDisplay();
            UpdateAmmoDisplay();
        }
        
        if (isConnectedToPowerCore && powerCoreHealth != null)
        {
            UpdatePowerCoreDisplay();
            CheckPowerCoreDestroyed();
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
        
        while (!isConnectedToPlayer && attemptCount < maxAttempts)
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
                    
                    // Need weapon at minimum
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
                            isConnectedToPlayer = true;
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
            
            if (showDebugLogs && attemptCount % 4 == 0)
                Debug.Log($"HUD: Still searching for player... (attempt {attemptCount}/{maxAttempts})");
        }
        
        if (!isConnectedToPlayer)
        {
            Debug.LogError($"HUD: Failed to find local player after {attemptCount} attempts!");
            Debug.LogError("Make sure your player prefab has: PlayerControllerNetwork, Health, and WeaponControllerNetwork components.");
        }
    }
    
    /// <summary>
    /// Finds the PowerCore (Objective) in the scene
    /// IMPROVED: Better network sync handling
    /// </summary>
    IEnumerator FindPowerCore()
    {
        if (showDebugLogs)
            Debug.Log("HUD: Searching for PowerCore...");
        
        int attemptCount = 0;
        const int maxAttempts = 30; // Increased attempts for network sync
        
        while (!isConnectedToPowerCore && attemptCount < maxAttempts)
        {
            attemptCount++;
            
            // Wait before checking (longer on first attempt for network spawn)
            yield return new WaitForSeconds(attemptCount == 1 ? 1.0f : 0.5f);
            
            // Find objective by tag
            GameObject objectiveObject = GameObject.FindGameObjectWithTag("Objective");
            
            if (objectiveObject != null)
            {
                if (showDebugLogs && attemptCount == 1)
                    Debug.Log($"HUD: Found Objective '{objectiveObject.name}'");
                
                // Get Health component
                powerCoreHealth = objectiveObject.GetComponent<Health>();
                powerCoreNetworkObject = objectiveObject.GetComponent<NetworkObject>();
                
                if (powerCoreHealth != null)
                {
                    // Check if NetworkObject is spawned (important for clients!)
                    if (powerCoreNetworkObject != null && powerCoreNetworkObject.IsSpawned)
                    {
                        // Wait a bit more for NetworkVariable to sync
                        yield return new WaitForSeconds(0.5f);
                        
                        // Verify health has actual values (not just defaults)
                        float maxHealth = powerCoreHealth.MaxHealth;
                        float currentHealth = powerCoreHealth.CurrentHealth;
                        
                        if (showDebugLogs)
                            Debug.Log($"HUD: PowerCore health check - Current: {currentHealth}, Max: {maxHealth}");
                        
                        // Only connect if we have valid health values
                        if (maxHealth > 0)
                        {
                            isConnectedToPowerCore = true;
                            powerCoreNetworkReady = true;
                            Debug.Log($"✓✓✓ HUD CONNECTED to PowerCore: {objectiveObject.name} (Health: {currentHealth}/{maxHealth}) ✓✓✓");
                            
                            // Force immediate update
                            UpdatePowerCoreDisplay();
                            
                            yield break;
                        }
                        else
                        {
                            if (showDebugLogs && attemptCount % 4 == 0)
                                Debug.Log($"HUD: PowerCore found but health not synced yet... (attempt {attemptCount}/{maxAttempts})");
                        }
                    }
                    else
                    {
                        if (showDebugLogs && attemptCount % 4 == 0)
                            Debug.Log($"HUD: PowerCore NetworkObject not spawned yet... (attempt {attemptCount}/{maxAttempts})");
                    }
                }
                else
                {
                    Debug.LogWarning($"HUD: Found Objective but it has NO HEALTH component!");
                }
            }
            else
            {
                if (showDebugLogs && attemptCount % 4 == 0)
                    Debug.Log($"HUD: Still searching for PowerCore... (attempt {attemptCount}/{maxAttempts})");
            }
        }
        
        if (!isConnectedToPowerCore)
        {
            Debug.LogError($"HUD: Failed to find PowerCore after {attemptCount} attempts!");
            Debug.LogError("Make sure you have a GameObject with tag 'Objective', Health component, and NetworkObject.");
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
    
    void UpdatePowerCoreDisplay()
    {
        if (powerCoreText == null || powerCoreHealth == null)
            return;
        
        float currentHealth = powerCoreHealth.CurrentHealth;
        float maxHealth = powerCoreHealth.MaxHealth;
        
        // Don't show anything until we have valid data
        if (maxHealth <= 0 && !powerCoreDestroyed)
        {
            powerCoreText.text = "Core: Syncing...";
            powerCoreText.color = Color.gray;
            return;
        }
        
        // Track if health actually changed (to detect real updates)
        if (currentHealth != powerCoreLastHealth)
        {
            powerCoreLastHealth = currentHealth;
            powerCoreNetworkReady = true;
        }
        
        float healthPercent = currentHealth / maxHealth;
        
        // Update text
        powerCoreText.text = $"Core: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        
        // Update color based on health percentage
        if (healthPercent <= criticalPowerCoreThreshold)
        {
            powerCoreText.color = criticalPowerCoreColor; // Red when critical
        }
        else if (healthPercent <= warningPowerCoreThreshold)
        {
            powerCoreText.color = warningPowerCoreColor; // Orange when warning
        }
        else
        {
            powerCoreText.color = normalPowerCoreColor; // Cyan when healthy
        }
    }
    
    /// <summary>
    /// Check if PowerCore is destroyed and show game over
    /// </summary>
    void CheckPowerCoreDestroyed()
    {
        if (powerCoreHealth == null || powerCoreDestroyed || !powerCoreNetworkReady)
            return;
        
        // Check if PowerCore health is 0 or below
        if (powerCoreHealth.CurrentHealth <= 0 && powerCoreHealth.MaxHealth > 0)
        {
            powerCoreDestroyed = true;
            ShowGameOver();
        }
    }
    
    /// <summary>
    /// Shows the game over UI
    /// </summary>
    void ShowGameOver()
    {
        Debug.Log("HUD: Showing GAME OVER screen");
        
        // Update PowerCore text to show destroyed
        if (powerCoreText != null)
        {
            powerCoreText.text = "CORE DESTROYED";
            powerCoreText.color = Color.red;
        }
        
        // Show game over panel if available
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        // Show game over text if available
        if (gameOverText != null)
        {
            gameOverText.text = "GAME OVER\n\nPower Core Destroyed";
            gameOverText.color = Color.red;
        }
        
        // Unlock cursor so player can click restart/quit buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Public method to manually reconnect to a new player
    /// Call this if the player respawns
    /// </summary>
    public void Reconnect()
    {
        isConnectedToPlayer = false;
        hasStartedSearching = false;
        playerHealth = null;
        weaponController = null;
        
        if (showDebugLogs)
            Debug.Log("HUD: Reconnecting to player...");
        
        StartCoroutine(FindLocalPlayer());
    }
    
    /// <summary>
    /// Public method to reconnect to PowerCore
    /// Call this if the PowerCore is destroyed and respawned (new round)
    /// </summary>
    public void ReconnectPowerCore()
    {
        isConnectedToPowerCore = false;
        powerCoreHealth = null;
        powerCoreNetworkObject = null;
        powerCoreDestroyed = false;
        powerCoreNetworkReady = false;
        powerCoreLastHealth = -1f;
        
        if (showDebugLogs)
            Debug.Log("HUD: Reconnecting to PowerCore...");
        
        StartCoroutine(FindPowerCore());
    }
    
    /// <summary>
    /// Check if HUD is connected to player
    /// </summary>
    public bool IsConnectedToPlayer => isConnectedToPlayer;
    
    /// <summary>
    /// Check if HUD is connected to PowerCore
    /// </summary>
    public bool IsConnectedToPowerCore => isConnectedToPowerCore;
}