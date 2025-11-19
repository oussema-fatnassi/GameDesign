using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller handling movement, looking, and jumping
/// This script should be added to the ownerOnlyScripts array in PlayerInitializer
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerController : MonoBehaviour
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
    }

    void OnEnable()
    {
        // Enable input actions
        moveAction = inputActions.Player.Move;
        moveAction.Enable();

        lookAction = inputActions.Player.Look;
        lookAction.Enable();

        jumpAction = inputActions.Player.Jump;
        jumpAction.performed += OnJump;
        jumpAction.Enable();

        sprintAction = inputActions.Player.Sprint;
        sprintAction?.Enable();
    }

    void OnDisable()
    {
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
        // Configure rigidbody
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = true;

        // Initialize rotation values
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = GetCameraTransform().localEulerAngles.x;

        if (verticalRotation > 180f)
        {
            verticalRotation -= 360f;
        }
    }

    void Update()
    {
        // Handle looking
        ProcessLookInput();
        ApplyRotation();
    }

    void FixedUpdate()
    {
        // Handle movement
        isGrounded = CheckGrounded();
        ProcessMovement();
    }

    void ProcessLookInput()
    {
        Vector2 rawLookInput = lookAction.ReadValue<Vector2>();

        if (Mathf.Abs(rawLookInput.x) < inputThreshold) rawLookInput.x = 0f;
        if (Mathf.Abs(rawLookInput.y) < inputThreshold) rawLookInput.y = 0f;

        smoothedLookInput = Vector2.SmoothDamp(
            smoothedLookInput,
            rawLookInput,
            ref currentLookVelocity,
            lookSmoothTime
        );

        float horizontalDelta = smoothedLookInput.x * playerConfig.CurrentHorizontalSensitivity * Time.deltaTime;
        float verticalDelta = smoothedLookInput.y * playerConfig.CurrentVerticalSensitivity * Time.deltaTime;

        horizontalRotation += horizontalDelta;
        verticalRotation -= verticalDelta;

        verticalRotation = Mathf.Clamp(verticalRotation, -playerConfig.VerticalLookClamp, playerConfig.VerticalLookClamp);
    }

    void ApplyRotation()
    {
        Quaternion bodyRotation = Quaternion.Euler(0f, horizontalRotation, 0f);
        rb.MoveRotation(bodyRotation);

        Transform camTransform = GetCameraTransform();
        camTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void ProcessMovement()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        if (moveInput.magnitude < inputThreshold)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (right * moveInput.x + forward * moveInput.y).normalized;
        Vector3 targetVelocity = moveDirection * playerConfig.Speed;

        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.AddForce(Vector3.up * playerConfig.CurrentJumpForce, ForceMode.Impulse);
        }
    }

    bool CheckGrounded()
    {
        Vector3 origin = transform.position;
        float radius = col.bounds.extents.x * 0.9f;
        float distance = col.bounds.extents.y + playerConfig.GroundCheckDistance;

        return Physics.SphereCast(origin, radius, Vector3.down, out _, distance, groundMask);
    }

    Transform GetCameraTransform()
    {
        if (cameraHolder != null)
            return cameraHolder;

        if (playerCamera != null)
            return playerCamera.transform;

        Debug.LogError("No camera or camera holder assigned!");
        return transform;
    }

    // Public getters
    public bool IsGrounded => isGrounded;
    public Vector2 MoveInput => moveInput;
    public Rigidbody Rigidbody => rb;

    void OnDrawGizmosSelected()
    {
        if (col == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position;
        float radius = col.bounds.extents.x * 0.9f;
        float distance = col.bounds.extents.y + (playerConfig != null ? playerConfig.GroundCheckDistance : 0.2f);

        Gizmos.DrawWireSphere(origin + Vector3.down * distance, radius);
    }
}