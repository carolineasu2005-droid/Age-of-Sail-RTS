using UnityEngine;

internal readonly struct BroadsideGeometryResult
{
    internal BroadsideGeometryResult(
        Vector3 worldDirection,
        CombatSide? side,
        bool inBroadsideArc,
        float localBearingDegrees
    )
    {
        WorldDirection = worldDirection;
        Side = side;
        InBroadsideArc = inBroadsideArc;
        LocalBearingDegrees = localBearingDegrees;
    }


    internal Vector3 WorldDirection { get; }

    internal CombatSide? Side { get; }

    internal bool InBroadsideArc { get; }

    internal float LocalBearingDegrees { get; }
}

internal static class ShipBroadsideGeometry
{
    internal const float MaximumBroadsideArcDegrees = 160f;
    internal const float MaximumSideArcLimitDegrees = 90f;

    private const float BoundaryToleranceDegrees = 0.0001f;
    private const float MinimumHorizontalDirectionSqrMagnitude = 0.000001f;


    internal static bool HasValidArcConfiguration(
        float forwardArcLimitDegrees,
        float aftArcLimitDegrees
    )
    {
        return IsFinite(forwardArcLimitDegrees)
            && IsFinite(aftArcLimitDegrees)
            && forwardArcLimitDegrees >= 0f
            && forwardArcLimitDegrees <= MaximumSideArcLimitDegrees
            && aftArcLimitDegrees >= 0f
            && aftArcLimitDegrees <= MaximumSideArcLimitDegrees
            && forwardArcLimitDegrees + aftArcLimitDegrees > 0f
            && forwardArcLimitDegrees + aftArcLimitDegrees
                <= MaximumBroadsideArcDegrees;
    }


    internal static bool TryEvaluate(
        Transform shooterRoot,
        Vector3 worldDirection,
        float forwardArcLimitDegrees,
        float aftArcLimitDegrees,
        out BroadsideGeometryResult result
    )
    {
        result = default;

        if (shooterRoot == null
            || !HasValidArcConfiguration(
                forwardArcLimitDegrees,
                aftArcLimitDegrees
            )
            || !IsFinite(worldDirection)
            || !IsFinite(shooterRoot.forward)
            || !IsFinite(shooterRoot.right))
        {
            return false;
        }

        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude
            <= MinimumHorizontalDirectionSqrMagnitude)
        {
            return false;
        }

        worldDirection.Normalize();
        Vector3 localDirection =
            shooterRoot.InverseTransformDirection(worldDirection);
        float localBearingDegrees = Mathf.Atan2(
            localDirection.x,
            localDirection.z
        ) * Mathf.Rad2Deg;

        if (!IsFinite(localBearingDegrees))
        {
            return false;
        }

        bool inBroadsideArc = IsInBroadsideArc(
            localBearingDegrees,
            forwardArcLimitDegrees,
            aftArcLimitDegrees
        );
        CombatSide? side = null;

        if (inBroadsideArc)
        {
            side = localBearingDegrees < 0f
                ? CombatSide.Port
                : CombatSide.Starboard;
        }

        result = new BroadsideGeometryResult(
            worldDirection,
            side,
            inBroadsideArc,
            localBearingDegrees
        );
        return true;
    }


    private static bool IsInBroadsideArc(
        float localBearingDegrees,
        float forwardArcLimitDegrees,
        float aftArcLimitDegrees
    )
    {
        float absoluteBearingDegrees = Mathf.Abs(localBearingDegrees);

        if (absoluteBearingDegrees <= BoundaryToleranceDegrees
            || 180f - absoluteBearingDegrees
                <= BoundaryToleranceDegrees)
        {
            return false;
        }

        float forwardBoundaryDegrees =
            90f - forwardArcLimitDegrees;
        float aftBoundaryDegrees = 90f + aftArcLimitDegrees;

        return absoluteBearingDegrees
                >= forwardBoundaryDegrees - BoundaryToleranceDegrees
            && absoluteBearingDegrees
                <= aftBoundaryDegrees + BoundaryToleranceDegrees;
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
