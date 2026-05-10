using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Система «поводка» (leash) для игрока в 3D-платформере.
///
/// Логика:
///   1. Игрок всегда отслеживает ближайшую «допустимую» платформу (PlatformLeash).
///   2. Если игрок покидает леер-радиус всех платформ ИЛИ падает ниже killY —
///      запускается возврат на последнюю безопасную платформу.
///   3. Возврат можно сделать мгновенным или плавным (lerp / fade-to-black).
///
/// Как использовать:
///   • Повесьте этот скрипт на объект игрока (с CharacterController или Rigidbody).
///   • На каждую платформу повесьте PlatformLeash.
///   • Настройте killY, graceTime и стиль возврата.
/// </summary>
[RequireComponent(typeof(CharacterController))]  // убрать, если используете Rigidbody
public class PlayerPlatformLeash : MonoBehaviour
{
    // ─── Настройки ──────────────────────────────────────────────

    [Header("Границы мира")]
    [Tooltip("Если игрок опустится ниже этой Y-координаты — мгновенный возврат.")]
    public float killY = -20f;

    [Header("Леер игрока")]
    [Tooltip("Дополнительное время (сек), которое игрок может быть вне леера " +
             "всех платформ, прежде чем сработает возврат. " +
             "0 = мгновенно.")]
    public float graceTime = 0.6f;

    [Header("Возврат")]
    public RespawnStyle respawnStyle = RespawnStyle.FadeAndTeleport;

    [Tooltip("Длительность fade-эффекта (если выбран FadeAndTeleport).")]
    public float fadeDuration = 0.4f;

    [Tooltip("Сколько секунд неуязвимости после респавна.")]
    public float respawnInvincibility = 1.0f;

    // ─── Публичные данные (для UI / других систем) ───────────────

    /// <summary>Платформа, к которой игрок сейчас «привязан».</summary>
    public PlatformLeash CurrentPlatform { get; private set; }

    /// <summary>Последняя платформа, на которой игрок стоял.</summary>
    public PlatformLeash LastSafePlatform { get; private set; }

    /// <summary>true во время процесса возврата.</summary>
    public bool IsRespawning { get; private set; }

    // ─── Внутреннее состояние ────────────────────────────────────

    private float _outOfLeashTimer;
    private CharacterController _cc;
    private List<PlatformLeash> _platforms = new();

    // Кеш для fade-overlay (создаётся лениво)
    private CanvasGroup _fadeOverlay;

    // ─── Инициализация ──────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void Start()
    {
        RefreshPlatformList();

        // Привязать к ближайшей платформе на старте
        CurrentPlatform  = FindClosestPlatform();
        LastSafePlatform = CurrentPlatform;
    }

    // ─── Основной цикл ──────────────────────────────────────────

    private void Update()
    {
        if (IsRespawning) return;

        // 1. Kill-plane
        if (transform.position.y < killY)
        {
            StartCoroutine(RespawnCoroutine());
            return;
        }

        // 2. Найти ближайшую активную платформу
        PlatformLeash closest = FindClosestPlatform();

        if (closest != null)
        {
            float dist = Vector3.Distance(transform.position, closest.transform.position);

            if (dist <= closest.leashRadius)
            {
                // Игрок внутри леера — обновляем привязку
                CurrentPlatform    = closest;
                _outOfLeashTimer   = 0f;

                // Обновляем «последнюю безопасную», только если стоим на чём-то
                if (IsGrounded())
                    LastSafePlatform = closest;
            }
            else
            {
                // Вне леера всех платформ
                _outOfLeashTimer += Time.deltaTime;

                if (_outOfLeashTimer >= graceTime)
                    StartCoroutine(RespawnCoroutine());
            }
        }
        else
        {
            // Платформ вообще нет — фолбэк
            _outOfLeashTimer += Time.deltaTime;
            if (_outOfLeashTimer >= graceTime)
                StartCoroutine(RespawnCoroutine());
        }
    }

    // ─── Респавн ────────────────────────────────────────────────

    private IEnumerator RespawnCoroutine()
    {
        if (IsRespawning) yield break;
        IsRespawning = true;

        PlatformLeash target = LastSafePlatform ?? FindClosestPlatform();
        Vector3 destination  = target != null
            ? target.RespawnPoint
            : Vector3.up * 5f;   // крайний фолбэк

        switch (respawnStyle)
        {
            case RespawnStyle.Instant:
                Teleport(destination);
                break;

            case RespawnStyle.FadeAndTeleport:
                yield return StartCoroutine(Fade(1f));   // затемнение
                Teleport(destination);
                yield return new WaitForSeconds(0.15f);
                yield return StartCoroutine(Fade(0f));   // осветление
                break;
        }

        _outOfLeashTimer = 0f;

        // Период неуязвимости (можно подключить к системе здоровья)
        yield return new WaitForSeconds(respawnInvincibility);

        IsRespawning = false;
    }

    private void Teleport(Vector3 pos)
    {
        // CharacterController блокирует transform.position — отключаем на кадр
        _cc.enabled = false;
        transform.position = pos;
        _cc.enabled = true;

        // Обнуляем инерцию, если есть Rigidbody
        if (TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = Vector3.zero;
    }

    // ─── Поиск платформы ────────────────────────────────────────

    /// <summary>Возвращает ближайшую активную платформу или null.</summary>
    private PlatformLeash FindClosestPlatform()
    {
        float bestDist = float.MaxValue;
        PlatformLeash best = null;

        for (int i = _platforms.Count - 1; i >= 0; i--)
        {
            // Удаляем уничтоженные
            if (_platforms[i] == null) { _platforms.RemoveAt(i); continue; }
            if (!_platforms[i].isActive) continue;

            float d = Vector3.Distance(transform.position, _platforms[i].transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = _platforms[i];
            }
        }

        return best;
    }

    /// <summary>
    /// Пересканировать сцену. Вызывайте, если платформы создаются динамически.
    /// </summary>
    public void RefreshPlatformList()
    {
        _platforms = new List<PlatformLeash>(FindObjectsByType<PlatformLeash>(FindObjectsSortMode.None));
    }

    // ─── Утилиты ────────────────────────────────────────────────

    private bool IsGrounded()
    {
        if (_cc != null) return _cc.isGrounded;
        // Фолбэк через Raycast
        return Physics.Raycast(transform.position, Vector3.down, 0.15f);
    }

    // ─── Fade-эффект (UI overlay) ───────────────────────────────

    private IEnumerator Fade(float targetAlpha)
    {
        EnsureFadeOverlay();
        float start = _fadeOverlay.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _fadeOverlay.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }

        _fadeOverlay.alpha = targetAlpha;
    }

    private void EnsureFadeOverlay()
    {
        if (_fadeOverlay != null) return;

        // Создаём Canvas + чёрный Image для затемнения
        var go = new GameObject("RespawnFadeOverlay");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(go.transform, false);

        var img = imgGo.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;

        var rect = imgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        _fadeOverlay = go.AddComponent<CanvasGroup>();
        _fadeOverlay.alpha = 0f;
        _fadeOverlay.blocksRaycasts = false;

        DontDestroyOnLoad(go);
    }

    // ─── Enum ───────────────────────────────────────────────────

    public enum RespawnStyle
    {
        Instant,
        FadeAndTeleport
    }
}