using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Wave system manager - Runs ONLY on HOST
/// Spawns enemies at 4 spawn points with sub-wave system
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject scoutPrefab;
    [SerializeField] private GameObject runnerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints; // 4 spawn points in the world

    [Header("Wave Configuration")]
    [SerializeField] private List<WaveConfig> waves = new List<WaveConfig>();
    [SerializeField] private float timeBetweenWaves = 10f;

    [Header("Test Mode")]
    [SerializeField] private bool useTestMode = false;
    [SerializeField] private WaveConfig testWave;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // State
    private int currentWaveIndex = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool isWaveActive = false;
    private bool isGameActive = false;
    private Dictionary<EEnemyType, GameObject> prefabDictionary;

    // Events
    public System.Action<int, int> OnWaveStart; // (waveNumber, totalEnemies)
    public System.Action<string> OnSubWaveStart; // (subWaveName)
    public System.Action<int> OnEnemyCountChanged; // (remainingEnemies)
    public System.Action OnWaveComplete;
    public System.Action OnAllWavesComplete;

    void Awake()
    {
        // Build prefab dictionary
        prefabDictionary = new Dictionary<EEnemyType, GameObject>
        {
            { EEnemyType.Scout, scoutPrefab },
            { EEnemyType.Runner, runnerPrefab }
        };
    }

    void Start()
    {
        // Validation
        if (spawnPoints == null || spawnPoints.Length != 4)
        {
            Debug.LogError("WaveManager needs exactly 4 spawn points!");
            enabled = false;
            return;
        }

        ValidatePrefabs();
    }

    void ValidatePrefabs()
    {
        if (scoutPrefab == null)
        {
            Debug.LogError("Scout prefab not assigned!");
        }

        if (runnerPrefab == null)
        {
            Debug.LogError("Runner prefab not assigned!");
        }
    }

    /// <summary>
    /// Start the wave system
    /// </summary>
    public void StartWaveSystem()
    {
        // Only HOST can start
        if (GameManager.Instance.ClientType != EClientType.HOST)
        {
            Debug.LogWarning("Only HOST can start wave system!");
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("NetworkManager not ready!");
            return;
        }

        // Test mode
        if (useTestMode && testWave != null)
        {
            if (showDebugLogs)
                Debug.Log("=== STARTING TEST MODE ===");

            SpawnWave(testWave, 0);
            return;
        }

        // Normal mode
        if (waves.Count == 0)
        {
            Debug.LogError("No waves configured!");
            return;
        }

        isGameActive = true;
        currentWaveIndex = 0;

        if (showDebugLogs)
            Debug.Log("=== WAVE SYSTEM STARTED ===");

        StartCoroutine(StartNextWave());
    }

    /// <summary>
    /// Start next wave
    /// </summary>
    IEnumerator StartNextWave()
    {
        // Check if all waves completed
        if (currentWaveIndex >= waves.Count)
        {
            if (showDebugLogs)
                Debug.Log("=== ALL WAVES COMPLETED! VICTORY! ===");

            OnAllWavesComplete?.Invoke();
            isGameActive = false;
            yield break;
        }

        // Wait between waves (except first)
        if (currentWaveIndex > 0)
        {
            if (showDebugLogs)
                Debug.Log($"Next wave in {timeBetweenWaves} seconds...");

            yield return new WaitForSeconds(timeBetweenWaves);
        }

        // Start wave
        SpawnWave(waves[currentWaveIndex], currentWaveIndex);
    }

    /// <summary>
    /// Spawn a wave (with all its sub-waves)
    /// </summary>
    void SpawnWave(WaveConfig waveConfig, int waveNumber)
    {
        isWaveActive = true;
        activeEnemies.Clear();

        int totalEnemies = waveConfig.GetTotalEnemyCount();

        if (showDebugLogs)
        {
            Debug.Log($"=== {waveConfig.waveName} Started ===");
            Debug.Log($"Total Enemies: {totalEnemies}");
            Debug.Log($"Description: {waveConfig.waveDescription}");
        }

        OnWaveStart?.Invoke(waveNumber + 1, totalEnemies);

        // Start all sub-waves
        foreach (var subWave in waveConfig.subWaves)
        {
            StartCoroutine(SpawnSubWave(subWave));
        }
    }

    /// <summary>
    /// Spawn a sub-wave after its delay
    /// </summary>
    IEnumerator SpawnSubWave(WaveConfig.SubWave subWave)
    {
        // Wait for delay
        if (subWave.spawnDelay > 0)
        {
            yield return new WaitForSeconds(subWave.spawnDelay);
        }

        if (showDebugLogs)
            Debug.Log($"--- {subWave.subWaveName} Started ---");

        OnSubWaveStart?.Invoke(subWave.subWaveName);

        // Spawn each enemy group
        foreach (var group in subWave.enemyGroups)
        {
            StartCoroutine(SpawnEnemyGroup(group));
        }
    }

    /// <summary>
    /// Spawn a group of enemies with interval
    /// </summary>
    IEnumerator SpawnEnemyGroup(WaveConfig.EnemyGroup group)
    {
        // Get prefab
        if (!prefabDictionary.TryGetValue(group.enemyType, out GameObject prefab))
        {
            Debug.LogError($"No prefab for enemy type: {group.enemyType}");
            yield break;
        }

        // Spawn enemies
        for (int i = 0; i < group.count; i++)
        {
            SpawnEnemy(prefab, group.enemyType);

            // Wait between spawns
            if (i < group.count - 1 && group.spawnInterval > 0)
            {
                yield return new WaitForSeconds(group.spawnInterval);
            }
        }
    }

    /// <summary>
    /// Spawn a single enemy at random spawn point
    /// </summary>
    void SpawnEnemy(GameObject enemyPrefab, EEnemyType enemyType)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab is null!");
            return;
        }

        // Get RANDOM spawn point from the 4 available
        Transform spawnPoint = spawnPoints[Random.Range(0, 4)];

        // Small random offset to avoid stacking
        Vector3 randomOffset = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        );

        Vector3 spawnPosition = spawnPoint.position + randomOffset;
        Quaternion spawnRotation = spawnPoint.rotation;

        // Instantiate
        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
        enemy.name = $"{enemyType}_{activeEnemies.Count}";

        // Network spawn
        NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            activeEnemies.Add(enemy);

            // Subscribe to death
            Health health = enemy.GetComponent<Health>();
            if (health != null)
            {
                health.OnDeath.AddListener(() => OnEnemyDied(enemy));
            }

            if (showDebugLogs)
                Debug.Log($"Spawned {enemyType} at spawn point {spawnPoint.name}");
        }
        else
        {
            Debug.LogError($"Enemy prefab missing NetworkObject!");
            Destroy(enemy);
        }
    }

    /// <summary>
    /// Called when enemy dies
    /// </summary>
    void OnEnemyDied(GameObject enemy)
    {
        activeEnemies.Remove(enemy);

        if (showDebugLogs)
            Debug.Log($"Enemy killed. Remaining: {activeEnemies.Count}");

        OnEnemyCountChanged?.Invoke(activeEnemies.Count);

        // Check if wave complete
        if (isWaveActive && activeEnemies.Count == 0)
        {
            WaveComplete();
        }
    }

    /// <summary>
    /// Wave completed
    /// </summary>
    void WaveComplete()
    {
        isWaveActive = false;

        if (showDebugLogs)
            Debug.Log($"=== Wave {currentWaveIndex + 1} Complete! ===");

        OnWaveComplete?.Invoke();

        // Test mode: stop
        if (useTestMode)
        {
            if (showDebugLogs)
                Debug.Log("Test mode complete.");
            return;
        }

        // Next wave
        currentWaveIndex++;
        StartCoroutine(StartNextWave());
    }

    /// <summary>
    /// Stop wave system
    /// </summary>
    public void StopWaveSystem()
    {
        isGameActive = false;
        StopAllCoroutines();

        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        activeEnemies.Clear();

        if (showDebugLogs)
            Debug.Log("Wave system stopped.");
    }

    // Public getters
    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => waves.Count;
    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsWaveActive => isWaveActive;
}