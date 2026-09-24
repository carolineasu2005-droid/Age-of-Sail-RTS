using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipBlindFireEligibilityTests
{
    private const float Tolerance = 0.0001f;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private ShipCombatState combatState;
    private ShipFireEligibility eligibility;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = new GameObject("Blind Fire Shooter Root");
        combatState = shooterRoot.AddComponent<ShipCombatState>();
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetEligibilityConfiguration(100f, 200f);
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void WorldPoint_PortAimResolvesPortAndCanBlindFire()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.left * 50f
        );

        Assert.That(result.ValidAim, Is.True);
        Assert.That(result.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(result.InBroadsideArc, Is.True);
        Assert.That(result.CanBlindFire, Is.True);
        Assert.That(result.Aim.WorldAimPoint, Is.EqualTo(Vector3.left * 50f));
    }


    [Test]
    public void WorldPoint_StarboardAimResolvesStarboardAndCanBlindFire()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.right * 50f
        );

        Assert.That(result.ValidAim, Is.True);
        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void WorldPoint_BowDeadZoneRejectsWithoutSteering()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.forward * 50f
        );

        AssertArcRejected(result);
    }


    [Test]
    public void WorldPoint_SternDeadZoneRejectsWithoutSteering()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.back * 50f
        );

        AssertArcRejected(result);
    }


    [TestCase(10f, CombatSide.Starboard)]
    [TestCase(-10f, CombatSide.Port)]
    [TestCase(160f, CombatSide.Starboard)]
    [TestCase(-160f, CombatSide.Port)]
    public void WorldDirection_ExactArcBoundariesAreInclusive(
        float bearingDegrees,
        CombatSide expectedSide
    )
    {
        BlindFireEligibilityResult result = EvaluateDirection(
            DirectionAtBearing(bearingDegrees)
        );

        Assert.That(result.InBroadsideArc, Is.True);
        Assert.That(result.Side, Is.EqualTo(expectedSide));
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void WorldDirection_RootRotationControlsLocalClassification()
    {
        shooterRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        BlindFireEligibilityResult result = EvaluateDirection(
            shooterRoot.transform.right
        );

        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(
            result.AimLocalBearingDegrees,
            Is.EqualTo(90f).Within(Tolerance)
        );
    }


    [Test]
    public void WorldPoint_RootTranslationPreservesRelativeDirection()
    {
        shooterRoot.transform.position = new Vector3(25f, 3f, -40f);
        Vector3 point = shooterRoot.transform.position
            + Vector3.left * 50f;

        BlindFireEligibilityResult result = EvaluatePoint(point);

        Assert.That(result.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(result.Aim.WorldAimDirection, Is.EqualTo(Vector3.left));
    }


    [Test]
    public void WorldPoint_VerticalOffsetDoesNotChangeBearing()
    {
        BlindFireEligibilityResult level = EvaluatePoint(
            Vector3.right * 50f
        );
        BlindFireEligibilityResult elevated = EvaluatePoint(
            new Vector3(50f, 75f, 0f)
        );

        Assert.That(
            elevated.AimLocalBearingDegrees,
            Is.EqualTo(level.AimLocalBearingDegrees).Within(Tolerance)
        );
        Assert.That(
            elevated.Aim.WorldAimDirection,
            Is.EqualTo(level.Aim.WorldAimDirection)
        );
    }


    [Test]
    public void WorldPoint_InsideMaximumPassesRangeLegality()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.right * 50f
        );

        Assert.That(result.RangeApplicable, Is.True);
        Assert.That(result.Aim.AimPointDistanceMeters, Is.EqualTo(50f));
        Assert.That(result.WithinMaximumRange, Is.True);
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void WorldPoint_ExactMaximumIsInclusive()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.right * 200f
        );

        Assert.That(result.WithinMaximumRange, Is.True);
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void WorldPoint_BeyondMaximumRejects()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.right * 201f
        );

        Assert.That(result.WithinMaximumRange, Is.False);
        Assert.That(result.CanBlindFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.BeyondMaximumRange
            ),
            Is.True
        );
    }


    [Test]
    public void WorldPoint_BeyondEffectiveInsideMaximumRemainsLegal()
    {
        BlindFireEligibilityResult result = EvaluatePoint(
            Vector3.right * 150f
        );

        Assert.That(result.Aim.AimPointDistanceMeters, Is.EqualTo(150f));
        Assert.That(result.WithinMaximumRange, Is.True);
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void WorldDirection_HasNoInventedPointDistanceOrRangeVerdict()
    {
        BlindFireEligibilityResult result = EvaluateDirection(
            Vector3.right * 100000f
        );

        Assert.That(result.Aim.WorldAimDirection, Is.EqualTo(Vector3.right));
        Assert.That(result.Aim.HasWorldAimPoint, Is.False);
        Assert.That(result.Aim.WorldAimPoint, Is.Null);
        Assert.That(result.Aim.AimPointDistanceMeters, Is.Null);
        Assert.That(result.RangeApplicable, Is.False);
        Assert.That(result.WithinMaximumRange, Is.Null);
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void WorldDirection_ZeroHorizontalDirectionRejectsSafely()
    {
        BlindFireEligibilityResult result = EvaluateDirection(Vector3.up);

        Assert.That(result.ValidAim, Is.False);
        Assert.That(result.CanBlindFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.InvalidAim
            ),
            Is.True
        );
    }


    [Test]
    public void NonFinitePointAndDirectionRejectSafely()
    {
        BlindFireEligibilityResult point = EvaluatePoint(
            new Vector3(float.NaN, 0f, 0f)
        );
        BlindFireEligibilityResult direction = EvaluateDirection(
            new Vector3(float.PositiveInfinity, 0f, 0f)
        );

        Assert.That(point.ValidAim, Is.False);
        Assert.That(point.CanBlindFire, Is.False);
        Assert.That(direction.ValidAim, Is.False);
        Assert.That(direction.CanBlindFire, Is.False);
    }


    [Test]
    public void PortReloadingRejectsPortOnly()
    {
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );

        BlindFireEligibilityResult port = EvaluatePoint(
            Vector3.left * 50f
        );
        BlindFireEligibilityResult starboard = EvaluatePoint(
            Vector3.right * 50f
        );

        Assert.That(port.ReloadReady, Is.False);
        Assert.That(port.CanBlindFire, Is.False);
        Assert.That(starboard.ReloadReady, Is.True);
        Assert.That(starboard.CanBlindFire, Is.True);
    }


    [Test]
    public void StarboardReloadingRejectsStarboardOnly()
    {
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Starboard),
            Is.True
        );

        BlindFireEligibilityResult starboard = EvaluatePoint(
            Vector3.right * 50f
        );
        BlindFireEligibilityResult port = EvaluatePoint(
            Vector3.left * 50f
        );

        Assert.That(starboard.ReloadReady, Is.False);
        Assert.That(starboard.CanBlindFire, Is.False);
        Assert.That(port.ReloadReady, Is.True);
        Assert.That(port.CanBlindFire, Is.True);
    }


    [Test]
    public void OppositeSideReloadDoesNotAffectApplicableSide()
    {
        combatState.TryCommitBroadsideFire(CombatSide.Port);

        BlindFireEligibilityResult result = EvaluateDirection(
            Vector3.right
        );

        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.ReloadReady, Is.True);
        Assert.That(result.CanBlindFire, Is.True);
    }


    [Test]
    public void QueryDoesNotCommitOrConsumeEitherBroadsideReload()
    {
        BroadsideReloadState portState = combatState.PortBroadsideState;
        float portRemaining = combatState.PortReloadRemainingSeconds;
        BroadsideReloadState starboardState =
            combatState.StarboardBroadsideState;
        float starboardRemaining =
            combatState.StarboardReloadRemainingSeconds;

        EvaluatePoint(Vector3.left * 50f);
        EvaluatePoint(Vector3.right * 50f);

        Assert.That(combatState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(
            combatState.PortReloadRemainingSeconds,
            Is.EqualTo(portRemaining)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(starboardState)
        );
        Assert.That(
            combatState.StarboardReloadRemainingSeconds,
            Is.EqualTo(starboardRemaining)
        );
    }


    [Test]
    public void QueryReadsLifecycleBridgeWithoutInventingLifecycleState()
    {
        bool autoFireEnabled = combatState.AutoFireEnabled;
        GameObject manualTarget = combatState.ManualTarget;

        BlindFireEligibilityResult result = EvaluateDirection(Vector3.right);

        Assert.That(result.LifecycleAllowsFire, Is.True);
        Assert.That(combatState.AutoFireEnabled, Is.EqualTo(autoFireEnabled));
        Assert.That(combatState.ManualTarget, Is.SameAs(manualTarget));
        Assert.That(
            typeof(ShipCombatState).GetProperty("IsDisabled"),
            Is.Null
        );
    }


    [Test]
    public void QueryRequiresNoTargetGameObject()
    {
        MethodInfo pointMethod = typeof(ShipFireEligibility).GetMethod(
            "TryEvaluateBlindFireAtPoint"
        );
        MethodInfo directionMethod = typeof(ShipFireEligibility).GetMethod(
            "TryEvaluateBlindFireDirection"
        );

        Assert.That(pointMethod, Is.Not.Null);
        Assert.That(directionMethod, Is.Not.Null);
        Assert.That(
            pointMethod.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(GameObject)
            ),
            Is.False
        );
        Assert.That(
            directionMethod.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(GameObject)
            ),
            Is.False
        );
        Assert.That(EvaluateDirection(Vector3.right).CanBlindFire, Is.True);
    }


    [Test]
    public void QueryHasNoExposureDependency()
    {
        Assert.That(
            shooterRoot.GetComponent<ShipExposureReference>(),
            Is.Null
        );
        Assert.That(
            shooterRoot.GetComponent<ShipArtDefinition>(),
            Is.Null
        );
        Assert.That(EvaluatePoint(Vector3.right * 50f).CanBlindFire, Is.True);
    }


    [Test]
    public void QueryHasNoRendererMeshOrVisualRootDependency()
    {
        Assert.That(shooterRoot.GetComponentInChildren<Renderer>(), Is.Null);
        Assert.That(
            shooterRoot.transform.Find("VisualRoot"),
            Is.Null
        );

        BlindFireEligibilityResult result = EvaluateDirection(Vector3.left);

        Assert.That(result.CanBlindFire, Is.True);
        Assert.That(
            GetBlindFireSourceSlice(),
            Does.Not.Contain("Renderer")
        );
        Assert.That(GetBlindFireSourceSlice(), Does.Not.Contain("Mesh"));
        Assert.That(
            GetBlindFireSourceSlice(),
            Does.Not.Contain("VisualRoot")
        );
    }


    [Test]
    public void QueryPerformsNoPhysicsObstructionQuery()
    {
        string source = GetBlindFireSourceSlice();

        Assert.That(source, Does.Not.Contain("Physics."));
        Assert.That(source, Does.Not.Contain("Raycast"));
        Assert.That(source, Does.Not.Contain("BlockingCollider"));
    }


    [Test]
    public void QueryDoesNotWriteTransformMovementOrReloadState()
    {
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;
        BroadsideReloadState portState = combatState.PortBroadsideState;
        BroadsideReloadState starboardState =
            combatState.StarboardBroadsideState;
        string source = GetBlindFireSourceSlice();

        EvaluatePoint(Vector3.right * 50f);

        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(combatState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(starboardState)
        );
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("ShipDestinationController"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("transform.position ="));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
    }


    [Test]
    public void TargetAwareFireEligibilityRetainsPhaseTwoBehavior()
    {
        ConfigureArtCenter(shooterRoot);
        GameObject targetRoot = new GameObject("Target Root");

        try
        {
            targetRoot.AddComponent<ShipCombatState>();
            targetRoot.AddComponent<ShipCombatGeometry>();
            ConfigureArtCenter(targetRoot);
            targetRoot.transform.position = Vector3.right * 50f;

            bool evaluated = eligibility.TryEvaluate(
                targetRoot,
                true,
                out FireEligibilityResult result
            );

            Assert.That(evaluated, Is.True);
            Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
            Assert.That(result.WithinMaximumRange, Is.True);
            Assert.That(result.CanFire, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetRoot);
        }
    }


    [Test]
    public void AimAndResultAreImmutableReadOnlyValues()
    {
        AssertImmutableValue<BlindFireAim>();
        AssertImmutableValue<BlindFireEligibilityResult>();
        Assert.That(
            typeof(BlindFireEligibilityFailure).IsDefined(
                typeof(FlagsAttribute),
                false
            ),
            Is.True
        );
    }


    private BlindFireEligibilityResult EvaluatePoint(Vector3 worldAimPoint)
    {
        bool evaluated = eligibility.TryEvaluateBlindFireAtPoint(
            worldAimPoint,
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private BlindFireEligibilityResult EvaluateDirection(
        Vector3 worldAimDirection
    )
    {
        bool evaluated = eligibility.TryEvaluateBlindFireDirection(
            worldAimDirection,
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private static Vector3 DirectionAtBearing(float bearingDegrees)
    {
        float radians = bearingDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }


    private static void AssertArcRejected(BlindFireEligibilityResult result)
    {
        Assert.That(result.ValidAim, Is.True);
        Assert.That(result.Side, Is.Null);
        Assert.That(result.InBroadsideArc, Is.False);
        Assert.That(result.CanBlindFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.NoBroadsideArc
            ),
            Is.True
        );
    }


    private static void AssertImmutableValue<T>()
    {
        Type type = typeof(T);
        Assert.That(type.IsValueType, Is.True);
        Assert.That(
            type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
        Assert.That(
            type.GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly
            ).All(property => property.SetMethod == null),
            Is.True
        );
    }


    private static void ConfigureArtCenter(GameObject root)
    {
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        GameObject center = new GameObject("Center Reference");
        center.transform.SetParent(root.transform, false);
        SetPrivateField(artDefinition, "centerReference", center.transform);
    }


    private void SetEligibilityConfiguration(
        float effectiveMeters,
        float maximumMeters
    )
    {
        SetPrivateField(eligibility, "forwardArcLimitDegrees", 80f);
        SetPrivateField(eligibility, "aftArcLimitDegrees", 70f);
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


    private static string GetBlindFireSourceSlice()
    {
        string path = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipFireEligibility.cs"
        );
        string source = File.ReadAllText(path);
        int start = source.IndexOf(
            "public bool TryEvaluateBlindFireAtPoint",
            StringComparison.Ordinal
        );
        int end = source.IndexOf(
            "private bool TryEvaluateGeometry",
            start,
            StringComparison.Ordinal
        );

        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return source.Substring(start, end - start);
    }
}
