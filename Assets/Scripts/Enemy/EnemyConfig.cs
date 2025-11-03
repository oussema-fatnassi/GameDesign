using UnityEngine;

/// <summary>
/// ScriptableObject to store enemy configuration values
/// Create via: Assets > Create > Enemies > Enemy Config
/// </summary>
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Enemies/Enemy Config", order = 1)]
public class EnemyConfig : ScriptableObject
{
    [Header("Enemy Info")]
    [Tooltip("Enemy type name for display")]
    public string enemyName = "Scout";
    
    [Tooltip("Enemy description")]
    [TextArea(2, 4)]
    public string description = "Basic melee enemy";
    
    [Header("Health")]
    [Tooltip("Maximum health points")]
    [SerializeField] private float maxHealth = 50f;
    
    [Header("Movement")]
    [Tooltip("Movement speed in units per second")]
    [SerializeField] private float moveSpeed = 3f;
    
    [Tooltip("How close to get before attacking (meters)")]
    [SerializeField] private float attackRange = 2f;
    
    [Tooltip("How close to get to objective before attacking it")]
    [SerializeField] private float objectiveAttackRange = 3f;
    
    [Header("Combat")]
    [Tooltip("Damage dealt per attack")]
    [SerializeField] private float attackDamage = 10f;
    
    [Tooltip("Time between attacks in seconds")]
    [SerializeField] private float attackCooldown = 1.5f;
    
    [Tooltip("Can this enemy attack players?")]
    [SerializeField] private bool canAttackPlayers = true;
    
    [Tooltip("Can this enemy attack the objective?")]
    [SerializeField] private bool canAttackObjective = true;
    
    [Header("Behavior")]
    [Tooltip("Primary target priority")]
    [SerializeField] private TargetPriority targetPriority = TargetPriority.Objective;
    
    [Tooltip("Detection range for players (0 = always heads to objective)")]
    [SerializeField] private float playerDetectionRange = 10f;
    
    [Header("Rewards")]
    [Tooltip("Points awarded for killing this enemy")]
    [SerializeField] private int killPoints = 10;
    
    // Public properties
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float AttackRange => attackRange;
    public float ObjectiveAttackRange => objectiveAttackRange;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public bool CanAttackPlayers => canAttackPlayers;
    public bool CanAttackObjective => canAttackObjective;
    public TargetPriority Priority => targetPriority;
    public float PlayerDetectionRange => playerDetectionRange;
    public int KillPoints => killPoints;
}

/// <summary>
/// Enemy targeting priority
/// </summary>
public enum TargetPriority
{
    Objective,      // Always goes for objective
    NearestPlayer,  // Attacks nearest player
    ClosestThreat,  // Attacks whoever shot them last
    Mixed           // Sometimes objective, sometimes players
}