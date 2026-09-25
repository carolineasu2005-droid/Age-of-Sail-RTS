using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FireAimBasisTests
{
    private const float Tolerance = 0.0001f;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipCombatState shooterState;
    private ShipFireEligibility fireEligibility;
    private ShipExposureReference targetExposure;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateShip("Shooter", false);
        targetRoot = CreateShip("Target", true);
        targetRoot.transform.position = Vector3.right * 100f;
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
        fireEligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        targetExposure = targetRoot.GetComponent<ShipExposureReference>();
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(targetRoot);
        UnityEngine.Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void TargetedBasis_UsesExposureCenterEligibilityDistanceAndSide()
    {
        FireEligibilityResult eligibility = EvaluateTargetedEligibility();
        Assert.That(
            targetExposure.TryCalculateExposure(
                shooterRoot.transform.position,
                out ExposureRect exposure
            ),
            Is.True
        );

        FireAimBasis basis = BuildTargeted(eligibility);

        Assert.That(basis.SourceKind, Is.EqualTo(FireAimSourceKind.Targeted));
        AssertVector(basis.AimPlaneCenterWorld, exposure.CenterWorld);
        Assert.That(
            Mathf.Abs(
                Vector3.Distance(
                    shooterRoot.transform.position,
                    exposure.CenterWorld
                ) - eligibility.DistanceMeters
            ),
            Is.GreaterThan(0.01f),
            "The fixture must distinguish eligibility distance from aim-center distance."
        );
        Assert.That(
            basis.AimDistanceMeters,
            Is.EqualTo(eligibility.DistanceMeters).Within(Tolerance)
        );
        Assert.That(basis.Side, Is.EqualTo(eligibility.Side.Value));
    }


    [Test]
    public void TargetedBasis_DirectionIsFiniteHorizontalAndNormalized()
    {
        FireAimBasis basis = BuildTargeted(EvaluateTargetedEligibility());

        Assert.That(float.IsNaN(basis.AimDirectionWorld.x), Is.False);
        Assert.That(float.IsInfinity(basis.AimDirectionWorld.x), Is.False);
        Assert.That(basis.AimDirectionWorld.y, Is.Zero.Within(Tolerance));
        Assert.That(
            basis.AimDirectionWorld.magnitude,
            Is.EqualTo(1f).Within(Tolerance)
        );
    }


    [Test]
    public void Basis_IsImmutableAndRetainsNoTargetOrExposureValue()
    {
        Type type = typeof(FireAimBasis);
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly
        );

        Assert.That(type.IsValueType, Is.True);
        Assert.That(fields.All(field => field.IsInitOnly), Is.True);
        Assert.That(
            type.GetProperties().All(property => property.SetMethod == null),
            Is.True
        );
        CollectionAssert.AreEquivalent(
            new[]
            {
                nameof(FireAimBasis.Side),
                nameof(FireAimBasis.SourceKind),
                nameof(FireAimBasis.AimPlaneCenterWorld),
                nameof(FireAimBasis.AimDirectionWorld),
                nameof(FireAimBasis.AimDistanceMeters)
            },
            type.GetProperties().Select(property => property.Name)
        );
        Assert.That(
            fields.Any(field =>
                typeof(Transform).IsAssignableFrom(field.FieldType)
                || typeof(GameObject).IsAssignableFrom(field.FieldType)
                || field.FieldType == typeof(ExposureRect)),
            Is.False
        );
    }


    [Test]
    public void PointBlindFire_BuildsFiniteAuthoritativeBasis()
    {
        Vector3 aimPoint = new Vector3(80f, 12f, 10f);
        BlindFireEligibilityResult eligibility =
            EvaluateBlindFirePoint(aimPoint);

        bool built = ShipFireAimBasisBuilder.TryBuildBlindFirePoint(
            eligibility,
            out FireAimBasis basis,
            out FireAimBasisFailure failure
        );

        Assert.That(eligibility.CanBlindFire, Is.True);
        Assert.That(built, Is.True);
        Assert.That(failure, Is.EqualTo(FireAimBasisFailure.None));
        Assert.That(
            basis.SourceKind,
            Is.EqualTo(FireAimSourceKind.BlindFirePoint)
        );
        Assert.That(basis.Side, Is.EqualTo(eligibility.Side.Value));
        AssertVector(basis.AimPlaneCenterWorld, aimPoint);
        Assert.That(
            basis.AimDistanceMeters,
            Is.EqualTo(eligibility.Aim.AimPointDistanceMeters.Value)
                .Within(Tolerance)
        );
        AssertVector(
            basis.AimDirectionWorld,
            eligibility.Aim.WorldAimDirection
        );
    }


    [Test]
    public void DirectionOnlyBlindFire_PhysicalBasisRequiresFiniteAimPoint()
    {
        BlindFireEligibilityResult eligibility =
            EvaluateBlindFireDirection(Vector3.right);

        bool built = ShipFireAimBasisBuilder.TryBuildBlindFirePoint(
            eligibility,
            out FireAimBasis basis,
            out FireAimBasisFailure failure
        );

        Assert.That(eligibility.CanBlindFire, Is.True);
        Assert.That(eligibility.RangeApplicable, Is.False);
        Assert.That(built, Is.False);
        Assert.That(basis, Is.EqualTo(default(FireAimBasis)));
        Assert.That(
            failure,
            Is.EqualTo(FireAimBasisFailure.FiniteAimPointRequired)
        );
    }


    [Test]
    public void DirectionOnlyRejection_DoesNotMutateCombatOrMovementState()
    {
        shooterState.SetAutoFireEnabled(true);
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;
        BroadsideReloadState port = shooterState.PortBroadsideState;
        BroadsideReloadState starboard =
            shooterState.StarboardBroadsideState;
        BlindFireEligibilityResult eligibility =
            EvaluateBlindFireDirection(Vector3.right);

        ShipFireAimBasisBuilder.TryBuildBlindFirePoint(
            eligibility,
            out _,
            out _
        );

        Assert.That(shooterState.AutoFireEnabled, Is.True);
        Assert.That(shooterState.PortBroadsideState, Is.EqualTo(port));
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(starboard)
        );
        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
    }


    [Test]
    public void TargetedConstruction_DoesNotWriteEitherRootPose()
    {
        Vector3 shooterPosition = shooterRoot.transform.position;
        Quaternion shooterRotation = shooterRoot.transform.rotation;
        Vector3 targetPosition = targetRoot.transform.position;
        Quaternion targetRotation = targetRoot.transform.rotation;

        BuildTargeted(EvaluateTargetedEligibility());

        Assert.That(shooterRoot.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(shooterRotation));
        Assert.That(targetRoot.transform.position, Is.EqualTo(targetPosition));
        Assert.That(targetRoot.transform.rotation, Is.EqualTo(targetRotation));
    }


    private FireAimBasis BuildTargeted(FireEligibilityResult eligibility)
    {
        bool built = ShipFireAimBasisBuilder.TryBuildTargeted(
            shooterRoot,
            targetRoot,
            eligibility,
            out FireAimBasis basis,
            out FireAimBasisFailure failure
        );

        Assert.That(built, Is.True);
        Assert.That(failure, Is.EqualTo(FireAimBasisFailure.None));
        return basis;
    }


    private FireEligibilityResult EvaluateTargetedEligibility()
    {
        Physics.SyncTransforms();
        bool evaluated = fireEligibility.TryEvaluate(
            targetRoot,
            true,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        Assert.That(result.CanFire, Is.True);
        return result;
    }


    private BlindFireEligibilityResult EvaluateBlindFirePoint(
        Vector3 worldAimPoint
    )
    {
        bool evaluated = fireEligibility.TryEvaluateBlindFireAtPoint(
            worldAimPoint,
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private BlindFireEligibilityResult EvaluateBlindFireDirection(
        Vector3 worldAimDirection
    )
    {
        bool evaluated = fireEligibility.TryEvaluateBlindFireDirection(
            worldAimDirection,
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private static GameObject CreateShip(string name, bool includeExposure)
    {
        GameObject root = new GameObject(name);
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


    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }
}
