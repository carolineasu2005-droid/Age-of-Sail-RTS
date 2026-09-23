using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipFireEligibilityTests
{
    private const float Tolerance = 0.001f;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private ShipFireEligibility eligibility;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = new GameObject("Shooter Root");
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetConfiguration(80f, 70f, 100f, 200f);
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void Evaluate_PortBroadside_ClassifiesPort()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(-50f, 0f, 0f)
        );

        Assert.That(result.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(result.InBroadsideArc, Is.True);
        Assert.That(
            result.TargetLocalBearingDegrees,
            Is.EqualTo(-90f).Within(Tolerance)
        );
    }


    [Test]
    public void Evaluate_StarboardBroadside_ClassifiesStarboard()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(50f, 0f, 0f)
        );

        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.InBroadsideArc, Is.True);
        Assert.That(
            result.TargetLocalBearingDegrees,
            Is.EqualTo(90f).Within(Tolerance)
        );
    }


    [Test]
    public void Evaluate_BowDeadZone_HasNoLegalSide()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(0f, 0f, 50f)
        );

        Assert.That(result.Side, Is.Null);
        Assert.That(result.InBroadsideArc, Is.False);
        Assert.That(result.CanFireGeometry, Is.False);
    }


    [Test]
    public void Evaluate_SternDeadZone_HasNoLegalSide()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(0f, 0f, -50f)
        );

        Assert.That(result.Side, Is.Null);
        Assert.That(result.InBroadsideArc, Is.False);
        Assert.That(result.CanFireGeometry, Is.False);
    }


    [Test]
    public void Evaluate_ArcBoundaries_AreInclusiveAndNearbyOutsideIsRejected()
    {
        FireEligibilityResult forwardBoundary = EvaluateAtBearing(10f, 50f);
        FireEligibilityResult beforeForwardBoundary = EvaluateAtBearing(
            9.99f,
            50f
        );
        FireEligibilityResult aftBoundary = EvaluateAtBearing(160f, 50f);
        FireEligibilityResult afterAftBoundary = EvaluateAtBearing(
            160.01f,
            50f
        );

        Assert.That(forwardBoundary.InBroadsideArc, Is.True);
        Assert.That(beforeForwardBoundary.InBroadsideArc, Is.False);
        Assert.That(aftBoundary.InBroadsideArc, Is.True);
        Assert.That(afterAftBoundary.InBroadsideArc, Is.False);
    }


    [Test]
    public void Evaluate_RootRotationPreservesShipLocalResult()
    {
        shooterRoot.transform.rotation = Quaternion.Euler(0f, 57f, 0f);
        Vector3 targetWorldPosition = shooterRoot.transform.position
            + shooterRoot.transform.right * 75f;

        FireEligibilityResult result = Evaluate(targetWorldPosition);

        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.InBroadsideArc, Is.True);
        Assert.That(
            result.TargetLocalBearingDegrees,
            Is.EqualTo(90f).Within(Tolerance)
        );
    }


    [Test]
    public void Evaluate_RootTranslationPreservesRelativeResult()
    {
        FireEligibilityResult atOrigin = Evaluate(
            new Vector3(-60f, 0f, 0f)
        );

        shooterRoot.transform.position = new Vector3(125f, 8f, -74f);
        FireEligibilityResult translated = Evaluate(
            shooterRoot.transform.position + Vector3.left * 60f
        );

        Assert.That(translated.Side, Is.EqualTo(atOrigin.Side));
        Assert.That(
            translated.TargetLocalBearingDegrees,
            Is.EqualTo(atOrigin.TargetLocalBearingDegrees).Within(Tolerance)
        );
        Assert.That(
            translated.DistanceMeters,
            Is.EqualTo(atOrigin.DistanceMeters).Within(Tolerance)
        );
    }


    [Test]
    public void Evaluate_VerticalOffsetDoesNotChangeHorizontalSideOrBearing()
    {
        FireEligibilityResult level = Evaluate(
            new Vector3(50f, 0f, 0f)
        );
        FireEligibilityResult elevated = Evaluate(
            new Vector3(50f, 80f, 0f)
        );

        Assert.That(elevated.Side, Is.EqualTo(level.Side));
        Assert.That(elevated.InBroadsideArc, Is.EqualTo(level.InBroadsideArc));
        Assert.That(
            elevated.TargetLocalBearingDegrees,
            Is.EqualTo(level.TargetLocalBearingDegrees).Within(Tolerance)
        );
    }


    [Test]
    public void Evaluate_DistanceUsesWorldSpaceMeters()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(30f, 40f, 0f)
        );

        Assert.That(
            result.DistanceMeters,
            Is.EqualTo(50f).Within(Tolerance)
        );
    }


    [Test]
    public void Evaluate_InsideEffectiveRange_IsLegalAndEffective()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(50f, 0f, 0f)
        );

        Assert.That(result.WithinEffectiveRange, Is.True);
        Assert.That(result.WithinMaximumRange, Is.True);
        Assert.That(result.CanFireGeometry, Is.True);
    }


    [Test]
    public void Evaluate_BetweenEffectiveAndMaximum_RemainsGeometricallyLegal()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(150f, 0f, 0f)
        );

        Assert.That(result.WithinEffectiveRange, Is.False);
        Assert.That(result.WithinMaximumRange, Is.True);
        Assert.That(result.CanFireGeometry, Is.True);
    }


    [Test]
    public void Evaluate_BeyondMaximumRange_IsNotGeometricallyLegal()
    {
        FireEligibilityResult result = Evaluate(
            new Vector3(200.01f, 0f, 0f)
        );

        Assert.That(result.WithinEffectiveRange, Is.False);
        Assert.That(result.WithinMaximumRange, Is.False);
        Assert.That(result.CanFireGeometry, Is.False);
    }


    [Test]
    public void Evaluate_ExactRangeBoundariesAreInclusive()
    {
        FireEligibilityResult effectiveBoundary = Evaluate(
            new Vector3(100f, 0f, 0f)
        );
        FireEligibilityResult maximumBoundary = Evaluate(
            new Vector3(200f, 0f, 0f)
        );

        Assert.That(effectiveBoundary.WithinEffectiveRange, Is.True);
        Assert.That(maximumBoundary.WithinMaximumRange, Is.True);
        Assert.That(maximumBoundary.WithinEffectiveRange, Is.False);
    }


    [Test]
    public void Evaluate_InvalidConfigurationFailsWithoutCorrection()
    {
        SetConfiguration(90f, 90f, 250f, 200f);

        bool evaluated = TryEvaluateGeometry(
            new Vector3(50f, 0f, 0f),
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
        Assert.That(eligibility.ForwardArcLimitDegrees, Is.EqualTo(90f));
        Assert.That(eligibility.AftArcLimitDegrees, Is.EqualTo(90f));
        Assert.That(eligibility.EffectiveRangeMeters, Is.EqualTo(250f));
        Assert.That(eligibility.MaximumRangeMeters, Is.EqualTo(200f));
    }


    [TestCase(-1f, 70f, 100f, 200f)]
    [TestCase(91f, 60f, 100f, 200f)]
    [TestCase(90f, 71f, 100f, 200f)]
    [TestCase(0f, 0f, 100f, 200f)]
    [TestCase(80f, 70f, -1f, 200f)]
    [TestCase(80f, 70f, 100f, -1f)]
    [TestCase(80f, 70f, 201f, 200f)]
    public void Evaluate_InvalidConfigurationInvariantFails(
        float forwardDegrees,
        float aftDegrees,
        float effectiveMeters,
        float maximumMeters
    )
    {
        SetConfiguration(
            forwardDegrees,
            aftDegrees,
            effectiveMeters,
            maximumMeters
        );

        bool evaluated = TryEvaluateGeometry(
            new Vector3(50f, 0f, 0f),
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
    }


    [Test]
    public void Evaluate_NonFiniteConfigurationFailsWithoutCorrection()
    {
        SetConfiguration(80f, 70f, float.NaN, 200f);

        bool evaluated = TryEvaluateGeometry(
            new Vector3(50f, 0f, 0f),
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
        Assert.That(float.IsNaN(eligibility.EffectiveRangeMeters), Is.True);
    }


    [Test]
    public void Evaluate_NoHorizontalTargetDirectionFailsSafely()
    {
        bool evaluated = TryEvaluateGeometry(
            new Vector3(0f, 25f, 0f),
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
    }


    [Test]
    public void Evaluate_NonFiniteTargetPositionFailsSafely()
    {
        bool evaluated = TryEvaluateGeometry(
            new Vector3(float.NaN, 0f, 50f),
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.False);
        Assert.That(result, Is.EqualTo(default(FireEligibilityResult)));
    }


    [Test]
    public void Evaluate_DoesNotWriteRootTransform()
    {
        shooterRoot.transform.SetPositionAndRotation(
            new Vector3(21f, 3f, -17f),
            Quaternion.Euler(0f, 38f, 0f)
        );
        shooterRoot.transform.localScale = new Vector3(1.2f, 0.8f, 1.1f);
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;
        Vector3 scale = shooterRoot.transform.localScale;

        Evaluate(position + shooterRoot.transform.right * 50f);

        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(shooterRoot.transform.localScale, Is.EqualTo(scale));
    }


    [Test]
    public void Evaluate_DoesNotWriteMovementState()
    {
        ShipSailingSpeed sailingSpeed =
            shooterRoot.AddComponent<ShipSailingSpeed>();
        SetPrivateField(sailingSpeed, "currentSpeed", 3.25f);
        SetPrivateField(
            sailingSpeed,
            "actualVelocity",
            new Vector3(1.5f, 0f, 2.75f)
        );
        SetPrivateField(sailingSpeed, "courseSpeed", 3.1f);
        SetPrivateField(sailingSpeed, "courseHeading", 28f);
        float currentSpeed = sailingSpeed.CurrentSpeed;
        Vector3 actualVelocity = sailingSpeed.ActualVelocity;
        float courseSpeed = sailingSpeed.CourseSpeed;
        float courseHeading = sailingSpeed.CourseHeading;

        Evaluate(new Vector3(50f, 0f, 0f));

        Assert.That(sailingSpeed.CurrentSpeed, Is.EqualTo(currentSpeed));
        Assert.That(sailingSpeed.ActualVelocity, Is.EqualTo(actualVelocity));
        Assert.That(sailingSpeed.CourseSpeed, Is.EqualTo(courseSpeed));
        Assert.That(sailingSpeed.CourseHeading, Is.EqualTo(courseHeading));
    }


    [Test]
    public void Evaluate_IsIndependentOfRendererAndVisualRoot()
    {
        FireEligibilityResult beforeVisual = Evaluate(
            new Vector3(-50f, 0f, 0f)
        );
        GameObject visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(shooterRoot.transform, false);
        visualRoot.transform.localPosition = new Vector3(900f, 400f, -700f);
        visualRoot.transform.localRotation = Quaternion.Euler(31f, 72f, 18f);
        visualRoot.AddComponent<MeshRenderer>();

        FireEligibilityResult afterVisual = Evaluate(
            new Vector3(-50f, 0f, 0f)
        );

        Assert.That(afterVisual.Side, Is.EqualTo(beforeVisual.Side));
        Assert.That(
            afterVisual.TargetLocalBearingDegrees,
            Is.EqualTo(beforeVisual.TargetLocalBearingDegrees).Within(Tolerance)
        );
        Assert.That(
            afterVisual.DistanceMeters,
            Is.EqualTo(beforeVisual.DistanceMeters).Within(Tolerance)
        );
    }


    [Test]
    public void Result_IsImmutableReadOnlyValueWithFinalCanFireClaim()
    {
        Assert.That(typeof(FireEligibilityResult).IsValueType, Is.True);
        Assert.That(
            typeof(FireEligibilityResult).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
        Assert.That(
            typeof(FireEligibilityResult).GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly
            ).All(property => property.SetMethod == null),
            Is.True
        );
        Assert.That(
            typeof(FireEligibilityResult).GetProperty("CanFire"),
            Is.Not.Null
        );
        Assert.That(
            typeof(FireEligibilityResult).GetProperty("RangeQuality"),
            Is.Null
        );
    }


    [Test]
    public void ConfigurationSurface_IsCentralizedSerializedAndReadOnly()
    {
        FieldInfo[] serializedFields = typeof(ShipFireEligibility)
            .GetFields(PrivateInstance | BindingFlags.DeclaredOnly)
            .Where(field => field.IsDefined(typeof(SerializeField), false))
            .ToArray();
        string[] expectedFieldNames =
        {
            "forwardArcLimitDegrees",
            "aftArcLimitDegrees",
            "effectiveRangeMeters",
            "maximumRangeMeters"
        };

        Assert.That(
            serializedFields.Select(field => field.Name),
            Is.EquivalentTo(expectedFieldNames)
        );
        Assert.That(
            typeof(ShipFireEligibility).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly
            ),
            Is.Empty
        );
        Assert.That(
            typeof(ShipFireEligibility).GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly
            ).All(property => property.SetMethod == null),
            Is.True
        );
    }


    [Test]
    public void Evaluator_HasNoRendererProjectileOrMovementContract()
    {
        Type[] contractTypes = typeof(ShipFireEligibility)
            .GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
            .Select(field => field.FieldType)
            .Concat(typeof(ShipFireEligibility)
                .GetProperties(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.DeclaredOnly
                )
                .Select(property => property.PropertyType))
            .Concat(typeof(ShipFireEligibility)
                .GetMethods(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.DeclaredOnly
                )
                .SelectMany(method => method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType)))
            .ToArray();

        Type[] forbiddenTypes =
        {
            typeof(Renderer),
            typeof(Mesh),
            typeof(ShipExposureReference),
            typeof(ShipSailingSpeed),
            typeof(ShipTurning),
            typeof(ShipTacking),
            typeof(ShipWearing),
            typeof(ShipLeeway),
            typeof(ShipDestinationController),
            typeof(ShipManeuverPlanner)
        };

        foreach (Type forbiddenType in forbiddenTypes)
        {
            Assert.That(contractTypes, Has.No.Member(forbiddenType));
        }

        Assert.That(
            contractTypes.Any(type => type.Name.Contains("Projectile")),
            Is.False
        );
        Assert.That(
            typeof(ShipFireEligibility).GetEvents(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ),
            Is.Empty
        );
    }


    private FireEligibilityResult Evaluate(Vector3 targetWorldPosition)
    {
        bool evaluated = TryEvaluateGeometry(
            targetWorldPosition,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private bool TryEvaluateGeometry(
        Vector3 targetWorldPosition,
        out FireEligibilityResult result
    )
    {
        MethodInfo method = typeof(ShipFireEligibility).GetMethod(
            "TryEvaluateGeometry",
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null);
        object[] arguments =
        {
            targetWorldPosition,
            default(FireEligibilityResult)
        };
        bool evaluated = (bool)method.Invoke(eligibility, arguments);
        result = (FireEligibilityResult)arguments[1];
        return evaluated;
    }


    private FireEligibilityResult EvaluateAtBearing(
        float bearingDegrees,
        float distanceMeters
    )
    {
        float bearingRadians = bearingDegrees * Mathf.Deg2Rad;
        Vector3 targetWorldPosition = new Vector3(
            Mathf.Sin(bearingRadians) * distanceMeters,
            0f,
            Mathf.Cos(bearingRadians) * distanceMeters
        );
        return Evaluate(targetWorldPosition);
    }


    private void SetConfiguration(
        float forwardDegrees,
        float aftDegrees,
        float effectiveMeters,
        float maximumMeters
    )
    {
        SetPrivateField(
            eligibility,
            "forwardArcLimitDegrees",
            forwardDegrees
        );
        SetPrivateField(
            eligibility,
            "aftArcLimitDegrees",
            aftDegrees
        );
        SetPrivateField(
            eligibility,
            "effectiveRangeMeters",
            effectiveMeters
        );
        SetPrivateField(
            eligibility,
            "maximumRangeMeters",
            maximumMeters
        );
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
