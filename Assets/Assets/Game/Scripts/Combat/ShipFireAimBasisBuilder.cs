using UnityEngine;

public static class ShipFireAimBasisBuilder
{
    private const float MinimumHorizontalDirectionSqrMagnitude = 0.000001f;


    public static bool TryBuildTargeted(
        GameObject shooterShipRoot,
        GameObject targetShipRoot,
        FireEligibilityResult fireEligibility,
        out FireAimBasis basis,
        out FireAimBasisFailure failure
    )
    {
        basis = default;
        failure = FireAimBasisFailure.FireEligibilityRejected;

        if (!fireEligibility.CanFire
            || !fireEligibility.Side.HasValue
            || shooterShipRoot == null
            || targetShipRoot == null
            || shooterShipRoot == targetShipRoot)
        {
            return false;
        }

        ShipExposureReference exposureReference =
            targetShipRoot.GetComponent<ShipExposureReference>();

        if (exposureReference == null
            || !exposureReference.TryCalculateExposure(
                shooterShipRoot.transform.position,
                out ExposureRect exposure
            ))
        {
            failure = FireAimBasisFailure.ExposureUnavailable;
            return false;
        }

        Vector3 horizontalDirection =
            exposure.CenterWorld - shooterShipRoot.transform.position;

        return TryCreate(
            fireEligibility.Side.Value,
            FireAimSourceKind.Targeted,
            exposure.CenterWorld,
            horizontalDirection,
            fireEligibility.DistanceMeters,
            out basis,
            out failure
        );
    }


    public static bool TryBuildBlindFirePoint(
        BlindFireEligibilityResult blindFireEligibility,
        out FireAimBasis basis,
        out FireAimBasisFailure failure
    )
    {
        basis = default;

        if (blindFireEligibility.Aim.IsValid
            && !blindFireEligibility.Aim.HasWorldAimPoint)
        {
            failure = FireAimBasisFailure.FiniteAimPointRequired;
            return false;
        }

        failure = FireAimBasisFailure.BlindFireEligibilityRejected;

        if (!blindFireEligibility.CanBlindFire
            || !blindFireEligibility.Side.HasValue
            || !blindFireEligibility.Aim.WorldAimPoint.HasValue
            || !blindFireEligibility.Aim.AimPointDistanceMeters.HasValue)
        {
            return false;
        }

        return TryCreate(
            blindFireEligibility.Side.Value,
            FireAimSourceKind.BlindFirePoint,
            blindFireEligibility.Aim.WorldAimPoint.Value,
            blindFireEligibility.Aim.WorldAimDirection,
            blindFireEligibility.Aim.AimPointDistanceMeters.Value,
            out basis,
            out failure
        );
    }


    private static bool TryCreate(
        CombatSide side,
        FireAimSourceKind sourceKind,
        Vector3 aimPlaneCenterWorld,
        Vector3 horizontalDirection,
        float aimDistanceMeters,
        out FireAimBasis basis,
        out FireAimBasisFailure failure
    )
    {
        basis = default;
        failure = FireAimBasisFailure.InvalidGeometry;
        horizontalDirection.y = 0f;

        if (!IsFinite(aimPlaneCenterWorld)
            || !IsFinite(horizontalDirection)
            || horizontalDirection.sqrMagnitude
                <= MinimumHorizontalDirectionSqrMagnitude
            || !IsFinite(aimDistanceMeters)
            || aimDistanceMeters <= 0f)
        {
            return false;
        }

        Vector3 aimDirectionWorld = horizontalDirection.normalized;

        if (!IsFinite(aimDirectionWorld))
        {
            return false;
        }

        basis = new FireAimBasis(
            side,
            sourceKind,
            aimPlaneCenterWorld,
            aimDirectionWorld,
            aimDistanceMeters
        );
        failure = FireAimBasisFailure.None;
        return true;
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
