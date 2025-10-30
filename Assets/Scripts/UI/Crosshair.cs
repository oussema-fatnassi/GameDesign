using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple crosshair that stays centered on screen
/// </summary>
public class Crosshair : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFeedbackDuration = 0.1f;
    
    [Header("Crosshair Style")]
    [SerializeField] private Sprite dotCrosshair;
    [SerializeField] private Sprite crossCrosshair;
    [SerializeField] private float size = 10f;
    
    private Color currentColor;
    private float hitTimer;
    
    void Awake()
    {
        // Get Image component if not assigned
        if (crosshairImage == null)
        {
            crosshairImage = GetComponent<Image>();
        }
        
        // Set initial properties
        if (crosshairImage != null)
        {
            currentColor = defaultColor;
            crosshairImage.color = currentColor;
            
            // Set size
            RectTransform rect = crosshairImage.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
        }
    }
    
    void Update()
    {
        // Reset color after hit feedback
        if (hitTimer > 0)
        {
            hitTimer -= Time.deltaTime;
            if (hitTimer <= 0)
            {
                crosshairImage.color = defaultColor;
            }
        }
    }
    
    /// <summary>
    /// Shows hit feedback
    /// </summary>
    public void ShowHitFeedback()
    {
        if (crosshairImage != null)
        {
            crosshairImage.color = hitColor;
            hitTimer = hitFeedbackDuration;
        }
    }
    
    /// <summary>
    /// Changes crosshair color
    /// </summary>
    public void SetColor(Color color)
    {
        defaultColor = color;
        if (crosshairImage != null && hitTimer <= 0)
        {
            crosshairImage.color = color;
        }
    }
}