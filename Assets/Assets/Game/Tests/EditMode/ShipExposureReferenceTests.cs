using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipExposureReferenceTests
{
    private const float Tolerance = 0.0001f;

    private GameObject root;
    private ShipArtDefinition artDefinition;
    private ShipExposureReference exposureReference;
    private Transform visualRoot;


    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Ship Root");
        artDefinition = root.AddComponent<ShipArtDefinition>();
        exposureReference = root.AddComponent<ShipExposureReference>();
        SetExposureArtDefinition(artDefinition);

        visualRoot = CreateReference("VisualRoot", Vector3.zero);
        SetArtReference("visualRoot", visualRoot);
        SetArtReference(
            "waterlineReference",
            CreateReference("Waterline", new Vector3(0f, -1f, 0f))
        );
        SetArtReference(
            "centerReference",
            CreateReference("Center", new Vector3(2f, 7f, 3f))
        );
        SetArtReference(
            "bowReference",
            CreateReference("Bow", new Vector3(0f, 0f, 15f))
        );
        SetArtReference(
            "sternReference",
            CreateReference("Stern", new Vector3(0f, 0f, -15f))
        );
        SetArtReference(
            "portReference",
            CreateReference("Port", new Vector3(-5f, 0f, 0f))
        );
        SetArtReference(
            "starboardReference",
            CreateReference("Starboard", new Vector3(5f, 0f, 0f))
        );
        SetArtReference(
            "deckReference",
            CreateReference("Deck", new Vector3(0f, 3f, 0f))
        );
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }


    [Test]
    public void ReferenceDimensions_UseArtDefinitionSemanticAxes()
    {
        bool available = exposureReference.TryGetReferenceDimensions(
            out Vector3 dimensionsMeters
        );

        Assert.That(available, Is.True);
        AssertVector(dimensionsMeters, new Vector3(10f, 4f, 30f));
    }


    [Test]
    public void ReferenceCenter_UsesCenterHorizontalAndWaterlineDeckMidpoint()
    {
        bool available = exposureReference.TryGetReferenceCenter(
            out Vector3 centerWorld
        );

        Assert.That(available, Is.True);
        AssertVector(centerWorld, new Vector3(2f, 1f, 3f));
    }


    [Test]
    public void BowOnProjection_UsesBeam()
    {
        ExposureRect exposure = QueryFromRootDirection(Vector3.forward, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(10f).Within(Tolerance));
    }


    [Test]
    public void SternOnProjection_UsesBeam()
    {
        ExposureRect exposure = QueryFromRootDirection(Vector3.back, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(10f).Within(Tolerance));
    }


    [Test]
    public void PortBroadsideProjection_UsesLength()
    {
        ExposureRect exposure = QueryFromRootDirection(Vector3.left, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(30f).Within(Tolerance));
    }


    [Test]
    public void StarboardBroadsideProjection_UsesLength()
    {
        ExposureRect exposure = QueryFromRootDirection(Vector3.right, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(30f).Within(Tolerance));
    }


    [Test]
    public void IntermediateBearing_UsesContinuousBoxProjectionFormula()
    {
        Vector3 bearing = (Vector3.forward + Vector3.right).normalized;
        ExposureRect exposure = QueryFromRootDirection(bearing, 100f);
        float expectedWidth = Mathf.Abs(bearing.z) * 10f
            + Mathf.Abs(bearing.x) * 30f;

        Assert.That(
            exposure.WidthMeters,
            Is.EqualTo(expectedWidth).Within(Tolerance)
        );
        Assert.That(exposure.WidthMeters, Is.GreaterThan(10f));
        Assert.That(exposure.WidthMeters, Is.LessThan(30f));
    }


    [Test]
    public void NeighboringBearings_HaveNoThresholdJump()
    {
        float below = QueryAtBearingDegrees(44.9f).WidthMeters;
        float middle = QueryAtBearingDegrees(45f).WidthMeters;
        float above = QueryAtBearingDegrees(45.1f).WidthMeters;

        Assert.That(Mathf.Abs(middle - below), Is.LessThan(0.1f));
        Assert.That(Mathf.Abs(above - middle), Is.LessThan(0.1f));
    }


    [Test]
    public void ObserverDistance_DoesNotChangeExposureDimensions()
    {
        Vector3 bearing = new Vector3(0.4f, 0f, 0.9f).normalized;
        ExposureRect near = QueryFromRootDirection(bearing, 25f);
        ExposureRect far = QueryFromRootDirection(bearing, 2500f);

        Assert.That(far.WidthMeters, Is.EqualTo(near.WidthMeters).Within(Tolerance));
        Assert.That(far.HeightMeters, Is.EqualTo(near.HeightMeters).Within(Tolerance));
        Assert.That(
            far.AreaSquareMeters,
            Is.EqualTo(near.AreaSquareMeters).Within(Tolerance)
        );
    }


    [Test]
    public void ObserverElevation_DoesNotChangeHorizontalExposure()
    {
        Vector3 center = GetCenterWorld();
        Vector3 horizontalOffset = new Vector3(40f, 0f, 90f);

        Assert.That(
            exposureReference.TryCalculateExposure(
                center + horizontalOffset,
                out ExposureRect level
            ),
            Is.True
        );
        Assert.That(
            exposureReference.TryCalculateExposure(
                center + horizontalOffset + Vector3.up * 500f,
                out ExposureRect elevated
            ),
            Is.True
        );

        Assert.That(
            elevated.WidthMeters,
            Is.EqualTo(level.WidthMeters).Within(Tolerance)
        );
        Assert.That(
            elevated.HeightMeters,
            Is.EqualTo(level.HeightMeters).Within(Tolerance)
        );
    }


    [Test]
    public void TranslatingTargetAndObserverTogether_PreservesExposure()
    {
        Vector3 bearing = new Vector3(-0.7f, 0f, 0.3f).normalized;
        Vector3 observer = GetCenterWorld() + bearing * 80f;
        Assert.That(
            exposureReference.TryCalculateExposure(observer, out ExposureRect before),
            Is.True
        );

        Vector3 translation = new Vector3(120f, 19f, -47f);
        root.transform.position += translation;
        observer += translation;

        Assert.That(
            exposureReference.TryCalculateExposure(observer, out ExposureRect after),
            Is.True
        );
        Assert.That(after.WidthMeters, Is.EqualTo(before.WidthMeters).Within(Tolerance));
        Assert.That(after.HeightMeters, Is.EqualTo(before.HeightMeters).Within(Tolerance));
    }


    [Test]
    public void TargetYaw_ChangesWidthForFixedWorldBearing()
    {
        artDefinition.CenterReference.localPosition = new Vector3(0f, 7f, 0f);
        Vector3 observer = GetCenterWorld() + Vector3.forward * 100f;

        Assert.That(
            exposureReference.TryCalculateExposure(observer, out ExposureRect bowOn),
            Is.True
        );

        root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        Assert.That(
            exposureReference.TryCalculateExposure(observer, out ExposureRect broadside),
            Is.True
        );
        Assert.That(bowOn.WidthMeters, Is.EqualTo(10f).Within(Tolerance));
        Assert.That(broadside.WidthMeters, Is.EqualTo(30f).Within(Tolerance));
    }


    [Test]
    public void TargetYaw_DoesNotChangeProjectedHeight()
    {
        ExposureRect before = QueryFromRootDirection(Vector3.forward, 100f);

        root.transform.rotation = Quaternion.Euler(0f, 63f, 0f);
        ExposureRect after = QueryFromRootDirection(Vector3.forward, 100f);

        Assert.That(before.HeightMeters, Is.EqualTo(4f).Within(Tolerance));
        Assert.That(after.HeightMeters, Is.EqualTo(4f).Within(Tolerance));
    }


    [Test]
    public void TargetPitchAndRoll_DoNotChangeHorizontalBowOnProjection()
    {
        ExposureRect level = QueryFromRootDirection(Vector3.forward, 100f);

        root.transform.rotation = Quaternion.Euler(24f, 0f, -17f);
        ExposureRect tilted = QueryFromRootDirection(Vector3.forward, 100f);

        Assert.That(tilted.WidthMeters, Is.EqualTo(level.WidthMeters).Within(Tolerance));
        Assert.That(tilted.HeightMeters, Is.EqualTo(level.HeightMeters).Within(Tolerance));
    }


    [Test]
    public void RootPitchAndRoll_DoNotTiltExposureAxesOrChangeDimensions()
    {
        artDefinition.CenterReference.localPosition = new Vector3(0f, 7f, 0f);
        ExposureRect level = QueryFromRootDirection(Vector3.forward, 100f);

        root.transform.rotation = Quaternion.Euler(21f, 37f, -16f);
        ExposureRect tilted = QueryFromRootDirection(Vector3.forward, 100f);

        Assert.That(
            tilted.WidthMeters,
            Is.EqualTo(level.WidthMeters).Within(Tolerance)
        );
        Assert.That(
            tilted.HeightMeters,
            Is.EqualTo(level.HeightMeters).Within(Tolerance)
        );
        AssertVector(tilted.VerticalAxisWorld, Vector3.up);
        Assert.That(tilted.PlaneNormalWorld.y, Is.Zero.Within(Tolerance));
        Assert.That(tilted.HorizontalAxisWorld.y, Is.Zero.Within(Tolerance));
    }


    [Test]
    public void ExposureAxes_UseHorizontalSightPlaneAndWorldUp()
    {
        ExposureRect exposure = QueryFromRootDirection(
            new Vector3(2f, 5f, 3f),
            100f
        );

        AssertVector(exposure.VerticalAxisWorld, Vector3.up);
        Assert.That(exposure.PlaneNormalWorld.y, Is.Zero.Within(Tolerance));
        Assert.That(exposure.HorizontalAxisWorld.y, Is.Zero.Within(Tolerance));
        Assert.That(exposure.PlaneNormalWorld.magnitude, Is.EqualTo(1f).Within(Tolerance));
        Assert.That(exposure.HorizontalAxisWorld.magnitude, Is.EqualTo(1f).Within(Tolerance));
        Assert.That(
            Vector3.Dot(
                exposure.PlaneNormalWorld,
                exposure.HorizontalAxisWorld
            ),
            Is.Zero.Within(Tolerance)
        );
    }


    [Test]
    public void Calculation_RequiresNoRendererOrMesh()
    {
        Assert.That(root.GetComponentInChildren<Renderer>(), Is.Null);
        Assert.That(root.GetComponentInChildren<MeshFilter>(), Is.Null);

        ExposureRect exposure = QueryFromRootDirection(Vector3.forward, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(10f).Within(Tolerance));
    }


    [Test]
    public void Calculation_RequiresNoCombatGeometryOrCollider()
    {
        Assert.That(root.GetComponent<ShipCombatGeometry>(), Is.Null);
        Assert.That(root.GetComponentInChildren<Collider>(), Is.Null);

        ExposureRect exposure = QueryFromRootDirection(Vector3.right, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(30f).Within(Tolerance));
    }


    [Test]
    public void HorizontallyCoincidentObserver_FailsSafely()
    {
        Vector3 observer = GetCenterWorld() + Vector3.up * 200f;

        bool available = exposureReference.TryCalculateExposure(
            observer,
            out ExposureRect exposure
        );

        Assert.That(available, Is.False);
        Assert.That(exposure, Is.EqualTo(default(ExposureRect)));
    }


    [Test]
    public void MissingArtDefinition_FailsSafely()
    {
        SetExposureArtDefinition(null);

        bool available = exposureReference.TryCalculateExposure(
            root.transform.position + Vector3.forward * 100f,
            out ExposureRect exposure
        );

        Assert.That(available, Is.False);
        Assert.That(exposure, Is.EqualTo(default(ExposureRect)));
    }


    [TestCase("centerReference")]
    [TestCase("bowReference")]
    [TestCase("sternReference")]
    [TestCase("portReference")]
    [TestCase("starboardReference")]
    [TestCase("waterlineReference")]
    [TestCase("deckReference")]
    public void MissingRequiredArtReference_FailsSafely(string fieldName)
    {
        SetArtReference(fieldName, null);

        bool available = exposureReference.TryCalculateExposure(
            root.transform.position + Vector3.forward * 100f,
            out ExposureRect exposure
        );

        Assert.That(available, Is.False);
        Assert.That(exposure, Is.EqualTo(default(ExposureRect)));
    }


    [Test]
    public void ZeroDimension_FailsSafely()
    {
        artDefinition.BowReference.localPosition =
            artDefinition.SternReference.localPosition;

        bool available = exposureReference.TryCalculateExposure(
            root.transform.position + Vector3.forward * 100f,
            out ExposureRect exposure
        );

        Assert.That(available, Is.False);
        Assert.That(exposure, Is.EqualTo(default(ExposureRect)));
    }


    [Test]
    public void SequentialObservers_DoNotMutateOrCorruptResults()
    {
        ExposureRect bowOn = QueryFromRootDirection(Vector3.forward, 100f);
        ExposureRect broadside = QueryFromRootDirection(Vector3.right, 100f);
        ExposureRect bowOnAgain = QueryFromRootDirection(Vector3.forward, 100f);

        Assert.That(bowOn.WidthMeters, Is.EqualTo(10f).Within(Tolerance));
        Assert.That(broadside.WidthMeters, Is.EqualTo(30f).Within(Tolerance));
        Assert.That(
            bowOnAgain.WidthMeters,
            Is.EqualTo(bowOn.WidthMeters).Within(Tolerance)
        );
    }


    [Test]
    public void VisualRootRotation_DoesNotChangeExposure()
    {
        Vector3 bearing = new Vector3(0.6f, 0f, 0.8f).normalized;
        ExposureRect before = QueryFromRootDirection(bearing, 100f);

        visualRoot.localRotation = Quaternion.Euler(24f, 137f, -18f);
        ExposureRect after = QueryFromRootDirection(bearing, 100f);

        Assert.That(after.WidthMeters, Is.EqualTo(before.WidthMeters).Within(Tolerance));
        Assert.That(after.HeightMeters, Is.EqualTo(before.HeightMeters).Within(Tolerance));
        AssertVector(after.CenterWorld, before.CenterWorld);
    }


    [Test]
    public void Query_DoesNotWriteTargetOrReferenceTransforms()
    {
        root.transform.SetPositionAndRotation(
            new Vector3(31f, 2f, -74f),
            Quaternion.Euler(0f, 28f, 0f)
        );
        Vector3 rootPosition = root.transform.position;
        Quaternion rootRotation = root.transform.rotation;
        Vector3 centerPosition = artDefinition.CenterReference.position;

        QueryFromRootDirection(new Vector3(0.2f, 0f, 0.9f), 100f);

        AssertVector(root.transform.position, rootPosition);
        Assert.That(root.transform.rotation, Is.EqualTo(rootRotation));
        AssertVector(artDefinition.CenterReference.position, centerPosition);
    }


    [Test]
    public void Contract_IsReadOnlyAndStoresNoPerObserverState()
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
        Assert.That(exposureReference.ArtDefinition, Is.SameAs(artDefinition));

        foreach (PropertyInfo property in typeof(ExposureRect).GetProperties())
        {
            Assert.That(property.GetMethod, Is.Not.Null);
            Assert.That(property.SetMethod, Is.Null, property.Name);
        }
    }


    private ExposureRect QueryAtBearingDegrees(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        Vector3 bearing = new Vector3(
            Mathf.Sin(radians),
            0f,
            Mathf.Cos(radians)
        );
        return QueryFromRootDirection(bearing, 100f);
    }


    private ExposureRect QueryFromRootDirection(
        Vector3 rootLocalBearing,
        float distance
    )
    {
        Vector3 horizontalBearing = root.transform.TransformDirection(
            rootLocalBearing
        );
        horizontalBearing.y = 0f;
        horizontalBearing.Normalize();
        Vector3 observerWorldPosition = GetCenterWorld()
            + horizontalBearing * distance;

        bool available = exposureReference.TryCalculateExposure(
            observerWorldPosition,
            out ExposureRect exposure
        );

        Assert.That(available, Is.True);
        return exposure;
    }


    private Vector3 GetCenterWorld()
    {
        Assert.That(
            exposureReference.TryGetReferenceCenter(out Vector3 centerWorld),
            Is.True
        );
        return centerWorld;
    }


    private Transform CreateReference(string name, Vector3 localPosition)
    {
        GameObject referenceObject = new GameObject(name);
        referenceObject.transform.SetParent(root.transform, false);
        referenceObject.transform.localPosition = localPosition;
        return referenceObject.transform;
    }


    private void SetExposureArtDefinition(ShipArtDefinition value)
    {
        FieldInfo field = typeof(ShipExposureReference).GetField(
            "shipArtDefinition",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null);
        field.SetValue(exposureReference, value);
    }


    private void SetArtReference(string fieldName, Transform value)
    {
        FieldInfo field = typeof(ShipArtDefinition).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(artDefinition, value);
    }


    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }
}
