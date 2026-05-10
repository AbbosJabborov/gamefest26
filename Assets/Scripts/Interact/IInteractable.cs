/// <summary>
/// Implement this on any GameObject the player can interact with via the E key.
///
/// Examples:
///   - ThrowableStone    → "Pick up"
///   - DoorMechanism     → "Open door" / "Close door"
///   - GeneratorSwitch   → "Activate generator"
///   - StoneFragment     → "Collect fragment"
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Shown in the UI as "Press E to [InteractPrompt]"
    /// Keep it short — verb phrase only. e.g. "pick up", "activate", "open"
    /// </summary>
    string InteractPrompt { get; }

    /// <summary>
    /// Called when the player presses E while looking at this object.
    /// The interactor is passed so the object can access player context if needed
    /// (e.g. stone needs to know the holdPoint).
    /// </summary>
    void Interact(PlayerInteractor interactor);
}
