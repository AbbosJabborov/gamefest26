using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any GameObject in the Main Menu scene.
/// Wire up the Play and Quit buttons in the Inspector.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button playButton;
    public Button quitButton;

    [Header("Scene")]
    [Tooltip("Build index of the game scene to load.")]
    public int gameSceneBuildIndex = 1;

    void Start()
    {
        // Uncover the screen when main menu loads
        SceneTransitionManager.Instance?.Uncover();

        playButton.onClick.AddListener(OnPlay);
        quitButton.onClick.AddListener(OnQuit);
    }

    void OnPlay()
    {
        // Pass the Play button's screen position as the cover origin
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(
            null, playButton.transform.position);

        SceneTransitionManager.Instance?.TransitionToScene(gameSceneBuildIndex, screenPos);
    }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
