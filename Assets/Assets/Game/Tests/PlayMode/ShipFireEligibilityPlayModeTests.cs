using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ShipFireEligibilityPlayModeTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new();
    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipFireEligibility eligibility;
    private ShipIntegrityProfile integrityProfile;
    private CombatObstructionProfile obstructionProfile;


    [UnitySetUp]
    public IEnumerator SetUp()
    {
        integrityProfile =
            ScriptableObject.CreateInstance<ShipIntegrityProfile>();
        SetPrivateField(integrityProfile, "maximumIntegrity", 1000f);
        SetPrivateField(
            integrityProfile,
            "combatDisabledThresholdNormalized",
            0.25f
        );
        SetPrivateField(
            integrityProfile,
            "sinkingThresholdNormalized",
            0.05f
        );
        obstructionProfile = ScriptableObject.CreateInstance<
            CombatObstructionProfile
        >();
        shooterRoot = CreateShip("Shooter Root", Vector3.zero, 1);
        targetRoot = CreateShip(
            "Target Root",
            Vector3.right * 100f,
            2
        );
        AddMuzzleSockets(shooterRoot);
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetPrivateField(eligibility, "effectiveRangeMeters", 100f);
        SetPrivateField(eligibility, "maximumRangeMeters", 200f);
        SetPrivateField(
            eligibility,
            "combatObstructionProfile",
            obstructionProfile
        );
        Physics.SyncTransforms();
        yield return null;
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.Destroy(createdObject);
            }
        }

        createdObjects.Clear();
        Object.Destroy(integrityProfile);
        Object.Destroy(obstructionProfile);
        yield return null;
    }


    [UnityTest]
    public IEnumerator ClearThirteenMuzzleRays_AreFireLegal()
    {
        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.HasObstructionPath, Is.True);
        Assert.That(result.Obstruction.ParticipatingRayCount, Is.EqualTo(13));
        Assert.That(result.Obstruction.BlockedRayCount, Is.Zero);
        Assert.That(result.Obstruction.BlockedFraction, Is.Zero);
        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator OneOfThirteenBlockedAtZeroTolerance_HoldsFire()
    {
        Vector3 endpoint = new Vector3(100f, 5f, 0f);
        Transform muzzle = shooterRoot.GetComponent<ShipMuzzleSockets>()
            .StarboardMuzzles[0];
        CreateWorldBlocker(
            "One Ray Blocker",
            Vector3.Lerp(muzzle.position, endpoint, 0.5f),
            Vector3.one * 0.2f
        );

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.Obstruction.BlockedRayCount, Is.EqualTo(1));
        Assert.That(result.Obstruction.BlockedFraction,
            Is.EqualTo(1f / 13f));
        Assert.That(result.Blocked, Is.True);
        Assert.That(result.CanFire, Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator FriendlyLineAheadShip_BlocksRearTargetedFire()
    {
        Collider blocker = CreateShipBlocker(
            "Friendly Front Ship",
            Vector3.right * 50f,
            1
        );

        FireEligibilityResult result = Evaluate(targetRoot);

        AssertBlockedByShip(
            result,
            blocker,
            CombatRelationship.Friendly,
            13
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator HostileThirdShip_BlocksTargetedFire()
    {
        Collider blocker = CreateShipBlocker(
            "Hostile Interceptor",
            Vector3.right * 50f,
            3
        );

        FireEligibilityResult result = Evaluate(targetRoot);

        AssertBlockedByShip(
            result,
            blocker,
            CombatRelationship.Hostile,
            13
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator UnknownThirdShip_BlocksTargetedFire()
    {
        Collider blocker = CreateShipBlocker(
            "Unknown Interceptor",
            Vector3.right * 50f,
            null
        );

        FireEligibilityResult result = Evaluate(targetRoot);

        AssertBlockedByShip(
            result,
            blocker,
            CombatRelationship.Unknown,
            13
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator WorldObstacleBetweenShips_BlocksAllRays()
    {
        Collider blocker = CreateWorldBlocker(
            "Island Obstruction",
            Vector3.right * 50f + Vector3.up * 5f,
            new Vector3(4f, 20f, 40f)
        );

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.BlockingCollider, Is.SameAs(blocker));
        Assert.That(result.Obstruction.BlockedRayCount, Is.EqualTo(13));
        Assert.That(
            result.Obstruction.RepresentativeBlockerKind,
            Is.EqualTo(CombatFireBlockerKind.WorldObstacle)
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator ObstructionBehindIntendedTarget_DoesNotBlock()
    {
        CreateWorldBlocker(
            "Behind Target",
            Vector3.right * 150f + Vector3.up * 5f,
            new Vector3(4f, 20f, 40f)
        );

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.Blocked, Is.False);
        Assert.That(result.Obstruction.BlockedRayCount, Is.Zero);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator DefaultAndVisualGeometry_DoNotRedefineObstruction()
    {
        GameObject visual = CreateRoot(
            "VisualRoot Mesh",
            Vector3.right * 50f + Vector3.up * 5f
        );
        visual.AddComponent<MeshFilter>();
        visual.AddComponent<MeshRenderer>();
        BoxCollider defaultCollider = visual.AddComponent<BoxCollider>();
        defaultCollider.size = new Vector3(4f, 20f, 40f);

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(visual.layer, Is.Zero);
        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator IntendedTargetAndSourceHierarchy_AreExcluded()
    {
        CombatHitRegion sourceRegion = shooterRoot
            .GetComponent<ShipCombatGeometry>()
            .MidshipRegion;
        sourceRegion.transform.localPosition =
            Vector3.right * 50f + Vector3.up * 5f;
        ((BoxCollider)sourceRegion.QueryCollider).size =
            new Vector3(4f, 20f, 40f);

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator PortAndStarboardUseTheSameQuery()
    {
        FireEligibilityResult starboard = Evaluate(targetRoot);
        targetRoot.transform.position = Vector3.left * 100f;
        Physics.SyncTransforms();
        FireEligibilityResult port = Evaluate(targetRoot);

        Assert.That(starboard.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(port.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(starboard.Obstruction.ParticipatingRayCount,
            Is.EqualTo(13));
        Assert.That(port.Obstruction.ParticipatingRayCount, Is.EqualTo(13));
        Assert.That(starboard.Blocked, Is.False);
        Assert.That(port.Blocked, Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator AutoTargetSelection_ConsumesTargetedObstruction()
    {
        ShipCombatState combatState =
            shooterRoot.GetComponent<ShipCombatState>();
        combatState.SetAutoFireEnabled(true);
        Vector3 shooterPosition = shooterRoot.transform.position;
        Quaternion shooterRotation = shooterRoot.transform.rotation;
        CreateWorldBlocker(
            "Auto Fire Blocker",
            Vector3.right * 50f + Vector3.up * 5f,
            new Vector3(4f, 20f, 40f)
        );

        bool selected = ShipAutoTargetSelector.TrySelect(
            shooterRoot,
            new[] { new AutoTargetCandidate(targetRoot, true) },
            out AutoTargetSelectionResult result
        );

        Assert.That(selected, Is.False);
        Assert.That(result.HasAnyTarget, Is.False);
        Assert.That(combatState.AutoFireEnabled, Is.True);
        Assert.That(combatState.ManualTarget, Is.Null);
        Assert.That(shooterRoot.transform.position, Is.EqualTo(
            shooterPosition
        ));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(
            shooterRotation
        ));
        yield return null;
    }


    [UnityTest]
    public IEnumerator PointBlindFire_UsesCommonWorldObstruction()
    {
        CreateWorldBlocker(
            "Blind Fire Blocker",
            Vector3.right * 50f + Vector3.up * 5f,
            new Vector3(4f, 20f, 40f)
        );

        bool evaluated = eligibility.TryEvaluateBlindFireAtPoint(
            new Vector3(100f, 5f, 0f),
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        Assert.That(result.HasObstructionPath, Is.True);
        Assert.That(result.Obstruction.ParticipatingRayCount, Is.EqualTo(13));
        Assert.That(result.Obstruction.BlockedRayCount, Is.EqualTo(13));
        Assert.That(result.Blocked, Is.True);
        Assert.That(result.CanBlindFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.Obstructed
            ),
            Is.True
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator PointBlindFire_FriendlyShipBlocksAtZeroTolerance()
    {
        Collider blocker = CreateShipBlocker(
            "Friendly Blind Fire Blocker",
            Vector3.right * 50f,
            1
        );

        bool evaluated = eligibility.TryEvaluateBlindFireAtPoint(
            new Vector3(100f, 5f, 0f),
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        Assert.That(
            obstructionProfile.BlindFireFriendlyEdgeTolerance,
            Is.Zero
        );
        Assert.That(result.HasObstructionPath, Is.True);
        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Obstruction.BlockedRayCount, Is.EqualTo(13));
        Assert.That(
            result.Obstruction.RepresentativeBlockerKind,
            Is.EqualTo(CombatFireBlockerKind.Ship)
        );
        Assert.That(
            result.Obstruction.RepresentativeBlockerName,
            Is.EqualTo(blocker.transform.root.name)
        );
        Assert.That(
            result.Obstruction.BlockerRelationship,
            Is.EqualTo(CombatRelationship.Friendly)
        );
        Assert.That(result.CanBlindFire, Is.False);
        yield return null;
    }


    private FireEligibilityResult Evaluate(GameObject target)
    {
        Physics.SyncTransforms();
        bool evaluated = eligibility.TryEvaluate(
            target,
            true,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private GameObject CreateShip(
        string name,
        Vector3 position,
        int? teamId
    )
    {
        GameObject root = CreateRoot(name, position);
        root.SetActive(false);
        ShipIntegrity integrity = root.AddComponent<ShipIntegrity>();
        SetPrivateField(integrity, "integrityProfile", integrityProfile);
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition art = root.AddComponent<ShipArtDefinition>();
        ConfigureArtReferences(root, art);
        ShipExposureReference exposure =
            root.AddComponent<ShipExposureReference>();
        SetPrivateField(exposure, "shipArtDefinition", art);
        ShipCombatGeometry geometry =
            root.AddComponent<ShipCombatGeometry>();
        CreateSemanticRegion(
            "Midship Combat Geometry",
            root.transform,
            geometry,
            new Vector3(10f, 20f, 40f)
        );

        if (teamId.HasValue)
        {
            ShipCombatAffiliation affiliation =
                root.AddComponent<ShipCombatAffiliation>();
            SetPrivateField(affiliation, "teamId", teamId.Value);
        }

        root.SetActive(true);
        return root;
    }


    private Collider CreateShipBlocker(
        string name,
        Vector3 position,
        int? teamId
    )
    {
        GameObject root = CreateRoot(name, position);
        ShipCombatGeometry geometry =
            root.AddComponent<ShipCombatGeometry>();

        if (teamId.HasValue)
        {
            ShipCombatAffiliation affiliation =
                root.AddComponent<ShipCombatAffiliation>();
            SetPrivateField(affiliation, "teamId", teamId.Value);
        }

        return CreateSemanticRegion(
            "Blocking Hull",
            root.transform,
            geometry,
            new Vector3(4f, 20f, 40f)
        ).QueryCollider;
    }


    private CombatHitRegion CreateSemanticRegion(
        string name,
        Transform parent,
        ShipCombatGeometry owner,
        Vector3 size
    )
    {
        GameObject regionObject = new GameObject(name);
        regionObject.transform.SetParent(parent, false);
        regionObject.layer = CombatFireObstructionQuery
            .ShipCombatGeometryLayer;
        BoxCollider collider = regionObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        CombatHitRegion region =
            regionObject.AddComponent<CombatHitRegion>();
        SetPrivateField(region, "region", CombatHullRegion.Midship);
        SetPrivateField(region, "owner", owner);
        SetPrivateField(region, "queryCollider", collider);
        SetPrivateField(owner, "midshipRegion", region);
        return region;
    }


    private Collider CreateWorldBlocker(
        string name,
        Vector3 position,
        Vector3 size
    )
    {
        GameObject blocker = CreateRoot(name, position);
        blocker.layer = CombatObstructionVolume.LayerIndex;
        BoxCollider collider = blocker.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        CombatObstructionVolume volume =
            blocker.AddComponent<CombatObstructionVolume>();
        SetPrivateField(volume, "queryCollider", collider);
        Physics.SyncTransforms();
        return collider;
    }


    private static void AddMuzzleSockets(GameObject root)
    {
        ShipMuzzleSockets sockets = root.AddComponent<ShipMuzzleSockets>();
        Transform port = new GameObject("Port").transform;
        port.SetParent(root.transform, false);
        Transform starboard = new GameObject("Starboard").transform;
        starboard.SetParent(root.transform, false);

        for (int index = 0; index < 13; index++)
        {
            float z = 12f - index * 2f;
            Transform portMuzzle = new GameObject($"P{index + 1:00}")
                .transform;
            portMuzzle.SetParent(port, false);
            portMuzzle.localPosition = new Vector3(-5f, 5f, z);
            Transform starboardMuzzle =
                new GameObject($"S{index + 1:00}").transform;
            starboardMuzzle.SetParent(starboard, false);
            starboardMuzzle.localPosition = new Vector3(5f, 5f, z);
        }

        SetPrivateField(sockets, "portMuzzlesContainer", port);
        SetPrivateField(sockets, "starboardMuzzlesContainer", starboard);
    }


    private static void ConfigureArtReferences(
        GameObject root,
        ShipArtDefinition art
    )
    {
        SetArtReference(root, art, "centerReference", Vector3.up * 5f);
        SetArtReference(root, art, "waterlineReference", Vector3.zero);
        SetArtReference(root, art, "deckReference", Vector3.up * 10f);
        SetArtReference(root, art, "bowReference", Vector3.forward * 20f);
        SetArtReference(root, art, "sternReference", Vector3.back * 20f);
        SetArtReference(root, art, "portReference", Vector3.left * 5f);
        SetArtReference(root, art, "starboardReference", Vector3.right * 5f);
    }


    private static void SetArtReference(
        GameObject root,
        ShipArtDefinition art,
        string fieldName,
        Vector3 localPosition
    )
    {
        Transform reference = new GameObject(fieldName).transform;
        reference.SetParent(root.transform, false);
        reference.localPosition = localPosition;
        SetPrivateField(art, fieldName, reference);
    }


    private GameObject CreateRoot(string name, Vector3 position)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        createdObjects.Add(root);
        return root;
    }


    private static void AssertBlockedByShip(
        FireEligibilityResult result,
        Collider blocker,
        CombatRelationship relationship,
        int blockedRayCount
    )
    {
        Assert.That(result.Blocked, Is.True);
        Assert.That(result.BlockingCollider, Is.SameAs(blocker));
        Assert.That(result.Obstruction.BlockedRayCount,
            Is.EqualTo(blockedRayCount));
        Assert.That(
            result.Obstruction.RepresentativeBlockerKind,
            Is.EqualTo(CombatFireBlockerKind.Ship)
        );
        Assert.That(
            result.Obstruction.BlockerRelationship,
            Is.EqualTo(relationship)
        );
        Assert.That(result.CanFire, Is.False);
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            PrivateInstance
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
