using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DispersionFoundationTests
{
    private const float Tolerance = 0.0001f;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipFireEligibility fireEligibility;
    private ShipArtDefinition targetArtDefinition;
    private DispersionProfile profile;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateShip("Shooter", false);
        targetRoot = CreateShip("Target", true);
        targetRoot.transform.position = Vector3.right * 100f;
        fireEligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        targetArtDefinition = targetRoot.GetComponent<ShipArtDefinition>();
        profile = ScriptableObject.CreateInstance<DispersionProfile>();
        ConfigureProfile(2f, 1f, 1f);
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(targetRoot);
        Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void Plane_UsesFrozenHorizontalVerticalNormalBasis()
    {
        Calculate(out DispersionRect rect, out _);
        DispersionPlane plane = rect.Plane;

        Assert.That(plane.NormalWorld.y, Is.Zero.Within(Tolerance));
        Assert.That(plane.NormalWorld.magnitude, Is.EqualTo(1f).Within(Tolerance));
        AssertVector(plane.VerticalAxisWorld, Vector3.up);
        Assert.That(
            Vector3.Dot(plane.HorizontalAxisWorld, plane.VerticalAxisWorld),
            Is.Zero.Within(Tolerance)
        );
        Assert.That(
            Vector3.Dot(plane.HorizontalAxisWorld, plane.NormalWorld),
            Is.Zero.Within(Tolerance)
        );
        Assert.That(
            Vector3.Dot(plane.VerticalAxisWorld, plane.NormalWorld),
            Is.Zero.Within(Tolerance)
        );
        Assert.That(
            Vector3.Dot(
                Vector3.Cross(
                    plane.HorizontalAxisWorld,
                    plane.VerticalAxisWorld
                ),
                plane.NormalWorld
            ),
            Is.EqualTo(1f).Within(Tolerance)
        );
    }


    [Test]
    public void SemiAxesAndRect_UseFrozenDistanceAngleScaleFormula()
    {
        FireAimBasis basis = BuildTargetedBasis();
        Calculate(basis, out DispersionRect rect, out DispersionEllipse ellipse);
        float expectedA = basis.AimDistanceMeters
            * Mathf.Tan(2f * Mathf.Deg2Rad);
        float expectedB = basis.AimDistanceMeters
            * Mathf.Tan(1f * Mathf.Deg2Rad);

        Assert.That(
            ellipse.HorizontalSemiAxisMeters,
            Is.EqualTo(expectedA).Within(Tolerance)
        );
        Assert.That(
            ellipse.VerticalSemiAxisMeters,
            Is.EqualTo(expectedB).Within(Tolerance)
        );
        Assert.That(
            rect.WidthMeters,
            Is.EqualTo(expectedA * 2f).Within(Tolerance)
        );
        Assert.That(
            rect.HeightMeters,
            Is.EqualTo(expectedB * 2f).Within(Tolerance)
        );
        Assert.That(
            rect.HorizontalSemiAxisMeters,
            Is.EqualTo(ellipse.HorizontalSemiAxisMeters).Within(Tolerance)
        );
        Assert.That(
            rect.VerticalSemiAxisMeters,
            Is.EqualTo(ellipse.VerticalSemiAxisMeters).Within(Tolerance)
        );
    }


    [Test]
    public void DoublingAuthoritativeDistance_DoublesBothSemiAxes()
    {
        Calculate(out _, out DispersionEllipse near);

        targetRoot.transform.position = Vector3.right * 200f;
        Calculate(out _, out DispersionEllipse far);

        Assert.That(
            far.HorizontalSemiAxisMeters,
            Is.EqualTo(near.HorizontalSemiAxisMeters * 2f).Within(Tolerance)
        );
        Assert.That(
            far.VerticalSemiAxisMeters,
            Is.EqualTo(near.VerticalSemiAxisMeters * 2f).Within(Tolerance)
        );
    }


    [Test]
    public void ExposureDimensions_DoNotChangeDispersionDimensions()
    {
        Calculate(out _, out DispersionEllipse before);

        targetArtDefinition.BowReference.localPosition = Vector3.forward * 40f;
        targetArtDefinition.SternReference.localPosition = Vector3.back * 35f;
        targetArtDefinition.PortReference.localPosition = Vector3.left * 12f;
        targetArtDefinition.StarboardReference.localPosition = Vector3.right * 9f;
        targetArtDefinition.DeckReference.localPosition = Vector3.up * 14f;
        Calculate(out _, out DispersionEllipse after);

        AssertSameSemiAxes(before, after);
    }


    [Test]
    public void TargetOrientationAtEqualDistance_DoesNotChangeDispersionDimensions()
    {
        targetRoot.transform.rotation = Quaternion.identity;
        Calculate(out _, out DispersionEllipse broadside);

        targetRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Calculate(out _, out DispersionEllipse bowOn);

        AssertSameSemiAxes(broadside, bowOn);
    }


    [Test]
    public void SpreadScale_ChangesOnlySemiAxesAndRectSize()
    {
        Calculate(out DispersionRect beforeRect, out DispersionEllipse before);

        ConfigureProfile(2f, 1f, 1.75f);
        Calculate(out DispersionRect afterRect, out DispersionEllipse after);

        Assert.That(
            after.HorizontalSemiAxisMeters,
            Is.EqualTo(before.HorizontalSemiAxisMeters * 1.75f)
                .Within(Tolerance)
        );
        Assert.That(
            after.VerticalSemiAxisMeters,
            Is.EqualTo(before.VerticalSemiAxisMeters * 1.75f)
                .Within(Tolerance)
        );
        AssertVector(afterRect.Plane.CenterWorld, beforeRect.Plane.CenterWorld);
        AssertVector(afterRect.Plane.NormalWorld, beforeRect.Plane.NormalWorld);
        AssertVector(
            afterRect.Plane.HorizontalAxisWorld,
            beforeRect.Plane.HorizontalAxisWorld
        );
        AssertVector(
            afterRect.Plane.VerticalAxisWorld,
            beforeRect.Plane.VerticalAxisWorld
        );
    }


    [Test]
    public void PureMapping_UsesSqrtRadiusFormulaExactly()
    {
        Calculate(out _, out DispersionEllipse ellipse);

        bool mapped = DeterministicDispersionSampler.TryMapUnitSquareSample(
            ellipse,
            0.25f,
            0.25f,
            out Vector3 point
        );
        Vector3 expected = ellipse.Plane.CenterWorld
            + ellipse.Plane.VerticalAxisWorld
                * ellipse.VerticalSemiAxisMeters
                * 0.5f;

        Assert.That(mapped, Is.True);
        AssertVector(point, expected);
    }


    [Test]
    public void PureMapping_AllKnownSamplesStayInsideEllipseBoundary()
    {
        Calculate(out _, out DispersionEllipse ellipse);
        float[] inputs = { 0f, 0.01f, 0.25f, 0.5f, 0.999999f };

        foreach (float u in inputs)
        {
            foreach (float v in inputs)
            {
                Assert.That(
                    DeterministicDispersionSampler.TryMapUnitSquareSample(
                        ellipse,
                        u,
                        v,
                        out Vector3 point
                    ),
                    Is.True
                );
                Vector3 offset = point - ellipse.Plane.CenterWorld;
                float x = Vector3.Dot(
                    offset,
                    ellipse.Plane.HorizontalAxisWorld
                ) / ellipse.HorizontalSemiAxisMeters;
                float y = Vector3.Dot(
                    offset,
                    ellipse.Plane.VerticalAxisWorld
                ) / ellipse.VerticalSemiAxisMeters;

                Assert.That(
                    x * x + y * y,
                    Is.LessThanOrEqualTo(1f + Tolerance)
                );
            }
        }
    }


    [Test]
    public void FixedSeed_ReproducesExactSampleSequenceIncludingZeroSeed()
    {
        Calculate(out _, out DispersionEllipse ellipse);
        DeterministicRandom32 first = new DeterministicRandom32(0u);
        DeterministicRandom32 second = new DeterministicRandom32(0u);
        DeterministicRandom32 defaultConstructed = default;

        for (int index = 0; index < 8; index++)
        {
            Assert.That(
                DeterministicDispersionSampler.TrySample(
                    ref first,
                    ellipse,
                    out Vector3 firstPoint
                ),
                Is.True
            );
            Assert.That(
                DeterministicDispersionSampler.TrySample(
                    ref second,
                    ellipse,
                    out Vector3 secondPoint
                ),
                Is.True
            );
            Assert.That(firstPoint, Is.EqualTo(secondPoint));
            Assert.That(first.State, Is.EqualTo(second.State));
            Assert.That(
                DeterministicDispersionSampler.TrySample(
                    ref defaultConstructed,
                    ellipse,
                    out Vector3 defaultPoint
                ),
                Is.True
            );
            Assert.That(defaultPoint, Is.EqualTo(firstPoint));
            Assert.That(defaultConstructed.State, Is.EqualTo(first.State));
        }
    }


    [Test]
    public void DifferentSeeds_ProduceDifferentSequence()
    {
        Calculate(out _, out DispersionEllipse ellipse);
        DeterministicRandom32 first = new DeterministicRandom32(123u);
        DeterministicRandom32 second = new DeterministicRandom32(456u);

        DeterministicDispersionSampler.TrySample(
            ref first,
            ellipse,
            out Vector3 firstPoint
        );
        DeterministicDispersionSampler.TrySample(
            ref second,
            ellipse,
            out Vector3 secondPoint
        );

        Assert.That(firstPoint, Is.Not.EqualTo(secondPoint));
        Assert.That(first.State, Is.Not.EqualTo(second.State));
    }


    [Test]
    public void SamplerSource_HasNoGlobalRandomDependency()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/DeterministicDispersionSampler.cs"
        );
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Not.Contain("UnityEngine.Random"));
        Assert.That(source, Does.Not.Contain("Random.Range"));
        Assert.That(source, Does.Contain("Mathf.Sqrt(u)"));
    }


    [Test]
    public void FoundationSources_DoNotWriteMovementOrOwnDownstreamExecution()
    {
        string combatPath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string combinedSource = File.ReadAllText(
            Path.Combine(combatPath, "ShipFireAimBasisBuilder.cs")
        ) + File.ReadAllText(
            Path.Combine(combatPath, "DispersionGeometry.cs")
        ) + File.ReadAllText(
            Path.Combine(combatPath, "DeterministicDispersionSampler.cs")
        );

        Assert.That(combinedSource, Does.Not.Contain("transform.position ="));
        Assert.That(combinedSource, Does.Not.Contain("transform.rotation ="));
        Assert.That(combinedSource, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(combinedSource, Does.Not.Contain("ShipTurning"));
        Assert.That(combinedSource, Does.Not.Contain(
            "ShipBroadsideShotSampler"
        ));
        Assert.That(combinedSource, Does.Not.Contain(
            "ShipBroadsideFireExecutor"
        ));
        Assert.That(combinedSource, Does.Not.Contain("CombatProjectile"));
        Assert.That(combinedSource, Does.Not.Contain(
            "TryCommitBroadsideFire"
        ));
    }


    private FireAimBasis BuildTargetedBasis()
    {
        Physics.SyncTransforms();
        Assert.That(
            fireEligibility.TryEvaluate(
                targetRoot,
                true,
                out FireEligibilityResult eligibility
            ),
            Is.True
        );
        Assert.That(eligibility.CanFire, Is.True);
        Assert.That(
            ShipFireAimBasisBuilder.TryBuildTargeted(
                shooterRoot,
                targetRoot,
                eligibility,
                out FireAimBasis basis,
                out FireAimBasisFailure failure
            ),
            Is.True
        );
        Assert.That(failure, Is.EqualTo(FireAimBasisFailure.None));
        return basis;
    }


    private void Calculate(
        out DispersionRect rect,
        out DispersionEllipse ellipse
    )
    {
        Calculate(BuildTargetedBasis(), out rect, out ellipse);
    }


    private void Calculate(
        FireAimBasis basis,
        out DispersionRect rect,
        out DispersionEllipse ellipse
    )
    {
        Assert.That(
            DispersionGeometry.TryCalculate(
                basis,
                profile,
                out rect,
                out ellipse
            ),
            Is.True
        );
    }


    private void ConfigureProfile(
        float horizontalDegrees,
        float verticalDegrees,
        float spreadScale
    )
    {
        SetPrivateField(
            profile,
            "horizontalHalfAngleDegrees",
            horizontalDegrees
        );
        SetPrivateField(
            profile,
            "verticalHalfAngleDegrees",
            verticalDegrees
        );
        SetPrivateField(profile, "foundationSpreadScale", spreadScale);
    }


    private static GameObject CreateShip(string name, bool includeExposure)
    {
        GameObject root = new GameObject(name);
        CombatLifecycleTestUtility.AddOperationalIntegrity(root);
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        SetArtReference(root, artDefinition, "centerReference", new Vector3(0f, 2f, 4f));
        SetArtReference(root, artDefinition, "waterlineReference", Vector3.zero);
        SetArtReference(root, artDefinition, "deckReference", Vector3.up * 5f);
        SetArtReference(root, artDefinition, "bowReference", Vector3.forward * 15f);
        SetArtReference(root, artDefinition, "sternReference", Vector3.back * 15f);
        SetArtReference(root, artDefinition, "portReference", Vector3.left * 5f);
        SetArtReference(root, artDefinition, "starboardReference", Vector3.right * 5f);
        root.AddComponent<ShipCombatGeometry>();

        if (includeExposure)
        {
            ShipExposureReference exposure =
                root.AddComponent<ShipExposureReference>();
            SetPrivateField(exposure, "shipArtDefinition", artDefinition);
        }

        return root;
    }


    private static void SetArtReference(
        GameObject root,
        ShipArtDefinition artDefinition,
        string fieldName,
        Vector3 localPosition
    )
    {
        GameObject reference = new GameObject(fieldName);
        reference.transform.SetParent(root.transform, false);
        reference.transform.localPosition = localPosition;
        SetPrivateField(artDefinition, fieldName, reference.transform);
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


    private static void AssertSameSemiAxes(
        DispersionEllipse expected,
        DispersionEllipse actual
    )
    {
        Assert.That(
            actual.HorizontalSemiAxisMeters,
            Is.EqualTo(expected.HorizontalSemiAxisMeters).Within(Tolerance)
        );
        Assert.That(
            actual.VerticalSemiAxisMeters,
            Is.EqualTo(expected.VerticalSemiAxisMeters).Within(Tolerance)
        );
    }


    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }
}
