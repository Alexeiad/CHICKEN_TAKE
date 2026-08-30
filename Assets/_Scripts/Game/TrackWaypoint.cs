using UnityEngine;

public class TrackWaypoint : MonoBehaviour
{
    [Header("Spline Curvature")]
    [Tooltip("Сделать линию до этой точки строго прямой без изгиба")]
    public bool isLinear = false;

    [Header("Speed Zone Settings")]
    [Tooltip("Нужно ли снижать скорость в зоне этого чекпоинта?")]
    public bool isSlowZone = false;

    [Tooltip("Коэффициент скорости (0.5 = в 2 раза медленнее, 0.7 = на 30% медленнее)")]
    [Range(0.1f, 1f)]
    public float speedMultiplier = 0.5f;

    [Tooltip("Радиус действия зоны притормаживания (в метрах)")]
    public float zoneRadius = 15f;

    // Визуализация зоны в окне Scene (Желтая прозрачная сфера)
    private void OnDrawGizmosSelected()
    {
        if (isSlowZone)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f);
            Gizmos.DrawSphere(transform.position, zoneRadius);
        }
    }
}