using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person platformer motor tuned for challenge:
///   - Momentum:  sustained running builds speed, stopping or sharp turns kill it
///   - Tight air control:  commit to your arc
///   - Air dash:  one horizontal burst per jump — skill-expressive gap closer
///   - Sprint stamina:  a resource to manage mid-run
///   - Landing recovery:  big drops stagger you briefly
///   - Short coyote / buffer:  rewards timing over mashing
///   - Variable jump height:  tap = short hop, hold = full arc
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSMotor : MonoBehaviour
{
    // ─── Movement ──────────────────────────────────────────────────────────

    [Header("Movement")]
    [SerializeField] private float walkSpeed       = 5f;
    [SerializeField] private float sprintSpeed     = 9f;
    [SerializeField] private float groundAccel     = 10f;
    [SerializeField] private float groundDecel     = 18f;

    [Header("Momentum")]
    [Tooltip("Max bonus speed earned by sustained running.")]
    [SerializeField] private float momentumBonus     = 2.5f;
    [Tooltip("Seconds of continuous running to reach full momentum.")]
    [SerializeField] private float momentumBuildTime = 2.5f;
    [Tooltip("How fast momentum bleeds when you stop or turn hard.")]
    [SerializeField] private float momentumDecay     = 4f;

    [Header("Air Control")]
    [Tooltip("Fraction of ground accel while airborne. Low = committed arcs.")]
    [SerializeField, Range(0f, 1f)] private float airControlFactor = 0.2f;

    // ─── Jump ──────────────────────────────────────────────────────────────

    [Header("Jump")]
    [SerializeField] private float jumpHeight        = 1.6f;
    [SerializeField] private float fallMultiplier    = 2.8f;
    [SerializeField] private float lowJumpMultiplier = 4.5f;

    [Tooltip("Short coyote — rewards precision.")]
    [SerializeField] private float coyoteTime      = 0.08f;
    [Tooltip("Tight buffer — rewards timing.")]
    [SerializeField] private float jumpBufferTime  = 0.07f;

    // ─── Air Dash ──────────────────────────────────────────────────────────

    [Header("Air Dash")]
    [Tooltip("Horizontal burst speed.")]
    [SerializeField] private float dashSpeed    = 12f;
    [Tooltip("Duration of the dash impulse.")]
    [SerializeField] private float dashDuration = 0.12f;
    [Tooltip("Slight vertical lift to keep you airborne through the dash.")]
    [SerializeField] private float dashLift     = 1.5f;

    // ─── Sprint Stamina ────────────────────────────────────────────────────

    [Header("Stamina")]
    [SerializeField] private float maxStamina       = 100f;
    [SerializeField] private float staminaDrain      = 25f;
    [SerializeField] private float staminaRegen      = 15f;
    [Tooltip("Delay after depletion before regen kicks in.")]
    [SerializeField] private float staminaRegenDelay = 1.2f;

    // ─── Landing Recovery ──────────────────────────────────────────────────

    [Header("Landing Recovery")]
    [Tooltip("Fall speed that triggers the stagger.")]
    [SerializeField] private float hardLandingThreshold = 12f;
    [Tooltip("Speed multiplier during stagger.")]
    [SerializeField, Range(0f, 1f)] private float recoverySpeedMult = 0.3f;
    [SerializeField] private float recoveryDuration = 0.45f;

    // ─── Gravity ───────────────────────────────────────────────────────────

    [Header("Gravity")]
    [SerializeField] private float gravityScale = 1f;

    // ─── Public State ──────────────────────────────────────────────────────

    public bool  IsGrounded   { get; private set; }
    public bool  IsSprinting  => _sprintHeld && _stamina > 0f && IsMoving;
    public bool  IsMoving     => _smoothInput.sqrMagnitude > 0.01f && IsGrounded;
    public bool  IsDashing    => _dashTimer > 0f;
    public bool  IsRecovering => _recoveryTimer > 0f;
    public float Momentum01   => _momentum / momentumBonus;
    public float Stamina01    => _stamina / maxStamina;
    public float CurrentSpeed => _controller.velocity.HorizontalMag();
    public Vector3 Velocity  => _controller.velocity;
    public Vector2 MoveInput => _moveInput;

    // ─── Private ───────────────────────────────────────────────────────────

    private CharacterController _controller;
    private Vector2 _moveInput;
    private Vector2 _smoothInput;
    private float   _verticalVelocity;
    private bool    _sprintHeld;
    private bool    _jumpHeld;

    private float _coyoteTimer;
    private float _jumpBufferTimer;

    private float _momentum;
    private float _runTimer;

    private bool    _dashAvailable;
    private float   _dashTimer;
    private Vector3 _dashDir;

    private float _stamina;
    private float _staminaRegenCooldown;

    private float _recoveryTimer;
    private float _prevVerticalVelocity;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stamina    = maxStamina;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        GroundCheck();
        UpdateTimers(dt);
        SmoothInput(dt);
        UpdateMomentum(dt);
        UpdateStamina(dt);
        ApplyGravity(dt);
        TryConsumeJump();
        UpdateDash(dt);
        MoveCharacter(dt);

        _prevVerticalVelocity = _verticalVelocity;
    }

    // ─── Ground ────────────────────────────────────────────────────────────

    private void GroundCheck()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = _controller.isGrounded;

        if (IsGrounded)
        {
            _coyoteTimer   = coyoteTime;
            _dashAvailable = true;

            if (_verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (!wasGrounded)
                EvaluateLanding();
        }
    }

    private void EvaluateLanding()
    {
        float fallSpeed = Mathf.Abs(_prevVerticalVelocity);
        if (fallSpeed >= hardLandingThreshold)
        {
            _recoveryTimer = recoveryDuration;
            _momentum      = 0f;
            _runTimer      = 0f;
        }
    }

    // ─── Timers ────────────────────────────────────────────────────────────

    private void UpdateTimers(float dt)
    {
        _coyoteTimer     -= dt;
        _jumpBufferTimer -= dt;
        _recoveryTimer   -= dt;
    }

    // ─── Input Smoothing ───────────────────────────────────────────────────

    private void SmoothInput(float dt)
    {
        bool hasInput = _moveInput.sqrMagnitude > 0.01f;
        float rate    = hasInput ? groundAccel : groundDecel;

        if (!IsGrounded)
            rate = groundAccel * airControlFactor;

        _smoothInput = Vector2.MoveTowards(_smoothInput, _moveInput, rate * dt);
    }

    // ─── Momentum ──────────────────────────────────────────────────────────

    private void UpdateMomentum(float dt)
    {
        bool running = _smoothInput.sqrMagnitude > 0.6f
                    && IsGrounded
                    && !IsRecovering;

        // Punish sharp direction reversals
        if (running && _moveInput.sqrMagnitude > 0.01f)
        {
            float dot = Vector2.Dot(_smoothInput.normalized, _moveInput.normalized);
            if (dot < 0.3f)
            {
                _momentum -= momentumDecay * 2f * dt;
                running = false;
            }
        }

        if (running)
        {
            _runTimer += dt;
            float t    = Mathf.Clamp01(_runTimer / momentumBuildTime);
            _momentum  = momentumBonus * (t * t);   // ease-in curve
        }
        else
        {
            _runTimer = Mathf.Max(0f, _runTimer - momentumDecay * dt);
            _momentum = Mathf.MoveTowards(_momentum, 0f, momentumDecay * dt);
        }
    }

    // ─── Stamina ───────────────────────────────────────────────────────────

    private void UpdateStamina(float dt)
    {
        if (IsSprinting)
        {
            _stamina              = Mathf.Max(0f, _stamina - staminaDrain * dt);
            _staminaRegenCooldown = staminaRegenDelay;
        }
        else
        {
            _staminaRegenCooldown -= dt;
            if (_staminaRegenCooldown <= 0f)
                _stamina = Mathf.Min(maxStamina, _stamina + staminaRegen * dt);
        }
    }

    // ─── Gravity ───────────────────────────────────────────────────────────

    private void ApplyGravity(float dt)
    {
        if (IsDashing) return;

        float g = Physics.gravity.y * gravityScale;

        if (_verticalVelocity < 0f)
            _verticalVelocity += g * fallMultiplier * dt;
        else if (_verticalVelocity > 0f && !_jumpHeld)
            _verticalVelocity += g * lowJumpMultiplier * dt;
        else
            _verticalVelocity += g * dt;
    }

    // ─── Jump ──────────────────────────────────────────────────────────────

    private void TryConsumeJump()
    {
        if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f) return;
        if (IsRecovering) return;

        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityScale);
        _jumpBufferTimer  = 0f;
        _coyoteTimer      = 0f;
    }

    // ─── Air Dash ──────────────────────────────────────────────────────────

    private void UpdateDash(float dt)
    {
        if (_dashTimer <= 0f) return;
        _dashTimer -= dt;

        if (_dashTimer <= 0f)
            _verticalVelocity = Mathf.Max(_verticalVelocity, 0f);
    }

    private void TryDash()
    {
        if (IsGrounded || !_dashAvailable || IsDashing) return;

        _dashAvailable = false;
        _dashTimer     = dashDuration;

        Vector3 inputDir =
            transform.right   * _moveInput.x +
            transform.forward * _moveInput.y;

        _dashDir = inputDir.sqrMagnitude > 0.01f
            ? inputDir.normalized
            : transform.forward;

        _verticalVelocity = dashLift;
    }

    // ─── Move ──────────────────────────────────────────────────────────────

    private void MoveCharacter(float dt)
    {
        float speed = _sprintHeld && _stamina > 0f ? sprintSpeed : walkSpeed;
        speed += _momentum;

        if (IsRecovering)
            speed *= recoverySpeedMult;

        Vector3 horizontal;

        if (IsDashing)
        {
            horizontal = _dashDir * dashSpeed;
        }
        else
        {
            horizontal =
                (transform.right   * _smoothInput.x +
                 transform.forward * _smoothInput.y) * speed;
        }

        Vector3 motion = horizontal + Vector3.up * _verticalVelocity;
        _controller.Move(motion * dt);
    }

    // ─── Input Callbacks ───────────────────────────────────────────────────

    public void OnMove(InputValue value)   => _moveInput  = value.Get<Vector2>();
    public void OnSprint(InputValue value) => _sprintHeld = value.isPressed;

    public void OnJump(InputValue value)
    {
        _jumpHeld = value.isPressed;
        if (value.isPressed)
            _jumpBufferTimer = jumpBufferTime;
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed)
            TryDash();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out LevelEnder ender))
        {
            ender.End();
        }
    }
}

// ─── Extension ─────────────────────────────────────────────────────────────

public static class Vector3Extensions
{
    public static float HorizontalMag(this Vector3 v)
        => Mathf.Sqrt(v.x * v.x + v.z * v.z);
}