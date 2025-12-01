using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

/// <summary>
/// Handles weapon shooting mechanics with raycast hit detection
/// Requires: WeaponConfig, Camera reference
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
    
    [Header("Shooting Settings")]
    [SerializeField] private LayerMask hitLayers = -1; // What can be hit
    [SerializeField] private bool showDebugRays = true;
    
    // Components
    private PlayerInputs inputActions;
    private InputAction attackAction;
    private InputAction reloadAction;
    
    // Ammo tracking
    private int currentAmmo;
    private int reserveAmmo;
    private bool isReloading;
    
    // Fire rate control
    private float nextFireTime;
    
    // State
    private bool canShoot = true;

    public static event Action OnHit;
    void Awake()
    {
        // Initialize input
        inputActions = new PlayerInputs();
        
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
        
        // Subscribe to reload input (if you want manual reload with R key)
        // You'll need to add a Reload action in your input system for this
        // For now, we'll handle auto-reload when magazine is empty
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
        
        // Manual reload with R key (add this to your input actions if needed)
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < weaponConfig.MagazineSize)
        {
            StartCoroutine(Reload());
        }
    }
    
    /// <summary>
    /// Called when attack input is triggered
    /// </summary>
    void OnAttack(InputAction.CallbackContext context)
    {
        TryShoot();
    }
    
    /// <summary>
    /// Attempts to shoot the weapon
    /// </summary>
    void TryShoot()
    {
        // Check if we can shoot
        if (!canShoot || isReloading || Time.time < nextFireTime)
            return;
        
        // Check ammo
        if (currentAmmo <= 0)
        {
            // Auto-reload if we have reserve ammo
            if (reserveAmmo > 0 && !isReloading)
            {
                StartCoroutine(Reload());
            }
            return;
        }
        
        // Shoot!
        Shoot();
        
        // Update fire rate timer
        nextFireTime = Time.time + weaponConfig.TimeBetweenShots;
        
        // Decrease ammo
        currentAmmo--;
    }
    
    /// <summary>
    /// Performs the actual shooting
    /// </summary>
    void Shoot()
    {
        // Play muzzle flash
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
        
        // Calculate shot origin from center of screen
        Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // Center of screen
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
        
        // Calculate the end point
        Vector3 hitPoint = didHit ? hit.point : shootOrigin + shootDirection * weaponConfig.Range;
        
        // Draw debug ray
        if (showDebugRays)
        {
            Debug.DrawRay(shootOrigin, shootDirection * (didHit ? hit.distance : weaponConfig.Range), Color.red, 1f);
        }
        
        if (didHit)
        {
            // We hit something!
            HandleHit(hit);
            
            // Show crosshair hit feedback
            OnHit?.Invoke();
        }
        
        // Show bullet tracer
        if (bulletTracerPrefab != null)
        {
            StartCoroutine(ShowBulletTracer(GetMuzzlePosition(), hitPoint));
        }
        
        // Apply recoil (optional - implement in PlayerController)
        // You could add an event here that PlayerController subscribes to
    }
    
    /// <summary>
    /// Handles what happens when we hit something
    /// </summary>
    void HandleHit(RaycastHit hit)
    {
        // Spawn impact effect
        if (impactEffect != null)
        {
            GameObject impact = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(impact, 2f);
        }
        
        // Check if we hit an enemy
        // Try to get Health component (you'll create this for enemies)
        Health health = hit.collider.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(weaponConfig.Damage);
        }
        
        // Optional: Add force to rigidbody
        Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(-hit.normal * 100f);
        }
        
        // Debug
        Debug.Log($"Hit: {hit.collider.name} at {hit.point}");
    }
    
    /// <summary>
    /// Applies random spread to the shoot direction
    /// </summary>
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
    
    /// <summary>
    /// Gets the muzzle position (or camera position if no muzzle)
    /// </summary>
    Vector3 GetMuzzlePosition()
    {
        if (weaponMuzzle != null)
            return weaponMuzzle.position;
        
        return playerCamera.transform.position;
    }
    
    /// <summary>
    /// Shows a bullet tracer line
    /// </summary>
    IEnumerator ShowBulletTracer(Vector3 start, Vector3 end)
    {
        if (bulletTracerPrefab == null)
            yield break;
        
        // Create a temporary line renderer instance
        LineRenderer tracer = Instantiate(bulletTracerPrefab);
        
        // Make sure it has 2 positions
        tracer.positionCount = 2;
        
        // Set the start and end points
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        
        // Wait for the duration
        yield return new WaitForSeconds(tracerDuration);
        
        // Destroy the tracer
        if (tracer != null)
        {
            Destroy(tracer.gameObject);
        }
    }
    
    /// <summary>
    /// Reloads the weapon
    /// </summary>
    IEnumerator Reload()
    {
        if (isReloading || reserveAmmo <= 0)
            yield break;
        
        isReloading = true;
        Debug.Log("Reloading...");
        
        yield return new WaitForSeconds(weaponConfig.ReloadTime);
        
        // Calculate how much ammo to reload
        int ammoNeeded = weaponConfig.MagazineSize - currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);
        
        currentAmmo += ammoToReload;
        reserveAmmo -= ammoToReload;
        
        isReloading = false;
        Debug.Log($"Reload complete. Ammo: {currentAmmo}/{reserveAmmo}");
    }
    
    // Public getters for UI
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;
    public WeaponConfig Config => weaponConfig;
    public int MaxAmmo => weaponConfig != null ? weaponConfig.MagazineSize : 0;

    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!showDebugRays || playerCamera == null)
            return;
        
        // Draw shoot direction
        Gizmos.color = Color.yellow;
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        Gizmos.DrawRay(origin, direction * (weaponConfig != null ? weaponConfig.Range : 100f));
    }
}