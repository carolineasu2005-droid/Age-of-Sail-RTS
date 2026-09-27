using UnityEngine;

public static class CombatRakingEvaluator
{
    private const float MinimumDirectionSqrMagnitude = 0.000001f;
    private const float AngleComparisonToleranceDegrees = 0.0001f;
    private const float NeutralDamageMultiplier = 1f;


    public static bool TryEvaluate(
        CombatHitContext hitContext,
        CombatRakingProfile profile,
        out CombatRakingResult result
    )
    {
        result = CreateInvalidResult();

        if (hitContext.TargetShipRoot == null
            || !HasValidProfile(profile))
        {
            return false;
        }

        Vector3 targetForwardWorld =
            hitContext.TargetShipRoot.transform.forward;
        Vector3 travelDirectionWorld =
            hitContext.IncomingDirectionWorld;
        targetForwardWorld.y = 0f;
        travelDirectionWorld.y = 0f;

        if (!TryNormalizeHorizontal(
                targetForwardWorld,
                out targetForwardWorld
            )
            || !TryNormalizeHorizontal(
                travelDirectionWorld,
                out travelDirectionWorld
            ))
        {
            return false;
        }

        float signedAlignment = Mathf.Clamp(
            Vector3.Dot(travelDirectionWorld, targetForwardWorld),
            -1f,
            1f
        );
        float longitudinalAlignment = Mathf.Abs(signedAlignment);
        float longitudinalAngleDegrees = Mathf.Acos(Mathf.Clamp01(
            longitudinalAlignment
        )) * Mathf.Rad2Deg;

        if (!IsFinite(longitudinalAngleDegrees))
        {
            return false;
        }

        CombatRakingType type = CombatRakingType.None;
        float damageMultiplier = NeutralDamageMultiplier;
        bool withinRakingAngle = longitudinalAngleDegrees
            <= profile.RakingHalfAngleDegrees
                + AngleComparisonToleranceDegrees;

        if (withinRakingAngle && signedAlignment < 0f)
        {
            type = CombatRakingType.Bow;
            damageMultiplier = profile.BowDamageMultiplier;
        }
        else if (withinRakingAngle && signedAlignment > 0f)
        {
            type = CombatRakingType.Stern;
            damageMultiplier = profile.SternDamageMultiplier;
        }

        result = new CombatRakingResult(
            true,
            type,
            longitudinalAngleDegrees,
            damageMultiplier
        );
        return true;
    }


    private static CombatRakingResult CreateInvalidResult()
    {
        return new CombatRakingResult(
            false,
            CombatRakingType.None,
            0f,
            NeutralDamageMultiplier
        );
    }


    private static bool HasValidProfile(CombatRakingProfile profile)
    {
        return profile != null
            && IsFinite(profile.RakingHalfAngleDegrees)
            && profile.RakingHalfAngleDegrees >= 0f
            && profile.RakingHalfAngleDegrees <= 90f
            && IsFinite(profile.BowDamageMultiplier)
            && profile.BowDamageMultiplier >= 0f
            && IsFinite(profile.SternDamageMultiplier)
            && profile.SternDamageMultiplier >= 0f;
    }


    private static bool TryNormalizeHorizontal(
        Vector3 value,
        out Vector3 normalized
    )
    {
        normalized = default;

        if (!IsFinite(value)
            || value.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        normalized = value.normalized;
        return IsFinite(normalized);
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
