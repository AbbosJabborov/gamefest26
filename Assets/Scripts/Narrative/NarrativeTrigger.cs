using UnityEngine;

/// <summary>
/// Place on any GameObject with a Collider (set Is Trigger = true).
/// When the player enters, fires the narrative step once then disables itself.
///
/// Tag your Player GameObject as "Player" or assign the player layer
/// to the layerMask so other objects don't trigger it.
///
/// Setup:
///   Any GameObject in the scene
///     ├── Collider    (Box/Sphere, Is Trigger = true)
///     └── NarrativeTrigger
///           stepId: "move"   ← must match an ID in NarrativeManager's steps list
/// </summary>
public class NarrativeTrigger : MonoBehaviour
{
    [Tooltip("Must match a step ID defined in NarrativeManager.")]
    [SerializeField] private string stepId;

    [Tooltip("Only triggers if the entering collider is on this layer. "
           + "Set to Player layer to avoid accidental triggers.")]
    [SerializeField] private LayerMask playerLayer = ~0;

    private bool _fired;

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

        _fired = true;

        NarrativeManager.Instance?.Play(stepId);

        // Disable collider so it never fires again — keeps the GameObject
        // in the scene in case you want to inspect or re-enable it in editor
        GetComponent<Collider>().enabled = false;
    }
}
