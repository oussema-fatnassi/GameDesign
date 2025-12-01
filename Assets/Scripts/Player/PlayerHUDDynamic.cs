using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Dynamic HUD that automatically finds and connects to the local player and PowerCore
/// Shows game over message when PowerCore is destroyed
/// FIXED: Event subscription + coroutine wait for NetworkVariable sync
/// </summary>
public class PlayerHUDDynamic : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reservesText;
    [SerializeField] private TextMeshProUGUI powerCoreText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;

    [Header("Settings")]
    [SerializeField] private bool showHealthPercentage = false;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private Color normalHealthColor = Color.white;
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color normalAmmoColor = Color.white;
    [SerializeField] private float lowAmmoThreshold = 0.25f;

    [SerializeField] private Color criticalPowerCoreColor = Color.red;
    [SerializeField] private Color warningPowerCoreColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color normalPowerCoreColor = Color.cyan;
    [SerializeField] private float criticalPowerCoreThreshold = 0.25f;
    [SerializeField] private float warningPowerCoreThreshold = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private GameObject UIContainer;
    [SerializeField] private GameObject crosshairObject;

    // Dynamic references
    private Health playerHealth;
    private WeaponControllerNetwork weaponController;
    private Health powerCoreHealth;
    private NetworkObject powerCoreNetworkObject;

    private bool isConnectedToPlayer = false;
    private bool isConnectedToPowerCore = false;
    private bool hasStartedSearching = false;
    private bool powerCoreDestroyed = false;

    void Start()
    {
        if (UIContainer != null)
        {
            UIContainer.SetActive(false);
        }
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(false);
        }
    }

    void Update()
    {
        // Wait for GAME to start
        if (!hasStartedSearching && GameManager.Instance != null && GameManager.Instance.IsStarted)
        {
            hasStartedSearching = true;

            if (showDebugLogs)
                Debug.Log("HUD: Game started, beginning search...");

            if (UIContainer != null)
            {
                UIContainer.SetActive(true);
            }
            if (crosshairObject != null)
            {
                crosshairObject.SetActive(true);
            }

            GameManager.Instance._isGameStartedEvent += OnGameStarted;

            StartCoroutine(FindLocalPlayer());
            StartCoroutine(FindPowerCore());
        }

        // Update player displays
        if (isConnectedToPlayer && playerHealth != null && weaponController != null)
        {
            UpdateHealthDisplay();
            UpdateAmmoDisplay();
        }

        // Check for destroyed state
        if (isConnectedToPowerCore && powerCoreHealth != null && !powerCoreDestroyed)
        {
            CheckPowerCoreDestroyed();
        }
    }

    void OnGameStarted(bool isStarted)
    {
        if (isStarted)
        {
            if (UIContainer != null)
            {
                UIContainer.SetActive(true);
            }
            if (crosshairObject != null)
            {
                crosshairObject.SetActive(true);
            }
        }
    }

    IEnumerator FindLocalPlayer()
    {
        if (showDebugLogs)
            Debug.Log("HUD: Searching for local player...");

        int attemptCount = 0;
        const int maxAttempts = 20;

        while (!isConnectedToPlayer && attemptCount < maxAttempts)
        {
            attemptCount++;
            yield return new WaitForSeconds(0.5f);

            PlayerControllerNetwork[] players = FindObjectsOfType<PlayerControllerNetwork>();

            foreach (var player in players)
            {
                if (player.IsOwner && player.IsSpawned)
                {
                    playerHealth = player.GetComponent<Health>();
                    weaponController = player.GetComponent<WeaponControllerNetwork>();

                    if (playerHealth == null)
                    {
                        playerHealth = player.GetComponentInChildren<Health>();
                    }

                    if (playerHealth != null && weaponController != null)
                    {
                        isConnectedToPlayer = true;
                        Debug.Log($"✓ HUD CONNECTED to local player: {player.gameObject.name}");

                        UpdateHealthDisplay();
                        UpdateAmmoDisplay();
                        yield break;
                    }
                }
            }
        }

        if (!isConnectedToPlayer)
        {
            Debug.LogError($"HUD: Failed to find local player!");
        }
    }

    /// <summary>
    /// Finds the PowerCore and subscribes to its OnDamaged event
    /// </summary>
    IEnumerator FindPowerCore()
    {
        if (showDebugLogs)
            Debug.Log("HUD: Searching for PowerCore...");

        int attemptCount = 0;
        const int maxAttempts = 30;

        while (!isConnectedToPowerCore && attemptCount < maxAttempts)
        {
            attemptCount++;
            yield return new WaitForSeconds(attemptCount == 1 ? 1.0f : 0.5f);

            GameObject objectiveObject = GameObject.FindGameObjectWithTag("Objective");

            if (objectiveObject != null)
            {
                powerCoreHealth = objectiveObject.GetComponent<Health>();
                powerCoreNetworkObject = objectiveObject.GetComponent<NetworkObject>();

                if (powerCoreHealth != null && powerCoreNetworkObject != null && powerCoreNetworkObject.IsSpawned)
                {
                    // Wait for NetworkVariable to sync
                    yield return new WaitForSeconds(0.5f);

                    float maxHealth = powerCoreHealth.MaxHealth;
                    float currentHealth = powerCoreHealth.CurrentHealth;

                    if (showDebugLogs)
                        Debug.Log($"HUD: PowerCore health check - Current: {currentHealth}, Max: {maxHealth}");

                    if (maxHealth > 0)
                    {
                        isConnectedToPowerCore = true;

                        Debug.Log("HUD: ★★★ Subscribing to PowerCore OnDamaged event ★★★");

                        // 🎯 CHANGEMENT 1: Subscribe to OnDamaged event
                        powerCoreHealth.OnDamaged.AddListener(OnPowerCoreDamaged);

                        Debug.Log($"✓ HUD CONNECTED to PowerCore: {objectiveObject.name} (Health: {currentHealth}/{maxHealth})");

                        // Initial display update
                        UpdatePowerCoreDisplay();

                        yield break;
                    }
                }
            }
        }

        if (!isConnectedToPowerCore)
        {
            Debug.LogError($"HUD: Failed to find PowerCore!");
        }
    }

    /// <summary>
    /// 🎯 CHANGEMENT 2: Called whenever PowerCore takes damage
    /// Uses coroutine to wait for NetworkVariable sync
    /// </summary>
    void OnPowerCoreDamaged(float damage)
    {
        Debug.Log($"██████████████████████████████████████");
        Debug.Log($"HUD: OnPowerCoreDamaged called!");
        Debug.Log($"  damage: {damage}");

        // 🎯 CHANGEMENT 3: Wait for NetworkVariable to sync before updating
        StartCoroutine(WaitForPowerCoreHealthSync());

        Debug.Log($"██████████████████████████████████████");
    }

    /// <summary>
    /// 🎯 CHANGEMENT 4: Coroutine to wait for NetworkVariable sync
    /// Prevents race condition between ClientRpc and NetworkVariable update
    /// </summary>
    IEnumerator WaitForPowerCoreHealthSync()
    {
        if (powerCoreHealth == null) yield break;

        // Wait 1 frame for NetworkVariable to sync
        yield return null;

        Debug.Log($"▼▼▼ PowerCore health synced, updating display ▼▼▼");

        // Now update the display with synced values
        UpdatePowerCoreDisplay();
    }

    void UpdateHealthDisplay()
    {
        if (healthText == null || playerHealth == null)
            return;

        float currentHealth = playerHealth.CurrentHealth;
        float maxHealth = playerHealth.MaxHealth;

        if (maxHealth <= 0) return;

        float healthPercent = currentHealth / maxHealth;

        if (showHealthPercentage)
        {
            healthText.text = $"Health: {Mathf.CeilToInt(healthPercent * 100)}%";
        }
        else
        {
            healthText.text = $"Health: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }

        healthText.color = healthPercent <= lowHealthThreshold ? lowHealthColor : normalHealthColor;
    }

    void UpdateAmmoDisplay()
    {
        if (ammoText == null || reservesText == null || weaponController == null)
            return;

        int currentAmmo = weaponController.CurrentAmmo;
        int maxAmmo = weaponController.MaxAmmo;
        int reserves = weaponController.ReserveAmmo;

        ammoText.text = $"{currentAmmo}";
        reservesText.text = $"/ {reserves}";

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

        if (maxHealth <= 0 && !powerCoreDestroyed)
        {
            powerCoreText.text = "Core: Syncing...";
            powerCoreText.color = Color.gray;
            return;
        }

        float healthPercent = currentHealth / maxHealth;

        // Update text
        powerCoreText.text = $"Core: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";

        // Update color
        if (healthPercent <= criticalPowerCoreThreshold)
        {
            powerCoreText.color = criticalPowerCoreColor;
        }
        else if (healthPercent <= warningPowerCoreThreshold)
        {
            powerCoreText.color = warningPowerCoreColor;
        }
        else
        {
            powerCoreText.color = normalPowerCoreColor;
        }
    }

    void CheckPowerCoreDestroyed()
    {
        if (powerCoreHealth == null || powerCoreDestroyed)
            return;

        if (powerCoreHealth.CurrentHealth <= 0 && powerCoreHealth.MaxHealth > 0)
        {
            powerCoreDestroyed = true;
            ShowGameOver();
        }
    }

    void ShowGameOver()
    {
        Debug.Log("HUD: Showing GAME OVER screen");

        if (powerCoreText != null)
        {
            powerCoreText.text = "CORE DESTROYED";
            powerCoreText.color = Color.red;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (gameOverText != null)
        {
            gameOverText.text = "GAME OVER\n\nPower Core Destroyed";
            gameOverText.color = Color.red;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        // 🎯 CHANGEMENT 5: Unsubscribe from events
        if (powerCoreHealth != null)
        {
            powerCoreHealth.OnDamaged.RemoveListener(OnPowerCoreDamaged);
        }
    }

    public void Reconnect()
    {
        isConnectedToPlayer = false;
        playerHealth = null;
        weaponController = null;

        if (showDebugLogs)
            Debug.Log("HUD: Reconnecting to player...");

        StartCoroutine(FindLocalPlayer());
    }

    public void ReconnectPowerCore()
    {
        // 🎯 CHANGEMENT 6: Unsubscribe before reconnecting
        if (powerCoreHealth != null)
        {
            powerCoreHealth.OnDamaged.RemoveListener(OnPowerCoreDamaged);
        }

        isConnectedToPowerCore = false;
        powerCoreHealth = null;
        powerCoreNetworkObject = null;
        powerCoreDestroyed = false;

        if (showDebugLogs)
            Debug.Log("HUD: Reconnecting to PowerCore...");

        StartCoroutine(FindPowerCore());
    }

    public bool IsConnectedToPlayer => isConnectedToPlayer;
    public bool IsConnectedToPowerCore => isConnectedToPowerCore;
}