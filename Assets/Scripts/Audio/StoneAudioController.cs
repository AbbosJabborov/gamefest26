using UnityEngine;

/// <summary>
/// Sits on the Stone prefab alongside ThrowableStone.
/// Subscribes to stone events and drives audio through AudioManager.
///
/// Requires one AudioSource component (used for the in-flight loop).
/// Add a second AudioSource if you want bounce sounds spatialized
/// separately from the loop — one source can only play one clip at a time.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ThrowableStone))]
public class StoneAudioController : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private float throwVolume  = 0.9f;
    [SerializeField] private float bounceVolume = 0.85f;
    [SerializeField] private float stickVolume  = 1f;
    [SerializeField] private float pickupVolume = 0.7f;
    [SerializeField] private float inflightVolume = 0.4f;

    private ThrowableStone _stone;
    private AudioSource    _loopSource;   // dedicated to the inflight loop

    private void Awake()
    {
        _stone      = GetComponent<ThrowableStone>();
        _loopSource = GetComponent<AudioSource>();

        // Configure loop source — it should be 3D and non-playing at start
        _loopSource.spatialBlend = 1f;
        _loopSource.loop         = true;
        _loopSource.playOnAwake  = false;
        _loopSource.volume       = inflightVolume;
    }

    private void OnEnable()
    {
        _stone.OnThrown   += HandleThrown;
        _stone.OnBounced  += HandleBounced;
        _stone.OnStuck    += HandleStuck;
        _stone.OnPickedUp += HandlePickedUp;
    }

    private void OnDisable()
    {
        _stone.OnThrown   -= HandleThrown;
        _stone.OnBounced  -= HandleBounced;
        _stone.OnStuck    -= HandleStuck;
        _stone.OnPickedUp -= HandlePickedUp;
    }

    // ─── Handlers ──────────────────────────────────────────────────────────

    private void HandleThrown(Vector3 velocity)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Throw whoosh — pitch slightly reflects throw speed
        float speedRatio = velocity.magnitude / 20f;   // normalized against max force
        am.PlayOneShot(am.StoneThrow, transform.position, throwVolume, speedRatio * 0.2f);

        // Start in-flight whistle loop
        am.PlayLoop(am.StoneInflight, _loopSource, inflightVolume);
    }

    private void HandleBounced(int remainingBounces)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Stop in-flight loop briefly — or keep playing, your call
        // Choosing to keep it: the stone is still moving after a bounce

        // Pick clip: if heavy version exists and this is the last bounce, use it
        bool      isLastBounce = remainingBounces == 0;
        AudioClip clip         = (isLastBounce && am.StoneBounceHeavy != null)
            ? am.StoneBounceHeavy
            : am.StoneBounce;

        // Pitch goes slightly lower per bounce — first bounce is higher, last is heavier
        float pitchOffset = remainingBounces * 0.08f;

        am.PlayOneShot(clip, transform.position, bounceVolume, pitchOffset);
    }

    private void HandleStuck()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Stop in-flight loop — stone is no longer moving
        am.FadeOutLoop(_loopSource, 0.1f);

        am.PlayOneShot(am.StoneStick, transform.position, stickVolume);
    }

    private void HandlePickedUp()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Kill any leftover loop immediately on pickup
        am.StopLoop(_loopSource);

        am.PlayOneShotUI(am.StonePickup, pickupVolume);
    }
}
