using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton narrative manager. Drives the subtitle typewriter and
/// tells KeyHintUI which keys to show for each step.
///
/// Trigger from anywhere:
///   NarrativeManager.Instance.Play("move");
///
/// Or use a NarrativeTrigger volume in the scene.
///
/// Setup:
///   Any persistent GameObject
///     └── NarrativeManager
///           Assign: subtitleText, subtitleGroup, keyHintUI
///           Fill:   steps list in the Inspector
/// </summary>
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    // ─── Data ──────────────────────────────────────────────────────────────

    [Serializable]
    public class NarrativeStep
    {
        [Tooltip("Unique ID used to trigger this step from code or NarrativeTrigger.")]
        public string id;

        [TextArea(2, 4)]
        public string text;

        [Tooltip("Seconds the subtitle stays fully visible after typewriter finishes.")]
        public float holdDuration = 3f;

        [Tooltip("Characters per second for the typewriter effect.")]
        public float typewriterSpeed = 40f;

        [Tooltip("Keys to highlight in KeyHintUI while this step is active.")]
        public Key[] keysToShow;
    }

    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private CanvasGroup     subtitleGroup;
    [SerializeField] private KeyHintUI       keyHintUI;

    [Header("Fade")]
    [SerializeField] private float fadeInSpeed  = 6f;
    [SerializeField] private float fadeOutSpeed = 3f;

    [Header("Steps")]
    [SerializeField] private List<NarrativeStep> steps = new();

    // ─── Private ───────────────────────────────────────────────────────────

    private Dictionary<string, NarrativeStep> _stepMap;
    private Coroutine                          _activeRoutine;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Build lookup so Play() is O(1)
        _stepMap = new Dictionary<string, NarrativeStep>(steps.Count);
        foreach (var step in steps)
            if (!string.IsNullOrEmpty(step.id))
                _stepMap[step.id] = step;

        subtitleGroup.alpha = 0f;
        subtitleText.text   = string.Empty;
    }

    // ─── Public API ────────────────────────────────────────────────────────

    /// Plays a narrative step by ID. Safe to call mid-step — interrupts current.
    public void Play(string id)
    {
        if (!_stepMap.TryGetValue(id, out NarrativeStep step))
        {
            Debug.LogWarning($"[NarrativeManager] No step with id '{id}'.", this);
            return;
        }

        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _activeRoutine = StartCoroutine(RunStep(step));
    }

    /// Immediately hides the subtitle and clears key hints.
    public void Clear()
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        StartCoroutine(FadeOut());
        keyHintUI?.HideAll();
    }

    // ─── Coroutines ────────────────────────────────────────────────────────

    private IEnumerator RunStep(NarrativeStep step)
    {
        // Show key hints for this step
        keyHintUI?.Show(step.keysToShow);

        // Fade subtitle in
        yield return StartCoroutine(FadeIn());

        // Typewriter
        subtitleText.text     = string.Empty;
        subtitleText.maxVisibleCharacters = 0;
        subtitleText.text     = step.text;

        int    total   = step.text.Length;
        float  elapsed = 0f;

        while (subtitleText.maxVisibleCharacters < total)
        {
            elapsed += Time.unscaledDeltaTime;   // unscaled — works while paused
            subtitleText.maxVisibleCharacters =
                Mathf.Min(Mathf.FloorToInt(elapsed * step.typewriterSpeed), total);
            yield return null;
        }

        // Hold
        yield return new WaitForSecondsRealtime(step.holdDuration);

        // Fade out
        yield return StartCoroutine(FadeOut());

        keyHintUI?.HideAll();
        _activeRoutine = null;
    }

    private IEnumerator FadeIn()
    {
        while (subtitleGroup.alpha < 1f)
        {
            subtitleGroup.alpha += fadeInSpeed * Time.unscaledDeltaTime;
            yield return null;
        }
        subtitleGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        while (subtitleGroup.alpha > 0f)
        {
            subtitleGroup.alpha -= fadeOutSpeed * Time.unscaledDeltaTime;
            yield return null;
        }
        subtitleGroup.alpha = 0f;
        subtitleText.text   = string.Empty;
    }
}
