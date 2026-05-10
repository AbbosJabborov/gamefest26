using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private Camera            playerCamera;
    [SerializeField] private Transform         holdPoint;
    [SerializeField] private Transform         throwOrigin;
    [SerializeField] private TrajectoryPreview preview;
    [SerializeField] private FPSLook           fpsLook;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 4f;

    [Header("Throw")]
    [SerializeField] private float throwAngleOffset = 10f;
    [SerializeField] private float minThrowForce    = 5f;
    [SerializeField] private float maxThrowForce    = 20f;

    [Header("Charge")]
    [SerializeField] private float maxChargeTime  = 1.5f;
    [SerializeField] private float chargeExponent = 1f;

    [Header("Bounce")]
    [SerializeField] private int bounceCount    = 0;
    [SerializeField] private int maxBounceCount = 2;

    [Header("Inventory")]
    [SerializeField] private int   maxHeldStones      = 5;
    [SerializeField] private float stoneStackSpacing   = 0.25f;
    [SerializeField] private float stackAnimDuration   = 0.25f;
    [SerializeField] private Ease  stackAnimEase       = Ease.OutBack;

    // ─── UI-Ready State ────────────────────────────────────────────────────
    //
    //  Hook these up to your HUD — no UI code here, just clean data.
    //
    //  HeldStoneCount   → "3 / 5" inventory counter
    //  ActiveStoneIndex → highlight the active slot
    //  ActiveStone      → show stone name / icon in the active slot
    //  ThrownStoneCount → "2 stones out" indicator
    //  IsRecalling      → show recall VFX / icon pulse
    //  ChargeAmount     → charge bar fill (0-1)
    //  BounceCount      → bounce mode indicator (0 / 1 / 2)
    //

    public IInteractable  CurrentInteractable     { get; private set; }
    public bool           IsLookingAtInteractable => CurrentInteractable != null;
    public bool           IsHoldingStone          => _heldStones.Count > 0;
    public bool           IsCharging              => _isCharging;
    public bool           IsRecalling             => _recallingStone != null;
    public int            BounceCount             => bounceCount;
    public int            MaxBounceCount          => maxBounceCount;
    public float          ChargeAmount            => _chargeAmount;
    public Transform      HoldPoint               => holdPoint;
    public Camera         PlayerCamera            => playerCamera;

    public int            HeldStoneCount          => _heldStones.Count;
    public int            MaxHeldStones           => maxHeldStones;
    public int            ActiveStoneIndex        => _activeIndex;
    public ThrowableStone ActiveStone             => _activeIndex >= 0 && _activeIndex < _heldStones.Count
                                                        ? _heldStones[_activeIndex]
                                                        : null;
    public int            ThrownStoneCount        => _thrownStones.Count;

    /// All held stones as readonly list — for drawing inventory slots.
    public IReadOnlyList<ThrowableStone> HeldStones   => _heldStones;

    /// All thrown stones as readonly list — for minimap markers, etc.
    public IReadOnlyList<ThrowableStone> ThrownStones => _thrownStones;

    // ─── Private ───────────────────────────────────────────────────────────

    private readonly List<ThrowableStone> _heldStones   = new();
    private readonly List<ThrowableStone> _thrownStones = new();
    private ThrowableStone               _recallingStone;
    private int                          _activeIndex;
    private bool                         _isCharging;
    private float                        _chargeTime;
    private float                        _chargeAmount;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Update()
    {
        ScanForInteractable();
        HandleThrowAiming();
        UpdateTrajectory();
    }

    // ─── Interaction ───────────────────────────────────────────────────────

    private void ScanForInteractable()
    {
        if (_heldStones.Count >= maxHeldStones) { CurrentInteractable = null; return; }

        Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);

        CurrentInteractable = Physics.Raycast(ray, out RaycastHit hit, interactDistance)
            ? hit.collider.GetComponent<IInteractable>()
            : null;
    }

    public void OnInteract(InputValue value)
    {
        if (!value.isPressed) return;

        // Drop active stone if holding any
        if (_heldStones.Count > 0 && CurrentInteractable == null)
        {
            Drop();
            return;
        }

        CurrentInteractable?.Interact(this);
    }

    // ─── Cycle Active Stone (scroll / Q) ───────────────────────────────────

    /// Bind to scroll wheel or Q key in Input Actions.
    public void OnCycleStone(InputValue value)
    {
        if (_heldStones.Count <= 1) return;

        float scroll = value.Get<float>();
        int dir = scroll > 0f ? 1 : -1;

        SetActiveIndex((_activeIndex + dir + _heldStones.Count) % _heldStones.Count);
    }

    // ─── Recall (R key) ────────────────────────────────────────────────────

    public void OnRecall(InputValue value)
    {
        if (!value.isPressed)          return;
        if (_recallingStone != null)   return;   // one at a time
        if (_heldStones.Count >= maxHeldStones) return; // inventory full

        PurgeQueue();
        if (_thrownStones.Count == 0)  return;

        _recallingStone = _thrownStones[0];
        _thrownStones.RemoveAt(0);

        _recallingStone.OnRecallArrived += HandleRecallArrived;
        _recallingStone.StartRecall(holdPoint);
    }

    // ─── Stone Management ──────────────────────────────────────────────────

    public void GrabStone(ThrowableStone stone)
    {
        if (_heldStones.Count >= maxHeldStones) return;

        _heldStones.Add(stone);
        _thrownStones.Remove(stone);

        if (_recallingStone == stone)
        {
            _recallingStone.OnRecallArrived -= HandleRecallArrived;
            _recallingStone = null;
        }

        // New stone becomes active
        SetActiveIndex(_heldStones.Count - 1);
    }

    private void HandleRecallArrived()
    {
        if (_recallingStone == null) return;

        var stone = _recallingStone;
        stone.OnRecallArrived -= HandleRecallArrived;
        _recallingStone = null;

        stone.Interact(this);
    }

    private void PurgeQueue()
    {
        _thrownStones.RemoveAll(s => s == null || s.IsHeld);
    }

    // ─── Held Stones Layout ──────────────────────────────────────────────

    private void SetActiveIndex(int index)
    {
        _activeIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _heldStones.Count - 1));
        RefreshHeldLayout();
    }

    /// Stacks all held stones vertically at holdPoint with animation.
    /// Active stone at bottom (offset 0), others above with OutBack ease.
    private void RefreshHeldLayout()
    {
        for (int i = 0; i < _heldStones.Count; i++)
        {
            if (_heldStones[i] == null) continue;

            int stackPos = (i - _activeIndex + _heldStones.Count) % _heldStones.Count;
            Vector3 target = Vector3.up * (stackPos * stoneStackSpacing);

            _heldStones[i].AnimateToStack(target, stackAnimDuration, stackAnimEase);
            _heldStones[i].SetHeldVisible(true);
        }
    }

    // ─── Throw & Charge ────────────────────────────────────────────────────

    private void HandleThrowAiming()
    {
        var active = ActiveStone;

        if (active == null || active.IsAnimating || !fpsLook.IsCursorLocked)
        {
            CancelCharge();
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            bounceCount = (bounceCount + 1) % (maxBounceCount + 1);
            preview?.Clear();
            AudioManager.Instance?.PlayOneShotUI(
                AudioManager.Instance.BounceClick,
                volume: 0.6f,
                pitchOffset: bounceCount * 0.08f
            );
        }

        if (Mouse.current.leftButton.isPressed)
        {
            _isCharging  = true;
            _chargeTime  = Mathf.Min(_chargeTime + Time.deltaTime, maxChargeTime);
            _chargeAmount = Mathf.Pow(_chargeTime / maxChargeTime, chargeExponent);
        }

        if (_isCharging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Throw();
            CancelCharge();
        }
    }

    private void UpdateTrajectory()
    {
        if (!_isCharging || ActiveStone == null) { preview?.Clear(); return; }
        preview?.DrawTrajectory(throwOrigin.position, GetThrowVelocity(), bounceCount);
    }

    private void Throw()
    {
        var stone = ActiveStone;
        if (stone == null) return;

        _heldStones.RemoveAt(_activeIndex);
        _thrownStones.Add(stone);

        // Snap to throw origin so trajectory matches the preview exactly
        stone.transform.SetParent(null);
        stone.transform.position = throwOrigin.position;

        stone.Throw(GetThrowVelocity(), bounceCount);

        SetActiveIndex(Mathf.Min(_activeIndex, _heldStones.Count - 1));
    }

    private void Drop()
    {
        var stone = ActiveStone;
        if (stone == null) return;

        CancelCharge();

        _heldStones.RemoveAt(_activeIndex);
        _thrownStones.Add(stone);

        stone.Throw(Vector3.zero, 0);

        SetActiveIndex(Mathf.Min(_activeIndex, _heldStones.Count - 1));
    }

    private void CancelCharge()
    {
        _isCharging   = false;
        _chargeTime   = 0f;
        _chargeAmount = 0f;
    }

    private Vector3 GetThrowVelocity()
    {
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, _chargeAmount);

        Vector3 dir = Quaternion.AngleAxis(
            -throwAngleOffset, playerCamera.transform.right
        ) * playerCamera.transform.forward;

        return dir * force;
    }

    private void OnDestroy()
    {
        if (_recallingStone != null)
            _recallingStone.OnRecallArrived -= HandleRecallArrived;
    }
    
}