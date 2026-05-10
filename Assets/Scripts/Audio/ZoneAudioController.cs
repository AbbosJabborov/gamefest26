using UnityEngine;

/// <summary>
/// Drives audio for a Past Zone — the sphere that restores the past.
/// Call Activate() when the zone expands, Deactivate() when it collapses.
///
/// Attach to the same GameObject as your PastZone component (not yet built).
/// When you build PastZone, call these from there directly.
///
/// Requires one AudioSource for the ambient loop.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ZoneAudioController : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private float activateVolume   = 1f;
    [SerializeField] private float ambientVolume    = 0.5f;
    [SerializeField] private float deactivateVolume = 0.9f;

    [Header("Ambient Fade")]
    [Tooltip("Seconds for ambient loop to fade in after activation.")]
    [SerializeField] private float ambientFadeIn  = 0.4f;
    [Tooltip("Seconds for ambient loop to fade out on deactivation.")]
    [SerializeField] private float ambientFadeOut = 0.3f;

    private AudioSource _loopSource;

    private void Awake()
    {
        _loopSource              = GetComponent<AudioSource>();
        _loopSource.loop         = true;
        _loopSource.playOnAwake  = false;
        _loopSource.spatialBlend = 1f;
        _loopSource.volume       = ambientVolume;
    }

    // ─── Public API ────────────────────────────────────────────────────────

    /// Call when the stone lands and the zone begins expanding.
    public void Activate()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.PlayOneShot(am.ZoneActivate, transform.position, activateVolume);

        // Start ambient loop — fades in so it doesn't punch in harshly
        StartCoroutine(FadeInLoop(am.ZoneAmbientLoop, ambientFadeIn));
    }

    /// Call when the stone is picked up and the zone collapses.
    public void Deactivate()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.FadeOutLoop(_loopSource, ambientFadeOut);
        am.PlayOneShot(am.ZoneDeactivate, transform.position, deactivateVolume);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private System.Collections.IEnumerator FadeInLoop(AudioClip clip, float duration)
    {
        if (clip == null) yield break;

        _loopSource.clip   = clip;
        _loopSource.volume = 0f;
        _loopSource.Play();

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed           += Time.deltaTime;
            _loopSource.volume = Mathf.Lerp(0f, ambientVolume, elapsed / duration);
            yield return null;
        }

        _loopSource.volume = ambientVolume;
    }
}
