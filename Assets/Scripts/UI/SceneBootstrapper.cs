using UnityEngine;

/// <summary>
/// Drop this on any GameObject in your Game scene (or any scene that isn't Main Menu).
/// It tells the TransitionManager to dissolve the black overlay away on arrival.
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    [Tooltip("UV origin for the uncover dissolve. Center (0.5, 0.5) by default.")]
    public Vector2 uncoverOrigin = new Vector2(0.5f, 0.5f);

    void Start()
    {
        SceneTransitionManager.Instance?.Uncover(uncoverOrigin);
    }
}
