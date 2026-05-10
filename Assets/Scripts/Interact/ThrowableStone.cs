using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class ThrowableStone : MonoBehaviour, IInteractable
{
    // ─── Static Registry ───────────────────────────────────────────────────

    private static readonly List<ThrowableStone> _allStones = new();
    public  static IReadOnlyList<ThrowableStone> AllStones  => _allStones;

    public static List<ThrowableStone> GetOwnedBy(PlayerInteractor owner)
    {
        var result = new List<ThrowableStone>();
        foreach (var s in _allStones)
            if (s.Owner == owner) result.Add(s);
        return result;
    }

    // ─── Events ────────────────────────────────────────────────────────────

    public event Action<Vector3> OnThrown;
    public event Action<int>     OnBounced;
    public event Action          OnStuck;
    public event Action          OnPickedUp;
    public event Action          OnRecallStarted;
    public event Action          OnRecallArrived;

    // ─── IInteractable ─────────────────────────────────────────────────────

    public string InteractPrompt => "pick up";

    public void Interact(PlayerInteractor playerInteractor)
    {
        Owner = playerInteractor;
        playerInteractor.GrabStone(this);
        FlyToHand(playerInteractor.HoldPoint).Forget();
    }

    // ─── Public State ──────────────────────────────────────────────────────

    public bool             IsHeld          { get; private set; }
    public bool             IsStuck         { get; private set; }
    public bool             IsRecalling     { get; private set; }
    public bool             IsAnimating     { get; private set; }
    public float            RecallProgress  { get; private set; }
    public PlayerInteractor Owner           { get; private set; }

    // ─── Inspector ─────────────────────────────────────────────────────────

    [SerializeField] private StoneZoneController interactor;
    [SerializeField] private TrailRenderer       flightTrail;

    [Header("Pickup Flight (E)")]
    [SerializeField] private float pickupDuration = 0.15f;
    [SerializeField] private Ease  pickupEase     = Ease.InQuad;

    [Header("Recall — Phase 0: Wobble")]
    [SerializeField] private float wobbleDuration  = 0.25f;
    [SerializeField] private float wobbleStrength  = 0.06f;
    [SerializeField] private int   wobbleVibrato   = 30;

    [Header("Recall — Phase 1: Jerk Lift")]
    [SerializeField] private float liftDuration  = 0.12f;
    [SerializeField] private float liftHeight    = 0.5f;
    [SerializeField] private Ease  liftEase      = Ease.OutBack;

    [Header("Recall — Phase 2: Curved Rush")]
    [SerializeField] private float rushDuration  = 0.4f;
    [SerializeField] private Ease  rushEase      = Ease.InCubic;
    [SerializeField] private float arcHeightMul  = 0.25f;

    [Header("Recall — Flight Spin")]
    [SerializeField] private float spinRevolutions = 3f;
    [SerializeField] private float tumbleStrength  = 120f;

    [Header("Recall — Phase 3: Catch Overshoot")]
    [SerializeField] private float overshootDistance = 0.15f;
    [SerializeField] private float overshootDuration = 0.1f;
    [SerializeField] private int   overshootVibrato  = 1;

    // ─── Private ───────────────────────────────────────────────────────────

    private Rigidbody _rb;
    private Collider  _col;
    private Renderer[] _renderers;
    private int       _remainingBounces;
    private Vector3   _preCollisionVelocity;

    private Tween                   _activeTween;
    private Tween                   _spinTween;
    private CancellationTokenSource _flightCts;
    private Tween                   _layoutTween;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb        = GetComponent<Rigidbody>();
        _col       = GetComponent<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _allStones.Add(this);

        if (flightTrail) flightTrail.enabled = false;
    }

    private void FixedUpdate()
    {
        if (!IsHeld && !IsStuck && !IsAnimating)
            _preCollisionVelocity = _rb.linearVelocity;
    }

    private void OnDestroy()
    {
        KillFlight();
        KillLayoutTween();
        _allStones.Remove(this);
    }

    // ─── Held Layout ──────────────────────────────────────────────────────

    public void SetHeldVisible(bool visible)
    {
        foreach (var r in _renderers)
            r.enabled = visible;
    }

    /// Smoothly moves the stone to a local position within holdPoint.
    /// Used by PlayerInteractor to animate stack rearrangement.
    public void AnimateToStack(Vector3 localTarget, float duration, Ease ease)
    {
        KillLayoutTween();
        _layoutTween = transform
            .DOLocalMove(localTarget, duration)
            .SetEase(ease)
            .SetLink(gameObject);
    }

    private void KillLayoutTween()
    {
        _layoutTween?.Kill();
        _layoutTween = null;
    }

    // ─── Pickup Flight (E key) ─────────────────────────────────────────────

    private async UniTaskVoid FlyToHand(Transform holdPoint)
    {
        interactor.Deactivate();
        PrepareFlight();
        IsHeld = true;

        // Parent FIRST so stone moves with camera during flight.
        // DOLocalMove animates from current offset → zero (holdPoint center).
        Vector3 worldPos = transform.position;
        transform.SetParent(holdPoint);
        transform.position = worldPos;   // preserve world pos after reparenting

        _activeTween = transform
            .DOLocalMove(Vector3.zero, pickupDuration)
            .SetEase(pickupEase)
            .SetLink(gameObject);

        if (!await SafeAwait(_activeTween)) return;

        FinishPickup(holdPoint);
    }

    private void FinishPickup(Transform holdPoint)
    {
        KillSpin();

        IsHeld      = true;
        IsStuck     = false;
        IsRecalling = false;
        IsAnimating = false;

        _rb.isKinematic     = true;
        _rb.constraints     = RigidbodyConstraints.None;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _col.enabled        = false;
        if (flightTrail) flightTrail.enabled = false;

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        OnPickedUp?.Invoke();
    }

    // ─── Throw ─────────────────────────────────────────────────────────────

    public void Throw(Vector3 velocity, int bounceCount)
    {
        KillFlight();
        KillSpin();
        KillLayoutTween();

        IsHeld      = false;
        IsStuck     = false;
        IsRecalling = false;
        IsAnimating = false;

        SetHeldVisible(true);   // make sure it's visible when leaving hand

        transform.SetParent(null);

        _rb.constraints     = RigidbodyConstraints.None;
        _rb.isKinematic     = false;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _col.enabled        = true;
        _col.isTrigger      = false;

        _remainingBounces = bounceCount;
        _rb.AddForce(velocity, ForceMode.VelocityChange);

        OnThrown?.Invoke(velocity);
    }

    // ─── Recall (Mjölnir) ──────────────────────────────────────────────────

    public void StartRecall(Transform target)
    {
        if (IsHeld || IsAnimating) return;
        RecallFlight(target).Forget();
    }

    private async UniTaskVoid RecallFlight(Transform target)
    {
        interactor.Deactivate();
        PrepareFlight();

        IsRecalling    = true;
        RecallProgress = 0f;

        float totalDur = wobbleDuration + liftDuration + rushDuration + overshootDuration;

        OnRecallStarted?.Invoke();
        if (flightTrail) flightTrail.enabled = true;

        // Phase 0 — Wobble
        _activeTween = transform
            .DOShakePosition(wobbleDuration, wobbleStrength, wobbleVibrato, 90f, false, true, ShakeRandomnessMode.Harmonic)
            .SetLink(gameObject)
            .OnUpdate(() => RecallProgress = _activeTween.Elapsed() / totalDur);

        if (!await SafeAwait(_activeTween)) return;

        // Phase 1 — Jerk Lift
        Vector3 liftTarget = transform.position + Vector3.up * liftHeight;

        _activeTween = transform
            .DOMove(liftTarget, liftDuration)
            .SetEase(liftEase)
            .SetLink(gameObject)
            .OnUpdate(() =>
                RecallProgress = (wobbleDuration + _activeTween.Elapsed()) / totalDur);

        if (!await SafeAwait(_activeTween)) return;

        // Phase 2 — Curved Rush + Spin
        Vector3 rushStart = transform.position;
        float   distance  = Vector3.Distance(rushStart, target.position);
        float   arcPeak   = distance * arcHeightMul;
        Vector3 tumbleAxis = UnityEngine.Random.onUnitSphere;

        _activeTween = DOTween.To(
            () => 0f,
            t =>
            {
                if (target == null) return;

                Vector3 basePos = Vector3.Lerp(rushStart, target.position, t);
                float arc = Mathf.Sin(t * Mathf.PI) * arcPeak * (1f - t);
                transform.position = basePos + Vector3.up * arc;

                float spinAngle  = t * 360f * spinRevolutions;
                float tumble     = Mathf.Sin(t * Mathf.PI * 2f) * tumbleStrength * (1f - t);

                Vector3 toTarget = target.position - transform.position;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion lookRot  = Quaternion.LookRotation(toTarget);
                    Quaternion spin     = Quaternion.AngleAxis(spinAngle, Vector3.forward);
                    Quaternion tumbleQ  = Quaternion.AngleAxis(tumble, tumbleAxis);
                    float blendToClean  = Mathf.Pow(t, 2f);
                    Quaternion wild     = lookRot * spin * tumbleQ;
                    transform.rotation  = Quaternion.Slerp(wild, lookRot, blendToClean);
                }

                RecallProgress = (wobbleDuration + liftDuration + rushDuration * t) / totalDur;
            },
            1f,
            rushDuration
        ).SetEase(rushEase).SetLink(gameObject);

        if (!await SafeAwait(_activeTween)) return;

        // Phase 3 — Overshoot Catch
        if (target != null)
            transform.position = target.position;

        Vector3 punchDir = target != null
            ? (target.position - rushStart).normalized * overshootDistance
            : Vector3.forward * overshootDistance;

        _activeTween = transform
            .DOPunchPosition(punchDir, overshootDuration, overshootVibrato, 0f)
            .SetLink(gameObject);

        if (!await SafeAwait(_activeTween)) return;

        IsRecalling    = false;
        RecallProgress = 1f;
        _col.isTrigger = false;

        OnRecallArrived?.Invoke();
    }

    // ─── Bounce & Stick ────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        if (IsHeld || IsStuck || IsAnimating) return;

        if (_remainingBounces <= 0) { Stick(); return; }

        _rb.linearVelocity = Vector3.Reflect(
            _preCollisionVelocity,
            collision.contacts[0].normal
        );

        _remainingBounces--;
        OnBounced?.Invoke(_remainingBounces);
    }

    private void Stick()
    {
        IsStuck = true;

        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.constraints     = RigidbodyConstraints.FreezeAll;

        interactor.Activate();
        OnStuck?.Invoke();
    }

    // ─── Flight Safety ─────────────────────────────────────────────────────

    private void PrepareFlight()
    {
        KillFlight();
        KillSpin();
        KillLayoutTween();

        _flightCts  = new CancellationTokenSource();
        IsAnimating = true;
        IsStuck     = false;

        _rb.constraints     = RigidbodyConstraints.None;
        _rb.isKinematic     = true;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _col.enabled        = true;
        _col.isTrigger      = true;

        transform.SetParent(null);
    }

    private async UniTask<bool> SafeAwait(Tween tween)
    {
        try
        {
            await tween.ToUniTask(cancellationToken: _flightCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            CleanupFlight();
            return false;
        }
    }

    private void CleanupFlight()
    {
        IsAnimating    = false;
        IsRecalling    = false;
        RecallProgress = 0f;
        _col.isTrigger = false;
        if (flightTrail) flightTrail.enabled = false;
    }

    private void KillFlight()
    {
        _activeTween?.Kill();
        _activeTween = null;

        _flightCts?.Cancel();
        _flightCts?.Dispose();
        _flightCts = null;

        IsAnimating = false;
        if (flightTrail) flightTrail.enabled = false;
    }

    private void KillSpin()
    {
        _spinTween?.Kill();
        _spinTween = null;
    }
}