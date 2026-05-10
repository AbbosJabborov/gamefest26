using UnityEngine;

/// <summary>
/// Handles all player-owned audio: footsteps, jump, land.
/// Reads state from FPSMotor — no coupling into movement logic.
///
/// Footsteps use a timer rather than animation events since
/// we're using a CharacterController, not an Animator.
/// </summary>
[RequireComponent(typeof(FPSMotor))]
public class PlayerAudioController : MonoBehaviour
{
    [Header("Footsteps")]
    [Tooltip("Time between footstep sounds at full walk speed.")]
    [SerializeField] private float stepInterval = 0.45f;
    [SerializeField] private float footstepVolume = 0.6f;

    [Header("Jump / Land")]
    [SerializeField] private float jumpVolume = 0.7f;
    [SerializeField] private float landVolume = 0.8f;
    [Tooltip("Minimum fall speed to trigger a land sound. "
           + "Prevents land sound on tiny bumps.")]
    [SerializeField] private float landSpeedThreshold = 2f;

    // ─── Private ───────────────────────────────────────────────────────────

    private FPSMotor _motor;
    private float    _stepTimer;
    private bool     _wasGrounded;
    private float    _fallSpeed;   // tracked to gate land sound

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake() => _motor = GetComponent<FPSMotor>();

    private void Update()
    {
        HandleFootsteps();
        HandleLanding();

        _wasGrounded = _motor.IsGrounded;
        _fallSpeed   = Mathf.Abs(_motor.Velocity.y);
    }

    // ─── Footsteps ─────────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        if (!_motor.IsMoving) { _stepTimer = 0f; return; }

        _stepTimer += Time.deltaTime;

        if (_stepTimer >= stepInterval)
        {
            _stepTimer = 0f;
            AudioManager.Instance?.PlayRandom(
                AudioManager.Instance.Footsteps,
                transform.position,
                footstepVolume
            );
        }
    }

    // ─── Jump & Land ───────────────────────────────────────────────────────

    private void HandleLanding()
    {
        bool justLanded = !_wasGrounded && _motor.IsGrounded;

        if (justLanded && _fallSpeed >= landSpeedThreshold)
        {
            // Volume scales with fall speed — a short hop is quieter than a long fall
            float vol = Mathf.Lerp(landVolume * 0.5f, landVolume, _fallSpeed / 10f);
            AudioManager.Instance?.PlayOneShot(
                AudioManager.Instance.Land,
                transform.position,
                vol
            );
        }
    }

    /// Called by PlayerInput component (Space)
    public void OnJump(UnityEngine.InputSystem.InputValue value)
    {
        if (!value.isPressed || !_motor.IsGrounded) return;

        AudioManager.Instance?.PlayOneShot(
            AudioManager.Instance.Jump,
            transform.position,
            jumpVolume
        );
    }
}
