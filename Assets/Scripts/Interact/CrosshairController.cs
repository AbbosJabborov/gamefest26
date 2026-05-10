using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates the crosshair between two states:
///
///   IDLE:  circleImage visible at normalSize,  holeImage hidden
///   HOVER: circleImage hidden,  holeImage visible at hoverSize
///
/// Both images share the same RectTransform center.
/// We crossfade alpha and lerp size simultaneously.
///
/// Setup:
///   1. Create a Canvas (Screen Space - Overlay)
///   2. Add an empty child: "Crosshair" — attach this script
///   3. Add two child Image GameObjects inside Crosshair:
///        - "Circle"  → assign circleImage  (your 10x10 circle sprite)
///        - "Hole"    → assign holeImage    (your 15x15 hole sprite)
///      Both anchored to center, pivot 0.5/0.5
///   4. Assign PlayerInteractor reference
/// </summary>
public class CrosshairController : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Image            circleImage;
    [SerializeField] private Image            holeImage;

    [Header("Sizes")]
    [SerializeField] private float normalSize = 10f;
    [SerializeField] private float hoverSize  = 15f;

    [Header("Animation")]
    [Tooltip("Higher = snappier transition.")]
    [SerializeField] private float lerpSpeed = 10f;

    // ─── Private ───────────────────────────────────────────────────────────

    // 0 = idle (circle), 1 = hover (hole)
    private float _t;

    private RectTransform _circleRect;
    private RectTransform _holeRect;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _circleRect = circleImage.rectTransform;
        _holeRect   = holeImage.rectTransform;

        // Start in idle state
        SetState(0f);
    }

    private void Update()
    {
        float target = interactor.IsLookingAtInteractable ? 1f : 0f;

        _t = Mathf.Lerp(_t, target, lerpSpeed * Time.deltaTime);

        SetState(_t);
    }

    // ─── State ─────────────────────────────────────────────────────────────

    private void SetState(float t)
    {
        // Size: lerp from normalSize → hoverSize
        float size = Mathf.Lerp(normalSize, hoverSize, t);
        _circleRect.sizeDelta = new Vector2(size, size);
        _holeRect.sizeDelta   = new Vector2(size, size);

        // Alpha: circle fades out as hole fades in
        SetAlpha(circleImage, 1f - t);
        SetAlpha(holeImage,   t);
    }

    private static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a     = a;
        img.color = c;
    }
}
