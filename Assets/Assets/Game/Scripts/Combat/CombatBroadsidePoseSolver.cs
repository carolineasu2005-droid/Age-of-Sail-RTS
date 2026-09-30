using UnityEngine;

public static class CombatBroadsidePoseSolver
{
    private const float MinimumHorizontalDistanceMeters = 0.001f;
    private const float MinimumHorizontalForwardSqrMagnitude = 0.00000001f;

    public static bool TrySolve(
        GameObject shooterShipRoot,
        GameObject targetShipRoot,
        CombatSide? currentPreferredSide,
        CombatAIProfile profile,
        out CombatBroadsidePoseResult result
    )
    {
        result = default;

        if (profile == null
            || !profile.IsPoseValid
            || !CombatAITargetAcquisition.IsLegal(
                shooterShipRoot,
                targetShipRoot,
                profile.TargetAcquisitionRadiusMeters
            ))
        {
            return false;
        }

        ShipFireEligibility weapon =
            shooterShipRoot.GetComponent<ShipFireEligibility>();
        ShipIntegrity shooterIntegrity =
            shooterShipRoot.GetComponent<ShipIntegrity>();
        if (shooterIntegrity == null
            || !shooterIntegrity.IsInitialized
            || shooterIntegrity.IsCombatDisabled
            || weapon == null
            || !ShipBroadsideGeometry.HasValidArcConfiguration(
                weapon.ForwardArcLimitDegrees,
                weapon.AftArcLimitDegrees
            )
            || !IsFinite(weapon.MaximumRangeMeters)
            || !IsFinite(weapon.EffectiveRangeMeters)
            || weapon.MaximumRangeMeters <= 0f
            || weapon.EffectiveRangeMeters < 0f
            || weapon.EffectiveRangeMeters > weapon.MaximumRangeMeters)
        {
            return false;
        }

        Vector3 shooterPosition = shooterShipRoot.transform.position;
        Vector3 targetPosition = targetShipRoot.transform.position;
        Vector3 shooterForward = shooterShipRoot.transform.forward;
        if (!IsFinite(shooterPosition)
            || !IsFinite(targetPosition)
            || !IsFinite(shooterForward))
        {
            return false;
        }

        Vector3 toTarget = targetPosition - shooterPosition;
        toTarget.y = 0f;
        shooterForward.y = 0f;
        float distance = toTarget.magnitude;
        if (!IsFinite(distance)
            || distance <= MinimumHorizontalDistanceMeters
            || shooterForward.sqrMagnitude
                <= MinimumHorizontalForwardSqrMagnitude)
        {
            return false;
        }

        Vector3 targetDirection = toTarget / distance;
        shooterForward.Normalize();
        bool targetIsAft = Vector3.Dot(shooterForward, targetDirection) < 0f;
        float targetBearing = WindBeatingNavigationMath.GetHorizontalBearing(
            shooterPosition, targetPosition, 0f
        );
        float currentHeading = WindBeatingNavigationMath.GetHorizontalBearing(
            Vector3.zero, shooterForward, 0f
        );
        if (!IsFinite(targetBearing) || !IsFinite(currentHeading))
        {
            return false;
        }
        float desiredRange = weapon.MaximumRangeMeters
            * profile.DesiredCombatRangeRatio;
        Vector3 desiredPosition = targetPosition - targetDirection * desiredRange;
        if (!IsFinite(desiredRange) || !IsFinite(desiredPosition))
        {
            return false;
        }

        // Local +X is Starboard and -X is Port. A target at +90 degrees
        // relative to the bow is therefore on the Starboard broadside.
        CombatBroadsidePoseCandidate port = MakeCandidate(
            CombatSide.Port,
            targetBearing + 90f,
            desiredPosition,
            currentHeading
        );
        CombatBroadsidePoseCandidate starboard = MakeCandidate(
            CombatSide.Starboard,
            targetBearing - 90f,
            desiredPosition,
            currentHeading
        );
        CombatSide selected = SelectSide(
            port,
            starboard,
            currentPreferredSide,
            profile.BroadsideSideSwitchAdvantageDegrees
        );
        CombatBroadsidePoseCandidate chosen = selected == CombatSide.Port
            ? port : starboard;
        if (!IsFinite(chosen.DesiredHeadingDegrees))
        {
            return false;
        }

        CombatRangeState rangeState;
        CombatAIMovementIntent intent;
        float minimumBandRange = weapon.MaximumRangeMeters
            * profile.PreferredRangeBandMinRatio;
        float maximumBandRange = weapon.MaximumRangeMeters
            * profile.PreferredRangeBandMaxRatio;
        // Float distance and ratio arithmetic can straddle an exact boundary
        // by a few ULPs. Approximate equality remains inside the band.
        if (distance < minimumBandRange
            && !Mathf.Approximately(distance, minimumBandRange))
        {
            rangeState = CombatRangeState.TooClose;
            intent = CombatAIMovementIntent.OpenRange;
        }
        else if (distance > maximumBandRange
            && !Mathf.Approximately(distance, maximumBandRange))
        {
            rangeState = CombatRangeState.TooFar;
            intent = CombatAIMovementIntent.CloseRange;
        }
        else
        {
            rangeState = CombatRangeState.InBand;
            intent = Mathf.Approximately(chosen.HeadingCostDegrees, 0f)
                ? CombatAIMovementIntent.HoldCombatPose
                : CombatAIMovementIntent.AlignBroadside;
        }

        float approachHeading = 0f;
        Vector3 approachDestination = default;
        bool approachUsesCurrentHeading = false;
        if (rangeState == CombatRangeState.TooFar)
        {
            // Fore contacts retain the target-relative attack leg. An aft
            // contact turns out from the current course instead of reversing
            // toward the pursuer. MoveTowardsAngle limits the signed turn.
            approachUsesCurrentHeading = targetIsAft;
            float basisHeading = targetIsAft ? currentHeading : targetBearing;
            approachHeading = Mathf.MoveTowardsAngle(
                basisHeading,
                chosen.DesiredHeadingDegrees,
                profile.BroadsideApproachAngleDegrees
            );
            Vector3 approachForward = Quaternion.Euler(
                0f, approachHeading, 0f
            ) * Vector3.forward;
            approachDestination = shooterPosition + approachForward
                * profile.BroadsideApproachLeadDistanceMeters;
            if (!IsFinite(approachHeading) || !IsFinite(approachDestination))
            {
                return false;
            }
        }

        result = new CombatBroadsidePoseResult(
            port,
            starboard,
            selected,
            rangeState,
            intent,
            distance,
            weapon.MaximumRangeMeters,
            desiredRange,
            approachHeading,
            approachDestination,
            approachUsesCurrentHeading
        );
        return true;
    }

    private static CombatBroadsidePoseCandidate MakeCandidate(
        CombatSide side,
        float desiredHeading,
        Vector3 desiredPosition,
        float currentHeading
    )
    {
        desiredHeading = Mathf.Repeat(desiredHeading, 360f);
        return new CombatBroadsidePoseCandidate(
            side,
            desiredHeading,
            desiredPosition,
            Mathf.DeltaAngle(currentHeading, desiredHeading)
        );
    }

    private static CombatSide SelectSide(
        CombatBroadsidePoseCandidate port,
        CombatBroadsidePoseCandidate starboard,
        CombatSide? preferred,
        float switchAdvantageDegrees
    )
    {
        if (preferred == CombatSide.Port)
        {
            return starboard.HeadingCostDegrees < port.HeadingCostDegrees
                && port.HeadingCostDegrees - starboard.HeadingCostDegrees
                    >= switchAdvantageDegrees
                ? CombatSide.Starboard : CombatSide.Port;
        }

        if (preferred == CombatSide.Starboard)
        {
            return port.HeadingCostDegrees < starboard.HeadingCostDegrees
                && starboard.HeadingCostDegrees - port.HeadingCostDegrees
                    >= switchAdvantageDegrees
                ? CombatSide.Port : CombatSide.Starboard;
        }

        // With no retained side, Port wins an exact geometric tie.
        return Mathf.Approximately(
                port.HeadingCostDegrees,
                starboard.HeadingCostDegrees
            ) || port.HeadingCostDegrees < starboard.HeadingCostDegrees
            ? CombatSide.Port : CombatSide.Starboard;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
