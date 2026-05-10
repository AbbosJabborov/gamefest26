using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Displays a list of cutscene images one at a time.
/// Each page auto-advances after autoAdvanceTime seconds,
/// or immediately when the player clicks Continue.
/// On the last page, fades to black then loads the next scene.
///
/// Setup:
///   Canvas (Screen Space - Overlay)
///     └── CutsceneManager (this script)
///           ├── PageImage        Image  — displays current page sprite
///           ├── FadeOverlay      Image  — solid black, covers full screen
///           ├── ContinueButton   Button
///           ├── ContinueText     TextMeshProUGUI  (e.g. "Continue  ▶")
///           └── TimerText        TextMeshProUGUI  (e.g. "12s")  ← optional
///
///   Fill the `pages` list in the Inspector with your cutscene sprites.
///   Set nextSceneName to your tutorial scene name.
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Pages")]
    [Tooltip("Cutscene images shown in order. Add more here — no code changes needed.")]
    [SerializeField] private Sprite[] pages;

    [Header("References")]
    [SerializeField] private Image            pageImage;
    [SerializeField] private Image            fadeOverlay;
    [SerializeField] private Button           continueButton;
    [SerializeField] private TextMeshProUGUI  continueText;
    [SerializeField] private TextMeshProUGUI  timerText;       // optional

    [Header("Scene")]
    [Tooltip("Exact name of the scene to load after the last page.")]
    [SerializeField] private string nextSceneName = "Tutorial";

    [Header("Timing")]
    [Tooltip("Seconds before auto-advancing to the next page.")]
    [SerializeField] private float autoAdvanceTime = 30f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 0.6f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    // ─── Private ───────────────────────────────────────────────────────────

    private int   _currentPage = 0;
    private float _timer;
    private bool  _advancing;   // guard against double-advance

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Start fully black — fade in on first page
        SetFadeAlpha(1f);
        pageImage.color = Color.white;

        continueButton.onClick.AddListener(OnContinuePressed);
    }

    private void Start()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("[CutsceneManager] No pages assigned.", this);
            LoadNextScene();
            return;
        }

        ShowPage(_currentPage, fadeIn: true);
    }

    private void Update()
    {
        if (_advancing) return;

        _timer -= Time.deltaTime;

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(Mathf.Max(_timer, 0f)) + "s";

        if (_timer <= 0f)
            Advance();
    }

    // ─── Page Logic ────────────────────────────────────────────────────────

    private void ShowPage(int index, bool fadeIn = false)
    {
        pageImage.sprite = pages[index];

        // Update continue button label on last page
        if (continueText != null)
            continueText.text = index >= pages.Length - 1 ? "Begin  ▶" : "Continue  ▶";

        _timer     = autoAdvanceTime;
        _advancing = false;

        if (fadeIn)
            StartCoroutine(FadeFromBlack());
    }

    private void OnContinuePressed()
    {
        if (_advancing) return;
        Advance();
    }

    private void Advance()
    {
        if (_advancing) return;

        _advancing = true;
        _currentPage++;

        if (_currentPage >= pages.Length)
            StartCoroutine(FadeAndLoad());
        else
            StartCoroutine(FadeBetweenPages());
    }

    // ─── Coroutines ────────────────────────────────────────────────────────

    /// Fades black → shows current page → fades in on scene start.
    private IEnumerator FadeFromBlack()
    {
        SetFadeAlpha(1f);
        yield return StartCoroutine(FadeTo(0f, fadeInDuration));
    }

    /// Fades out → flips page → fades back in.
    private IEnumerator FadeBetweenPages()
    {
        yield return StartCoroutine(FadeTo(1f, fadeOutDuration * 0.5f));

        ShowPage(_currentPage, fadeIn: false);

        yield return StartCoroutine(FadeTo(0f, fadeInDuration));
    }

    /// Fades to black then loads the next scene.
    private IEnumerator FadeAndLoad()
    {
        yield return StartCoroutine(FadeTo(1f, fadeOutDuration));
        LoadNextScene();
    }

    /// Core fade — lerps fadeOverlay alpha between current value and target.
    private IEnumerator FadeTo(float target, float duration)
    {
        float start   = fadeOverlay.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }

        SetFadeAlpha(target);
    }

    private void SetFadeAlpha(float a)
    {
        Color c = fadeOverlay.color;
        c.a = a;
        fadeOverlay.color = c;
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[CutsceneManager] nextSceneName is empty.", this);
            return;
        }
        SceneManager.LoadScene(nextSceneName);
    }
}
