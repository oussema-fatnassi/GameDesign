using UnityEngine;

/// <summary>
/// ScriptableObject to store weapon configuration values
/// Create via: Assets > Create > Weapons > Weapon Config
/// </summary>
[CreateAssetMenu(fileName = "WeaponConfig", menuName = "Weapons/Weapon Config", order = 1)]
public class WeaponConfig : ScriptableObject
{
    [Header("Weapon Stats")]
    [Tooltip("Weapon name for display")]
    public string weaponName = "Assault Rifle";
    
    [Tooltip("Damage per shot")]
    [SerializeField] private float damage = 25f;
    
    [Tooltip("Fire rate in rounds per minute")]
    [SerializeField] private float fireRate = 600f;
    
    [Tooltip("Maximum射程 (shooting range)")]
    [SerializeField] private float range = 100f;
    
    [Header("Ammo Settings")]
    [Tooltip("Magazine size")]
    [SerializeField] private int magazineSize = 30;
    
    [Tooltip("Reserve ammo")]
    [SerializeField] private int reserveAmmo = 120;
    
    [Tooltip("Reload time in seconds")]
    [SerializeField] private float reloadTime = 2f;
    
    [Header("Accuracy")]
    [Tooltip("Spread angle in degrees (0 = perfect accuracy)")]
    [SerializeField] private float spread = 0.5f;
    
    [Header("Recoil")]
    [Tooltip("Vertical recoil amount")]
    [SerializeField] private float recoilVertical = 0.5f;
    
    [Tooltip("Horizontal recoil amount")]
    [SerializeField] private float recoilHorizontal = 0.3f;
    
    // Public properties
    public float Damage => damage;
    public float FireRate => fireRate;
    public float TimeBetweenShots => 60f / fireRate; // Convert RPM to seconds
    public float Range => range;
    public int MagazineSize => magazineSize;
    public int ReserveAmmo => reserveAmmo;
    public float ReloadTime => reloadTime;
    public float Spread => spread;
    public float RecoilVertical => recoilVertical;
    public float RecoilHorizontal => recoilHorizontal;
}