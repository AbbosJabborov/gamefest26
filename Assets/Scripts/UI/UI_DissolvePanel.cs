using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to the dark overlay Image (the panel).
/// Click anywhere on it to dissolve outward from that point, revealing the image below.
/// </summary>
[RequireComponent(typeof(Image))]
public class UI_DissolvePanel : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [Tooltip("The Image sitting behind the panel that gets revealed.")]
    public Image revealImage;

    [Tooltip("Grayscale noise texture (any size, Wrap Mode = Repeat).")]
    public Texture2D noiseTexture;

    [Header("Dissolve Settings")]
    [Tooltip("Duration of the dissolve animation in seconds.")]
    public float dissolveDuration = 1.4f;

    [Tooltip("Width of the glowing burn edge.")]
    [Range(0f, 0.12f)]
    public float edgeWidth = 0.025f;

    [Tooltip("Color of the burn / glow edge.")]
    public Color edgeColor = new Color(1f, 0.4f, 0.05f, 1f);

    [Tooltip("Only allow one dissolve; disable clicks while animating.")]
    public bool singleUse = true;

    // ── Shader property IDs ────────────────────────────────────────────────
    static readonly int k_Origin   = Shader.PropertyToID("_DissolveOrigin");
    static readonly int k_Progress = Shader.PropertyToID("_DissolveProgress");
    static readonly int k_EdgeW    = Shader.PropertyToID("_EdgeWidth");
    static readonly int k_EdgeC    = Shader.PropertyToID("_EdgeColor");
    static readonly int k_Noise    = Shader.PropertyToID("_NoiseTex");

    // ── Internal state ─────────────────────────────────────────────────────
    Image    _panel;
    Material _mat;
    bool     _dissolving;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    void Awake()
    {
        _panel = GetComponent<Image>();

        // Instance material so we never dirty the shared asset
        _mat = Instantiate(_panel.material);
        _panel.material = _mat;

        // Push settings into material
        _mat.SetFloat(k_Progress, 0f);
        _mat.SetFloat(k_EdgeW,    edgeWidth);
        _mat.SetColor(k_EdgeC,    edgeColor);
        if (noiseTexture != null)
            _mat.SetTexture(k_Noise, noiseTexture);

        // Reveal image starts invisible
        if (revealImage != null)
            revealImage.color = Color.clear;
    }

    void OnDestroy()
    {
        if (_mat) Destroy(_mat);
    }

    // ── IPointerClickHandler ───────────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_dissolving && singleUse) return;

        // Convert screen click → local UV on this RectTransform
        RectTransform rt = _panel.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        // local is in rect-space (pivot-relative).  Convert to 0-1 UV.
        Rect r = rt.rect;
        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
	// Inside OnPointerClick, before calling TriggerDissolve:
	GetComponent<UI_PanelRevealAnim>()?.StopAnim();	
        TriggerDissolve(new Vector2(u, v));
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Trigger the dissolve from a normalised UV position (0-1).</summary>
    public void TriggerDissolve(Vector2 uvOrigin)
    {
        if (_dissolving) StopAllCoroutines();
        StartCoroutine(Animate(uvOrigin));
    }

    /// <summary>
    /// Call this to reset the panel back to fully opaque
    /// (e.g. when re-entering the main menu).
    /// </summary>
    public void ResetPanel()
    {
        StopAllCoroutines();
        _dissolving = false;
        _mat.SetFloat(k_Progress, 0f);
        _panel.gameObject.SetActive(true);
        _panel.raycastTarget = true;

        if (revealImage != null)
            revealImage.color = Color.clear;
    }

    // ── Animation coroutine ────────────────────────────────────────────────
    IEnumerator Animate(Vector2 uv)
    {
        _dissolving = true;
        _panel.raycastTarget = false; // prevent double-clicks during animation

        _mat.SetVector(k_Origin, new Vector4(uv.x, uv.y, 0f, 0f));

        // Fade reveal image in immediately so it's visible through the dissolving hole
        if (revealImage != null)
            revealImage.color = Color.white;

        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);

            // Cubic ease-out  (fast start, gentle finish)
            float progress = 1f - Mathf.Pow(1f - t, 3f);
            _mat.SetFloat(k_Progress, progress);

            yield return null;
        }

        _mat.SetFloat(k_Progress, 1f);

        // Hide the panel GameObject entirely once dissolved
        _panel.gameObject.SetActive(false);
        _dissolving = false;
    }
}
