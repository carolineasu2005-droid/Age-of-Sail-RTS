using UnityEngine;

public class GlobalWind : MonoBehaviour
{
    [Header("Global Wind")]

    [Tooltip("Direction the wind is coming from. 0 degrees = +Z, 90 degrees = +X.")]
    [Range(0f, 360f)]
    public float windFromDegrees = 0f;

    [Tooltip("Global wind strength multiplier. Keep at 1.0 for the first prototype.")]
    [Min(0f)]
    public float windStrength = 1f;

    [Header("Debug")]

    [Tooltip("Length of the wind direction gizmo arrow.")]
    [Min(1f)]
    public float debugArrowLength = 30f;


    /// <summary>
    /// Direction pointing toward where the wind is coming from.
    /// </summary>
    public Vector3 WindFromDirection
    {
        get
        {
            return Quaternion.Euler(0f, windFromDegrees, 0f)
                   * Vector3.forward;
        }
    }


    /// <summary>
    /// Direction the wind is actually flowing toward.
    /// </summary>
    public Vector3 WindFlowDirection
    {
        get
        {
            return -WindFromDirection;
        }
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        DrawArrow(transform.position, WindFromDirection, debugArrowLength);

        Gizmos.color = Color.cyan;
        DrawArrow(transform.position, WindFlowDirection, debugArrowLength);
    }


    private void DrawArrow(Vector3 origin, Vector3 direction, float length)
    {
        direction = direction.normalized;

        if (direction == Vector3.zero)
        {
            return;
        }

        Vector3 end = origin + direction * length;
        float headLength = length * 0.15f;

        Gizmos.DrawLine(origin, end);
        Gizmos.DrawLine(
            end,
            end + Quaternion.Euler(0f, 150f, 0f) * direction * headLength
        );
        Gizmos.DrawLine(
            end,
            end + Quaternion.Euler(0f, -150f, 0f) * direction * headLength
        );
    }
}
