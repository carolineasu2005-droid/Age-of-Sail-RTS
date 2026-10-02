using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipAutoTargetCandidateProviderTests
{
    private const int CombatGeometryLayer = 8;
    private readonly List<GameObject> created = new List<GameObject>();
    private GameObject shooter;
    private ShipFireEligibility shooterEligibility;

    [SetUp]
    public void SetUp()
    {
        shooter = CreateRegisteredShip("Shooter", Vector3.zero, 1);
        shooterEligibility = shooter.GetComponent<ShipFireEligibility>();
        SetField(shooterEligibility, "maximumRangeMeters", 100f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject root in created)
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        created.Clear();
    }

    [Test]
    public void InvalidShooter_FailsWithEmptyResultWithoutMutation()
    {
        GameObject incomplete = CreateObject("Incomplete");
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Vector3 position = shooter.transform.position;

        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(
            null, out IReadOnlyList<AutoTargetCandidate> missing), Is.False);
        Assert.That(missing, Is.Empty);
        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(
            incomplete, out IReadOnlyList<AutoTargetCandidate> invalid), Is.False);
        Assert.That(invalid, Is.Empty);

        incomplete.AddComponent<ShipCombatGeometry>();
        incomplete.AddComponent<ShipFireEligibility>();
        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(
            incomplete, out IReadOnlyList<AutoTargetCandidate> unregistered),
            Is.False);
        Assert.That(unregistered, Is.Empty);

        SetField(shooterEligibility, "maximumRangeMeters", float.NaN);
        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(
            shooter, out IReadOnlyList<AutoTargetCandidate> badRange), Is.False);
        Assert.That(badRange, Is.Empty);
        Assert.That(state.AutoFireEnabled, Is.False);
        Assert.That(state.ManualTarget, Is.Null);
        Assert.That(shooter.transform.position, Is.EqualTo(position));
    }

    [Test]
    public void OwnRegisteredRegions_NeverProduceSelfCandidate()
    {
        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Is.Empty);
    }

    [Test]
    public void BowMidshipAndStern_ProduceOneTargetRoot()
    {
        GameObject target = CreateRegisteredShip("Target",
            Vector3.right * 40f, 2);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Has.Count.EqualTo(1));
        Assert.That(candidates[0].TargetShipRoot, Is.SameAs(target));
    }

    [Test]
    public void SeveralShips_EachProduceOneAuthoritativeRoot()
    {
        GameObject first = CreateRegisteredShip("First",
            Vector3.right * 30f, 2);
        GameObject second = CreateRegisteredShip("Second",
            Vector3.left * 45f, 2);
        GameObject third = CreateRegisteredShip("Third",
            Vector3.forward * 55f, 2);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();
        HashSet<GameObject> roots = new HashSet<GameObject>();
        foreach (AutoTargetCandidate candidate in candidates)
        {
            roots.Add(candidate.TargetShipRoot);
        }

        Assert.That(candidates, Has.Count.EqualTo(3));
        Assert.That(roots, Has.Count.EqualTo(3));
        Assert.That(roots.Contains(first), Is.True);
        Assert.That(roots.Contains(second), Is.True);
        Assert.That(roots.Contains(third), Is.True);
    }

    [Test]
    public void RelationshipFact_UsesCurrentAffiliations()
    {
        GameObject hostile = CreateRegisteredShip("Hostile",
            Vector3.right * 30f, 2);
        GameObject friendly = CreateRegisteredShip("Friendly",
            Vector3.left * 40f, 1);
        GameObject unknown = CreateRegisteredShip("Unknown",
            Vector3.forward * 50f, -1);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Has.Count.EqualTo(3));
        Assert.That(AllowsFire(candidates, hostile), Is.True);
        Assert.That(AllowsFire(candidates, friendly), Is.False);
        Assert.That(AllowsFire(candidates, unknown), Is.False);

        SetTeam(hostile, 1);
        Assert.That(AllowsFire(Collect(), hostile), Is.False);
        SetTeam(shooter, -1);
        Assert.That(AllowsFire(Collect(), friendly), Is.False);
    }

    [Test]
    public void LifecycleStates_RemainSpatialCandidates()
    {
        GameObject operational = CreateRegisteredShip("Operational",
            Vector3.right * 30f, 2);
        GameObject disabled = CreateRegisteredShip("Disabled",
            Vector3.left * 40f, 2);
        GameObject sinking = CreateRegisteredShip("Sinking",
            Vector3.forward * 50f, 2);
        CombatLifecycleTestUtility.AddOperationalIntegrity(operational);
        CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(disabled,
            ShipCombatLifecycleState.CombatDisabled);
        CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(sinking,
            ShipCombatLifecycleState.Sinking);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Has.Count.EqualTo(3));
        Assert.That(Contains(candidates, operational), Is.True);
        Assert.That(Contains(candidates, disabled), Is.True);
        Assert.That(Contains(candidates, sinking), Is.True);
    }

    [Test]
    public void LayerEightColliderWithoutRegistration_IsExcluded()
    {
        GameObject unregistered = CreateObject("Unregistered Collider");
        unregistered.layer = CombatGeometryLayer;
        unregistered.transform.position = Vector3.right * 30f;
        BoxCollider collider = unregistered.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        Assert.That(Collect(), Is.Empty);
    }

    [Test]
    public void GenericAndVisualColliders_AreNotCandidateAuthority()
    {
        GameObject generic = CreateObject("Generic ShipCollider");
        generic.transform.position = Vector3.right * 30f;
        generic.AddComponent<BoxCollider>();
        GameObject visual = CreateObject("VisualRoot");
        visual.transform.position = Vector3.left * 30f;
        visual.layer = CombatGeometryLayer;
        visual.AddComponent<BoxCollider>().isTrigger = true;

        Assert.That(Collect(), Is.Empty);
    }

    [Test]
    public void RepeatedCollection_UsesEntityIdOrder()
    {
        GameObject first = CreateRegisteredShip("First",
            Vector3.forward * 55f, 2);
        GameObject second = CreateRegisteredShip("Second",
            Vector3.left * 35f, 2);
        GameObject third = CreateRegisteredShip("Third",
            Vector3.right * 45f, 2);
        List<GameObject> expected = new List<GameObject>
        {
            third, first, second
        };
        expected.Sort((left, right) =>
            left.GetEntityId().CompareTo(right.GetEntityId()));

        IReadOnlyList<AutoTargetCandidate> firstPass = Collect();
        IReadOnlyList<AutoTargetCandidate> secondPass = Collect();

        Assert.That(firstPass, Has.Count.EqualTo(expected.Count));
        Assert.That(secondPass, Has.Count.EqualTo(expected.Count));
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.That(firstPass[index].TargetShipRoot,
                Is.SameAs(expected[index]));
            Assert.That(secondPass[index].TargetShipRoot,
                Is.SameAs(expected[index]));
        }
    }

    [Test]
    public void Collection_DoesNotChangeCombatOrMovementOwners()
    {
        GameObject target = CreateRegisteredShip("Target",
            Vector3.right * 40f, 2);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipDestinationController movement =
            shooter.AddComponent<ShipDestinationController>();
        ShipIntegrity integrity =
            CombatLifecycleTestUtility.AddOperationalIntegrity(shooter);
        ShipIntegrity targetIntegrity =
            CombatLifecycleTestUtility.AddOperationalIntegrity(target);
        float integrityBefore = integrity.CurrentIntegrity;
        float targetIntegrityBefore = targetIntegrity.CurrentIntegrity;
        Vector3 position = shooter.transform.position;
        Quaternion rotation = shooter.transform.rotation;
        state.SetAutoFireEnabled(true);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Has.Count.EqualTo(1));
        Assert.That(candidates[0].TargetShipRoot, Is.SameAs(target));
        Assert.That(state.AutoFireEnabled, Is.True);
        Assert.That(state.ManualTarget, Is.Null);
        Assert.That(state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(integrityBefore));
        Assert.That(targetIntegrity.CurrentIntegrity,
            Is.EqualTo(targetIntegrityBefore));
        Assert.That(integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational));
        Assert.That(shooter.transform.position, Is.EqualTo(position));
        Assert.That(shooter.transform.rotation, Is.EqualTo(rotation));
        Assert.That(movement.HasDestination, Is.False);
        Assert.That(shooter.GetComponent<ShipCombatAffiliation>().TeamId,
            Is.EqualTo(1));
        Assert.That(target.GetComponent<ShipCombatAffiliation>().TeamId,
            Is.EqualTo(2));
    }

    [Test]
    public void MaximumRange_IsOnlyTheOverlapRadius()
    {
        GameObject near = CreateRegisteredShip("Near",
            Vector3.right * 40f, 2);
        CreateRegisteredShip("Far", Vector3.right * 130f, 2);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Has.Count.EqualTo(1));
        Assert.That(candidates[0].TargetShipRoot, Is.SameAs(near));
    }

    [Test]
    public void OverlappingHull_DoesNotAddAnExactRootRangeGate()
    {
        GameObject target = CreateRegisteredShip("Hull At Radius",
            Vector3.right * 102f, 2);
        BoxCollider midship = target.GetComponent<ShipCombatGeometry>()
            .MidshipRegion.QueryCollider as BoxCollider;
        Assert.That(midship, Is.Not.Null);
        midship.size = new Vector3(10f, 2f, 2f);

        IReadOnlyList<AutoTargetCandidate> candidates = Collect();

        Assert.That(candidates, Has.Count.EqualTo(1));
        Assert.That(candidates[0].TargetShipRoot, Is.SameAs(target));
    }

    private IReadOnlyList<AutoTargetCandidate> Collect()
    {
        Physics.SyncTransforms();
        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(
            shooter, out IReadOnlyList<AutoTargetCandidate> candidates), Is.True);
        return candidates;
    }

    private GameObject CreateRegisteredShip(
        string name, Vector3 position, int teamId)
    {
        GameObject root = CreateObject(name);
        root.transform.position = position;
        root.AddComponent<ShipCombatState>();
        root.AddComponent<ShipFireEligibility>();
        ShipCombatGeometry geometry = root.AddComponent<ShipCombatGeometry>();
        ShipCombatAffiliation affiliation =
            root.AddComponent<ShipCombatAffiliation>();
        SetField(affiliation, "teamId", teamId);

        CombatHitRegion bow = CreateRegion(geometry,
            CombatHullRegion.Bow, 2f);
        CombatHitRegion midship = CreateRegion(geometry,
            CombatHullRegion.Midship, 0f);
        CombatHitRegion stern = CreateRegion(geometry,
            CombatHullRegion.Stern, -2f);
        SetField(geometry, "bowRegion", bow);
        SetField(geometry, "midshipRegion", midship);
        SetField(geometry, "sternRegion", stern);
        return root;
    }

    private static CombatHitRegion CreateRegion(
        ShipCombatGeometry owner, CombatHullRegion identity, float localZ)
    {
        GameObject child = new GameObject(identity.ToString());
        child.layer = CombatGeometryLayer;
        child.transform.SetParent(owner.transform, false);
        child.transform.localPosition = new Vector3(0f, 0f, localZ);
        BoxCollider collider = child.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        CombatHitRegion region = child.AddComponent<CombatHitRegion>();
        SetField(region, "owner", owner);
        SetField(region, "region", identity);
        SetField(region, "queryCollider", collider);
        return region;
    }

    private GameObject CreateObject(string name)
    {
        GameObject root = new GameObject(name);
        created.Add(root);
        return root;
    }

    private static bool Contains(IReadOnlyList<AutoTargetCandidate> candidates,
        GameObject target)
    {
        foreach (AutoTargetCandidate candidate in candidates)
        {
            if (candidate.TargetShipRoot == target)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AllowsFire(
        IReadOnlyList<AutoTargetCandidate> candidates, GameObject target)
    {
        foreach (AutoTargetCandidate candidate in candidates)
        {
            if (candidate.TargetShipRoot == target)
            {
                return candidate.RelationshipAllowsFire;
            }
        }

        Assert.Fail("Expected candidate Root was not discovered.");
        return false;
    }

    private static void SetTeam(GameObject root, int teamId)
    {
        SetField(root.GetComponent<ShipCombatAffiliation>(), "teamId", teamId);
    }

    private static void SetField(object owner, string name, object value)
    {
        FieldInfo field = owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(owner, value);
    }
}
