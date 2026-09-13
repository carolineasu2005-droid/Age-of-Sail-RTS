using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipCombatGeometryTests
{
    private GameObject root;
    private ShipCombatGeometry geometry;
    private Transform mainHullRoot;
    private CombatHitRegion bowRegion;
    private CombatHitRegion midshipRegion;
    private CombatHitRegion sternRegion;


    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Ship Root");
        geometry = root.AddComponent<ShipCombatGeometry>();

        GameObject mainHullObject = new GameObject("MainHull");
        mainHullObject.transform.SetParent(root.transform, false);
        mainHullRoot = mainHullObject.transform;

        bowRegion = CreateRegion("Bow", CombatHullRegion.Bow);
        midshipRegion = CreateRegion("Midship", CombatHullRegion.Midship);
        sternRegion = CreateRegion("Stern", CombatHullRegion.Stern);

        SetField(geometry, "mainHullRoot", mainHullRoot);
        SetField(geometry, "bowRegion", bowRegion);
        SetField(geometry, "midshipRegion", midshipRegion);
        SetField(geometry, "sternRegion", sternRegion);
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }


    [Test]
    public void Regions_ReportAuthoredSemanticIdentity()
    {
        Assert.That(bowRegion.Region, Is.EqualTo(CombatHullRegion.Bow));
        Assert.That(
            midshipRegion.Region,
            Is.EqualTo(CombatHullRegion.Midship)
        );
        Assert.That(sternRegion.Region, Is.EqualTo(CombatHullRegion.Stern));
    }


    [TestCase(CombatHullRegion.Bow)]
    [TestCase(CombatHullRegion.Midship)]
    [TestCase(CombatHullRegion.Stern)]
    public void ConfiguredCollider_ResolvesToItsRegion(
        CombatHullRegion expectedRegion
    )
    {
        CombatHitRegion expected = GetRegion(expectedRegion);

        bool resolved = geometry.TryResolveRegion(
            expected.QueryCollider,
            out CombatHitRegion actual
        );

        Assert.That(resolved, Is.True);
        Assert.That(actual, Is.SameAs(expected));
        Assert.That(actual.Region, Is.EqualTo(expectedRegion));
    }


    [Test]
    public void UnrelatedCollider_FailsWithoutInventingRegion()
    {
        GameObject unrelatedObject = new GameObject("Unrelated");
        unrelatedObject.transform.SetParent(root.transform, false);
        Collider unrelatedCollider = unrelatedObject.AddComponent<BoxCollider>();

        bool resolved = geometry.TryResolveRegion(
            unrelatedCollider,
            out CombatHitRegion region
        );

        Assert.That(resolved, Is.False);
        Assert.That(region, Is.Null);
    }


    [Test]
    public void NullCollider_FailsSafely()
    {
        bool resolved = geometry.TryResolveRegion(
            null,
            out CombatHitRegion region
        );

        Assert.That(resolved, Is.False);
        Assert.That(region, Is.Null);
    }


    [Test]
    public void ConfiguredRegions_ResolveToOwningShipCombatGeometry()
    {
        CombatHitRegion[] regions = { bowRegion, midshipRegion, sternRegion };

        foreach (CombatHitRegion expected in regions)
        {
            bool resolved = geometry.TryResolveRegion(
                expected.QueryCollider,
                out CombatHitRegion actual
            );

            Assert.That(resolved, Is.True);
            Assert.That(actual.Owner, Is.SameAs(geometry));
        }
    }


    [Test]
    public void MissingRegion_DoesNotBreakOtherResolutionOrThrow()
    {
        SetField<CombatHitRegion>(geometry, "bowRegion", null);

        Assert.DoesNotThrow(() =>
            geometry.TryResolveRegion(
                bowRegion.QueryCollider,
                out CombatHitRegion _
            )
        );
        Assert.That(
            geometry.TryResolveRegion(
                bowRegion.QueryCollider,
                out CombatHitRegion missing
            ),
            Is.False
        );
        Assert.That(missing, Is.Null);
        Assert.That(
            geometry.TryResolveRegion(
                midshipRegion.QueryCollider,
                out CombatHitRegion available
            ),
            Is.True
        );
        Assert.That(available, Is.SameAs(midshipRegion));
    }


    [Test]
    public void Resolution_DoesNotDependOnGameObjectNames()
    {
        root.name = "Arbitrary Owner";
        mainHullRoot.name = "Arbitrary Group";
        bowRegion.name = "Not Bow";
        midshipRegion.name = "Not Midship";
        sternRegion.name = "Not Stern";

        Assert.That(
            geometry.TryResolveRegion(
                midshipRegion.QueryCollider,
                out CombatHitRegion resolved
            ),
            Is.True
        );
        Assert.That(resolved.Region, Is.EqualTo(CombatHullRegion.Midship));
    }


    [Test]
    public void Resolution_DoesNotRequireRenderComponents()
    {
        Assert.That(root.GetComponentInChildren<Renderer>(), Is.Null);
        Assert.That(root.GetComponentInChildren<MeshFilter>(), Is.Null);
        Assert.That(
            geometry.TryResolveRegion(
                sternRegion.QueryCollider,
                out CombatHitRegion resolved
            ),
            Is.True
        );
        Assert.That(resolved, Is.SameAs(sternRegion));
    }


    [Test]
    public void Resolution_DoesNotRequireMovementComponents()
    {
        Assert.That(root.GetComponent("ShipSailingSpeed"), Is.Null);
        Assert.That(root.GetComponent("ShipTurning"), Is.Null);
        Assert.That(root.GetComponent("ShipHeadingController"), Is.Null);
        Assert.That(root.GetComponent("ShipDestinationController"), Is.Null);
        Assert.That(
            geometry.TryResolveRegion(
                bowRegion.QueryCollider,
                out CombatHitRegion resolved
            ),
            Is.True
        );
        Assert.That(resolved, Is.SameAs(bowRegion));
    }


    [Test]
    public void MainHull_IsLogicalGroupingAndNeedsNoCollider()
    {
        Assert.That(mainHullRoot.GetComponent<Collider>(), Is.Null);
        Assert.That(geometry.MainHullRoot, Is.SameAs(mainHullRoot));
        Assert.That(
            geometry.TryResolveRegion(
                bowRegion.QueryCollider,
                out CombatHitRegion resolved
            ),
            Is.True
        );
        Assert.That(resolved, Is.SameAs(bowRegion));
    }


    [Test]
    public void AuthoredReferences_ArePubliclyReadOnly()
    {
        AssertGetterOnly<ShipCombatGeometry, Transform>("MainHullRoot");
        AssertGetterOnly<ShipCombatGeometry, CombatHitRegion>("BowRegion");
        AssertGetterOnly<ShipCombatGeometry, CombatHitRegion>("MidshipRegion");
        AssertGetterOnly<ShipCombatGeometry, CombatHitRegion>("SternRegion");
        AssertGetterOnly<CombatHitRegion, CombatHullRegion>("Region");
        AssertGetterOnly<CombatHitRegion, ShipCombatGeometry>("Owner");
        AssertGetterOnly<CombatHitRegion, Collider>("QueryCollider");
    }


    private CombatHitRegion CreateRegion(
        string name,
        CombatHullRegion regionIdentity
    )
    {
        GameObject regionObject = new GameObject(name);
        regionObject.transform.SetParent(mainHullRoot, false);
        Collider collider = regionObject.AddComponent<BoxCollider>();
        CombatHitRegion region = regionObject.AddComponent<CombatHitRegion>();
        SetField(region, "region", regionIdentity);
        SetField(region, "owner", geometry);
        SetField(region, "queryCollider", collider);
        return region;
    }


    private CombatHitRegion GetRegion(CombatHullRegion region)
    {
        switch (region)
        {
            case CombatHullRegion.Bow:
                return bowRegion;
            case CombatHullRegion.Midship:
                return midshipRegion;
            case CombatHullRegion.Stern:
                return sternRegion;
            default:
                Assert.Fail($"Unexpected Combat Hull region {region}.");
                return null;
        }
    }


    private static void AssertGetterOnly<TComponent, TValue>(
        string propertyName
    )
    {
        PropertyInfo property = typeof(TComponent).GetProperty(propertyName);

        Assert.That(property, Is.Not.Null, $"Missing property {propertyName}.");
        Assert.That(property.PropertyType, Is.EqualTo(typeof(TValue)));
        Assert.That(property.GetMethod, Is.Not.Null);
        Assert.That(property.GetMethod.IsPublic, Is.True);
        Assert.That(
            property.SetMethod,
            Is.Null,
            $"{propertyName} must be read-only."
        );
    }


    private static void SetField<TValue>(
        object target,
        string fieldName,
        TValue value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
