using UnityEngine;

public static class CombatOutcomeVFXBridge
{
    private const float MinimumDirectionSqrMagnitude = 0.000001f;


    public static bool TryEmitResolvedOutcome(
        FoundationShotOutcome outcome,
        ICombatVFXEventReceiver receiver
    )
    {
        if (receiver == null)
        {
            return false;
        }

        if (outcome.Kind == FoundationShotOutcomeKind.HullHit
            && outcome.HasHitContext
            && outcome.HitContext.TargetShipRoot != null)
        {
            receiver.OnHullImpact(new CombatHullImpactEvent(
                outcome.WorldPoint,
                outcome.WorldNormal,
                outcome.HitContext.TargetShipRoot,
                outcome.HitContext.Region
            ));
            return true;
        }

        if (outcome.Kind == FoundationShotOutcomeKind.WaterMiss
            && !outcome.HasHitContext)
        {
            receiver.OnWaterImpact(new CombatWaterImpactEvent(
                outcome.WorldPoint,
                outcome.WorldNormal
            ));
            return true;
        }

        return false;
    }


    public static bool TryEmitMuzzleFire(
        ShotSample shotSample,
        string muzzleIdentifier,
        ICombatVFXEventReceiver receiver
    )
    {
        if (receiver == null
            || shotSample.SourceShipRootIdentity == null
            || shotSample.AmmunitionType
                != FoundationAmmunitionType.RoundShot
            || string.IsNullOrEmpty(muzzleIdentifier)
            || !IsFinite(shotSample.OriginWorld)
            || !IsFinite(shotSample.InitialVelocityWorld)
            || shotSample.InitialVelocityWorld.sqrMagnitude
                <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        receiver.OnMuzzleFire(new CombatMuzzleFireEvent(
            shotSample.OriginWorld,
            shotSample.InitialVelocityWorld.normalized,
            shotSample.SourceShipRootIdentity,
            muzzleIdentifier
        ));
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
