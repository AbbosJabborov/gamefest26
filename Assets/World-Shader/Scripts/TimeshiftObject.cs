using UnityEngine;

public enum TimeshiftType { PastOnly, PresentOnly }

public class TimeshiftObject : MonoBehaviour
{
    public TimeshiftType type = TimeshiftType.PastOnly;

    Collider[] _colliders;

    void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>();
        SetSolid(type == TimeshiftType.PresentOnly);
    }

    public void UpdateZone(Vector3 stonePos, float radius)
    {
        bool inZone = radius > 0.01f &&
                      Vector3.Distance(transform.position, stonePos) < radius;

        SetSolid(type == TimeshiftType.PastOnly ? inZone : !inZone);
    }

    void SetSolid(bool solid)
    {
        foreach (var c in _colliders)
            c.enabled = solid;
    }
}