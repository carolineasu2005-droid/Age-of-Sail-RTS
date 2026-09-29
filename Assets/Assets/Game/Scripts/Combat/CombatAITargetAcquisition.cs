using System.Collections.Generic;
using UnityEngine;

public static class CombatAITargetAcquisition
{
    public static GameObject Select(
        GameObject sourceShipRoot,
        GameObject currentTargetShipRoot,
        float acquisitionRadiusMeters
    )
    {
        if (!HasValidSource(sourceShipRoot)
            || !IsValidRadius(acquisitionRadiusMeters))
        {
            return null;
        }

        if (IsLegal(sourceShipRoot, currentTargetShipRoot, acquisitionRadiusMeters))
        {
            return currentTargetShipRoot;
        }

        Collider[] colliders = Physics.OverlapSphere(
            sourceShipRoot.transform.position,
            acquisitionRadiusMeters,
            CombatPhysicsQuery.CombatGeometryMask,
            QueryTriggerInteraction.Collide
        );
        HashSet<GameObject> visitedRoots = new HashSet<GameObject>();
        GameObject nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        Vector3 sourcePosition = sourceShipRoot.transform.position;

        foreach (Collider collider in colliders)
        {
            if (!TryGetShipRoot(collider, out GameObject candidate)
                || !visitedRoots.Add(candidate)
                || !IsLegal(sourceShipRoot, candidate, acquisitionRadiusMeters))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - sourcePosition;
            float distanceSquared = offset.x * offset.x + offset.z * offset.z;
            if (distanceSquared < nearestDistanceSquared
                || (distanceSquared == nearestDistanceSquared
                    && nearest != null
                    && candidate.GetEntityId().CompareTo(
                        nearest.GetEntityId()
                    ) < 0))
            {
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    public static bool IsLegal(
        GameObject sourceShipRoot,
        GameObject candidateShipRoot,
        float acquisitionRadiusMeters
    )
    {
        if (!HasValidSource(sourceShipRoot)
            || candidateShipRoot == null
            || candidateShipRoot == sourceShipRoot
            || !candidateShipRoot.activeInHierarchy
            || !IsValidRadius(acquisitionRadiusMeters)
            || !HasDiscoverableGeometry(candidateShipRoot))
        {
            return false;
        }

        ShipIntegrity integrity = candidateShipRoot.GetComponent<ShipIntegrity>();
        if (integrity == null || !integrity.IsInitialized || integrity.IsSinking)
        {
            return false;
        }

        if (CombatRelationshipResolver.Resolve(
            sourceShipRoot.GetComponent<ShipCombatAffiliation>(),
            candidateShipRoot.GetComponent<ShipCombatAffiliation>()
        ) != CombatRelationship.Hostile)
        {
            return false;
        }

        Vector3 offset = candidateShipRoot.transform.position
            - sourceShipRoot.transform.position;
        float distanceSquared = offset.x * offset.x + offset.z * offset.z;
        return !float.IsNaN(distanceSquared)
            && distanceSquared <= acquisitionRadiusMeters * acquisitionRadiusMeters;
    }

    private static bool TryGetShipRoot(Collider collider, out GameObject shipRoot)
    {
        shipRoot = null;
        if (collider == null
            || collider.gameObject.layer != CombatPhysicsQuery.CombatGeometryLayer
            || !collider.isTrigger)
        {
            return false;
        }

        CombatHitRegion region = collider.GetComponent<CombatHitRegion>();
        ShipCombatGeometry owner = region != null ? region.Owner : null;
        if (owner == null
            || owner.transform == region.transform
            || !region.transform.IsChildOf(owner.transform)
            || !owner.TryResolveRegion(collider, out CombatHitRegion resolved)
            || resolved != region)
        {
            return false;
        }

        shipRoot = owner.gameObject;
        return true;
    }

    private static bool HasValidSource(GameObject sourceShipRoot)
    {
        return sourceShipRoot != null
            && sourceShipRoot.activeInHierarchy
            && sourceShipRoot.GetComponent<ShipCombatGeometry>() != null;
    }

    private static bool HasDiscoverableGeometry(GameObject shipRoot)
    {
        ShipCombatGeometry geometry = shipRoot.GetComponent<ShipCombatGeometry>();
        if (geometry == null || !geometry.isActiveAndEnabled)
        {
            return false;
        }

        return IsRegisteredEnabledRegion(geometry, geometry.BowRegion)
            || IsRegisteredEnabledRegion(geometry, geometry.MidshipRegion)
            || IsRegisteredEnabledRegion(geometry, geometry.SternRegion);
    }

    private static bool IsRegisteredEnabledRegion(
        ShipCombatGeometry geometry,
        CombatHitRegion region
    )
    {
        Collider collider = region != null ? region.QueryCollider : null;
        return region != null
            && region.isActiveAndEnabled
            && region.transform != geometry.transform
            && region.transform.IsChildOf(geometry.transform)
            && collider != null
            && collider.enabled
            && collider.gameObject.layer == CombatPhysicsQuery.CombatGeometryLayer
            && collider.isTrigger
            && geometry.TryResolveRegion(collider, out CombatHitRegion resolved)
            && resolved == region;
    }

    private static bool IsValidRadius(float radius)
    {
        return !float.IsNaN(radius)
            && !float.IsInfinity(radius)
            && radius > 0f;
    }
}
