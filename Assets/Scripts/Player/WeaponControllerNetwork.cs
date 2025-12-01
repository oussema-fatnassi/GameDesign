using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Networked weapon controller with ServerRPC hit detection
/// </summary>
public class WeaponControllerNetwork : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] private WeaponConfig weaponConfig;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform weaponMuzzle;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private LineRenderer bulletTracerPrefab; // This should just be a prefab, NO NetworkObject needed
    [SerializeField] private float tracerDuration = 0.05f;

    [Header("Shooting Settings")]
    [SerializeField] private LayerMask hitLayers = -1;
    [SerializeField] private bool showDebugRays = true;

    // Components
    private PlayerInputs inputActions;
    private InputAction attackAction;

    // Ammo tracking
    private int currentAmmo;
    private int reserveAmmo;
    private bool isReloading;

    // Fire rate control
    private float nextFireTime;
    private bool canShoot = true;

    public static event Action OnHit;

    void Awake()
    {
        // Get camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Initialize input for owner only
        inputActions = new PlayerInputs();

        // Initialize ammo
        if (weaponConfig != null)
        {
            currentAmmo = weaponConfig.MagazineSize;
            reserveAmmo = weaponConfig.ReserveAmmo;
        }

        // Subscribe to attack input
        attackAction = inputActions.Player.Attack;
        attackAction.performed += OnAttack;
        attackAction.Enable();

        Debug.Log("WeaponController initialized for OWNER");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        // Unsubscribe
        if (attackAction != null)
        {
            attackAction.performed -= OnAttack;
            attackAction.Disable();
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        // Handle continuous fire if holding down mouse button
        if (attackAction != null && attackAction.IsPressed() && canShoot && !isReloading)
        {
            TryShoot();
        }

        // Manual reload with R key
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < weaponConfig.MagazineSize)
        {
            StartCoroutine(Reload());
        }
    }

    void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        TryShoot();
    }

    void TryShoot()
    {
        if (!IsOwner) return;

        // Check if we can shoot
        if (!canShoot || isReloading || Time.time < nextFireTime)
            return;

        // Check ammo
        if (currentAmmo <= 0)
        {
            if (reserveAmmo > 0 && !isReloading)
            {
                StartCoroutine(Reload());
            }
            return;
        }

        // Shoot locally for immediate feedback
        ShootLocal();

        // Update fire rate timer
        nextFireTime = Time.time + weaponConfig.TimeBetweenShots;

        // Decrease ammo
        currentAmmo--;
    }

    /// <summary>
    /// Performs local shooting (visual effects only)
    /// </summary>
    void ShootLocal()
    {
        if (!IsOwner) return;

        // Play muzzle flash
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        // Calculate shot from center of screen
        Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 shootOrigin = cameraRay.origin;
        Vector3 shootDirection = cameraRay.direction;

        // Apply spread
        if (weaponConfig.Spread > 0)
        {
            shootDirection = ApplySpread(shootDirection);
        }

        // Perform raycast
        RaycastHit hit;
        bool didHit = Physics.Raycast(shootOrigin, shootDirection, out hit, weaponConfig.Range, hitLayers);

        // Calculate end point
        Vector3 hitPoint = didHit ? hit.point : shootOrigin + shootDirection * weaponConfig.Range;

        // Debug ray
        if (showDebugRays)
        {
            Debug.DrawRay(shootOrigin, shootDirection * (didHit ? hit.distance : weaponConfig.Range), Color.red, 1f);
        }

        // Show local effects
        if (didHit)
        {
            Debug.Log($"Hit: {hit.collider.name}");

            // Show crosshair feedback immediately
            OnHit?.Invoke();

            // Spawn impact effect locally (CLIENT-SIDE ONLY - no network)
            SpawnImpactEffect(hit.point, hit.normal);

            // Send hit to server for damage processing
            NetworkObject targetNetworkObject = hit.collider.GetComponent<NetworkObject>();
            if (targetNetworkObject != null)
            {
                // We hit a networked object, send ServerRPC
                RequestHitServerRpc(
                    targetNetworkObject.NetworkObjectId,
                    hit.point,
                    hit.normal,
                    weaponConfig.Damage
                );
            }
        }

        // Show bullet tracer (LOCAL ONLY - no network spawning)
        if (bulletTracerPrefab != null)
        {
            StartCoroutine(ShowBulletTracer(GetMuzzlePosition(), hitPoint));
        }
    }

    /// <summary>
    /// Server RPC to process hit on the server
    /// This will be called later when you have enemies
    /// </summary>
    [ServerRpc]
    void RequestHitServerRpc(ulong targetNetworkObjectId, Vector3 hitPoint, Vector3 hitNormal, float damage)
    {
        // Find the target NetworkObject
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
        {
            // Try to get Health component
            Health health = targetObject.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Debug.Log($"Server: Applied {damage} damage to {targetObject.name}");
            }

            // DON'T call ShowImpactClientRpc - impact is already shown locally
            // This prevents double impacts and network overhead
        }
    }

    /// <summary>
    /// Spawns impact effect at hit location (LOCAL ONLY)
    /// </summary>
    void SpawnImpactEffect(Vector3 point, Vector3 normal)
    {
        if (impactEffect != null)
        {
            GameObject impact = Instantiate(impactEffect, point, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
    }

    Vector3 ApplySpread(Vector3 direction)
    {
        float spread = weaponConfig.Spread;

        Vector3 randomSpread = new Vector3(
            UnityEngine.Random.Range(-spread, spread),
            UnityEngine.Random.Range(-spread, spread),
            UnityEngine.Random.Range(-spread, spread)
        );

        return (direction + randomSpread).normalized;
    }

    Vector3 GetMuzzlePosition()
    {
        if (weaponMuzzle != null)
            return weaponMuzzle.position;

        return playerCamera.transform.position;
    }

    /// <summary>
    /// Shows bullet tracer - LOCAL ONLY, no networking
    /// </summary>
    IEnumerator ShowBulletTracer(Vector3 start, Vector3 end)
    {
        if (bulletTracerPrefab == null)
            yield break;

        // Create a LOCAL instance (not networked)
        LineRenderer tracer = Instantiate(bulletTracerPrefab);
        tracer.positionCount = 2;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);

        yield return new WaitForSeconds(tracerDuration);

        if (tracer != null)
        {
            Destroy(tracer.gameObject);
        }
    }

    IEnumerator Reload()
    {

        if (isReloading || reserveAmmo <= 0)
            yield break;

        isReloading = true;
        Debug.Log("Reloading...");

        yield return new WaitForSeconds(weaponConfig.ReloadTime);

        int ammoNeeded = weaponConfig.MagazineSize - currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);

        currentAmmo += ammoToReload;
        reserveAmmo -= ammoToReload;

        isReloading = false;
        Debug.Log($"Reload complete. Ammo: {currentAmmo}/{reserveAmmo}");
    }

    // Public getters
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;
    public WeaponConfig Config => weaponConfig;

    public int MaxAmmo => weaponConfig != null ? weaponConfig.MagazineSize : 0;

    void OnDrawGizmos()
    {
        if (!showDebugRays || playerCamera == null)
            return;

        Gizmos.color = Color.yellow;
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        Gizmos.DrawRay(origin, direction * (weaponConfig != null ? weaponConfig.Range : 100f));
    }
}