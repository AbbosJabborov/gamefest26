using UnityEngine;

/// <summary>
/// Fires the dust impact particle system on bounce and stick.
///
/// No detach/reattach needed — the particle system's Simulation Space
/// is set to World in the Inspector, so particles stay in place in
/// world space even though the system is a child of the stone.
/// </summary>
[RequireComponent(typeof(ThrowableStone))]
public class StoneImpactFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem impactParticles;

    [Header("Scale")]
    [Tooltip("Burst scale on a regular bounce.")]
    [SerializeField] private float bounceScale = 1f;

    [Tooltip("Burst scale on final stick — slightly bigger for emphasis.")]
    [SerializeField] private float stickScale  = 1.4f;

    private ThrowableStone _stone;

    private void Awake() => _stone = GetComponent<ThrowableStone>();

    private void OnEnable()
    {
        _stone.OnBounced += HandleBounce;
        _stone.OnStuck   += HandleStuck;
    }

    private void OnDisable()
    {
        _stone.OnBounced -= HandleBounce;
        _stone.OnStuck   -= HandleStuck;
    }

    private void HandleBounce(int remainingBounces) => Burst(bounceScale);
    private void HandleStuck()                       => Burst(stickScale);

    private void Burst(float scale)
    {
        if (impactParticles == null) return;

        // Scale the burst — stop first so re-triggering mid-burst resets cleanly
        impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        impactParticles.transform.localScale = Vector3.one * scale;

        // Simulation Space = World means particles stay in world space
        // after spawning even though this transform is a child of the stone.
        // No SetParent needed.
        impactParticles.Play();
    }
}
