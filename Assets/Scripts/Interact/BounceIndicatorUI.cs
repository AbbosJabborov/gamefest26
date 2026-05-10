using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays two diamond slots at the bottom of screen showing selected bounce count.
/// Matches Valorant's recon bolt UI pattern.
///
///   0 bounces  →  ◇ ◇  (both hole)
///   1 bounce   →  ◆ ◇  (first filled, second hole)
///   2 bounces  →  ◆ ◆  (both filled)
///
/// Each slot holds two overlapping Images (filled + hole).
/// We lerp their alpha so the swap is smooth, not a hard cut.
///
/// Setup:
///   Canvas
///     └── BounceIndicator  ← this script, anchored bottom-center
///           ├── Slot0
///           │     ├── Filled   ← Image with your filled diamond sprite
///           │     └── Hole     ← Image with your hole diamond sprite
///           └── Slot1
///                 ├── Filled
///                 └── Hole
///
///   Assign the four Image references in the Inspector.
///   The indicator hides itself when the player has no stone.
/// </summary>
public class BounceIndicatorUI : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private PlayerInteractor interactor;

    [Header("Slot 0 (left diamond)")]
    [SerializeField] private Image slot0Filled;
    [SerializeField] private Image slot0Hole;

    [Header("Slot 1 (right diamond)")]
    [SerializeField] private Image slot1Filled;
    [SerializeField] private Image slot1Hole;

    [Header("Animation")]
    [SerializeField] private float lerpSpeed  = 12f;
    [SerializeField] private float fadeSpeed  = 8f;

    // ─── Private ───────────────────────────────────────────────────────────

    // t per slot: 0 = show hole, 1 = show filled
    private float _t0;
    private float _t1;

    private CanvasGroup _group;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // CanvasGroup lets us fade the whole indicator in/out as one unit
        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        _group.alpha = 0f;
    }

    private void Update()
    {
        // Fade whole indicator in when holding stone, out when not
        float targetAlpha   = interactor.IsHoldingStone ? 1f : 0f;
        _group.alpha        = Mathf.MoveTowards(_group.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        int bounces = interactor.BounceCount;

        // Target t for each slot: 1 (filled) if that slot's bounce is active
        // Slot 0 = first bounce, slot 1 = second bounce
        float target0 = bounces >= 1 ? 1f : 0f;
        float target1 = bounces >= 2 ? 1f : 0f;

        _t0 = Mathf.Lerp(_t0, target0, lerpSpeed * Time.deltaTime);
        _t1 = Mathf.Lerp(_t1, target1, lerpSpeed * Time.deltaTime);

        ApplySlot(slot0Filled, slot0Hole, _t0);
        ApplySlot(slot1Filled, slot1Hole, _t1);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static void ApplySlot(Image filled, Image hole, float t)
    {
        SetAlpha(filled, t);
        SetAlpha(hole,   1f - t);
    }

    private static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a     = a;
        img.color = c;
    }
}
