using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health component for players, enemies, and destructible objects
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

        // Initialize health on server
        if (IsServer)
        {
            _currentHealth.Value = _maxHealth;
        }

        // Subscribe to health changes on all clients
        _currentHealth.OnValueChanged += OnHealthChanged;
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
        if (_isDead || !IsServer)
            return;

        _currentHealth.Value -= damage;

        if (showDebugLogs)
        {
            Debug.Log($"[ServerRpc] {gameObject.name} took {damage} damage. Health: {_currentHealth.Value}/{_maxHealth}");
        }

        // Trigger damaged event on server
        OnDamaged?.Invoke(damage);

        // Notify all clients about damage taken
        NotifyDamageTakenClientRpc(damage);
    }

    /// <summary>
    /// Direct damage method for server-side calls (enemies attacking, etc.)
    /// This is called by EnemyAI.Attack() which already runs on server
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"TakeDamage called on client for {gameObject.name}! Use TakeDamageServerRpc instead.");
            return;
        }

        if (_isDead)
            return;

        _currentHealth.Value -= damage;

        if (showDebugLogs)
        {
            Debug.Log($"[Direct] {gameObject.name} took {damage} damage. Health: {_currentHealth.Value}/{_maxHealth}");
        }

        // Trigger damaged event
        OnDamaged?.Invoke(damage);

        // Notify all clients
        NotifyDamageTakenClientRpc(damage);
    }

    /// <summary>
    /// ClientRPC: Notifies all clients that damage was taken
    /// Use this for visual/audio feedback
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void NotifyDamageTakenClientRpc(float damage)
    {
        // This runs on all clients
        // OnDamaged event is already invoked on server, so only invoke on clients
        if (!IsServer)
        {
            OnDamaged?.Invoke(damage);
        }

        // Add visual/audio feedback here if needed
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

        // Only server handles destruction
        if (IsServer)
        {
            // Delay destruction to allow death animations/effects
            Destroy(gameObject, 2f);
        }
    }

    // Public getters
    public float CurrentHealth => _currentHealth.Value;
    public float MaxHealth => _maxHealth;
    public float HealthPercentage => _currentHealth.Value / _maxHealth;
    public bool IsDead => _isDead;
}