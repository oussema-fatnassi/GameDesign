using UnityEngine;

/// <summary>
/// ScriptableObject to store player configuration values
/// Create via: Assets > Create > Player > Player Config
/// </summary>
[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Player/Player Config", order = 1)]
public class PlayerConfig : ScriptableObject
{
    [Header("Movement Settings")]
    [Tooltip("Player movement speed in units per second")]
    [SerializeField] private float speed = 5f;
    
    [Tooltip("Jump force applied when jumping")]
    [SerializeField] private float jumpForce = 5f;
    
    [Header("Look Sensitivity")]
    [Tooltip("Horizontal look sensitivity (mouse/stick)")]
    [SerializeField] private float horizontalSensitivity = 1f;
    
    [Tooltip("Vertical look sensitivity (mouse/stick)")]
    [SerializeField] private float verticalSensitivity = 1f;
    
    [Header("Advanced Settings")]
    [Tooltip("Maximum vertical look angle (degrees)")]
    [SerializeField] private float verticalLookClamp = 80f;
    
    [Tooltip("Ground check distance")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    
    // Public properties
    public float Speed => speed;
    public float CurrentJumpForce => jumpForce;
    public float CurrentHorizontalSensitivity => horizontalSensitivity;
    public float CurrentVerticalSensitivity => verticalSensitivity;
    public float VerticalLookClamp => verticalLookClamp;
    public float GroundCheckDistance => groundCheckDistance;
}