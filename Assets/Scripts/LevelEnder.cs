using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelEnder : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float delayBeforeLoad = 0.3f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("References (auto-found if empty)")]
    [SerializeField] private PlayerInput playerInput;

    private bool _ending;
    private Image _fadeImage;

    private void Awake()
    {
        CreateFadeOverlay();

        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();
    }

    public void End()
    {
        if (_ending) return;
        _ending = true;

        // 1. Отключаем управление
        if (playerInput != null)
            playerInput.enabled = false;

        // Замораживаем курсор на месте
        Cursor.lockState = CursorLockMode.Locked;

        // 2. Затемнение → загрузка
        _fadeImage.gameObject.SetActive(true);
        _fadeImage
            .DOFade(255f, fadeDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true) // работает даже при timeScale = 0
            .OnComplete(LoadNext);
    }

    private void LoadNext()
    {
        DOVirtual.DelayedCall(delayBeforeLoad, () =>
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;

            // Если сцены кончились — возвращаемся на первую (или меню)
            if (next >= SceneManager.sceneCountInBuildSettings)
                next = 0;

            SceneManager.LoadScene(next);
        }, ignoreTimeScale: true);
    }

    // ─── Создаём оверлей программно, без префаба ───────────────────────

    private void CreateFadeOverlay()
    {
        var go = new GameObject("FadeOverlay");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        go.AddComponent<CanvasScaler>();

        _fadeImage = new GameObject("FadeImage").AddComponent<Image>();
        _fadeImage.transform.SetParent(go.transform, false);
        _fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        _fadeImage.raycastTarget = false;

        // Растягиваем на весь экран
        var rt = _fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.SetActive(false);

        DontDestroyOnLoad(go);
    }
}