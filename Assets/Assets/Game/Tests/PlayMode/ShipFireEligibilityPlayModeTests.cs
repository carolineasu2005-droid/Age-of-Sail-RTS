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
    private ProjectileFlightProfile flightProfile;
    private AutoTargetScoringProfile scoringProfile;


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
        flightProfile = ScriptableObject.CreateInstance<
            ProjectileFlightProfile
        >();
        scoringProfile = ScriptableObject.CreateInstance<
            AutoTargetScoringProfile
        >();
        shooterRoot = CreateShip("Shooter Root", Vector3.zero, 1);
        targetRoot = CreateShip(
            "Target Root",
            Vector3.right * 100f,
            2
        );
        AddMuzzleSockets(shooterRoot);
        ShipProjectileFlightConfiguration flight = shooterRoot
            .AddComponent<ShipProjectileFlightConfiguration>();
        SetPrivateField(flight, "projectileFlightProfile", flightProfile);
        ShipAutoTargetScoringConfiguration scoring = shooterRoot
            .AddComponent<ShipAutoTargetScoringConfiguration>();
        SetPrivateField(scoring, "scoringProfile", scoringProfile);
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
        Object.Destroy(flightProfile);
        Object.Destroy(scoringProfile);
        yield return null;
    }


    [UnityTest]
    public IEnumerator ClearThirteenMuzzleTrajectories_AreFireLegal()
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
        Assert.That(ShipBroadsideShotSampler.TryBuildNominalShots(
            shooterRoot, CombatSide.Starboard, new Vector3(100f, 5f, 0f),
            out ShotSample[] nominal), Is.True);
        CreateWorldBlocker(
            "One Trajectory Blocker",
            Vector3.Lerp(
                nominal[0].OriginWorld,
                CombatProjectileTrajectory.EvaluatePosition(
                    nominal[0], nominal[0].NominalFlightTimeSeconds),
                0.5f),
            new Vector3(0.2f, 2f, 0.2f)
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
    public IEnumerator FriendlyOnStraightChordBelowNominalArc_DoesNotHoldFire()
    {
        Vector3 chordMidpoint = ConfigureLongRangeClearCase();
        Collider blocker = CreateShipBlocker(
            "Low Friendly", chordMidpoint, 1);
        ((BoxCollider)blocker).size = new Vector3(4f, 4f, 40f);
        AssertAllNominalSweepsClearAbove(blocker, chordMidpoint);

        FireEligibilityResult first = Evaluate(targetRoot);
        FireEligibilityResult second = Evaluate(targetRoot);

        Assert.That(first.HasObstructionPath, Is.True);
        Assert.That(first.Blocked, Is.False);
        Assert.That(first.Obstruction.BlockedRayCount, Is.Zero);
        Assert.That(first.CanFire, Is.True);
        Assert.That(second.Obstruction.BlockedRayCount,
            Is.EqualTo(first.Obstruction.BlockedRayCount));
        yield return null;
    }


    [UnityTest]
    public IEnumerator WorldObstacleOnStraightChordBelowNominalArc_DoesNotHoldFire()
    {
        Vector3 chordMidpoint = ConfigureLongRangeClearCase();
        Collider blocker = CreateWorldBlocker(
            "Low World Obstacle", chordMidpoint,
            new Vector3(4f, 4f, 40f));
        AssertAllNominalSweepsClearAbove(blocker, chordMidpoint);

        FireEligibilityResult result = Evaluate(targetRoot);

        Assert.That(result.Blocked, Is.False);
        Assert.That(result.Obstruction.BlockedRayCount, Is.Zero);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator FriendlyLineAheadShip_BlocksRearTargetedFire()
    {
        Collider blocker = CreateShipBlocker(
            "Friendly Front Ship",
            Vector3.right * 50f + Vector3.up * 12f,
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
            Vector3.right * 50f + Vector3.up * 12f,
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
            Vector3.right * 50f + Vector3.up * 12f,
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
            Vector3.right * 50f + Vector3.up * 12f,
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
            Vector3.right * 50f + Vector3.up * 12f,
            new Vector3(4f, 20f, 40f)
        );
        Assert.That(Evaluate(targetRoot).Blocked, Is.True);

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
    public IEnumerator AutoTargetSelection_StillRejectsFriendlyObstruction()
    {
        ShipCombatState combatState =
            shooterRoot.GetComponent<ShipCombatState>();
        combatState.SetAutoFireEnabled(true);
        CreateShipBlocker("Auto Friendly Blocker",
            Vector3.right * 50f + Vector3.up * 12f, 1);
        Physics.SyncTransforms();
        Assert.That(Evaluate(targetRoot).Blocked, Is.True);

        bool selected = ShipAutoTargetSelector.TrySelect(
            shooterRoot,
            new[] { new AutoTargetCandidate(targetRoot, true) },
            out AutoTargetSelectionResult result);

        Assert.That(selected, Is.False);
        Assert.That(result.HasAnyTarget, Is.False);
        Assert.That(combatState.AutoFireEnabled, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator AutoTargetSelection_AcceptsFriendlyBelowNominalArc()
    {
        Vector3 chordMidpoint = ConfigureLongRangeClearCase();
        ShipCombatState combatState =
            shooterRoot.GetComponent<ShipCombatState>();
        combatState.SetAutoFireEnabled(true);
        Collider blocker = CreateShipBlocker(
            "Low Auto Friendly", chordMidpoint,
            1);
        ((BoxCollider)blocker).size = new Vector3(4f, 4f, 40f);
        Physics.SyncTransforms();
        AssertAllNominalSweepsClearAbove(blocker, chordMidpoint);

        bool selected = ShipAutoTargetSelector.TrySelect(
            shooterRoot,
            new[] { new AutoTargetCandidate(targetRoot, true) },
            out AutoTargetSelectionResult result);

        Assert.That(selected, Is.True);
        Assert.That(result.HasAnyTarget, Is.True);
        Assert.That(Evaluate(targetRoot).Blocked, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PointBlindFire_WorldObstructionDoesNotHoldFire()
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
        Assert.That(result.HasObstructionPath, Is.False);
        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanBlindFire, Is.True);
        Assert.That(result.FailureReasons,
            Is.EqualTo(BlindFireEligibilityFailure.None));
        yield return null;
    }


    [UnityTest]
    public IEnumerator PointBlindFire_FriendlyShipDoesNotHoldFire()
    {
        CreateShipBlocker(
            "Friendly Blind Fire Blocker",
            Vector3.right * 50f,
            1
        );

        bool evaluated = eligibility.TryEvaluateBlindFireAtPoint(
            new Vector3(100f, 5f, 0f),
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        Assert.That(result.HasObstructionPath, Is.False);
        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanBlindFire, Is.True);
        Assert.That(result.FailureReasons,
            Is.EqualTo(BlindFireEligibilityFailure.None));
        yield return null;
    }


    private Vector3 ConfigureLongRangeClearCase()
    {
        targetRoot.transform.position = Vector3.right * 360f;
        SetPrivateField(eligibility, "effectiveRangeMeters", 400f);
        SetPrivateField(eligibility, "maximumRangeMeters", 400f);
        Assert.That(targetRoot.GetComponent<ShipExposureReference>()
            .TryCalculateExposure(shooterRoot.transform.position,
                out ExposureRect exposure), Is.True);
        return Vector3.Lerp(
            shooterRoot.GetComponent<ShipMuzzleSockets>()
                .StarboardMuzzles[0].position,
            exposure.CenterWorld,
            0.5f);
    }


    private void AssertAllNominalSweepsClearAbove(
        Collider blocker, Vector3 chordMidpoint)
    {
        Physics.SyncTransforms();
        Assert.That(blocker.bounds.Contains(chordMidpoint), Is.True,
            "The old straight chord must pass through the blocker interior.");
        Assert.That(targetRoot.GetComponent<ShipExposureReference>()
            .TryCalculateExposure(shooterRoot.transform.position,
                out ExposureRect exposure), Is.True);
        Assert.That(ShipBroadsideShotSampler.TryBuildNominalShots(
            shooterRoot, CombatSide.Starboard, exposure.CenterWorld,
            out ShotSample[] shots), Is.True);
        Assert.That(shots, Has.Length.EqualTo(13));
        Bounds bounds = blocker.bounds;
        Assert.That(CombatFireObstructionQuery.TryClassifyBlockingCollider(
            blocker, out CombatFireBlockerKind kind), Is.True);

        for (int index = 0; index < shots.Length; index++)
        {
            ShotSample shot = shots[index];
            bool crossedBlockerX = false;
            for (float t0 = 0f; t0 < shot.NominalFlightTimeSeconds;)
            {
                float t1 = Mathf.Min(t0 + 1f,
                    shot.NominalFlightTimeSeconds);
                Vector3 p0 = CombatProjectileTrajectory.EvaluatePosition(
                    shot, t0);
                Vector3 p1 = CombatProjectileTrajectory.EvaluatePosition(
                    shot, t1);
                float segmentMinX = Mathf.Min(p0.x, p1.x);
                float segmentMaxX = Mathf.Max(p0.x, p1.x);
                bool overlapsX = segmentMaxX >= bounds.min.x
                    && segmentMinX <= bounds.max.x;
                Vector3 delta = p1 - p0;
                bool contact = blocker.Raycast(new Ray(p0,
                    delta.normalized), out RaycastHit hit,
                    delta.magnitude);
                Collider firstContact = null;
                CombatFireBlockerKind firstKind = CombatFireBlockerKind.None;
                float firstDistance = float.PositiveInfinity;
                foreach (RaycastHit candidate in Physics.RaycastAll(
                    p0, delta.normalized, delta.magnitude,
                    CombatFireObstructionQuery.BlockingLayerMask,
                    QueryTriggerInteraction.Collide))
                {
                    if (candidate.collider == null
                        || candidate.collider.transform.IsChildOf(
                            shooterRoot.transform)
                        || !CombatFireObstructionQuery
                            .TryClassifyBlockingCollider(candidate.collider,
                                out CombatFireBlockerKind candidateKind)
                        || candidate.distance >= firstDistance)
                    {
                        continue;
                    }

                    firstContact = candidate.collider;
                    firstKind = candidateKind;
                    firstDistance = candidate.distance;
                }
                TestContext.WriteLine(
                    $"muzzle={index} origin={shot.OriginWorld} "
                    + $"flight={shot.NominalFlightTimeSeconds:F3}s "
                    + $"[{t0:F3},{t1:F3}] {p0}->{p1} "
                    + $"bounds={bounds} blockerContact="
                    + (contact ? hit.point.ToString() : "none")
                    + $" blockerKind={kind} "
                    + $"blockerRoot={blocker.transform.root.name} "
                    + $"firstContact={(firstContact != null ? firstContact.name : "none")} "
                    + $"firstKind={firstKind} "
                    + $"firstRoot={(firstContact != null ? firstContact.transform.root.name : "none")} "
                    + $"firstIsTarget={(firstContact != null && firstContact.transform.IsChildOf(targetRoot.transform))}");
                Assert.That(contact, Is.False,
                    $"Muzzle {index} predicted a blocker contact.");

                if (overlapsX)
                {
                    crossedBlockerX = true;
                    float x0 = Mathf.Max(bounds.min.x, segmentMinX);
                    float x1 = Mathf.Min(bounds.max.x, segmentMaxX);
                    float f0 = (x0 - p0.x) / (p1.x - p0.x);
                    float f1 = (x1 - p0.x) / (p1.x - p0.x);
                    float lowestSweepY = Mathf.Min(
                        Mathf.LerpUnclamped(p0.y, p1.y, f0),
                        Mathf.LerpUnclamped(p0.y, p1.y, f1));
                    Assert.That(lowestSweepY - bounds.max.y,
                        Is.GreaterThanOrEqualTo(2f),
                        $"Muzzle {index} lacks clear margin.");
                }

                t0 = t1;
            }

            Assert.That(crossedBlockerX, Is.True,
                $"Muzzle {index} never passed the blocker X interval.");
        }
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
