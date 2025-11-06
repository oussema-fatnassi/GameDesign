using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health component for players, enemies, and destructible objects
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("Events")]
    public UnityEvent<float> OnDamaged; // Called when taking damage
    public UnityEvent OnDeath; // Called when health reaches 0
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // State
    private bool isDead = false;
    
    void Awake()
    {
        currentHealth = maxHealth;
    }
    
    /// <summary>
    /// Applies damage to this object
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead)
            return;
        
        currentHealth -= damage;
        
        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
        }
        
        // Trigger damaged event
        OnDamaged?.Invoke(damage);
        
        // Check if dead
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Heals this object
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead)
            return;
        
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        
        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} healed {amount}. Health: {currentHealth}/{maxHealth}");
        }
    }
    
    /// <summary>
    /// Handles death
    /// </summary>
    void Die()
    {
        if (isDead)
            return;
        
        isDead = true;
        currentHealth = 0;
        
        if (showDebugLogs)
        {
            Debug.Log($"{gameObject.name} died!");
        }
        
        // Trigger death event
        OnDeath?.Invoke();
        
        // Optional: Destroy after delay
        Destroy(gameObject, 2f);
    }
    
    // Public getters
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsDead => isDead;
}