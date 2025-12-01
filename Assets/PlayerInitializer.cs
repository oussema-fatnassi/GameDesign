using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Initializes player components based on network ownership
/// Centralizes IsOwner checks for cleaner code
/// </summary>
public class PlayerInitializer : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera;

    // Components to control
    private PlayerController playerController;
    private WeaponController weaponController;

    void Awake()
    {
        // Get all owner-only components
        playerController = GetComponent<PlayerController>();
        weaponController = GetComponent<WeaponController>();

        // Disable them by default
        if (playerController != null) playerController.enabled = false;
        if (weaponController != null) weaponController.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Enable owner-only scripts
            if (playerController != null) playerController.enabled = true;
            if (weaponController != null) weaponController.enabled = true;

            // Enable camera and audio listener
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;
            }

            Debug.Log("Player initialized for OWNER");
        }
        else
        {
            // Keep scripts disabled for remote players (already disabled in Awake)

            // Disable camera and audio listener for remote players
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
            }

            Debug.Log("Player initialized for REMOTE");
        }
    }
}