using UnityEngine;

public static class ShipAutoTargetScorer
{
    public static bool TryEvaluate(
        GameObject shooterShipRoot,
        GameObject targetShipRoot,
        FireEligibilityResult fireEligibilityResult,
        out AutoTargetScoreResult result
    )
    {
        result = default;

        if (!fireEligibilityResult.CanFire
            || shooterShipRoot == null
            || targetShipRoot == null
            || shooterShipRoot == targetShipRoot)
        {
            return false;
        }

        ShipFireEligibility shooterEligibility =
            shooterShipRoot.GetComponent<ShipFireEligibility>();
        ShipExposureReference targetExposure =
            targetShipRoot.GetComponent<ShipExposureReference>();

        if (shooterEligibility == null
            || targetExposure == null
            || !TryGetValidatedRanges(
                shooterEligibility,
                out float effectiveRangeMeters,
                out float maximumRangeMeters
            )
            || !IsFinite(fireEligibilityResult.DistanceMeters)
            || fireEligibilityResult.DistanceMeters < 0f
            || !targetExposure.TryGetReferenceDimensions(
                out Vector3 dimensionsMeters
            )
            || !targetExposure.TryCalculateExposure(
                shooterShipRoot.transform.position,
                out ExposureRect exposure
            ))
        {
            return false;
        }

        float maximumBroadsideAreaSquareMeters =
            dimensionsMeters.z * dimensionsMeters.y;

        if (!IsFinite(maximumBroadsideAreaSquareMeters)
            || maximumBroadsideAreaSquareMeters <= 0f
            || !IsFinite(exposure.AreaSquareMeters)
            || exposure.AreaSquareMeters < 0f)
        {
            return false;
        }

        float exposureNormalized = Mathf.Clamp01(
            exposure.AreaSquareMeters
                / maximumBroadsideAreaSquareMeters
        );
        float rangeQualityNormalized = CalculateRangeQuality(
            fireEligibilityResult.DistanceMeters,
            effectiveRangeMeters,
            maximumRangeMeters
        );

        if (!IsFinite(exposureNormalized)
            || !IsFinite(rangeQualityNormalized))
        {
            return false;
        }

        result = new AutoTargetScoreResult(
            exposureNormalized,
            rangeQualityNormalized
        );
        return true;
    }


    private static float CalculateRangeQuality(
        float distanceMeters,
        float effectiveRangeMeters,
        float maximumRangeMeters
    )
    {
        if (distanceMeters <= effectiveRangeMeters)
        {
            return 1f;
        }

        if (distanceMeters >= maximumRangeMeters)
        {
            return 0f;
        }

        float falloffRangeMeters =
            maximumRangeMeters - effectiveRangeMeters;

        if (falloffRangeMeters <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            (maximumRangeMeters - distanceMeters)
                / falloffRangeMeters
        );
    }


    private static bool TryGetValidatedRanges(
        ShipFireEligibility eligibility,
        out float effectiveRangeMeters,
        out float maximumRangeMeters
    )
    {
        effectiveRangeMeters = eligibility.EffectiveRangeMeters;
        maximumRangeMeters = eligibility.MaximumRangeMeters;

        return IsFinite(effectiveRangeMeters)
            && IsFinite(maximumRangeMeters)
            && effectiveRangeMeters >= 0f
            && maximumRangeMeters >= 0f
            && effectiveRangeMeters <= maximumRangeMeters;
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
