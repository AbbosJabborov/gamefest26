using UnityEngine;
using DG.Tweening;


public class StoneZoneController : MonoBehaviour
{
    [SerializeField] float targetRadius = 5f;
    [SerializeField] float expandDuration = 0.8f;
    [SerializeField] float shrinkDuration = 0.4f;
    [SerializeField] Ease expandEase = Ease.OutCubic;
    [SerializeField] Ease shrinkEase = Ease.InCubic;

    ShaderInteractorPosition _sip;
    Tween _radiusTween;
    TimeshiftObject[] _timeshiftObjects;

    void Awake()
    {
        _sip = GetComponent<ShaderInteractorPosition>();
        _sip.radius = 0f;
    }

    void Start()
    {
        _timeshiftObjects = FindObjectsOfType<TimeshiftObject>();
    }

    void Update()
    {
        foreach (var obj in _timeshiftObjects)
            obj.UpdateZone(transform.position, _sip.radius);
    }

    public void Activate()
    {
        _radiusTween?.Kill();
        _radiusTween = DOTween.To(
            () => _sip.radius,
            x => _sip.radius = x,
            targetRadius,
            expandDuration
        ).SetEase(expandEase);
    }

    public void Deactivate()
    {
        _radiusTween?.Kill();
        _radiusTween = DOTween.To(
            () => _sip.radius,
            x => _sip.radius = x,
            0f,
            shrinkDuration
        ).SetEase(shrinkEase);
    }
}