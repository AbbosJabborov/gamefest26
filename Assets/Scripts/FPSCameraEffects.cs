using UnityEngine;

/// <summary>
/// Layered camera-effect system: each effect (dash, land, shake) runs on
/// its own independent timer.  Effects compose additively so they never
/// fight, and any effect can be interrupted mid-play without leaving
/// the camera in a dirty state.
///
/// Attach to the same GameObject as FPSMotor + FPSLook.
/// Drives the Camera component (FOV) and cameraRoot transform (position + roll).
///
/// Design rules that keep things safe:
///   1. No coroutines — everything is timer-driven in LateUpdate, so there's
///      nothing to cancel / leak.
///   2. Every frame starts from baseline and sums offsets — if a layer is
///      inactive its contribution is zero automatically.
///   3. Interruption = reset the layer timer.  The next frame just evaluates
///      the new curve from t=0.  No leftover state.
///   4. Curves are evaluated with Mathf functions, never with stored "previous
///      frame" deltas — so skipped frames or hitches can't accumulate error.
/// </summary>
public class FPSCameraEffects : MonoBehaviour
{
    // ─── References ────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The same cameraRoot used by FPSLook.")]
    [SerializeField] private Transform cameraRoot;
    [Tooltip("The actual Camera component (for FOV changes).")]
    [SerializeField] private Camera playerCamera;

    // ─── Dash Effect ───────────────────────────────────────────────────────

    [Header("Dash — FOV Kick")]
    [Tooltip("Extra FOV added at the peak of the dash.")]
    [SerializeField] private float dashFovAdd = 12f;
    [Tooltip("How long the FOV widens (attack).")]
    [SerializeField] private float dashFovInTime  = 0.08f;
    [Tooltip("How long the FOV eases back (release).")]
    [SerializeField] private float dashFovOutTime = 0.35f;

    [Header("Dash — Roll Tilt")]
    [Tooltip("Max Z-roll degrees toward the dash direction.")]
    [SerializeField] private float dashRollAngle  = 8f;
    [SerializeField] private float dashRollInTime  = 0.1f;
    [SerializeField] private float dashRollOutTime = 0.4f;

    // ─── Landing Effect ────────────────────────────────────────────────────

    [Header("Landing — Camera Punch")]
    [Tooltip("How far down the camera dips on a hard landing (units).")]
    [SerializeField] private float landPunchDown    = 0.25f;
    [Tooltip("Pitch kick in degrees (positive = look down).")]
    [SerializeField] private float landPitchKick    = 4f;
    [Tooltip("Duration of the full punch-and-recover animation.")]
    [SerializeField] private float landPunchDuration = 0.4f;

    [Header("Landing — FOV Squeeze")]
    [Tooltip("FOV subtracted on impact (squeeze feel).")]
    [SerializeField] private float landFovSqueeze     = 5f;
    [SerializeField] private float landFovDuration     = 0.35f;

    [Header("Landing — Shake")]
    [Tooltip("Intensity of the post-impact screen shake.")]
    [SerializeField] private float landShakeIntensity = 0.06f;
    [Tooltip("How long the shake lasts.")]
    [SerializeField] private float landShakeDuration  = 0.25f;
    [Tooltip("Shake frequency (oscillations per second).")]
    [SerializeField] private float landShakeFrequency = 30f;

    [Header("Landing — Soft Landing")]
    [Tooltip("Multiplier for soft landings (below hard threshold). 0.3 = 30% of full effect.")]
    [SerializeField, Range(0f, 1f)] private float softLandScale = 0.3f;

    // ─── Baseline ──────────────────────────────────────────────────────────

    private float _baseFov;
    private Vector3 _baseLocalPos;

    // ─── Layer: Dash FOV ───────────────────────────────────────────────────

    private float _dashFovTimer;
    private float _dashFovDuration;
    private bool  _dashFovAttacking;

    // ─── Layer: Dash Roll ──────────────────────────────────────────────────

    private float _dashRollTimer;
    private float _dashRollDuration;
    private float _dashRollDirection;   // -1 left, +1 right
    private bool  _dashRollAttacking;

    // ─── Layer: Land Punch ─────────────────────────────────────────────────

    private float _landPunchTimer;
    private float _landPunchScale;      // 1.0 for hard, softLandScale for soft

    // ─── Layer: Land FOV ───────────────────────────────────────────────────

    private float _landFovTimer;
    private float _landFovScale;

    // ─── Layer: Land Shake ─────────────────────────────────────────────────

    private float _landShakeTimer;
    private float _landShakeScale;

    // ─── Motor ref (for reading state) ─────────────────────────────────────

    private FPSMotor _motor;

    // track motor state changes
    private bool _wasDashing;
    private bool _wasGrounded;
    private float _prevVerticalSpeed;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _motor = GetComponent<FPSMotor>();

        if (_motor == null)
            Debug.LogError("[FPSCameraEffects] FPSMotor not found on this GameObject.", this);
        if (cameraRoot == null)
            Debug.LogError("[FPSCameraEffects] cameraRoot not assigned.", this);
        if (playerCamera == null)
            Debug.LogError("[FPSCameraEffects] playerCamera not assigned.", this);
    }

    private void Start()
    {
        // Capture baselines AFTER other Awake()s have run
        if (playerCamera != null)
            _baseFov = playerCamera.fieldOfView;
        if (cameraRoot != null)
            _baseLocalPos = cameraRoot.localPosition;
    }

    /// <summary>
    /// LateUpdate so we apply AFTER FPSLook has set the base rotation.
    /// </summary>
    private void LateUpdate()
    {
        DetectEvents();
        float dt = Time.deltaTime;

        // ── Tick all layer timers ──────────────────────────────────────────

        TickDashFov(dt);
        TickDashRoll(dt);
        TickLandPunch(dt);
        TickLandFov(dt);
        TickLandShake(dt);

        // ── Compose offsets (start from zero each frame) ───────────────────

        float fovOffset   = 0f;
        float rollOffset  = 0f;
        Vector3 posOffset = Vector3.zero;
        float pitchOffset = 0f;

        fovOffset  += EvalDashFov();
        rollOffset += EvalDashRoll();

        posOffset  += EvalLandPunchPos();
        pitchOffset += EvalLandPunchPitch();
        fovOffset  += EvalLandFov();
        posOffset  += EvalLandShake();

        // ── Apply ──────────────────────────────────────────────────────────

        if (playerCamera != null)
            playerCamera.fieldOfView = _baseFov + fovOffset;

        if (cameraRoot != null)
        {
            // Position: baseline + offset
            cameraRoot.localPosition = _baseLocalPos + posOffset;

            // Roll + pitch kick layered ON TOP of whatever FPSLook set
            Vector3 euler = cameraRoot.localEulerAngles;
            euler.z = rollOffset;                       // roll
            euler.x += pitchOffset;                     // pitch kick
            cameraRoot.localEulerAngles = euler;
        }

        // ── Store previous frame state ─────────────────────────────────────

        _wasDashing  = _motor != null && _motor.IsDashing;
        _wasGrounded = _motor != null && _motor.IsGrounded;
        _prevVerticalSpeed = _motor != null ? _motor.Velocity.y : 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EVENT DETECTION
    // ═══════════════════════════════════════════════════════════════════════

    private void DetectEvents()
    {
        if (_motor == null) return;

        // ── Dash started ───────────────────────────────────────────────────

        if (_motor.IsDashing && !_wasDashing)
            OnDashStart();

        // ── Dash ended ─────────────────────────────────────────────────────

        if (!_motor.IsDashing && _wasDashing)
            OnDashEnd();

        // ── Just landed ────────────────────────────────────────────────────

        if (_motor.IsGrounded && !_wasGrounded)
            OnLanded(Mathf.Abs(_prevVerticalSpeed));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TRIGGERS — these only reset timers, never store deltas
    // ═══════════════════════════════════════════════════════════════════════

    private void OnDashStart()
    {
        // FOV: attack phase
        _dashFovTimer     = 0f;
        _dashFovDuration  = dashFovInTime;
        _dashFovAttacking = true;

        // Roll: pick direction from horizontal input
        Vector2 moveInput = _motor.MoveInput;

        // Default to 0 (no roll) if going straight forward
        _dashRollDirection = 0f;
        if (Mathf.Abs(moveInput.x) > 0.1f)
            _dashRollDirection = -Mathf.Sign(moveInput.x); // tilt opposite to strafe

        _dashRollTimer     = 0f;
        _dashRollDuration  = dashRollInTime;
        _dashRollAttacking = true;
    }

    private void OnDashEnd()
    {
        // FOV: release phase
        _dashFovTimer     = 0f;
        _dashFovDuration  = dashFovOutTime;
        _dashFovAttacking = false;

        // Roll: release phase
        _dashRollTimer     = 0f;
        _dashRollDuration  = dashRollOutTime;
        _dashRollAttacking = false;
    }

    private void OnLanded(float fallSpeed)
    {
        // Determine scale: hard vs soft landing
        // Read hardLandingThreshold idea from motor — we use a local heuristic
        // to avoid tight coupling.  Anything above 8 m/s is "hard."
        const float hardThreshold = 8f;
        const float minThreshold  = 3f;

        if (fallSpeed < minThreshold) return;   // trivial drop, skip

        float scale = fallSpeed >= hardThreshold ? 1f : softLandScale;

        // Punch
        _landPunchTimer = landPunchDuration;
        _landPunchScale = scale;

        // FOV squeeze
        _landFovTimer = landFovDuration;
        _landFovScale = scale;

        // Shake (only on hard landings)
        if (fallSpeed >= hardThreshold)
        {
            _landShakeTimer = landShakeDuration;
            _landShakeScale = scale;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  LAYER TICKERS — decrement timers, nothing else
    // ═══════════════════════════════════════════════════════════════════════

    private void TickDashFov(float dt)
    {
        if (_dashFovDuration > 0f)
            _dashFovTimer = Mathf.Min(_dashFovTimer + dt, _dashFovDuration);
    }

    private void TickDashRoll(float dt)
    {
        if (_dashRollDuration > 0f)
            _dashRollTimer = Mathf.Min(_dashRollTimer + dt, _dashRollDuration);
    }

    private void TickLandPunch(float dt)
    {
        _landPunchTimer = Mathf.Max(0f, _landPunchTimer - dt);
    }

    private void TickLandFov(float dt)
    {
        _landFovTimer = Mathf.Max(0f, _landFovTimer - dt);
    }

    private void TickLandShake(float dt)
    {
        _landShakeTimer = Mathf.Max(0f, _landShakeTimer - dt);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  LAYER EVALUATORS — pure functions of timer → offset
    //  No stored "previous value", so skipped frames can't drift.
    // ═══════════════════════════════════════════════════════════════════════

    // ─── Dash FOV ──────────────────────────────────────────────────────────

    private float EvalDashFov()
    {
        if (_dashFovDuration <= 0f) return 0f;
        float t = _dashFovTimer / _dashFovDuration;

        if (_dashFovAttacking)
        {
            // Ease-out into the kick
            return dashFovAdd * EaseOutQuad(t);
        }
        else
        {
            // Ease-out back to zero
            return dashFovAdd * (1f - EaseOutQuad(t));
        }
    }

    // ─── Dash Roll ─────────────────────────────────────────────────────────

    private float EvalDashRoll()
    {
        if (_dashRollDuration <= 0f || Mathf.Approximately(_dashRollDirection, 0f))
            return 0f;

        float t = _dashRollTimer / _dashRollDuration;

        if (_dashRollAttacking)
            return dashRollAngle * _dashRollDirection * EaseOutQuad(t);
        else
            return dashRollAngle * _dashRollDirection * (1f - EaseOutQuad(t));
    }

    // ─── Land Punch (position) ─────────────────────────────────────────────

    private Vector3 EvalLandPunchPos()
    {
        if (_landPunchTimer <= 0f) return Vector3.zero;

        // t goes 1 → 0 (remaining → done)
        float t = _landPunchTimer / landPunchDuration;

        // Curve: sharp dip then smooth recover
        // sin(π·t) peaks at t=0.5 — but we want the peak at the start,
        // so use sin(π · (1-t)) which peaks right after impact.
        float curve = Mathf.Sin(Mathf.PI * (1f - t));

        // Dampen with t so the tail fades to exactly zero
        float offset = -landPunchDown * _landPunchScale * curve * t;

        return new Vector3(0f, offset, 0f);
    }

    // ─── Land Punch (pitch kick) ───────────────────────────────────────────

    private float EvalLandPunchPitch()
    {
        if (_landPunchTimer <= 0f) return 0f;

        float t     = _landPunchTimer / landPunchDuration;
        float curve = Mathf.Sin(Mathf.PI * (1f - t)) * t;

        return landPitchKick * _landPunchScale * curve;
    }

    // ─── Land FOV ──────────────────────────────────────────────────────────

    private float EvalLandFov()
    {
        if (_landFovTimer <= 0f) return 0f;

        float t = _landFovTimer / landFovDuration;
        // Quick squeeze in, ease out
        float curve = Mathf.Sin(Mathf.PI * t);

        return -landFovSqueeze * _landFovScale * curve;
    }

    // ─── Land Shake ────────────────────────────────────────────────────────

    private Vector3 EvalLandShake()
    {
        if (_landShakeTimer <= 0f) return Vector3.zero;

        float t = _landShakeTimer / landShakeDuration;

        // Decaying high-frequency oscillation
        float decay = t * t;   // quadratic fade-out
        float wave  = Mathf.Sin(Time.time * landShakeFrequency * Mathf.PI * 2f);

        float intensity = landShakeIntensity * _landShakeScale * decay;

        return new Vector3(
            wave * intensity * 0.6f,       // horizontal shake (subtle)
            Mathf.Abs(wave) * intensity,   // vertical shake (dominant)
            0f
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EASING HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Ease-out quadratic: fast start, gentle settle.</summary>
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — for manual triggers from other systems
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Force a landing-style impact effect (e.g. explosion knockback).</summary>
    public void TriggerImpact(float intensity01)
    {
        float scale = Mathf.Clamp01(intensity01);
        _landPunchTimer = landPunchDuration;
        _landPunchScale = scale;
        _landFovTimer   = landFovDuration;
        _landFovScale   = scale;
        _landShakeTimer = landShakeDuration;
        _landShakeScale = scale;
    }

    /// <summary>Force-reset every layer to zero. Safe to call any time.</summary>
    public void ResetAll()
    {
        _dashFovTimer   = 0f; _dashFovDuration  = 0f;
        _dashRollTimer  = 0f; _dashRollDuration = 0f;
        _landPunchTimer = 0f;
        _landFovTimer   = 0f;
        _landShakeTimer = 0f;

        if (playerCamera != null)
            playerCamera.fieldOfView = _baseFov;
        if (cameraRoot != null)
        {
            cameraRoot.localPosition    = _baseLocalPos;
            Vector3 e = cameraRoot.localEulerAngles;
            e.z = 0f;
            cameraRoot.localEulerAngles = e;
        }
    }
}