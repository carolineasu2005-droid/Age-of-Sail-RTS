using UnityEngine;

public enum FoundationShotOutcomeFailure
{
    None,
    InvalidShot,
    InvalidSpatialData,
    SemanticHitQueryFailed,
    UnknownTerminalKind
}

public static class FoundationShotOutcomeResolver
{
    public static bool TryResolve(
        ProjectileTerminalContact contact,
        out FoundationShotOutcome outcome,
        out FoundationShotOutcomeFailure failure
    )
    {
        outcome = default;

        if (contact.ShotSample.SourceShipRootIdentity == null
            || contact.ShotSample.AmmunitionType
                != FoundationAmmunitionType.RoundShot)
        {
            failure = FoundationShotOutcomeFailure.InvalidShot;
            return false;
        }

        switch (contact.Kind)
        {
            case ProjectileTerminalContactKind.CombatGeometryContact:
                if (!CombatSemanticHitQuery.TryResolve(
                    contact,
                    out CombatHitContext hitContext,
                    out _
                ))
                {
                    failure = FoundationShotOutcomeFailure
                        .SemanticHitQueryFailed;
                    return false;
                }

                outcome = new FoundationShotOutcome(
                    FoundationShotOutcomeKind.HullHit,
                    contact.ShotSample,
                    hitContext.WorldHitPoint,
                    hitContext.WorldHitNormal,
                    hitContext,
                    true
                );
                failure = FoundationShotOutcomeFailure.None;
                return true;

            case ProjectileTerminalContactKind.WaterContact:
                if (!IsFinite(contact.PointWorld))
                {
                    failure = FoundationShotOutcomeFailure
                        .InvalidSpatialData;
                    return false;
                }

                outcome = new FoundationShotOutcome(
                    FoundationShotOutcomeKind.WaterMiss,
                    contact.ShotSample,
                    contact.PointWorld,
                    Vector3.up,
                    default,
                    false
                );
                failure = FoundationShotOutcomeFailure.None;
                return true;

            case ProjectileTerminalContactKind.ExpiredSafetyFallback:
                if (!IsFinite(contact.PointWorld))
                {
                    failure = FoundationShotOutcomeFailure
                        .InvalidSpatialData;
                    return false;
                }

                outcome = new FoundationShotOutcome(
                    FoundationShotOutcomeKind.ExpiredNonHit,
                    contact.ShotSample,
                    contact.PointWorld,
                    Vector3.zero,
                    default,
                    false
                );
                failure = FoundationShotOutcomeFailure.None;
                return true;

            default:
                failure = FoundationShotOutcomeFailure.UnknownTerminalKind;
                return false;
        }
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
