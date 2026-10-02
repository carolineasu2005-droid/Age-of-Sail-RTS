using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShipAutoTargetCandidateProvider
{
    public static bool TryCollect(
        GameObject shooterShipRoot,
        out IReadOnlyList<AutoTargetCandidate> candidates)
    {
        candidates = Array.Empty<AutoTargetCandidate>();

        if (!HasValidShooter(shooterShipRoot,
            out ShipFireEligibility fireEligibility))
        {
            return false;
        }

        Collider[] overlaps = Physics.OverlapSphere(
            shooterShipRoot.transform.position,
            fireEligibility.MaximumRangeMeters,
            CombatPhysicsQuery.CombatGeometryMask,
            QueryTriggerInteraction.Collide);
        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();

        foreach (Collider collider in overlaps)
        {
            if (TryResolveRegisteredRoot(collider, out GameObject root)
                && root != shooterShipRoot)
            {
                uniqueRoots.Add(root);
            }
        }

        List<GameObject> orderedRoots = new List<GameObject>(uniqueRoots);
        orderedRoots.Sort(CompareEntityIds);
        ShipCombatAffiliation shooterAffiliation =
            shooterShipRoot.GetComponent<ShipCombatAffiliation>();
        List<AutoTargetCandidate> collected =
            new List<AutoTargetCandidate>(orderedRoots.Count);

        foreach (GameObject root in orderedRoots)
        {
            CombatRelationship relationship =
                CombatRelationshipResolver.Resolve(
                    shooterAffiliation,
                    root.GetComponent<ShipCombatAffiliation>());
            collected.Add(new AutoTargetCandidate(root,
                relationship == CombatRelationship.Hostile));
        }

        candidates = collected.AsReadOnly();
        return true;
    }

    private static bool HasValidShooter(
        GameObject shooterShipRoot,
        out ShipFireEligibility fireEligibility)
    {
        fireEligibility = null;
        if (shooterShipRoot == null || !shooterShipRoot.activeInHierarchy)
        {
            return false;
        }

        ShipCombatGeometry geometry =
            shooterShipRoot.GetComponent<ShipCombatGeometry>();
        fireEligibility =
            shooterShipRoot.GetComponent<ShipFireEligibility>();
        float radius = fireEligibility != null
            ? fireEligibility.MaximumRangeMeters
            : float.NaN;
        Vector3 position = shooterShipRoot.transform.position;
        return geometry != null
            && geometry.isActiveAndEnabled
            && HasRegisteredGeometry(geometry)
            && fireEligibility != null
            && fireEligibility.isActiveAndEnabled
            && IsFinite(position.x)
            && IsFinite(position.y)
            && IsFinite(position.z)
            && IsFinite(radius)
            && radius >= 0f;
    }

    private static bool HasRegisteredGeometry(ShipCombatGeometry geometry)
    {
        return IsRegisteredRegion(geometry, geometry.BowRegion)
            || IsRegisteredRegion(geometry, geometry.MidshipRegion)
            || IsRegisteredRegion(geometry, geometry.SternRegion);
    }

    private static bool IsRegisteredRegion(
        ShipCombatGeometry geometry, CombatHitRegion region)
    {
        return region != null
            && TryResolveRegisteredRoot(region.QueryCollider,
                out GameObject root)
            && root == geometry.gameObject;
    }

    private static bool TryResolveRegisteredRoot(
        Collider collider,
        out GameObject shipRoot)
    {
        shipRoot = null;
        if (collider == null
            || !collider.enabled
            || !collider.isTrigger
            || collider.gameObject.layer
                != CombatPhysicsQuery.CombatGeometryLayer)
        {
            return false;
        }

        CombatHitRegion region = collider.GetComponent<CombatHitRegion>();
        ShipCombatGeometry owner = region != null ? region.Owner : null;
        if (owner == null
            || !owner.isActiveAndEnabled
            || !region.isActiveAndEnabled
            || owner.transform == region.transform
            || !region.transform.IsChildOf(owner.transform)
            || !owner.TryResolveRegion(collider,
                out CombatHitRegion registeredRegion)
            || registeredRegion != region)
        {
            return false;
        }

        shipRoot = owner.gameObject;
        return shipRoot.activeInHierarchy;
    }

    private static int CompareEntityIds(GameObject left, GameObject right)
    {
        return left.GetEntityId().CompareTo(right.GetEntityId());
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
