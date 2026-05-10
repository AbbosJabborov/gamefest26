using TMPro;
using UnityEngine;

/// <summary>
/// Watches PlayerInteractor.CurrentInteractable and shows/hides
/// the "Press E to [prompt]" text.
///
/// Setup:
///   - Attach to a UI GameObject with a TextMeshProUGUI component
///   - Assign your PlayerInteractor reference
///   - Text fades in/out smoothly via CanvasGroup alpha
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class InteractPromptUI : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private TextMeshProUGUI  promptText;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 8f;

    [Header("Format")]
    [Tooltip("Use {0} where the verb should appear. e.g. 'Press E to {0}'")]
    [SerializeField] private string format = "Press <b>E</b> to {0}";

    // ─── Private ───────────────────────────────────────────────────────────

    private CanvasGroup _group;
    private float       _targetAlpha;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
    }

    private void Update()
    {
        IInteractable current = interactor.CurrentInteractable;

        if (current != null)
        {
            // Update text every frame so dynamic prompts (open/close toggle) work
            promptText.text = string.Format(format, current.InteractPrompt);
            _targetAlpha    = 1f;
        }
        else
        {
            _targetAlpha = 0f;
        }

        _group.alpha = Mathf.MoveTowards(
            _group.alpha,
            _targetAlpha,
            fadeSpeed * Time.deltaTime
        );
    }
}
