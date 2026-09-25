using UnityEngine;

public enum CombatSemanticHitQueryFailure
{
    None,
    NotCombatGeometryContact,
    InvalidShot,
    InvalidSpatialData,
    MissingContactedCollider,
    WrongPhysicsLayer,
    UnregisteredCombatRegion,
    InvalidOwnershipChain,
    SourceShipContact
}

public static class CombatSemanticHitQuery
{
    private const float MinimumVelocitySqrMagnitude = 0.000001f;


    public static bool TryResolve(
        ProjectileTerminalContact contact,
        out CombatHitContext hitContext,
        out CombatSemanticHitQueryFailure failure
    )
    {
        hitContext = default;

        if (contact.Kind
            != ProjectileTerminalContactKind.CombatGeometryContact)
        {
            failure = CombatSemanticHitQueryFailure
                .NotCombatGeometryContact;
            return false;
        }

        ShotSample shot = contact.ShotSample;

        if (shot.SourceShipRootIdentity == null
            || shot.AmmunitionType
                != FoundationAmmunitionType.RoundShot)
        {
            failure = CombatSemanticHitQueryFailure.InvalidShot;
            return false;
        }

        Vector3 incomingVelocity =
            CombatProjectileTrajectory.EvaluateVelocity(
                shot,
                contact.ElapsedFlightTimeSeconds
            );

        if (!IsFinite(contact.PointWorld)
            || !IsFinite(contact.NormalWorld)
            || contact.NormalWorld.sqrMagnitude <= 0f
            || !IsFinite(incomingVelocity)
            || incomingVelocity.sqrMagnitude
                <= MinimumVelocitySqrMagnitude)
        {
            failure = CombatSemanticHitQueryFailure.InvalidSpatialData;
            return false;
        }

        Collider collider = contact.ContactedCollider;

        if (collider == null)
        {
            failure = CombatSemanticHitQueryFailure
                .MissingContactedCollider;
            return false;
        }

        if (collider.gameObject.layer
            != CombatPhysicsQuery.CombatGeometryLayer)
        {
            failure = CombatSemanticHitQueryFailure.WrongPhysicsLayer;
            return false;
        }

        CombatHitRegion contactedRegion =
            collider.GetComponent<CombatHitRegion>();

        if (contactedRegion == null)
        {
            failure = CombatSemanticHitQueryFailure
                .UnregisteredCombatRegion;
            return false;
        }

        ShipCombatGeometry owner = contactedRegion.Owner;

        if (owner == null
            || !owner.TryResolveRegion(
                collider,
                out CombatHitRegion resolvedRegion
            )
            || resolvedRegion != contactedRegion
            || contactedRegion.transform == owner.transform
            || !contactedRegion.transform.IsChildOf(owner.transform))
        {
            failure = CombatSemanticHitQueryFailure
                .InvalidOwnershipChain;
            return false;
        }

        GameObject targetShipRoot = owner.gameObject;

        if (targetShipRoot == shot.SourceShipRootIdentity)
        {
            failure = CombatSemanticHitQueryFailure.SourceShipContact;
            return false;
        }

        hitContext = new CombatHitContext(
            shot,
            targetShipRoot,
            owner,
            resolvedRegion,
            contact.PointWorld,
            contact.NormalWorld,
            incomingVelocity
        );
        failure = CombatSemanticHitQueryFailure.None;
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
