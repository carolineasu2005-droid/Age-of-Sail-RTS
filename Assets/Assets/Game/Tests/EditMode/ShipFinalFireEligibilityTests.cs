using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipFinalFireEligibilityTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipCombatState shooterState;
    private ShipFireEligibility eligibility;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateShip("Shooter Root", true);
        targetRoot = CreateShip("Target Root", true);
        targetRoot.transform.position = Vector3.right * 50f;
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetEligibilityConfiguration(100f, 200f);
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(targetRoot);
        Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void PortReload_BlocksApplicablePortFire()
    {
        targetRoot.transform.position = Vector3.left * 50f;
        Assert.That(
            shooterState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );

        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(result.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(result.ReloadReady, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.BroadsideReloading
            ),
            Is.True
        );
    }


    [Test]
    public void StarboardReady_DoesNotRescueReloadingPort()
    {
        targetRoot.transform.position = Vector3.left * 50f;
        shooterState.TryCommitBroadsideFire(CombatSide.Port);

        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(result.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(result.ReloadReady, Is.False);
        Assert.That(result.CanFire, Is.False);
    }


    [Test]
    public void ReloadingPort_DoesNotBlockReadyStarboardFire()
    {
        shooterState.TryCommitBroadsideFire(CombatSide.Port);

        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.ReloadReady, Is.True);
        Assert.That(result.CanFire, Is.True);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.BroadsideReloading
            ),
            Is.False
        );
    }


    [Test]
    public void StarboardReload_BlocksApplicableStarboardFire()
    {
        shooterState.TryCommitBroadsideFire(CombatSide.Starboard);

        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(result.ReloadReady, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.BroadsideReloading
            ),
            Is.True
        );
    }


    [Test]
    public void NullTarget_IsRejectedWithExplainableResult()
    {
        FireEligibilityResult result = Evaluate(null, true);

        Assert.That(result.TargetLegal, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons,
            Is.EqualTo(FireEligibilityFailure.TargetIllegal)
        );
    }


    [Test]
    public void SelfTarget_IsRejectedWithExplainableResult()
    {
        FireEligibilityResult result = Evaluate(shooterRoot, true);

        Assert.That(result.TargetLegal, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.TargetIllegal
            ),
            Is.True
        );
    }


    [Test]
    public void TargetWithoutCombatSpatialIdentity_IsRejected()
    {
        GameObject targetWithoutGeometry = CreateShip(
            "Target Without Combat Geometry",
            false
        );

        try
        {
            targetWithoutGeometry.transform.position = Vector3.right * 50f;
            FireEligibilityResult result = Evaluate(
                targetWithoutGeometry,
                true
            );

            Assert.That(result.TargetLegal, Is.False);
            Assert.That(result.CanFire, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(targetWithoutGeometry);
        }
    }


    [Test]
    public void CallerRelationshipRejection_MakesStructuralTargetIllegal()
    {
        FireEligibilityResult result = Evaluate(targetRoot, false);

        Assert.That(result.TargetLegal, Is.False);
        Assert.That(result.InBroadsideArc, Is.True);
        Assert.That(result.ReloadReady, Is.True);
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.TargetIllegal
            ),
            Is.True
        );
    }


    [Test]
    public void BeyondEffectiveWithinMaximum_DoesNotBlockFire()
    {
        targetRoot.transform.position = Vector3.right * 150f;

        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(result.TargetLegal, Is.True);
        Assert.That(result.WithinEffectiveRange, Is.False);
        Assert.That(result.WithinMaximumRange, Is.True);
        Assert.That(result.CanFire, Is.True);
        Assert.That(
            result.FailureReasons,
            Is.EqualTo(FireEligibilityFailure.None)
        );
    }


    [Test]
    public void BeyondMaximum_BlocksFire()
    {
        targetRoot.transform.position = Vector3.right * 201f;

        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(result.WithinMaximumRange, Is.False);
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.BeyondMaximumRange
            ),
            Is.True
        );
    }


    [Test]
    public void MultipleFailures_AreReportedTogether()
    {
        targetRoot.transform.position = Vector3.left * 201f;
        shooterState.TryCommitBroadsideFire(CombatSide.Port);

        FireEligibilityResult result = Evaluate(targetRoot, false);

        FireEligibilityFailure expected =
            FireEligibilityFailure.TargetIllegal
            | FireEligibilityFailure.BeyondMaximumRange
            | FireEligibilityFailure.BroadsideReloading;
        Assert.That(result.FailureReasons, Is.EqualTo(expected));
        Assert.That(result.CanFire, Is.False);
    }


    [Test]
    public void LifecycleAbsent_DefaultsToAllowsFireWithoutStateOwner()
    {
        FireEligibilityResult result = Evaluate(targetRoot, true);

        Assert.That(result.LifecycleAllowsFire, Is.True);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.LifecycleDisallowsFire
            ),
            Is.False
        );
        Assert.That(
            typeof(ShipCombatState).GetProperty("IsDisabled"),
            Is.Null
        );
    }


    [Test]
    public void Evaluation_DoesNotWriteMovementRootOrMovementState()
    {
        ShipSailingSpeed sailingSpeed =
            shooterRoot.AddComponent<ShipSailingSpeed>();
        SetPrivateField(sailingSpeed, "currentSpeed", 3.5f);
        SetPrivateField(
            sailingSpeed,
            "actualVelocity",
            new Vector3(1f, 0f, 2f)
        );
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;
        float currentSpeed = sailingSpeed.CurrentSpeed;
        Vector3 actualVelocity = sailingSpeed.ActualVelocity;

        Evaluate(targetRoot, true);

        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(sailingSpeed.CurrentSpeed, Is.EqualTo(currentSpeed));
        Assert.That(sailingSpeed.ActualVelocity, Is.EqualTo(actualVelocity));
    }


    [Test]
    public void Evaluation_DoesNotWriteReloadStateOrTimers()
    {
        targetRoot.transform.position = Vector3.left * 50f;
        shooterState.TryCommitBroadsideFire(CombatSide.Port);
        BroadsideReloadState portState = shooterState.PortBroadsideState;
        float portRemaining = shooterState.PortReloadRemainingSeconds;
        BroadsideReloadState starboardState =
            shooterState.StarboardBroadsideState;
        float starboardRemaining =
            shooterState.StarboardReloadRemainingSeconds;

        Evaluate(targetRoot, true);

        Assert.That(shooterState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(
            shooterState.PortReloadRemainingSeconds,
            Is.EqualTo(portRemaining)
        );
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(starboardState)
        );
        Assert.That(
            shooterState.StarboardReloadRemainingSeconds,
            Is.EqualTo(starboardRemaining)
        );
    }


    private FireEligibilityResult Evaluate(
        GameObject target,
        bool targetRelationshipAllowsFire
    )
    {
        bool evaluated = eligibility.TryEvaluate(
            target,
            targetRelationshipAllowsFire,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private static GameObject CreateShip(
        string name,
        bool includeCombatGeometry
    )
    {
        GameObject root = new GameObject(name);
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        GameObject center = new GameObject("Center Reference");
        center.transform.SetParent(root.transform, false);
        SetPrivateField(
            artDefinition,
            "centerReference",
            center.transform
        );

        if (includeCombatGeometry)
        {
            root.AddComponent<ShipCombatGeometry>();
        }

        return root;
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
}
