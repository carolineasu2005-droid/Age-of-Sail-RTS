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
