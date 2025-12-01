using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Basic enemy AI with NavMesh pathfinding
/// Requires: NavMeshAgent, Health, EnemyConfig
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NetworkObject))]
public class EnemyAI : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private EnemyConfig enemyConfig;
    
    [Header("References")]
    [SerializeField] private Transform objectiveTarget; // Power core or objective
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Components
    private NavMeshAgent agent;
    private Health health;
    
    // Targeting
    private Transform currentTarget;
    private bool isAttacking;
    private float lastAttackTime;
    
    // State
    private EnemyState currentState = EnemyState.Moving;

    public Health Health { get { return health; } }
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        
        // Subscribe to death event
        health.OnDeath.AddListener(OnDeath);
    }
    
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (enemyConfig == null)
        {
            Debug.LogError($"EnemyConfig not assigned on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        // Configure NavMeshAgent from config
        agent.speed = enemyConfig.MoveSpeed;
        agent.stoppingDistance = enemyConfig.AttackRange;
        
        // Configure Health from config
        // Health component needs to be initialized with max health
        
        // Find objective if not assigned
        if (objectiveTarget == null)
        {
            GameObject objective = GameObject.FindGameObjectWithTag("Objective");
            if (objective != null)
            {
                objectiveTarget = objective.transform;
            }
        }
        
        // Start with objective as target
        currentTarget = objectiveTarget;
    }
    
    void Update()
    {
        if (!IsServer) return;
        if (health.IsDead) return;
        
        // Update behavior based on state
        switch (currentState)
        {
            case EnemyState.Moving:
                UpdateMovement();
                break;
            case EnemyState.Attacking:
                UpdateAttacking();
                break;
        }
    }
    
    void UpdateMovement()
    {
        // Check for player detection based on priority
        if (enemyConfig.Priority == TargetPriority.NearestPlayer || 
            enemyConfig.Priority == TargetPriority.Mixed)
        {
            Transform nearestPlayer = FindNearestPlayer();
            if (nearestPlayer != null)
            {
                float distance = Vector3.Distance(transform.position, nearestPlayer.position);
                if (distance <= enemyConfig.PlayerDetectionRange)
                {
                    currentTarget = nearestPlayer;
                }
                else
                {
                    currentTarget = objectiveTarget;
                }
            }
        }
        
        // Move toward target
        if (currentTarget != null)
        {
            agent.SetDestination(currentTarget.position);
            
            // Check if in attack range
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
            float attackRange = IsPlayerTarget() ? enemyConfig.AttackRange : enemyConfig.ObjectiveAttackRange;
            
            if (distanceToTarget <= attackRange)
            {
                currentState = EnemyState.Attacking;
                agent.isStopped = true;
            }
        }
    }
    
    void UpdateAttacking()
    {
        if (currentTarget == null)
        {
            currentState = EnemyState.Moving;
            agent.isStopped = false;
            return;
        }
        
        // Face the target
        Vector3 direction = (currentTarget.position - transform.position).normalized;
        direction.y = 0; // Keep rotation horizontal
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
        
        // Check if still in range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        float attackRange = IsPlayerTarget() ? enemyConfig.AttackRange : enemyConfig.ObjectiveAttackRange;
        
        if (distanceToTarget > attackRange * 1.2f) // 1.2x for hysteresis
        {
            currentState = EnemyState.Moving;
            agent.isStopped = false;
            return;
        }
        
        // Attack on cooldown
        if (Time.time >= lastAttackTime + enemyConfig.AttackCooldown)
        {
            Attack();
            lastAttackTime = Time.time;
        }
    }
    
    void Attack()
    {
        if (currentTarget == null)
            return;
        
        // Get Health component from target
        Health targetHealth = currentTarget.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(enemyConfig.AttackDamage);
            
            if (showDebugInfo)
            {
                Debug.Log($"{gameObject.name} attacked {currentTarget.name} for {enemyConfig.AttackDamage} damage!");
            }
        }
        
        // TODO: Add attack animation trigger here
        // TODO: Add attack sound effect here
    }
    
    bool IsPlayerTarget()
    {
        if (currentTarget == null)
            return false;
        
        return currentTarget.CompareTag("Player");
    }
    
    Transform FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform nearest = null;
        float nearestDistance = Mathf.Infinity;
        
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = player.transform;
            }
        }
        
        return nearest;
    }
    
    void OnDeath()
    {
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name} died!");
        }
        
        // Stop movement
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        
        // TODO: Add death animation
        // TODO: Add death sound
        // TODO: Spawn death particles
        
        // Destroy after delay
        Destroy(gameObject, 2f);
    }
    
    // Public method for damage feedback (optional)
    public void OnTakeDamage(float damage)
    {
        // Could change target to whoever damaged us
        if (enemyConfig.Priority == TargetPriority.ClosestThreat)
        {
            // Find who shot us and target them
            // This requires tracking who dealt damage (implement in Health.cs if needed)
        }
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (enemyConfig == null)
            return;
        
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enemyConfig.AttackRange);
        
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyConfig.PlayerDetectionRange);
        
        // Draw line to current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}

/// <summary>
/// Enemy AI states
/// </summary>
public enum EnemyState
{
    Moving,
    Attacking,
    Dead
}