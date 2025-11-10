using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller handling movement, looking, and jumping
/// Requires: Rigidbody, Collider, PlayerConfig ScriptableObject
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private PlayerConfig playerConfig;
    
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraHolder;
    
    [Header("Input Settings")]
    [SerializeField] private float lookSmoothTime = 0.1f;
    [SerializeField] private float inputThreshold = 0.01f;
    
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = -1;
    
    // Components
    private Rigidbody rb;
    private Collider col;
    private PlayerInputs inputActions;
    
    // Input actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    
    // Movement
    private Vector2 moveInput;
    private bool isGrounded;
    
    // Look rotation
    private float horizontalRotation;
    private float verticalRotation;
    private Vector2 smoothedLookInput;
    private Vector2 currentLookVelocity;
    
    void Awake()
    {
        // Get components
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        
        // Initialize input system
        inputActions = new PlayerInputs();
        
        // Configure rigidbody
        rb.freezeRotation = true; // Prevent physics from rotating the player
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Lock cursor
        //TODO : Change this later to be dynamic based on game state, GameManager?
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        if (!IsOwner) return;

        // Enable input actions
        moveAction = inputActions.Player.Move;
        moveAction.Enable();
        
        lookAction = inputActions.Player.Look;
        lookAction.Enable();
        
        jumpAction = inputActions.Player.Jump;
        jumpAction.performed += OnJump;
        jumpAction.Enable();
        
        // Optional: Sprint action
        sprintAction = inputActions.Player.Sprint;
        sprintAction?.Enable();
    }

    void OnDisable()
    {
        if (!IsOwner) return;

        // Disable input actions
        moveAction?.Disable();
        lookAction?.Disable();
        
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJump;
            jumpAction.Disable();
        }
        
        sprintAction?.Disable();
    }

    void Start()
    {
        if (!IsOwner) return;

        // Initialize rotation values
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = GetCameraTransform().localEulerAngles.x;
        
        // Normalize vertical rotation to -180 to 180 range
        if (verticalRotation > 180f)
        {
            verticalRotation -= 360f;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        // Handle looking (needs to be in Update for smooth camera movement)
        ProcessLookInput();
        ApplyRotation();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        // Handle movement (in FixedUpdate for physics)
        isGrounded = CheckGrounded();
        ProcessMovement();
    }

    /// <summary>
    /// Processes look input with smoothing and sensitivity
    /// </summary>
    void ProcessLookInput()
    {
        if (!IsOwner) return;

        // Read raw input
        Vector2 rawLookInput = lookAction.ReadValue<Vector2>();
        
        // Apply deadzone threshold
        if (Mathf.Abs(rawLookInput.x) < inputThreshold) rawLookInput.x = 0f;
        if (Mathf.Abs(rawLookInput.y) < inputThreshold) rawLookInput.y = 0f;

        // Smooth the input
        smoothedLookInput = Vector2.SmoothDamp(
            smoothedLookInput,
            rawLookInput,
            ref currentLookVelocity,
            lookSmoothTime
        );

        // Apply sensitivity and delta time
        float horizontalDelta = smoothedLookInput.x * playerConfig.CurrentHorizontalSensitivity * Time.deltaTime;
        float verticalDelta = smoothedLookInput.y * playerConfig.CurrentVerticalSensitivity * Time.deltaTime;
        
        // Update rotation values
        horizontalRotation += horizontalDelta;
        verticalRotation -= verticalDelta; // Inverted for natural camera movement
        
        // Clamp vertical rotation
        verticalRotation = Mathf.Clamp(verticalRotation, -playerConfig.VerticalLookClamp, playerConfig.VerticalLookClamp);
    }

    /// <summary>
    /// Applies calculated rotation to player body and camera
    /// </summary>
    void ApplyRotation()
    {
        if (!IsOwner) return;

        // Rotate player body horizontally
        Quaternion bodyRotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        rb.MoveRotation(bodyRotation);
        
        // Rotate camera vertically
        Transform camTransform = GetCameraTransform();
        camTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    /// <summary>
    /// Processes movement input and applies velocity
    /// </summary>
    void ProcessMovement()
    {
        if (!IsOwner) return;

        // Read movement input
        moveInput = moveAction.ReadValue<Vector2>();
        
        // Apply deadzone
        if (moveInput.magnitude < inputThreshold)
        {
            // Stop horizontal movement but keep vertical velocity (gravity/jumping)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }
        
        // Calculate movement direction relative to player rotation
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        // Project onto horizontal plane
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        // Calculate desired movement direction
        Vector3 moveDirection = (right * moveInput.x + forward * moveInput.y).normalized;
        
        // Apply movement speed
        Vector3 targetVelocity = moveDirection * playerConfig.Speed;
        
        // Apply velocity while preserving vertical component
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    /// <summary>
    /// Handles jump input
    /// </summary>
    void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (isGrounded)
        {
            rb.AddForce(Vector3.up * playerConfig.CurrentJumpForce, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Checks if player is on the ground using spherecast
    /// </summary>
    bool CheckGrounded()
    {
        Vector3 origin = transform.position;
        float radius = col.bounds.extents.x * 0.9f; // Slightly smaller than collider
        float distance = col.bounds.extents.y + playerConfig.GroundCheckDistance;
        
        return Physics.SphereCast(origin, radius, Vector3.down, out _, distance, groundMask);
    }

    /// <summary>
    /// Gets the camera transform (either from cameraHolder or playerCamera)
    /// </summary>
    Transform GetCameraTransform()
    {
        if (cameraHolder != null)
            return cameraHolder;
        
        if (playerCamera != null)
            return playerCamera.transform;
        
        Debug.LogError("No camera or camera holder assigned!");
        return transform;
    }

    // Public getters for other scripts
    public bool IsGrounded => isGrounded;
    public Vector2 MoveInput => moveInput;
    public Rigidbody Rigidbody => rb;

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (col == null) return;
        
        // Draw ground check sphere
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position;
        float radius = col.bounds.extents.x * 0.9f;
        float distance = col.bounds.extents.y + (playerConfig != null ? playerConfig.GroundCheckDistance : 0.2f);
        
        Gizmos.DrawWireSphere(origin + Vector3.down * distance, radius);
    }
}