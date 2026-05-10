using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Pause menu panel. Toggled with Escape (shares the key FPSLook already handles).
///
/// Controls:
///   - Resume       → unpause, re-lock cursor
///   - Restart      → reload current scene
///   - Menu         → load main menu scene by name
///   - Sensitivity  → writes to FPSLook.Sensitivity
///   - FOV          → writes to the player camera's fieldOfView
///   - Master Vol   → writes to AudioManager.Instance.MasterVolume
///
/// Setup:
///   Canvas (Screen Space Overlay)
///     └── PausePanel  ← this script here, starts inactive
///           ├── ResumeButton
///           ├── RestartButton
///           ├── MenuButton
///           ├── SensitivitySlider
///           ├── SensitivityValueText   (shows current value, optional)
///           ├── FOVSlider
///           ├── FOVValueText           (optional)
///           ├── VolumeSlider
///           └── VolumeValueText        (optional)
///
///   Wire all references in the Inspector.
///   Set PausePanel to inactive in the scene — the script activates it.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private FPSLook  fpsLook;
    [SerializeField] private Camera   playerCamera;
    [SerializeField] private GameObject pausePanel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [Header("Sensitivity")]
    [SerializeField] private Slider       sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;
    [SerializeField] private float        sensitivityMin = 10f;
    [SerializeField] private float        sensitivityMax = 400f;

    [Header("FOV")]
    [SerializeField] private Slider       fovSlider;
    [SerializeField] private TextMeshProUGUI fovValueText;
    [SerializeField] private float        fovMin = 60f;
    [SerializeField] private float        fovMax = 110f;

    [Header("Master Volume")]
    [SerializeField] private Slider       volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;

    [Header("Scene Names")]
    [Tooltip("Exact name of your main menu scene.")]
    [SerializeField] private string menuSceneName = "MainMenu";

    // ─── Private ───────────────────────────────────────────────────────────

    private bool _isPaused;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Buttons
        resumeButton .onClick.AddListener(Resume);
        restartButton.onClick.AddListener(Restart);
        menuButton   .onClick.AddListener(GoToMenu);

        // Sliders — initialise to current values, then listen for changes
        InitSensitivitySlider();
        InitFOVSlider();
        InitVolumeSlider();

        pausePanel.SetActive(false);
    }

    private void Update()
    {
        // Escape toggles pause — FPSLook also listens to Escape to unlock cursor,
        // so we just piggyback on the same key. Order: FPSLook unlocks first,
        // then this opens the panel on the same frame. Feels instant.
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isPaused) Resume();
            else           Pause();
        }
    }

    // ─── Pause / Resume ────────────────────────────────────────────────────

    private void Pause()
    {
        _isPaused          = true;
        Time.timeScale     = 0f;
        pausePanel.SetActive(true);
        fpsLook.UnlockCursor();
    }

    public void Resume()
    {
        _isPaused          = false;
        Time.timeScale     = 1f;
        pausePanel.SetActive(false);
        fpsLook.LockCursor();
    }

    // ─── Buttons ───────────────────────────────────────────────────────────

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    // ─── Sensitivity Slider ────────────────────────────────────────────────

    private void InitSensitivitySlider()
    {
        sensitivitySlider.minValue = sensitivityMin;
        sensitivitySlider.maxValue = sensitivityMax;
        sensitivitySlider.value    = fpsLook.Sensitivity;

        UpdateSensitivityText(fpsLook.Sensitivity);

        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            fpsLook.Sensitivity = value;
            UpdateSensitivityText(value);
        });
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = Mathf.RoundToInt(value).ToString();
    }

    // ─── FOV Slider ────────────────────────────────────────────────────────

    private void InitFOVSlider()
    {
        fovSlider.minValue = fovMin;
        fovSlider.maxValue = fovMax;
        fovSlider.value    = playerCamera.fieldOfView;

        UpdateFOVText(playerCamera.fieldOfView);

        fovSlider.onValueChanged.AddListener(value =>
        {
            playerCamera.fieldOfView = value;
            UpdateFOVText(value);
        });
    }

    private void UpdateFOVText(float value)
    {
        if (fovValueText != null)
            fovValueText.text = Mathf.RoundToInt(value).ToString();
    }

    // ─── Volume Slider ─────────────────────────────────────────────────────

    private void InitVolumeSlider()
    {
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;

        // AudioManager may not exist yet in Awake if it's on another object —
        // read it safely with a fallback
        float currentVolume = AudioManager.Instance != null
            ? AudioManager.Instance.MasterVolume
            : 1f;

        volumeSlider.value = currentVolume;
        UpdateVolumeText(currentVolume);

        volumeSlider.onValueChanged.AddListener(value =>
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.MasterVolume = value;
                // In PauseMenu.InitVolumeSlider() onValueChanged:
AmbientAudio.Instance?.SetVolume(value * 0.35f); // scale so ambient isn't full blast at max

            UpdateVolumeText(value);
        });
    }

    private void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
