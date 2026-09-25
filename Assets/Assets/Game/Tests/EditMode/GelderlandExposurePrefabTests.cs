using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GelderlandExposurePrefabTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const int CombatGeometryLayer = 8;

    private static readonly Type[] RequiredMovementComponents =
    {
        typeof(ShipMovementProfileController),
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipLeeway),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController)
    };


    [Test]
    public void ExposureReference_IsWiredToArtDefinitionOnAuthoritativeRoot()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipArtDefinition artDefinition = prefab.GetComponent<ShipArtDefinition>();
        ShipExposureReference exposureReference =
            prefab.GetComponent<ShipExposureReference>();

        Assert.That(artDefinition, Is.Not.Null);
        Assert.That(exposureReference, Is.Not.Null);
        Assert.That(exposureReference.transform, Is.SameAs(prefab.transform));
        Assert.That(exposureReference.ArtDefinition, Is.SameAs(artDefinition));
        Assert.That(prefab.transform.parent, Is.Null);
    }


    [Test]
    public void ExposureReference_UsesProvisionalArtReferenceDimensionsAndCenter()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipExposureReference exposureReference =
            prefab.GetComponent<ShipExposureReference>();

        Assert.That(
            exposureReference.TryGetReferenceDimensions(
                out Vector3 dimensionsMeters
            ),
            Is.True
        );
        Assert.That(dimensionsMeters.x, Is.EqualTo(11f).Within(0.0001f));
        Assert.That(dimensionsMeters.y, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(dimensionsMeters.z, Is.EqualTo(45f).Within(0.0001f));
        Assert.That(
            exposureReference.TryGetReferenceCenter(out Vector3 centerWorld),
            Is.True
        );
        Assert.That(
            Vector3.Distance(
                centerWorld,
                prefab.transform.TransformPoint(new Vector3(0f, 1f, 0f))
            ),
            Is.LessThan(0.0001f)
        );
    }


    [Test]
    public void ExposureIntegration_AddsNoRuntimeGeometryOrRigidbody()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipExposureReference exposureReference =
            prefab.GetComponent<ShipExposureReference>();

        Assert.That(prefab.transform.Find("Exposure"), Is.Null);
        Assert.That(exposureReference.GetComponents<Collider>(), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
    }


    [Test]
    public void ExposureIntegration_PreservesArtSocketsGeometryAndMovement()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipArtDefinition artDefinition = prefab.GetComponent<ShipArtDefinition>();
        ShipMuzzleSockets muzzleSockets = prefab.GetComponent<ShipMuzzleSockets>();
        ShipCombatGeometry combatGeometry = prefab.GetComponent<ShipCombatGeometry>();

        Assert.That(prefab.transform.Find("ArtReferences"), Is.Not.Null);
        Assert.That(artDefinition, Is.Not.Null);
        Assert.That(artDefinition.WaterlineReference, Is.Not.Null);
        Assert.That(artDefinition.CenterReference, Is.Not.Null);
        Assert.That(artDefinition.BowReference, Is.Not.Null);
        Assert.That(artDefinition.SternReference, Is.Not.Null);
        Assert.That(artDefinition.PortReference, Is.Not.Null);
        Assert.That(artDefinition.StarboardReference, Is.Not.Null);
        Assert.That(artDefinition.DeckReference, Is.Not.Null);
        Assert.That(muzzleSockets, Is.Not.Null);
        Assert.That(muzzleSockets.PortMuzzles, Has.Count.EqualTo(13));
        Assert.That(muzzleSockets.StarboardMuzzles, Has.Count.EqualTo(13));
        Assert.That(combatGeometry, Is.Not.Null);
        Assert.That(combatGeometry.BowRegion, Is.Not.Null);
        Assert.That(combatGeometry.MidshipRegion, Is.Not.Null);
        Assert.That(combatGeometry.SternRegion, Is.Not.Null);

        foreach (Type componentType in RequiredMovementComponents)
        {
            Assert.That(
                prefab.GetComponent(componentType),
                Is.Not.Null,
                $"Movement Root is missing {componentType.Name}."
            );
        }
    }


    [Test]
    public void GenericShipCollider_RemainsNonExposurePhysicalCollision()
    {
        GameObject prefab = LoadCombatPrefab();
        Transform shipColliderTransform = prefab.transform.Find(
            "CollisionRoot/ShipCollider"
        );
        Collider shipCollider = shipColliderTransform != null
            ? shipColliderTransform.GetComponent<Collider>()
            : null;

        Assert.That(shipColliderTransform, Is.Not.Null);
        Assert.That(shipCollider, Is.Not.Null);
        Assert.That(shipCollider.isTrigger, Is.False);
        Assert.That(shipCollider.gameObject.layer, Is.Not.EqualTo(CombatGeometryLayer));
        Assert.That(shipCollider.GetComponent<ShipExposureReference>(), Is.Null);
    }


    [Test]
    public void ExposureReference_OwnsOnlyPassiveArtDefinitionReference()
    {
        FieldInfo[] fields = typeof(ShipExposureReference).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly
        );

        Assert.That(fields, Has.Length.EqualTo(1));
        Assert.That(fields[0].Name, Is.EqualTo("shipArtDefinition"));
        Assert.That(fields[0].FieldType, Is.EqualTo(typeof(ShipArtDefinition)));

        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/CombatArt/ShipExposureReference.cs"
        );
        string source = File.ReadAllText(sourcePath);
        string[] forbiddenDependencies =
        {
            "ShipAutoTargetScorer",
            "ShipAutoTargetSelector",
            "ShipAutoTargetScoringConfiguration",
            "ShipCombatState",
            "ShipFireEligibility",
            "CombatGeometry",
            "Dispersion",
            "Projectile",
            "Rigidbody",
            "Collider",
            "Renderer",
            "Mesh"
        };

        foreach (string dependency in forbiddenDependencies)
        {
            Assert.That(source, Does.Not.Contain(dependency));
        }
    }


    [Test]
    public void GenericMovementProxies_DoNotContainExposureIntegration()
    {
        string[] paths =
        {
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
        };

        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Missing generic proxy {path}.");
            Assert.That(prefab.GetComponent<ShipExposureReference>(), Is.Null);
            Assert.That(prefab.transform.Find("Exposure"), Is.Null);
        }
    }


    private static GameObject LoadCombatPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null, "Combat placeholder prefab was not found.");
        return prefab;
    }
}
