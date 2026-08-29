using UnityEngine;

public class ShipLeeway : MonoBehaviour
{
    private const float ZeroThreshold = 0.0001f;

    [Header("Leeway Settings")]

    [SerializeField]
    private AnimationCurve leewayCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(45f, 0f, 0f, 0f),
        new Keyframe(60f, 0f, 0f, 0f),
        new Keyframe(65f, 0f, 0f, 4.5f),
        new Keyframe(67.5f, 11.25f, 4.5f, -1f / 6f),
        new Keyframe(75f, 10f, -1f / 6f, -0.2f),
        new Keyframe(90f, 7f, -0.2f, -0.1f),
        new Keyframe(120f, 3f, -0.1f, -1f / 15f),
        new Keyframe(150f, 1f, -1f / 15f, -1f / 30f),
        new Keyframe(180f, 0f, -1f / 30f, -1f / 30f)
    );


    [Header("Runtime Debug")]

    [SerializeField]
    private float currentAbsoluteWindAngle;

    [SerializeField]
    private float currentLeewayAngle;

    [SerializeField]
    private float lateralWindDot;

    [SerializeField]
    private float leewaySideSign;

    [SerializeField]
    private float leewayLateralSpeed;

    [SerializeField]
    private Vector3 leewayVelocity;

    public Vector3 CalculateLeewayVelocity(
        float forwardSpeed,
        float relativeWindAngleAbsolute,
        Vector3 windFlowDirection
    )
    {
        currentAbsoluteWindAngle = IsFinite(relativeWindAngleAbsolute)
            ? Mathf.Clamp(relativeWindAngleAbsolute, 0f, 180f)
            : 0f;
        float evaluatedLeewayAngle = leewayCurve != null
            ? Mathf.Max(0f, leewayCurve.Evaluate(currentAbsoluteWindAngle))
            : 0f;
        currentLeewayAngle = IsFinite(evaluatedLeewayAngle)
            ? evaluatedLeewayAngle
            : 0f;
        lateralWindDot = 0f;
        leewaySideSign = 0f;
        leewayLateralSpeed = 0f;
        leewayVelocity = Vector3.zero;

        float forwardSpeedMagnitude = Mathf.Abs(forwardSpeed);

        if (forwardSpeedMagnitude <= ZeroThreshold
            || currentLeewayAngle <= ZeroThreshold
            || float.IsNaN(forwardSpeedMagnitude)
            || float.IsInfinity(forwardSpeedMagnitude))
        {
            return leewayVelocity;
        }

        if (!IsFinite(windFlowDirection))
        {
            return leewayVelocity;
        }

        Vector3 horizontalWindFlowDirection = Vector3.ProjectOnPlane(
            windFlowDirection,
            Vector3.up
        );
        Vector3 horizontalRight = Vector3.ProjectOnPlane(
            transform.right,
            Vector3.up
        );

        if (horizontalWindFlowDirection.sqrMagnitude <= ZeroThreshold
            || horizontalRight.sqrMagnitude <= ZeroThreshold
            || !IsFinite(horizontalWindFlowDirection)
            || !IsFinite(horizontalRight))
        {
            return leewayVelocity;
        }

        horizontalWindFlowDirection.Normalize();
        horizontalRight.Normalize();

        lateralWindDot = Vector3.Dot(
            horizontalWindFlowDirection,
            horizontalRight
        );

        if (!IsFinite(lateralWindDot)
            || Mathf.Abs(lateralWindDot) <= ZeroThreshold)
        {
            lateralWindDot = 0f;
            return leewayVelocity;
        }

        leewaySideSign = Mathf.Sign(lateralWindDot);
        leewayLateralSpeed = forwardSpeedMagnitude * Mathf.Tan(
            currentLeewayAngle * Mathf.Deg2Rad
        );

        if (float.IsNaN(leewayLateralSpeed)
            || float.IsInfinity(leewayLateralSpeed)
            || leewayLateralSpeed <= ZeroThreshold)
        {
            leewayLateralSpeed = 0f;
            return leewayVelocity;
        }

        leewayVelocity = horizontalRight
            * leewaySideSign
            * leewayLateralSpeed;

        if (!IsFinite(leewayVelocity))
        {
            leewayVelocity = Vector3.zero;
        }

        return leewayVelocity;
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }
}
