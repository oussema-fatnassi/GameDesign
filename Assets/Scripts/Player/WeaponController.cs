using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Networked weapon controller with ServerRPC hit detection
/// Add this to ownerOnlyScripts array in PlayerInitializer
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private WeaponConfig weaponConfig;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform weaponMuzzle;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private LineRenderer bulletTracerPrefab;
    [SerializeField] private float tracerDuration = 0.05f;

    [Header("UI")]
    [SerializeField] private Crosshair crosshair;

    [Header("Shooting Settings")]
    [SerializeField] private LayerMask hitLayers = -1;
    [SerializeField] private bool showDebugRays = true;

    // Components
    private PlayerInputs inputActions;
    private InputAction attackAction;
    private NetworkObject networkObject;

    // Ammo tracking
    private int currentAmmo;
    private int reserveAmmo;
    private bool isReloading;

    // Fire rate control
    private float nextFireTime;
    private bool canShoot = true;

    void Awake()
    {
        // Initialize input
        inputActions = new PlayerInputs();

        // Get NetworkObject component
        networkObject = GetComponent<NetworkObject>();

        // Initialize ammo
        if (weaponConfig != null)
        {
            currentAmmo = weaponConfig.MagazineSize;
            reserveAmmo = weaponConfig.ReserveAmmo;
        }

        // Get camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    void OnEnable()
    {
        // Subscribe to attack input
        attackAction = inputActions.Player.Attack;
        attackAction.performed += OnAttack;
        attackAction.Enable();
    }

    void OnDisable()
    {
        // Unsubscribe
        if (attackAction != null)
        {
            attackAction.performed -= OnAttack;
            attackAction.Disable();
        }
    }

    void Update()
    {
        // Handle continuous fire if holding down mouse button
        if (attackAction.IsPressed() && canShoot && !isReloading)
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
        TryShoot();
    }

    void TryShoot()
    {
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
            // Show crosshair feedback immediately
            if (crosshair != null)
            {
                crosshair.ShowHitFeedback();
            }

            // Spawn impact effect locally
            SpawnImpactEffect(hit.point, hit.normal);

            // Send hit to server for damage processing
            // Get the NetworkObject of what we hit
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

        // Show bullet tracer
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
            }

            // Notify all clients to show impact effect
            ShowImpactClientRpc(hitPoint, hitNormal);
        }
    }

    /// <summary>
    /// Client RPC to show impact effect on all clients
    /// </summary>
    [ClientRpc]
    void ShowImpactClientRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        SpawnImpactEffect(hitPoint, hitNormal);
    }

    /// <summary>
    /// Spawns impact effect at hit location
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
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            Random.Range(-spread, spread)
        );

        return (direction + randomSpread).normalized;
    }

    Vector3 GetMuzzlePosition()
    {
        if (weaponMuzzle != null)
            return weaponMuzzle.position;

        return playerCamera.transform.position;
    }

    IEnumerator ShowBulletTracer(Vector3 start, Vector3 end)
    {
        if (bulletTracerPrefab == null)
            yield break;

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