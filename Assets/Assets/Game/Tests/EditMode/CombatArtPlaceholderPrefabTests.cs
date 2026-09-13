using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatArtPlaceholderPrefabTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const string MediumPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab";

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
    public void CombatPlaceholder_IsVariantOfMediumMovementProxy()
    {
        GameObject prefab = LoadCombatPrefab();
        string[] dependencies = AssetDatabase.GetDependencies(
            CombatPrefabPath,
            false
        );

        Assert.That(
            PrefabUtility.GetPrefabAssetType(prefab),
            Is.EqualTo(PrefabAssetType.Variant)
        );
        Assert.That(dependencies, Does.Contain(MediumPrefabPath));
        Assert.That(prefab.name, Is.EqualTo("PF_Ship_Gelderland_Combat_v01"));
    }


    [Test]
    public void CombatPlaceholder_AssignsAllPhaseOneReferences()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipArtDefinition definition = prefab.GetComponent<ShipArtDefinition>();
        Transform artReferences = prefab.transform.Find("ArtReferences");

        Assert.That(definition, Is.Not.Null);
        Assert.That(artReferences, Is.Not.Null);
        Assert.That(definition.VisualRoot, Is.SameAs(prefab.transform.Find("VisualRoot")));
        AssertReference(definition.WaterlineReference, artReferences, "Waterline");
        AssertReference(definition.CenterReference, artReferences, "Center");
        AssertReference(definition.BowReference, artReferences, "Bow");
        AssertReference(definition.SternReference, artReferences, "Stern");
        AssertReference(definition.PortReference, artReferences, "Port");
        AssertReference(definition.StarboardReference, artReferences, "Starboard");
        AssertReference(definition.DeckReference, artReferences, "Deck");
    }


    [Test]
    public void CombatPlaceholder_UsesOrderedPositiveRootLocalExtents()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipArtDefinition definition = prefab.GetComponent<ShipArtDefinition>();
        Vector3 bowLocal = prefab.transform.InverseTransformPoint(
            definition.BowReference.position
        );
        Vector3 sternLocal = prefab.transform.InverseTransformPoint(
            definition.SternReference.position
        );
        Vector3 portLocal = prefab.transform.InverseTransformPoint(
            definition.PortReference.position
        );
        Vector3 starboardLocal = prefab.transform.InverseTransformPoint(
            definition.StarboardReference.position
        );

        Assert.That(bowLocal.z, Is.GreaterThan(sternLocal.z));
        Assert.That(starboardLocal.x, Is.GreaterThan(portLocal.x));
        Assert.That(definition.TryGetLength(out float length), Is.True);
        Assert.That(definition.TryGetBeam(out float beam), Is.True);
        Assert.That(length, Is.GreaterThan(0f));
        Assert.That(beam, Is.GreaterThan(0f));
        Assert.That(length, Is.GreaterThan(beam));
    }


    [Test]
    public void CombatPlaceholder_DoesNotReuseHistoricalDebugPoints()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipArtDefinition definition = prefab.GetComponent<ShipArtDefinition>();

        Assert.That(
            definition.BowReference,
            Is.Not.SameAs(prefab.transform.Find("DebugRoot/BowPoint"))
        );
        Assert.That(
            definition.SternReference,
            Is.Not.SameAs(prefab.transform.Find("DebugRoot/SternPoint"))
        );
        Assert.That(
            definition.PortReference,
            Is.Not.SameAs(prefab.transform.Find("DebugRoot/PortPoint"))
        );
        Assert.That(
            definition.StarboardReference,
            Is.Not.SameAs(prefab.transform.Find("DebugRoot/StarboardPoint"))
        );
    }


    [Test]
    public void CombatPlaceholder_PreservesAuthoritativeMovementRoot()
    {
        GameObject prefab = LoadCombatPrefab();

        Assert.That(prefab.transform.parent, Is.Null);
        Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));

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
    public void CombatPlaceholder_DoesNotContainDeferredGeometryOrExposure()
    {
        GameObject prefab = LoadCombatPrefab();

        Assert.That(prefab.transform.Find("CombatGeometry"), Is.Null);
        Assert.That(prefab.transform.Find("Exposure"), Is.Null);
    }


    private static GameObject LoadCombatPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null, "Combat placeholder prefab was not found.");
        return prefab;
    }


    private static void AssertReference(
        Transform reference,
        Transform artReferences,
        string expectedName
    )
    {
        Assert.That(reference, Is.Not.Null, $"{expectedName} is not assigned.");
        Assert.That(reference.name, Is.EqualTo(expectedName));
        Assert.That(reference.parent, Is.SameAs(artReferences));
    }
}
