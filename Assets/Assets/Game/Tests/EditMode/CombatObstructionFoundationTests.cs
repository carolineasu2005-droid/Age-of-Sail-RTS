using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatObstructionFoundationTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";
    private const string IslandPrefabPath =
        "Assets/Assets/Game/Combat/Prefabs/"
        + "PF_Debug_CombatIsland_v01.prefab";
    private const string ProfilePath =
        "Assets/Assets/Game/Data/"
        + "SO_CombatObstruction_Foundation.asset";

    private static readonly string[] GenericMovementPrefabPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };

    private readonly List<UnityEngine.Object> temporaryObjects = new();


    [TearDown]
    public void TearDown()
    {
        for (int index = temporaryObjects.Count - 1; index >= 0; index--)
        {
            if (temporaryObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    temporaryObjects[index]
                );
            }
        }

        temporaryObjects.Clear();
    }


    [Test]
    public void SameConfiguredTeamId_IsFriendly()
    {
        ShipCombatAffiliation source = CreateAffiliation(4, "Source");
        ShipCombatAffiliation target = CreateAffiliation(4, "Target");

        Assert.That(
            CombatRelationshipResolver.Resolve(source, target),
            Is.EqualTo(CombatRelationship.Friendly)
        );
    }


    [TestCase(1, CombatRelationship.Friendly)]
    [TestCase(3, CombatRelationship.Hostile)]
    [TestCase(-1, CombatRelationship.Unknown)]
    public void NominalTrajectory_ClassifiesThirdPartyShipOnArc(
        int blockerTeamId,
        CombatRelationship expectedRelationship
    )
    {
        GameObject shooter = InstantiateCombatShip(Vector3.zero, 1);
        GameObject target = InstantiateCombatShip(Vector3.right * 100f, 2);
        GameObject thirdParty = InstantiateCombatShip(
            Vector3.right * 50f, blockerTeamId);
        ShipCombatGeometry geometry =
            thirdParty.GetComponent<ShipCombatGeometry>();
        geometry.BowRegion.QueryCollider.enabled = false;
        geometry.SternRegion.QueryCollider.enabled = false;
        ((BoxCollider)geometry.MidshipRegion.QueryCollider).size =
            Vector3.one * 40f;
        Physics.SyncTransforms();
        Vector3 endpoint = GetExposureCenter(shooter, target);

        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard, endpoint,
            AssetDatabase.LoadAssetAtPath<CombatObstructionProfile>(
                ProfilePath), out CombatFireObstructionResult result), Is.True);
        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.BlockedRayCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.RepresentativeBlockerKind,
            Is.EqualTo(CombatFireBlockerKind.Ship));
        Assert.That(result.BlockerRelationship,
            Is.EqualTo(expectedRelationship));
        Assert.That(result.RepresentativeBlockerName,
            Is.EqualTo(thirdParty.name));
    }


    [Test]
    public void FriendlyOnStraightChordBelowNominalArc_IsClear()
    {
        GameObject shooter = InstantiateCombatShip(Vector3.zero, 1);
        GameObject target = InstantiateCombatShip(Vector3.right * 360f, 2);
        GameObject friendly = InstantiateCombatShip(Vector3.right * 180f, 1);
        ShipCombatGeometry geometry =
            friendly.GetComponent<ShipCombatGeometry>();
        geometry.BowRegion.QueryCollider.enabled = false;
        geometry.SternRegion.QueryCollider.enabled = false;
        BoxCollider hull = (BoxCollider)geometry.MidshipRegion.QueryCollider;
        Vector3 endpoint = GetExposureCenter(shooter, target);
        Vector3 origin = shooter.GetComponent<ShipMuzzleSockets>()
            .StarboardMuzzles[0].position;
        Vector3 chordPoint = Vector3.Lerp(origin, endpoint, 0.5f);
        hull.size = new Vector3(4f, 4f, 40f);
        hull.center = hull.transform.InverseTransformPoint(chordPoint);
        Physics.SyncTransforms();
        Assert.That(hull.bounds.Contains(chordPoint), Is.True);
        AssertAllNominalSweepsClearAbove(shooter, endpoint, hull);

        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard, endpoint,
            AssetDatabase.LoadAssetAtPath<CombatObstructionProfile>(
                ProfilePath), out CombatFireObstructionResult result), Is.True);
        Assert.That(result.IsBlocked, Is.False);
        Assert.That(result.BlockedRayCount, Is.Zero);
        Assert.That(result.ParticipatingRayCount, Is.EqualTo(13));
    }


    [TestCase(false)]
    [TestCase(true)]
    public void NominalTrajectory_WorldBelowArcClearsAndWorldOnArcBlocks(
        bool expectedBlocked
    )
    {
        GameObject shooter = InstantiateCombatShip(Vector3.zero, 1);
        GameObject target = InstantiateCombatShip(Vector3.right * 360f, 2);
        Vector3 endpoint = GetExposureCenter(shooter, target);
        Assert.That(ShipBroadsideShotSampler.TryBuildNominalShots(
            shooter, CombatSide.Starboard, endpoint,
            out ShotSample[] nominal), Is.True);
        Vector3 chordMidpoint = Vector3.Lerp(
            nominal[0].OriginWorld, endpoint, 0.5f);
        Vector3 blockerCenter = expectedBlocked
            ? Vector3.Lerp(
                CombatProjectileTrajectory.EvaluatePosition(nominal[0], 1f),
                CombatProjectileTrajectory.EvaluatePosition(nominal[0], 2f),
                0.5f)
            : chordMidpoint;
        BoxCollider collider = CreateRegisteredWorldBlocker(
            blockerCenter, expectedBlocked
                ? new Vector3(8f, 8f, 40f)
                : new Vector3(4f, 4f, 40f));
        Physics.SyncTransforms();
        if (expectedBlocked)
        {
            Vector3 p0 = CombatProjectileTrajectory.EvaluatePosition(
                nominal[0], 1f);
            Vector3 p1 = CombatProjectileTrajectory.EvaluatePosition(
                nominal[0], 2f);
            Assert.That(collider.bounds.Contains(Vector3.Lerp(p0, p1, 0.5f)),
                Is.True, "Known nominal sweep must cross blocker interior.");
        }
        else
        {
            Assert.That(collider.bounds.Contains(chordMidpoint), Is.True);
            AssertAllNominalSweepsClearAbove(shooter, endpoint, collider);
        }

        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard,
            endpoint,
            AssetDatabase.LoadAssetAtPath<CombatObstructionProfile>(
                ProfilePath), out CombatFireObstructionResult result), Is.True);
        Assert.That(result.IsBlocked, Is.EqualTo(expectedBlocked));
        Assert.That(result.BlockedRayCount,
            expectedBlocked ? Is.GreaterThanOrEqualTo(1) : Is.Zero);
    }


    [Test]
    public void NominalObstructionSegmentInterval_IsPlaytestBoundOneSecond()
    {
        FieldInfo interval = typeof(CombatFireObstructionQuery).GetField(
            "NominalSegmentIntervalSeconds",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(interval, Is.Not.Null);
        Assert.That(interval.IsLiteral, Is.True);
        Assert.That(interval.GetRawConstantValue(), Is.EqualTo(1f));
    }


    [Test]
    public void NominalTrajectory_FinalRemainderSegmentIsSwept()
    {
        GameObject shooter = InstantiateCombatShip(Vector3.zero, 1);
        GameObject target = InstantiateCombatShip(Vector3.right * 300f, 2);
        Vector3 endpoint = GetExposureCenter(shooter, target);
        Assert.That(ShipBroadsideShotSampler.TryBuildNominalShots(
            shooter, CombatSide.Starboard, endpoint,
            out ShotSample[] shots), Is.True);
        ShotSample shot = shots[0];
        Assert.That(shot.NominalFlightTimeSeconds,
            Is.GreaterThan(2f).And.LessThan(3f));
        Vector3 p2 = CombatProjectileTrajectory.EvaluatePosition(shot, 2f);
        Vector3 terminal = CombatProjectileTrajectory.EvaluatePosition(
            shot, shot.NominalFlightTimeSeconds);
        BoxCollider blocker = CreateRegisteredWorldBlocker(
            Vector3.Lerp(p2, terminal, 0.5f),
            new Vector3(4f, 8f, 8f));
        Physics.SyncTransforms();
        Assert.That(blocker.bounds.Contains(Vector3.Lerp(
            p2, terminal, 0.5f)), Is.True);

        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard, endpoint, null,
            out CombatFireObstructionResult first), Is.True);
        Assert.That(first.IsBlocked, Is.True);
        Assert.That(first.BlockedRayCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard, endpoint, null,
            out CombatFireObstructionResult repeated), Is.True);
        Assert.That(repeated.BlockedRayCount,
            Is.EqualTo(first.BlockedRayCount));
        Assert.That(repeated.RepresentativeBlockerName,
            Is.EqualTo(first.RepresentativeBlockerName));
    }


    [Test]
    public void NominalTrajectory_SubsecondFlightStillSweepsOnce()
    {
        GameObject shooter = InstantiateCombatShip(Vector3.zero, 1);
        GameObject target = InstantiateCombatShip(Vector3.right * 100f, 2);
        Vector3 endpoint = GetExposureCenter(shooter, target);
        Assert.That(ShipBroadsideShotSampler.TryBuildNominalShots(
            shooter, CombatSide.Starboard, endpoint,
            out ShotSample[] shots), Is.True);
        ShotSample shot = shots[0];
        Assert.That(shot.NominalFlightTimeSeconds, Is.LessThan(1f));
        Vector3 terminal = CombatProjectileTrajectory.EvaluatePosition(
            shot, shot.NominalFlightTimeSeconds);
        BoxCollider blocker = CreateRegisteredWorldBlocker(
            Vector3.Lerp(shot.OriginWorld, terminal, 0.5f),
            new Vector3(4f, 8f, 8f));
        Physics.SyncTransforms();
        Assert.That(blocker.bounds.Contains(Vector3.Lerp(
            shot.OriginWorld, terminal, 0.5f)), Is.True);

        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard, endpoint, null,
            out CombatFireObstructionResult result), Is.True);
        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.BlockedRayCount, Is.GreaterThanOrEqualTo(1));
    }


    [Test]
    public void NominalTrajectory_IntendedTargetContactRemainsClear()
    {
        GameObject shooter = InstantiateCombatShip(Vector3.zero, 1);
        GameObject target = InstantiateCombatShip(Vector3.right * 100f, 2);
        ShipCombatGeometry geometry =
            target.GetComponent<ShipCombatGeometry>();
        geometry.BowRegion.QueryCollider.enabled = false;
        geometry.SternRegion.QueryCollider.enabled = false;
        BoxCollider targetHull =
            (BoxCollider)geometry.MidshipRegion.QueryCollider;
        targetHull.size = Vector3.one * 40f;
        Vector3 endpoint = GetExposureCenter(shooter, target);
        Physics.SyncTransforms();
        Assert.That(targetHull.bounds.Contains(endpoint), Is.True);

        Assert.That(CombatFireObstructionQuery.TryEvaluateTargeted(
            shooter, target, CombatSide.Starboard, endpoint, null,
            out CombatFireObstructionResult result), Is.True);
        Assert.That(result.IsBlocked, Is.False);
        Assert.That(result.BlockedRayCount, Is.Zero);
    }


    [Test]
    public void DifferentConfiguredTeamIds_AreHostile()
    {
        ShipCombatAffiliation source = CreateAffiliation(4, "Source");
        ShipCombatAffiliation target = CreateAffiliation(9, "Target");

        Assert.That(
            CombatRelationshipResolver.Resolve(source, target),
            Is.EqualTo(CombatRelationship.Hostile)
        );
    }


    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void MissingOrUnconfiguredAffiliation_IsUnknown(
        bool missingSource,
        bool missingTarget
    )
    {
        ShipCombatAffiliation source = missingSource
            ? null
            : CreateAffiliation(-1, "Source");
        ShipCombatAffiliation target = missingTarget
            ? null
            : CreateAffiliation(-1, "Target");

        Assert.That(
            CombatRelationshipResolver.Resolve(source, target),
            Is.EqualTo(CombatRelationship.Unknown)
        );
    }


    [Test]
    public void UnconfiguredSourceOrTarget_IsUnknown()
    {
        ShipCombatAffiliation configured = CreateAffiliation(3, "Configured");
        ShipCombatAffiliation unconfigured = CreateAffiliation(
            ShipCombatAffiliation.UnconfiguredTeamId,
            "Unconfigured"
        );

        Assert.That(
            CombatRelationshipResolver.Resolve(unconfigured, configured),
            Is.EqualTo(CombatRelationship.Unknown)
        );
        Assert.That(
            CombatRelationshipResolver.Resolve(configured, unconfigured),
            Is.EqualTo(CombatRelationship.Unknown)
        );
    }


    [Test]
    public void RelationshipResolution_DoesNotUseNamesTagsLayersOrRendering()
    {
        string resolverSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/CombatRelationshipResolver.cs"
        ));

        Assert.That(resolverSource, Does.Not.Contain(".name"));
        Assert.That(resolverSource, Does.Not.Contain(".tag"));
        Assert.That(resolverSource, Does.Not.Contain("CompareTag"));
        Assert.That(resolverSource, Does.Not.Contain("Renderer"));
        Assert.That(resolverSource, Does.Not.Contain("Material"));
        Assert.That(resolverSource, Does.Not.Contain("Color"));
        Assert.That(resolverSource, Does.Not.Contain("layer"));
    }


    [Test]
    public void FormalGelderland_IsExplicitlyUnconfigured()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        ShipCombatAffiliation[] affiliations = prefab
            .GetComponentsInChildren<ShipCombatAffiliation>(true);
        Assert.That(affiliations, Has.Length.EqualTo(1));
        Assert.That(affiliations[0].gameObject, Is.SameAs(prefab));
        Assert.That(
            affiliations[0].TeamId,
            Is.EqualTo(ShipCombatAffiliation.UnconfiguredTeamId)
        );
        Assert.That(affiliations[0].IsConfigured, Is.False);
    }


    [Test]
    public void GenericMovementProxies_HaveNoCombatAffiliation()
    {
        foreach (string path in GenericMovementPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(
                prefab.GetComponentsInChildren<ShipCombatAffiliation>(true),
                Is.Empty,
                path
            );
        }
    }


    [Test]
    public void GenericMovementProxies_RemainFreeOfCombatConfiguration()
    {
        Type[] forbiddenComponentTypes =
        {
            typeof(ShipCombatAffiliation),
            typeof(ShipCombatState),
            typeof(ShipFireEligibility),
            typeof(ShipAutoTargetScoringConfiguration),
            typeof(ShipDispersionConfiguration),
            typeof(ShipProjectileFlightConfiguration),
            typeof(ShipBroadsideFireExecutor),
            typeof(ShipIntegrity),
            typeof(ShipSinkingPresentation),
            typeof(CombatObstructionVolume)
        };

        foreach (string path in GenericMovementPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                path
            );
            Assert.That(prefab, Is.Not.Null, path);

            foreach (Type type in forbiddenComponentTypes)
            {
                Assert.That(
                    prefab.GetComponentsInChildren(type, true),
                    Is.Empty,
                    $"{path} contains {type.Name}."
                );
            }

            string[] dependencies = AssetDatabase.GetDependencies(
                path,
                true
            );
            Assert.That(
                dependencies,
                Does.Not.Contain(ProfilePath),
                $"{path} references the Combat obstruction profile."
            );
            Assert.That(
                dependencies,
                Does.Not.Contain(
                    "Assets/Assets/Game/Data/"
                        + "SO_CombatRaking_Foundation.asset"
                ),
                $"{path} references the Combat raking profile."
            );
        }
    }


    [Test]
    public void CombatObstructionLayer_IsDedicatedLayerNine()
    {
        Assert.That(
            LayerMask.NameToLayer(CombatObstructionVolume.LayerName),
            Is.EqualTo(CombatObstructionVolume.LayerIndex)
        );
        Assert.That(CombatObstructionVolume.LayerIndex, Is.EqualTo(9));
        Assert.That(
            CombatFireObstructionQuery.ShipCombatGeometryLayer,
            Is.EqualTo(8)
        );
        Assert.That(
            CombatFireObstructionQuery.WorldCombatObstructionLayer,
            Is.EqualTo(9)
        );
        Assert.That(
            CombatFireObstructionQuery.BlockingLayerMask,
            Is.EqualTo((1 << 8) | (1 << 9))
        );
    }


    [Test]
    public void DebugIsland_HasFrozenVisualAndGameplayHierarchy()
    {
        GameObject prefab = LoadIslandPrefab();
        Transform visualRoot = prefab.transform.Find("VisualRoot");
        Transform gameplayRoot = prefab.transform.Find("GameplayRoot");
        Transform cubeVisual = visualRoot?.Find("CubeVisual");
        Transform obstructionObject = gameplayRoot?.Find(
            "CombatObstructionVolume"
        );
        Transform navigationBlocker = gameplayRoot?.Find(
            "NavigationBlocker"
        );

        Assert.That(visualRoot, Is.Not.Null);
        Assert.That(gameplayRoot, Is.Not.Null);
        Assert.That(cubeVisual, Is.Not.Null);
        Assert.That(obstructionObject, Is.Not.Null);
        Assert.That(navigationBlocker, Is.Not.Null);
        Assert.That(cubeVisual.GetComponent<MeshFilter>(), Is.Not.Null);
        Assert.That(cubeVisual.GetComponent<MeshRenderer>(), Is.Not.Null);
        Assert.That(
            visualRoot.GetComponentsInChildren<Collider>(true),
            Is.Empty
        );
        Assert.That(
            visualRoot.GetComponentsInChildren<CombatObstructionVolume>(true),
            Is.Empty
        );
        Assert.That(
            navigationBlocker.GetComponents<Component>(),
            Has.Length.EqualTo(1),
            "NavigationBlocker reserves hierarchy only in Change 9.1."
        );
    }


    [Test]
    public void DebugIsland_UsesIndependentAuthoritativeObstructionVolume()
    {
        GameObject prefab = LoadIslandPrefab();
        CombatObstructionVolume volume = prefab
            .GetComponentInChildren<CombatObstructionVolume>(true);
        BoxCollider collider = volume?.QueryCollider as BoxCollider;

        Assert.That(volume, Is.Not.Null);
        Assert.That(collider, Is.Not.Null);
        Assert.That(volume.IsConfigured, Is.True);
        Assert.That(volume.gameObject.layer, Is.EqualTo(9));
        Assert.That(collider.isTrigger, Is.True);
        Assert.That(collider.size.y, Is.GreaterThan(10f));
        Assert.That(volume.GetComponent<Renderer>(), Is.Null);
        Assert.That(volume.GetComponent<MeshFilter>(), Is.Null);
        Assert.That(volume.GetComponent<ShipIntegrity>(), Is.Null);
        Assert.That(volume.GetComponent<CombatHitRegion>(), Is.Null);
        Assert.That(volume.GetComponent<ShipCombatGeometry>(), Is.Null);
    }


    [Test]
    public void ReplacingVisualRootContent_DoesNotChangeObstructionGeometry()
    {
        GameObject instance = UnityEngine.Object.Instantiate(
            LoadIslandPrefab()
        );
        temporaryObjects.Add(instance);
        Transform visualRoot = instance.transform.Find("VisualRoot");
        Transform cubeVisual = visualRoot.Find("CubeVisual");
        CombatObstructionVolume volume = instance
            .GetComponentInChildren<CombatObstructionVolume>(true);
        BoxCollider collider = (BoxCollider)volume.QueryCollider;
        Vector3 obstructionPosition = volume.transform.localPosition;
        Vector3 obstructionScale = volume.transform.localScale;
        Vector3 colliderCenter = collider.center;
        Vector3 colliderSize = collider.size;

        visualRoot.localScale = new Vector3(2f, 0.25f, 3f);
        UnityEngine.Object.DestroyImmediate(cubeVisual.gameObject);
        GameObject replacement = new GameObject("ReplacementVisual");
        replacement.transform.SetParent(visualRoot, false);

        Assert.That(volume, Is.Not.Null);
        Assert.That(
            volume.transform.localPosition,
            Is.EqualTo(obstructionPosition)
        );
        Assert.That(volume.transform.localScale, Is.EqualTo(obstructionScale));
        Assert.That(collider.center, Is.EqualTo(colliderCenter));
        Assert.That(collider.size, Is.EqualTo(colliderSize));
        Assert.That(volume.IsConfigured, Is.True);
    }


    [Test]
    public void ResizingGameplayVolume_RedefinesObstructionWithoutVisualChange()
    {
        GameObject instance = UnityEngine.Object.Instantiate(
            LoadIslandPrefab()
        );
        temporaryObjects.Add(instance);
        Transform visualRoot = instance.transform.Find("VisualRoot");
        CombatObstructionVolume volume = instance
            .GetComponentInChildren<CombatObstructionVolume>(true);
        BoxCollider collider = (BoxCollider)volume.QueryCollider;
        Vector3 visualScale = visualRoot.localScale;
        Vector3 originalSize = collider.size;

        collider.size = new Vector3(
            originalSize.x * 0.5f,
            originalSize.y * 2f,
            originalSize.z * 1.5f
        );
        Physics.SyncTransforms();

        Assert.That(collider.size, Is.Not.EqualTo(originalSize));
        Assert.That(collider.bounds.size.y,
            Is.EqualTo(originalSize.y * 2f).Within(0.001f));
        Assert.That(visualRoot.localScale, Is.EqualTo(visualScale));
        Assert.That(volume.IsConfigured, Is.True);
    }


    [Test]
    public void FoundationProfile_HasZeroPlaytestBoundTolerances()
    {
        CombatObstructionProfile profile =
            AssetDatabase.LoadAssetAtPath<CombatObstructionProfile>(
                ProfilePath
            );

        Assert.That(profile, Is.Not.Null);
        Assert.That(
            profile.TargetedAutoAllowedBlockedRayFraction,
            Is.Zero
        );
        Assert.That(profile.BlindFireFriendlyEdgeTolerance, Is.Zero);
    }


    [Test]
    public void QueryClassifiesWorldAndShipGeometryDistinctly()
    {
        CombatObstructionVolume volume = LoadIslandPrefab()
            .GetComponentInChildren<CombatObstructionVolume>(true);

        Assert.That(
            CombatFireObstructionQuery.TryClassifyBlockingCollider(
                volume.QueryCollider,
                out CombatFireBlockerKind worldKind
            ),
            Is.True
        );
        Assert.That(
            worldKind,
            Is.EqualTo(CombatFireBlockerKind.WorldObstacle)
        );

        GameObject shipRoot = new GameObject("Ship Root");
        temporaryObjects.Add(shipRoot);
        ShipCombatGeometry owner = shipRoot.AddComponent<ShipCombatGeometry>();
        GameObject regionObject = new GameObject("Region");
        regionObject.transform.SetParent(shipRoot.transform, false);
        regionObject.layer = CombatFireObstructionQuery
            .ShipCombatGeometryLayer;
        BoxCollider regionCollider = regionObject.AddComponent<BoxCollider>();
        regionCollider.isTrigger = true;
        CombatHitRegion region = regionObject.AddComponent<CombatHitRegion>();
        SetSerializedReference(region, "owner", owner);
        SetSerializedReference(region, "queryCollider", regionCollider);
        SetSerializedReference(owner, "bowRegion", region);

        Assert.That(
            CombatFireObstructionQuery.TryClassifyBlockingCollider(
                regionCollider,
                out CombatFireBlockerKind shipKind
            ),
            Is.True
        );
        Assert.That(shipKind, Is.EqualTo(CombatFireBlockerKind.Ship));
    }


    [Test]
    public void LayerAlone_DoesNotCreateCombatObstructionAuthority()
    {
        GameObject generic = new GameObject("Generic Collider");
        temporaryObjects.Add(generic);
        generic.layer = CombatObstructionVolume.LayerIndex;
        BoxCollider collider = generic.AddComponent<BoxCollider>();

        Assert.That(
            CombatFireObstructionQuery.TryClassifyBlockingCollider(
                collider,
                out CombatFireBlockerKind kind
            ),
            Is.False
        );
        Assert.That(kind, Is.EqualTo(CombatFireBlockerKind.None));
    }


    [Test]
    public void ObstructionResult_IsImmutableAndPreservesDiagnostics()
    {
        ConstructorInfo constructor = typeof(CombatFireObstructionResult)
            .GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            .Single();
        CombatFireObstructionResult result =
            (CombatFireObstructionResult)constructor.Invoke(new object[]
            {
                true,
                13,
                2,
                CombatFireBlockerKind.Ship,
                CombatRelationship.Unknown,
                "Unconfigured Blocker",
                null,
                Vector3.zero
            });

        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.ParticipatingRayCount, Is.EqualTo(13));
        Assert.That(result.BlockedRayCount, Is.EqualTo(2));
        Assert.That(result.BlockedFraction, Is.EqualTo(2f / 13f));
        Assert.That(
            result.RepresentativeBlockerKind,
            Is.EqualTo(CombatFireBlockerKind.Ship)
        );
        Assert.That(
            result.BlockerRelationship,
            Is.EqualTo(CombatRelationship.Unknown)
        );
        Assert.That(
            result.RepresentativeBlockerName,
            Is.EqualTo("Unconfigured Blocker")
        );
        Assert.That(
            typeof(CombatFireObstructionResult).GetProperties()
                .All(property => property.SetMethod == null),
            Is.True
        );
    }


    [Test]
    public void FoundationQuery_IsIntegratedOnlyIntoPreFireEligibility()
    {
        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string querySource = File.ReadAllText(Path.Combine(
            combatRoot,
            "CombatFireObstructionQuery.cs"
        ));

        Assert.That(querySource, Does.Not.Contain("Physics."));
        Assert.That(querySource, Does.Not.Contain("Renderer"));
        Assert.That(querySource, Does.Not.Contain("MeshFilter"));
        Assert.That(querySource, Does.Not.Contain("bounds"));
        Assert.That(querySource, Does.Contain("TryBuildNominalShots"));
        Assert.That(querySource, Does.Contain(
            "CombatProjectileTrajectory.EvaluatePosition"));
        Assert.That(querySource, Does.Contain(
            "CombatProjectileContactQuery"));
        Assert.That(querySource, Does.Not.Contain(
            "DeterministicDispersionSampler"));

        string eligibilitySource = File.ReadAllText(Path.Combine(
            combatRoot,
            "ShipFireEligibility.cs"
        ));
        Assert.That(
            eligibilitySource,
            Does.Contain("CombatFireObstructionQuery")
        );

        foreach (string fileName in new[]
        {
            "ShipTargetedFireCommand.cs",
            "ShipBlindFireCommand.cs",
            "CombatProjectile.cs",
            "CombatProjectileContactQuery.cs"
        })
        {
            string source = File.ReadAllText(Path.Combine(
                combatRoot,
                fileName
            ));
            Assert.That(
                source,
                Does.Not.Contain("CombatFireObstructionQuery"),
                fileName
            );
        }
    }


    [Test]
    public void UnknownAffiliation_DoesNotCreatePhysicalPassThroughFilter()
    {
        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );

        foreach (string fileName in new[]
        {
            "ShipFireEligibility.cs",
            "CombatPhysicsQuery.cs",
            "CombatProjectileContactQuery.cs"
        })
        {
            string source = File.ReadAllText(Path.Combine(
                combatRoot,
                fileName
            ));
            Assert.That(
                source,
                Does.Not.Contain("CombatRelationshipResolver"),
                fileName
            );
            Assert.That(
                source,
                Does.Not.Contain("ShipCombatAffiliation"),
                fileName
            );
        }
    }


    private BoxCollider CreateRegisteredWorldBlocker(
        Vector3 center, Vector3 size)
    {
        GameObject world = new GameObject("Registered World");
        temporaryObjects.Add(world);
        world.transform.position = center;
        world.layer = CombatObstructionVolume.LayerIndex;
        BoxCollider collider = world.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        CombatObstructionVolume volume =
            world.AddComponent<CombatObstructionVolume>();
        SetSerializedReference(volume, "queryCollider", collider);
        return collider;
    }


    private static void AssertAllNominalSweepsClearAbove(
        GameObject shooter, Vector3 endpoint, Collider blocker)
    {
        Assert.That(ShipBroadsideShotSampler.TryBuildNominalShots(
            shooter, CombatSide.Starboard, endpoint,
            out ShotSample[] shots), Is.True);
        Assert.That(shots, Has.Length.EqualTo(13));
        Bounds bounds = blocker.bounds;
        for (int index = 0; index < shots.Length; index++)
        {
            ShotSample shot = shots[index];
            bool crossesX = false;
            for (float t0 = 0f; t0 < shot.NominalFlightTimeSeconds;)
            {
                float t1 = Mathf.Min(t0 + 1f,
                    shot.NominalFlightTimeSeconds);
                Vector3 p0 = CombatProjectileTrajectory.EvaluatePosition(
                    shot, t0);
                Vector3 p1 = CombatProjectileTrajectory.EvaluatePosition(
                    shot, t1);
                if (p1.x >= bounds.min.x && p0.x <= bounds.max.x)
                {
                    crossesX = true;
                    float x0 = Mathf.Max(bounds.min.x, p0.x);
                    float x1 = Mathf.Min(bounds.max.x, p1.x);
                    float y0 = Mathf.LerpUnclamped(p0.y, p1.y,
                        (x0 - p0.x) / (p1.x - p0.x));
                    float y1 = Mathf.LerpUnclamped(p0.y, p1.y,
                        (x1 - p0.x) / (p1.x - p0.x));
                    Assert.That(Mathf.Min(y0, y1) - bounds.max.y,
                        Is.GreaterThanOrEqualTo(2f),
                        $"Muzzle {index} lacks clear vertical margin.");
                }

                t0 = t1;
            }

            Assert.That(crossesX, Is.True);
        }
    }


    private GameObject InstantiateCombatShip(Vector3 position, int teamId)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        temporaryObjects.Add(instance);
        instance.transform.position = position;
        ShipCombatAffiliation affiliation =
            instance.GetComponent<ShipCombatAffiliation>();
        Assert.That(affiliation, Is.Not.Null);
        SerializedObject serialized = new SerializedObject(affiliation);
        serialized.FindProperty("teamId").intValue = teamId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return instance;
    }


    private static Vector3 GetExposureCenter(
        GameObject shooter, GameObject target)
    {
        Assert.That(target.GetComponent<ShipExposureReference>()
            .TryCalculateExposure(shooter.transform.position,
                out ExposureRect exposure), Is.True);
        return exposure.CenterWorld;
    }


    private ShipCombatAffiliation CreateAffiliation(
        int teamId,
        string objectName
    )
    {
        GameObject gameObject = new GameObject(objectName);
        temporaryObjects.Add(gameObject);
        ShipCombatAffiliation affiliation =
            gameObject.AddComponent<ShipCombatAffiliation>();
        SerializedObject serialized = new SerializedObject(affiliation);
        serialized.FindProperty("teamId").intValue = teamId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return affiliation;
    }


    private static GameObject LoadIslandPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            IslandPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }


    private static void SetSerializedReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
