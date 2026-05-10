using UnityEngine;

/// <summary>
/// Помечает объект как «допустимая платформа» и хранит точку респавна.
/// Добавьте этот компонент на каждую платформу, на которую игрок может вернуться.
/// Платформе нужен Collider (Box / Mesh) — он же используется для определения
/// ближайшей точки возврата.
/// </summary>
public class PlatformLeash : MonoBehaviour
{
    [Header("Точка возврата")]
    [Tooltip("Локальное смещение точки респавна относительно центра платформы. " +
             "Если (0,1,0) — игрок появится на 1 м выше центра.")]
    public Vector3 respawnOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Леер (Leash)")]
    [Tooltip("Максимальное расстояние от центра платформы, в пределах которого " +
             "игрок считается «привязанным» к этой платформе.")]
    public float leashRadius = 12f;

    [Tooltip("Если true — платформа участвует в системе возврата. " +
             "Можно отключать динамически (разрушаемые платформы и т.д.)")]
    public bool isActive = true;

    /// <summary>Мировая позиция точки респавна.</summary>
    public Vector3 RespawnPoint => transform.TransformPoint(respawnOffset);

    // ─── Визуализация в редакторе ───────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Леер-радиус
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, leashRadius);

        // Точка респавна
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(RespawnPoint, 0.3f);
    }
}