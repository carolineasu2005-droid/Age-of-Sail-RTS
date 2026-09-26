using UnityEngine;

public enum CombatDamageResolutionFailure
{
    None,
    InvalidOutcome,
    InvalidHitContext,
    UnsupportedAmmunition,
    MissingDamageProfile,
    InvalidDamageProfile,
    MissingTargetIntegrity,
    IntegrityMutationRejected
}

public static class CombatDamageResolver
{
    public static bool TryResolveAndApply(
        FoundationShotOutcome outcome,
        CombatDamageProfile damageProfile,
        out CombatDamageResult result,
        out CombatDamageResolutionFailure failure
    )
    {
        result = default;

        if (outcome.SourceShot.SourceShipRootIdentity == null)
        {
            failure = CombatDamageResolutionFailure.InvalidOutcome;
            return false;
        }

        if (outcome.SourceShot.AmmunitionType
            != FoundationAmmunitionType.RoundShot)
        {
            failure = CombatDamageResolutionFailure
                .UnsupportedAmmunition;
            return false;
        }

        if (outcome.Kind == FoundationShotOutcomeKind.WaterMiss
            || outcome.Kind == FoundationShotOutcomeKind.ExpiredNonHit)
        {
            if (outcome.HasHitContext)
            {
                failure = CombatDamageResolutionFailure.InvalidOutcome;
                return false;
            }

            result = CreateNoDamageResult(outcome.SourceShot);
            failure = CombatDamageResolutionFailure.None;
            return true;
        }

        if (outcome.Kind != FoundationShotOutcomeKind.HullHit
            || !outcome.HasHitContext)
        {
            failure = CombatDamageResolutionFailure.InvalidOutcome;
            return false;
        }

        CombatHitContext context = outcome.HitContext;

        if (!HasValidHitContext(outcome, context))
        {
            failure = CombatDamageResolutionFailure.InvalidHitContext;
            return false;
        }

        if (context.AmmunitionType
            != FoundationAmmunitionType.RoundShot)
        {
            failure = CombatDamageResolutionFailure
                .UnsupportedAmmunition;
            return false;
        }

        if (damageProfile == null)
        {
            failure = CombatDamageResolutionFailure.MissingDamageProfile;
            return false;
        }

        float regionMultiplier = damageProfile.GetRegionMultiplier(
            context.Region
        );

        if (!HasValidProfile(damageProfile)
            || !IsFinite(regionMultiplier)
            || regionMultiplier <= 0f)
        {
            failure = CombatDamageResolutionFailure.InvalidDamageProfile;
            return false;
        }

        float requestedDamage =
            damageProfile.RoundShotBaseDamage * regionMultiplier;

        if (!IsFinite(requestedDamage) || requestedDamage <= 0f)
        {
            failure = CombatDamageResolutionFailure.InvalidDamageProfile;
            return false;
        }

        ShipIntegrity integrity =
            context.TargetShipRoot.GetComponent<ShipIntegrity>();

        if (integrity == null || !integrity.IsInitialized)
        {
            failure = CombatDamageResolutionFailure
                .MissingTargetIntegrity;
            return false;
        }

        if (integrity.CurrentIntegrity <= 0f)
        {
            ShipIntegrityTransition unchangedTransition =
                new ShipIntegrityTransition(
                    integrity.CurrentIntegrity,
                    integrity.CurrentIntegrity,
                    integrity.LifecycleState,
                    integrity.LifecycleState
                );
            result = CreateHullResult(
                outcome.SourceShot,
                context,
                requestedDamage,
                0f,
                false,
                unchangedTransition
            );
            failure = CombatDamageResolutionFailure.None;
            return true;
        }

        if (!integrity.TryApplyIntegrityLoss(
            requestedDamage,
            out ShipIntegrityTransition transition
        ))
        {
            failure = CombatDamageResolutionFailure
                .IntegrityMutationRejected;
            return false;
        }

        float appliedDamage =
            transition.PreviousIntegrity - transition.CurrentIntegrity;
        result = CreateHullResult(
            outcome.SourceShot,
            context,
            requestedDamage,
            appliedDamage,
            appliedDamage > 0f,
            transition
        );
        failure = CombatDamageResolutionFailure.None;
        return true;
    }


    private static CombatDamageResult CreateNoDamageResult(
        ShotSample sourceShot
    )
    {
        return new CombatDamageResult(
            true,
            false,
            sourceShot,
            null,
            false,
            default,
            0f,
            0f,
            false,
            default
        );
    }


    private static CombatDamageResult CreateHullResult(
        ShotSample sourceShot,
        CombatHitContext context,
        float requestedDamage,
        float appliedDamage,
        bool applied,
        ShipIntegrityTransition transition
    )
    {
        return new CombatDamageResult(
            true,
            applied,
            sourceShot,
            context.TargetShipRoot,
            true,
            context.Region,
            requestedDamage,
            appliedDamage,
            true,
            transition
        );
    }


    private static bool HasValidHitContext(
        FoundationShotOutcome outcome,
        CombatHitContext context
    )
    {
        return context.SourceShot.SourceShipRootIdentity != null
            && outcome.SourceShot.SourceShipRootIdentity
                == context.SourceShot.SourceShipRootIdentity
            && outcome.SourceShot.AmmunitionType
                == context.SourceShot.AmmunitionType
            && context.TargetShipRoot != null
            && context.TargetShipRoot
                != context.SourceShot.SourceShipRootIdentity
            && context.TargetCombatGeometry != null
            && context.TargetCombatGeometry.gameObject
                == context.TargetShipRoot
            && context.HitRegion != null
            && context.HitRegion.Owner
                == context.TargetCombatGeometry
            && context.HitRegion.Region == context.Region;
    }


    private static bool HasValidProfile(CombatDamageProfile profile)
    {
        return IsFinite(profile.RoundShotBaseDamage)
            && profile.RoundShotBaseDamage > 0f
            && IsFinite(profile.BowMultiplier)
            && profile.BowMultiplier > 0f
            && IsFinite(profile.MidshipMultiplier)
            && profile.MidshipMultiplier > 0f
            && IsFinite(profile.SternMultiplier)
            && profile.SternMultiplier > 0f;
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
