using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that lives forever across scenes.
/// Handles covering (black panel appears via reverse dissolve)
/// and uncovering (black panel dissolves away).
///
/// HOW TO USE:
///   SceneTransitionManager.Instance.TransitionToScene(buildIndex);
///   SceneTransitionManager.Instance.Uncover();   // call on scene arrive
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("The full-screen black Image using the UI_DissolveFromClick material.")]
    public Image overlayImage;

    [Header("Transition Settings")]
    [Tooltip("Duration of the cover animation (black panel appears).")]
    public float coverDuration = 1.0f;

    [Tooltip("Duration of the uncover animation (black panel dissolves).")]
    public float uncoverDuration = 1.2f;

    [Tooltip("How long to hold the black screen before loading the next scene.")]
    public float holdBeforeLoad = 0.15f;

    [Tooltip("Origin UV for transitions. Center (0.5,0.5) works for most cases.")]
    public Vector2 defaultOriginUV = new Vector2(0.5f, 0.5f);

    [Tooltip("Easing curve — EaseInOut recommended.")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ── Shader property IDs ────────────────────────────────────────────────
    static readonly int k_Origin   = Shader.PropertyToID("_DissolveOrigin");
    static readonly int k_Progress = Shader.PropertyToID("_DissolveProgress");

    Material _mat;
    bool     _busy;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _mat = Instantiate(overlayImage.material);
        overlayImage.material = _mat;

        // Start fully covering (progress 0 = panel visible)
        SetProgress(0f);
        overlayImage.raycastTarget = true;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Cover the screen then load a scene by build index.
    /// Optionally pass a world-space or screen-space origin for the cover animation.
    /// </summary>
    public void TransitionToScene(int buildIndex, Vector2? screenOrigin = null)
    {
        if (_busy) return;
        Vector2 uv = screenOrigin.HasValue
            ? ScreenToOverlayUV(screenOrigin.Value)
            : defaultOriginUV;

        StartCoroutine(CoverThenLoad(buildIndex, uv));
    }

    /// <summary>
    /// Dissolve the overlay away. Call this from each scene after it loads.
    /// </summary>
    public void Uncover(Vector2? uvOrigin = null)
    {
        if (_busy) return;
        StartCoroutine(UncoverRoutine(uvOrigin ?? defaultOriginUV));
    }

    // ── Coroutines ─────────────────────────────────────────────────────────

    IEnumerator CoverThenLoad(int buildIndex, Vector2 uvOrigin)
    {
        _busy = true;
        overlayImage.raycastTarget = true;

        // Animate progress 1 → 0  (panel grows from origin, covers screen)
        yield return StartCoroutine(AnimateProgress(1f, 0f, coverDuration, uvOrigin));

        yield return new WaitForSeconds(holdBeforeLoad);

        SceneManager.LoadScene(buildIndex);
        // Uncover is called manually from the new scene's bootstrapper
        _busy = false;
    }

    IEnumerator UncoverRoutine(Vector2 uvOrigin)
    {
        _busy = true;

        // Animate progress 0 → 1  (panel dissolves away)
        yield return StartCoroutine(AnimateProgress(0f, 1f, uncoverDuration, uvOrigin));

        overlayImage.raycastTarget = false;
        _busy = false;
    }

    IEnumerator AnimateProgress(float from, float to, float duration, Vector2 uvOrigin)
    {
        _mat.SetVector(k_Origin, new Vector4(uvOrigin.x, uvOrigin.y, 0, 0));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t        = easeCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            float progress = Mathf.Lerp(from, to, t);
            SetProgress(progress);
            yield return null;
        }
        SetProgress(to);
    }

    void SetProgress(float p)
    {
        _mat.SetFloat(k_Progress, p);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Convert a screen-space point to UV space on the overlay rect.</summary>
    Vector2 ScreenToOverlayUV(Vector2 screenPos)
    {
        RectTransform rt = overlayImage.rectTransform;
        Camera cam = overlayImage.canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : overlayImage.canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out Vector2 local))
        {
            Rect r = rt.rect;
            return new Vector2(
                Mathf.InverseLerp(r.xMin, r.xMax, local.x),
                Mathf.InverseLerp(r.yMin, r.yMax, local.y)
            );
        }
        return new Vector2(0.5f, 0.5f);
    }
}
