using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShipAutoTargetCandidateProviderPlayModeTests
{
    private const int CombatGeometryLayer = 8;
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private static readonly System.Type[] MovementBehaviourTypes =
    {
        typeof(ShipMovementProfileController),
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController)
    };

    private readonly List<GameObject> created = new List<GameObject>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject root in created)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        created.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator FormalShips_DiscoverByCombatGeometryThenUseExistingSelector()
    {
#if UNITY_EDITOR
        GameObject shooter = InstantiateShip("Shooter", Vector3.zero, 1);
        GameObject port = InstantiateShip("Port Hostile",
            Vector3.left * 120f, 2);
        GameObject starboard = InstantiateShip("Starboard Hostile",
            Vector3.right * 120f, 2);
        GameObject friendly = InstantiateShip("Friendly",
            Vector3.forward * 80f, 1);
        GameObject unknown = InstantiateShip("Unknown",
            Vector3.back * 80f, -1);
        GameObject genericOnly = InstantiateShip("Generic Only",
            Vector3.forward * 150f, 2);
        ShipCombatGeometry genericGeometry =
            genericOnly.GetComponent<ShipCombatGeometry>();
        genericGeometry.BowRegion.QueryCollider.enabled = false;
        genericGeometry.MidshipRegion.QueryCollider.enabled = false;
        genericGeometry.SternRegion.QueryCollider.enabled = false;
        Transform visualRoot = genericOnly.transform.Find("VisualRoot");
        Assert.That(visualRoot, Is.Not.Null);
        GameObject visualCollider = new GameObject("Visual Collider");
        visualCollider.transform.SetParent(visualRoot, false);
        visualCollider.layer = CombatGeometryLayer;
        visualCollider.AddComponent<BoxCollider>().isTrigger = true;

        Physics.SyncTransforms();
        yield return null;

        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(shooter,
            out IReadOnlyList<AutoTargetCandidate> candidates), Is.True);
        Assert.That(candidates, Has.Count.EqualTo(4));
        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();
        foreach (AutoTargetCandidate candidate in candidates)
        {
            uniqueRoots.Add(candidate.TargetShipRoot);
        }

        Assert.That(uniqueRoots, Has.Count.EqualTo(candidates.Count));
        Assert.That(Find(candidates, shooter), Is.Null);
        Assert.That(Find(candidates, genericOnly), Is.Null);
        AutoTargetCandidate? portCandidate = Find(candidates, port);
        AutoTargetCandidate? starboardCandidate = Find(candidates, starboard);
        AutoTargetCandidate? friendlyCandidate = Find(candidates, friendly);
        AutoTargetCandidate? unknownCandidate = Find(candidates, unknown);
        Assert.That(portCandidate, Is.Not.Null);
        Assert.That(starboardCandidate, Is.Not.Null);
        Assert.That(friendlyCandidate, Is.Not.Null);
        Assert.That(unknownCandidate, Is.Not.Null);
        Assert.That(portCandidate.Value.RelationshipAllowsFire, Is.True);
        Assert.That(starboardCandidate.Value.RelationshipAllowsFire, Is.True);
        Assert.That(friendlyCandidate.Value.RelationshipAllowsFire, Is.False);
        Assert.That(unknownCandidate.Value.RelationshipAllowsFire, Is.False);

        for (int index = 1; index < candidates.Count; index++)
        {
            Assert.That(candidates[index - 1].TargetShipRoot.GetEntityId()
                .CompareTo(candidates[index].TargetShipRoot.GetEntityId()),
                Is.LessThan(0));
        }

        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(shooter,
            out IReadOnlyList<AutoTargetCandidate> repeated), Is.True);
        Assert.That(repeated, Has.Count.EqualTo(candidates.Count));
        for (int index = 0; index < candidates.Count; index++)
        {
            Assert.That(repeated[index].TargetShipRoot,
                Is.SameAs(candidates[index].TargetShipRoot));
        }

        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        Assert.That(state.ManualTarget, Is.Null);
        Assert.That(ShipAutoTargetSelector.TrySelect(shooter, candidates,
            out AutoTargetSelectionResult selection), Is.True);
        Assert.That(selection.PortSelection.HasTarget, Is.True);
        Assert.That(selection.StarboardSelection.HasTarget, Is.True);
        Assert.That(selection.PortSelection.TargetShipRoot, Is.SameAs(port));
        Assert.That(selection.StarboardSelection.TargetShipRoot,
            Is.SameAs(starboard));
        Assert.That(state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }

#if UNITY_EDITOR
    private GameObject InstantiateShip(string name, Vector3 position, int team)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject root = Object.Instantiate(prefab);
        root.name = name;
        root.transform.position = position;
        created.Add(root);

        foreach (System.Type type in MovementBehaviourTypes)
        {
            Behaviour movement = root.GetComponent(type) as Behaviour;
            if (movement != null)
            {
                movement.enabled = false;
            }
        }

        ShipCombatAffiliation affiliation =
            root.GetComponent<ShipCombatAffiliation>();
        Assert.That(affiliation, Is.Not.Null);
        FieldInfo field = typeof(ShipCombatAffiliation).GetField("teamId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(affiliation, team);
        return root;
    }

    private static AutoTargetCandidate? Find(
        IReadOnlyList<AutoTargetCandidate> candidates, GameObject root)
    {
        foreach (AutoTargetCandidate candidate in candidates)
        {
            if (candidate.TargetShipRoot == root)
            {
                return candidate;
            }
        }

        return null;
    }
#endif
}
