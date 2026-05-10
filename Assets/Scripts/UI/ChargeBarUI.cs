using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays throw charge as a fill bar, visible only while charging.
///
/// Setup:
///   Canvas
///     └── ChargeBar  ← this script, CanvasGroup on same object
///           ├── Background  (Image, plain dark rect)
///           └── Fill        (Image, Image Type = Filled, Fill Method = Horizontal)
///
/// The bar fades in the moment LMB is pressed and fades out on release.
/// Fill amount tracks ChargeAmount (0-1) directly.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ChargeBarUI : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Image            fillImage;

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 12f;

    [Header("Color")]
    [Tooltip("Bar color at minimum charge.")]
    [SerializeField] private Color colorMin = Color.white;
    [Tooltip("Bar color at maximum charge (full).")]
    [SerializeField] private Color colorMax = new Color(1f, 0.6f, 0.1f);   // orange

    // ─── Private ───────────────────────────────────────────────────────────

    private CanvasGroup _group;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _group       = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
    }

    private void Update()
    {
        bool    charging = interactor.IsCharging;
        float   charge   = interactor.ChargeAmount;

        // Fade in while charging, fade out immediately on release
        float targetAlpha = charging ? 1f : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Fill and color track charge directly — no lerp, feels more responsive
        fillImage.fillAmount = charge;
        fillImage.color      = Color.Lerp(colorMin, colorMax, charge);
    }
}
