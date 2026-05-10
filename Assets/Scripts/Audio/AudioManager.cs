using UnityEngine;

/// <summary>
/// Singleton audio manager. All clips are assigned here in the Inspector.
/// Provides one-shot playback with pitch/volume variance, and loop management.
///
/// Usage:
///   AudioManager.Instance.PlayOneShot(AudioManager.Instance.StoneThrow, transform.position);
///   AudioManager.Instance.PlayLoop(AudioManager.Instance.StoneInflight, myAudioSource);
///   AudioManager.Instance.StopLoop(myAudioSource);
///
/// Setup:
///   Add this to a persistent GameObject in your scene (e.g. GameManager).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ─── Clips ─────────────────────────────────────────────────────────────

    [Header("Stone — Throw")]
    public AudioClip StoneThrow;

    [Header("Stone — In Flight")]
    public AudioClip StoneInflight;         // looped while airborne

    [Header("Stone — Bounce")]
    [Tooltip("Played on first bounce (most remaining bounces).")]
    public AudioClip StoneBounce;
    [Tooltip("Optional: played on the last bounce (fewer remaining = heavier).")]
    public AudioClip StoneBounceHeavy;

    [Header("Stone — Stick")]
    public AudioClip StoneStick;

    [Header("Stone — Pickup")]
    public AudioClip StonePickup;

    [Header("Past Zone")]
    public AudioClip ZoneActivate;
    public AudioClip ZoneAmbientLoop;       // looped while zone is active
    public AudioClip ZoneDeactivate;

    [Header("Player — Footsteps")]
    [Tooltip("Randomly selected from this list per step.")]
    public AudioClip[] Footsteps;

    [Header("Player — Movement")]
    public AudioClip Jump;
    public AudioClip Land;

    [Header("UI")]
    public AudioClip BounceClick;           // per RMB cycle tick

    // ─── Settings ──────────────────────────────────────────────────────────

    [Header("Global Settings")]
    [Range(0f, 1f)] public float MasterVolume   = 1f;
    [Range(0f, 0.3f)] public float PitchVariance = 0.08f;   // random pitch spread on one-shots

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── One-Shot ──────────────────────────────────────────────────────────

    /// Spawns a temporary AudioSource at position for fire-and-forget sounds.
    /// pitch variance randomizes slightly so repeated sounds don't stack identically.
    public void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f, float pitchOffset = 0f)
    {
        if (clip == null) return;

        GameObject obj = new($"OneShot_{clip.name}");
        obj.transform.position = position;

        AudioSource src    = obj.AddComponent<AudioSource>();
        src.clip           = clip;
        src.volume         = volume * MasterVolume;
        src.pitch          = 1f + pitchOffset + Random.Range(-PitchVariance, PitchVariance);
        src.spatialBlend   = 1f;   // 3D sound
        src.rolloffMode    = AudioRolloffMode.Linear;
        src.maxDistance    = 30f;
        src.Play();

        Destroy(obj, clip.length + 0.1f);
    }

    /// Plays a 2D non-spatialized one-shot (UI sounds, player-centric sounds).
    public void PlayOneShotUI(AudioClip clip, float volume = 1f, float pitchOffset = 0f)
    {
        if (clip == null) return;

        GameObject obj = new($"OneShotUI_{clip.name}");

        AudioSource src    = obj.AddComponent<AudioSource>();
        src.clip           = clip;
        src.volume         = volume * MasterVolume;
        src.pitch          = 1f + pitchOffset + Random.Range(-PitchVariance, PitchVariance);
        src.spatialBlend   = 0f;   // 2D
        src.Play();

        Destroy(obj, clip.length + 0.1f);
    }

    /// Plays a random clip from an array. Returns without playing if array is empty.
    public void PlayRandom(AudioClip[] clips, Vector3 position, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        PlayOneShot(clips[Random.Range(0, clips.Length)], position, volume);
    }

    // ─── Loops ─────────────────────────────────────────────────────────────

    /// Assigns a clip to an AudioSource and starts looping it.
    public void PlayLoop(AudioClip clip, AudioSource source, float volume = 1f)
    {
        if (clip == null || source == null) return;
        if (source.clip == clip && source.isPlaying) return;   // already playing

        source.clip   = clip;
        source.loop   = true;
        source.volume = volume * MasterVolume;
        source.Play();
    }

    /// Stops a looping AudioSource.
    public void StopLoop(AudioSource source)
    {
        if (source == null || !source.isPlaying) return;
        source.Stop();
    }

    /// Fades out a looping AudioSource over time then stops it.
    public void FadeOutLoop(AudioSource source, float duration = 0.3f)
    {
        if (source == null) return;
        StartCoroutine(FadeOutCoroutine(source, duration));
    }

    private System.Collections.IEnumerator FadeOutCoroutine(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed       += Time.deltaTime;
            source.volume  = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        source.Stop();
        source.volume = startVolume;   // reset for next play
    }
}
