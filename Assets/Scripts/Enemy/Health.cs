using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health component for players, enemies, and destructible objects
/// Handles networked health with proper server authority
/// FIXED: Proper despawn handling for networked objects
/// </summary>
public class Health : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    private NetworkVariable<float> _currentHealth = new NetworkVariable<float>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Events")]
    public UnityEvent OnDeath; // Called when health reaches 0
    public UnityEvent<float> OnDamaged; // Called when damage is taken

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // State
    private bool _isDead = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"═══ [Health-{gameObject.name}] OnNetworkSpawn ═══");
        Debug.Log($"  IsServer: {IsServer}");
        Debug.Log($"  IsClient: {IsClient}");
        Debug.Log($"  IsSpawned: {IsSpawned}");
        Debug.Log($"  NetworkObjectId: {NetworkObjectId}");

        // Initialize health on server
        if (IsServer)
        {
            _currentHealth.Value = _maxHealth;
            Debug.Log($"  [SERVER] Set _currentHealth.Value = {_maxHealth}");
        }

        // Subscribe to health changes on all clients
        _currentHealth.OnValueChanged += OnHealthChanged;

        Debug.Log($"  _currentHealth.Value RIGHT NOW = {_currentHealth.Value}");
        Debug.Log($"═══════════════════════════════════");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _currentHealth.OnValueChanged -= OnHealthChanged;
    }

    /// <summary>
    /// Called when health value changes (runs on all clients)
    /// </summary>
    private void OnHealthChanged(float previousValue, float newValue)
    {
        Debug.Log($"►►► [{(IsServer ? "SERVER" : "CLIENT")}] {gameObject.name} OnHealthChanged: {previousValue} -> {newValue}");

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} health changed: {previousValue} -> {newValue}");
        }

        // Check for death
        if (newValue <= 0 && !_isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// ServerRPC: Applies damage to this object (called by clients)
    /// Used when players shoot enemies or other networked damage sources
    /// </summary>
    [Rpc(SendTo.Server)]
    public void TakeDamageServerRpc(float damage)
    {
        Debug.Log($"►►► TakeDamageServerRpc called on {gameObject.name}");
        Debug.Log($"    IsServer: {IsServer}");
        Debug.Log($"    _isDead: {_isDead}");
        Debug.Log($"    damage: {damage}");

        if (_isDead || !IsServer)
            return;

        float oldValue = _currentHealth.Value;
        _currentHealth.Value -= damage;

        Debug.Log($"►►► [SERVER] {gameObject.name} Health changed: {oldValue} -> {_currentHealth.Value}");

        if (showDebugLogs)
        {
            Debug.Log($"[ServerRpc] {gameObject.name} took {damage} damage. Health: {_currentHealth.Value}/{_maxHealth}");
        }

        // Trigger damaged event on server
        OnDamaged?.Invoke(damage);

        // Notify all clients about damage taken
        Debug.Log($"►►► [SERVER] Calling NotifyDamageTakenClientRpc({damage})");
        NotifyDamageTakenClientRpc(damage);
    }

    /// <summary>
    /// Direct damage method for server-side calls (enemies attacking, etc.)
    /// This is called by EnemyAI.Attack() which already runs on server
    /// </summary>
    public void TakeDamage(float damage)
    {
        Debug.Log($"►►► TakeDamage called on {gameObject.name}");
        Debug.Log($"    IsServer: {IsServer}");
        Debug.Log($"    _isDead: {_isDead}");
        Debug.Log($"    damage: {damage}");

        if (!IsServer)
        {
            Debug.LogWarning($"TakeDamage called on client for {gameObject.name}! Use TakeDamageServerRpc instead.");
            return;
        }

        if (_isDead)
            return;

        float oldValue = _currentHealth.Value;
        _currentHealth.Value -= damage;

        Debug.Log($"►►► [SERVER] {gameObject.name} Health changed: {oldValue} -> {_currentHealth.Value}");

        if (showDebugLogs)
        {
            Debug.Log($"[Direct] {gameObject.name} took {damage} damage. Health: {_currentHealth.Value}/{_maxHealth}");
        }

        // Trigger damaged event
        OnDamaged?.Invoke(damage);

        // Notify all clients
        Debug.Log($"►►► [SERVER] Calling NotifyDamageTakenClientRpc({damage})");
        NotifyDamageTakenClientRpc(damage);
    }

    /// <summary>
    /// ClientRPC: Notifies all clients that damage was taken
    /// Use this for visual/audio feedback
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void NotifyDamageTakenClientRpc(float damage)
    {
        Debug.Log($"►►► [{(IsServer ? "SERVER" : "CLIENT")}] NotifyDamageTakenClientRpc received: damage={damage}");
        Debug.Log($"    Current _currentHealth.Value = {_currentHealth.Value}");

        // This runs on all clients
        // OnDamaged event is already invoked on server, so only invoke on clients
        if (!IsServer)
        {
            Debug.Log($"►►► [CLIENT] Invoking OnDamaged event with {damage}");
            OnDamaged?.Invoke(damage);
        }
    }

    /// <summary>
    /// Heals this object (server-side only)
    /// </summary>
    public void Heal(float amount)
    {
        if (!IsServer || _isDead)
            return;

        _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, _maxHealth);

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} healed {amount}. Health: {_currentHealth.Value}/{_maxHealth}");
        }
    }

    /// <summary>
    /// ServerRPC: Heals this object (called by clients)
    /// </summary>
    [Rpc(SendTo.Server)]
    public void HealServerRpc(float amount)
    {
        Heal(amount);
    }

    /// <summary>
    /// Handles death (runs on all clients via OnHealthChanged callback)
    /// 🎯 FIXED: Proper networked despawn - only server despawns, clients receive sync
    /// </summary>
    void Die()
    {
        if (_isDead)
            return;

        _isDead = true;

        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} died!");
        }

        // Trigger death event (runs on all clients)
        OnDeath?.Invoke();

        // 🎯 CRITICAL FIX: Only server handles destruction of networked objects
        if (IsServer && NetworkObject != null)
        {
            // NetworkObject.Despawn with destroy=true will:
            // 1. Despawn the object on the server
            // 2. Automatically notify all clients to despawn
            // 3. Destroy the GameObject on all machines
            Debug.Log($"[SERVER] Despawning networked object: {gameObject.name}");

            // Delay slightly to allow death effects/animations
            StartCoroutine(DespawnAfterDelay(0f));
        }
        else if (IsServer && NetworkObject == null)
        {
            // Non-networked object (rare case)
            Debug.Log($"[SERVER] Destroying non-networked object: {gameObject.name}");
            Destroy(gameObject, 0f);
        }
        else
        {
           
            //Destroy(gameObject, 0f);
        }
        // 🎯 IMPORTANT: Clients do NOTHING here
        // They will receive the despawn command from the server automatically
    }

    /// <summary>
    /// Coroutine to despawn after a delay (for death animations)
    /// </summary>
    private System.Collections.IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            // destroy=true ensures the GameObject is destroyed on all clients
            NetworkObject.Despawn(destroy: true);
            Debug.Log($"[SERVER] {gameObject.name} despawned and destroyed");
        }
    }

    // Public getters
    public float CurrentHealth
    {
        get
        {
            float val = _currentHealth.Value;
            Debug.Log($"►►► CurrentHealth getter called on {gameObject.name}: returning {val}");
            return val;
        }
    }

    public float MaxHealth => _maxHealth;
    public float HealthPercentage => _currentHealth.Value / _maxHealth;
    public bool IsDead => _isDead;
}