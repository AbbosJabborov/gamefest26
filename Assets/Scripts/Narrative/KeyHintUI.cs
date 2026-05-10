using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Manages a list of on-screen key hint images.
/// When a key is pressed its image turns green and fades out.
///
/// All entries live in a single list — same behaviour for every key,
/// no per-key serialized fields needed.
///
/// Setup:
///   Canvas
///     └── KeyHintContainer  ← KeyHintUI here, HorizontalLayoutGroup recommended
///           ├── W_Key        Image GameObject
///           ├── A_Key        Image GameObject
///           ├── S_Key        Image GameObject
///           ├── D_Key        Image GameObject
///           ├── E_Key        Image GameObject
///           ├── Space_Key    Image GameObject
///           ├── LMB_Key      Image GameObject
///           └── RMB_Key      Image GameObject
///           (add as many as your tutorial needs)
///
///   Fill the `hints` list in the Inspector: assign each Image and its Key.
///   NarrativeManager calls Show(keys) with the subset relevant to each step.
/// </summary>
public class KeyHintUI : MonoBehaviour
{
    // ─── Data ──────────────────────────────────────────────────────────────

    [Serializable]
    public class KeyHintEntry
    {
        [Tooltip("Which key triggers the press animation.")]
        public Key    key;

        [Tooltip("The Image GameObject for this key. Can use a sprite with key art.")]
        public Image  image;

        [Tooltip("Color when idle/visible.")]
        public Color  normalColor = Color.white;

        [Tooltip("Color flash when key is pressed.")]
        public Color  pressedColor = new Color(0.3f, 1f, 0.3f);   // green

        [Tooltip("Seconds to fade out after press.")]
        public float  fadeDuration = 0.6f;

        // Runtime state
        [NonSerialized] public bool  IsActive;
        [NonSerialized] public bool  IsPressed;
    }

    // ─── Inspector ─────────────────────────────────────────────────────────

    [SerializeField] private List<KeyHintEntry> hints = new();

    // ─── Private ───────────────────────────────────────────────────────────

    // Fast lookup: Key → entry
    private Dictionary<Key, KeyHintEntry> _map;

    // Track running fade coroutines so we can cancel on HideAll
    private Dictionary<Key, Coroutine> _fadeRoutines = new();

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _map = new Dictionary<Key, KeyHintEntry>(hints.Count);
        foreach (var h in hints)
            _map[h.key] = h;

        HideAll();
    }

    private void Update()
    {
        foreach (var h in hints)
        {
            if (!h.IsActive || h.IsPressed) continue;

            if (Keyboard.current[h.key].wasPressedThisFrame)
                StartPress(h);
        }
    }

    // ─── Public API ────────────────────────────────────────────────────────

    /// Shows only the keys in the provided array. Hides all others.
    public void Show(Key[] keys)
    {
        HideAll();

        if (keys == null) return;

        foreach (Key k in keys)
        {
            if (!_map.TryGetValue(k, out KeyHintEntry entry)) continue;

            entry.IsActive  = true;
            entry.IsPressed = false;

            SetImageAlpha(entry.image, 1f);
            entry.image.color       = entry.normalColor;
            entry.image.gameObject.SetActive(true);
        }
    }

    /// Hides all key hints and resets their state.
    public void HideAll()
    {
        foreach (var h in hints)
        {
            // Cancel any running fade
            if (_fadeRoutines.TryGetValue(h.key, out Coroutine c) && c != null)
                StopCoroutine(c);

            h.IsActive  = false;
            h.IsPressed = false;
            h.image.gameObject.SetActive(false);
        }

        _fadeRoutines.Clear();
    }

    // ─── Press Animation ───────────────────────────────────────────────────

    private void StartPress(KeyHintEntry entry)
    {
        entry.IsPressed  = true;
        entry.image.color = entry.pressedColor;

        if (_fadeRoutines.ContainsKey(entry.key) && _fadeRoutines[entry.key] != null)
            StopCoroutine(_fadeRoutines[entry.key]);

        _fadeRoutines[entry.key] = StartCoroutine(FadeOut(entry));
    }

    private IEnumerator FadeOut(KeyHintEntry entry)
    {
        float elapsed = 0f;

        while (elapsed < entry.fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / entry.fadeDuration;

            // Lerp color back toward normal while fading alpha out
            entry.image.color = Color.Lerp(entry.pressedColor, entry.normalColor, t);
            SetImageAlpha(entry.image, 1f - t);

            yield return null;
        }

        entry.image.gameObject.SetActive(false);
        entry.IsActive = false;
    }

    private static void SetImageAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a     = a;
        img.color = c;
    }
}
