using UnityEngine;

/// <summary>
/// Plays a single ambient loop continuously across all scenes.
/// Attach to the same GameObject as AudioManager (or any DontDestroyOnLoad object).
///
/// The clip starts in Awake and is never stopped or restarted —
/// scene loads have zero effect on it.
///
/// To change volume at runtime (e.g. from PauseMenu):
///   AmbientAudio.Instance.SetVolume(0.3f);
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientAudio : MonoBehaviour
{
    public static AmbientAudio Instance { get; private set; }

    [Header("Clip")]
    [Tooltip("The ambient loop. Plays from the first scene and never stops.")]
    [SerializeField] private AudioClip ambientClip;

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.35f;

    [Tooltip("Fade in duration on first play. 0 = instant.")]
    [SerializeField] private float fadeInDuration = 2f;

    private AudioSource _src;

    private void Awake()
    {
        // Only one instance ever — if a duplicate somehow loads, kill it
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src                  = GetComponent<AudioSource>();
        _src.clip             = ambientClip;
        _src.loop             = true;
        _src.spatialBlend     = 0f;     // 2D — ambient fills the whole world
        _src.playOnAwake      = false;
        _src.volume           = fadeInDuration > 0f ? 0f : volume;

        if (ambientClip == null)
        {
            Debug.LogWarning("[AmbientAudio] No clip assigned.", this);
            return;
        }

        _src.Play();

        if (fadeInDuration > 0f)
            StartCoroutine(FadeIn());
    }

    // ─── Public API ────────────────────────────────────────────────────────

    /// Called by PauseMenu volume slider or any other system.
    public void SetVolume(float v)
    {
        volume     = Mathf.Clamp01(v);
        _src.volume = volume;
    }

    // ─── Fade ──────────────────────────────────────────────────────────────

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed    += Time.deltaTime;
            _src.volume = Mathf.Lerp(0f, volume, elapsed / fadeInDuration);
            yield return null;
        }

        _src.volume = volume;
    }
}
