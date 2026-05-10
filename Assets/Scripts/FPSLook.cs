using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smooth first-person camera with damped input and cursor management.
///
/// To extend:
///   - Camera shake:  add ShakeOffset, apply in ApplyLook()
///   - Head bob:      read FPSMotor.IsMoving, drive cameraRoot.localPosition
///   - FOV kick:      lerp Camera.fieldOfView when FPSMotor.IsSprinting
///   - Landing punch: briefly dip cameraRoot.localPosition.y on hard landings
///   - Dash zoom:     widen FOV during FPSMotor.IsDashing for speed feel
/// </summary>
public class FPSLook : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Sensitivity")]
    [SerializeField] private float sensitivity = 150f;

    [Header("Smoothing")]
    [Tooltip("0 = raw. 0.03–0.06 feels fluid without laggy.")]
    [SerializeField, Range(0f, 0.15f)] private float smoothTime = 0.04f;

    [Header("Vertical Clamp")]
    [SerializeField] private float lookUpLimit   = 80f;
    [SerializeField] private float lookDownLimit = 80f;

    [Header("References")]
    [SerializeField] private Transform cameraRoot;

    // ─── Public State ──────────────────────────────────────────────────────

    public float PitchAngle     { get; private set; }
    public bool  IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

    /// Read/written by PauseMenu sensitivity slider.
    public float Sensitivity
    {
        get => sensitivity;
        set => sensitivity = value;
    }

    // ─── Private ───────────────────────────────────────────────────────────

    private Vector2 _lookInput;
    private Vector2 _smoothLook;
    private Vector2 _smoothLookVelocity;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (cameraRoot == null)
            Debug.LogError("[FPSLook] cameraRoot not assigned.", this);

        LockCursor();
    }

    private void Update()
    {
        HandleCursorToggle();

        if (IsCursorLocked)
            ApplyLook();
    }

    // ─── Look ──────────────────────────────────────────────────────────────

    private void ApplyLook()
    {
        float dt = Time.deltaTime;

        Vector2 rawDelta = _lookInput * sensitivity * dt;

        if (smoothTime > 0f)
        {
            _smoothLook = Vector2.SmoothDamp(
                _smoothLook, rawDelta,
                ref _smoothLookVelocity,
                smoothTime, Mathf.Infinity, dt);
        }
        else
        {
            _smoothLook = rawDelta;
        }

        PitchAngle -= _smoothLook.y;
        PitchAngle  = Mathf.Clamp(PitchAngle, -lookUpLimit, lookDownLimit);

        cameraRoot.localRotation = Quaternion.Euler(PitchAngle, 0f, 0f);
        transform.Rotate(Vector3.up * _smoothLook.x);
    }

    // ─── Cursor ────────────────────────────────────────────────────────────

    private void HandleCursorToggle()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();

        if (!IsCursorLocked && Time.timeScale > 0f && Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor();
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ─── Input ─────────────────────────────────────────────────────────────

    public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();
}
