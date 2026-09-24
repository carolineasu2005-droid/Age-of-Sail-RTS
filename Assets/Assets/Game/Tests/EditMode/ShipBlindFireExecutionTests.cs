using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipBlindFireExecutionTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipCombatState combatState;
    private ShipFireEligibility eligibility;
    private ShipBlindFireCommand command;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = new GameObject("Blind Fire Shooter Root");
        targetRoot = new GameObject("Existing Manual Target Root");
        combatState = shooterRoot.AddComponent<ShipCombatState>();
        targetRoot.AddComponent<ShipCombatState>();
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetPrivateField(eligibility, "effectiveRangeMeters", 100f);
        SetPrivateField(eligibility, "maximumRangeMeters", 200f);
        command = new ShipBlindFireCommand(eligibility, combatState);
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(targetRoot);
        UnityEngine.Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void LegalPortBlindFire_CommitsPort()
    {
        BlindFireExecutionResult result = Execute(
            PointAim(Vector3.left * 50f)
        );

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Side, Is.EqualTo(CombatSide.Port));
        Assert.That(result.FailureReasons, Is.EqualTo(
            BlindFireExecutionFailure.None
        ));
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
    }


    [Test]
    public void InvalidAim_RejectsBeforeEligibilityOrStateMutation()
    {
        combatState.SetAutoFireEnabled(true);

        BlindFireExecutionResult result = Execute(default, false);

        Assert.That(result.Side, Is.Null);
        Assert.That(result.FailureReasons.HasFlag(
            BlindFireExecutionFailure.InvalidAim
        ), Is.True);
        Assert.That(combatState.AutoFireEnabled, Is.True);
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    [Test]
    public void LegalStarboardBlindFire_CommitsStarboard()
    {
        BlindFireExecutionResult result = Execute(
            DirectionAim(Vector3.right)
        );

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Side, Is.EqualTo(CombatSide.Starboard));
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
    }


    [Test]
    public void PortCommit_LeavesStarboardUnchanged()
    {
        Execute(PointAim(Vector3.left * 50f));

        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void StarboardCommit_LeavesPortUnchanged()
    {
        Execute(PointAim(Vector3.right * 50f));

        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void PortThenStarboard_CanCommitIndependently()
    {
        BlindFireAim portAim = DirectionAim(Vector3.left);
        BlindFireAim starboardAim = DirectionAim(Vector3.right);

        BlindFireExecutionResult port = Execute(portAim);
        BlindFireExecutionResult starboard = Execute(starboardAim);

        Assert.That(port.Accepted, Is.True);
        Assert.That(starboard.Accepted, Is.True);
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
    }


    [Test]
    public void BowDeadZoneAttempt_ConsumesNoReload()
    {
        BlindFireExecutionResult result = Execute(
            PointAim(Vector3.forward * 50f),
            false
        );

        AssertRejectedWithoutReload(result);
    }


    [Test]
    public void SternDeadZoneAttempt_ConsumesNoReload()
    {
        BlindFireExecutionResult result = Execute(
            PointAim(Vector3.back * 50f),
            false
        );

        AssertRejectedWithoutReload(result);
    }


    [Test]
    public void BeyondMaximumPoint_ConsumesNoReload()
    {
        BlindFireExecutionResult result = Execute(
            PointAim(Vector3.right * 201f),
            false
        );

        AssertRejectedWithoutReload(result);
        Assert.That(
            result.Eligibility.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.BeyondMaximumRange
            ),
            Is.True
        );
    }


    [Test]
    public void ReloadingSide_Rejects()
    {
        BlindFireAim aim = DirectionAim(Vector3.left);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );

        BlindFireExecutionResult result = Execute(aim, false);

        Assert.That(result.Accepted, Is.False);
        Assert.That(
            result.Eligibility.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.BroadsideReloading
            ),
            Is.True
        );
    }


    [Test]
    public void RejectedReloadAttempt_DoesNotResetExistingTimer()
    {
        BlindFireAim aim = DirectionAim(Vector3.left);
        combatState.TryCommitBroadsideFire(CombatSide.Port);
        AdvanceReloads(2f);
        float remaining = combatState.PortReloadRemainingSeconds;

        Execute(aim, false);

        Assert.That(combatState.PortReloadRemainingSeconds, Is.EqualTo(
            remaining
        ));
    }


    [Test]
    public void SuccessfulExecution_DisablesAutoFire()
    {
        combatState.SetAutoFireEnabled(true);

        Execute(DirectionAim(Vector3.right));

        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void AutoFire_RemainsOffAfterSuccessfulExecution()
    {
        combatState.SetAutoFireEnabled(true);
        Execute(DirectionAim(Vector3.left));

        Execute(DirectionAim(Vector3.right));

        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void RejectedAttemptWhileAutoOn_LeavesAutoOn()
    {
        BlindFireAim bowAim = DirectionAim(Vector3.forward);
        combatState.SetAutoFireEnabled(true);

        Execute(bowAim, false);

        Assert.That(combatState.AutoFireEnabled, Is.True);
    }


    [Test]
    public void ExistingManualTarget_SurvivesSuccessfulBlindFire()
    {
        Assert.That(combatState.AssignManualTarget(targetRoot), Is.True);

        Execute(DirectionAim(Vector3.right));

        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void ExistingManualTarget_SurvivesRejectedBlindFire()
    {
        Assert.That(combatState.AssignManualTarget(targetRoot), Is.True);

        Execute(DirectionAim(Vector3.forward), false);

        Assert.That(combatState.ManualTarget, Is.SameAs(targetRoot));
    }


    [Test]
    public void Execution_ReevaluatesStalePointAimAgainstCurrentRootPose()
    {
        BlindFireAim previouslyLegalAim = PointAim(Vector3.right * 50f);
        shooterRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        BlindFireExecutionResult result = Execute(
            previouslyLegalAim,
            false
        );

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Eligibility.InBroadsideArc, Is.False);
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    [Test]
    public void Execution_RequiresNoTargetEntity()
    {
        MethodInfo method = typeof(ShipBlindFireCommand).GetMethod(
            "TryExecuteBlindFire"
        );

        Assert.That(method, Is.Not.Null);
        Assert.That(
            method.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(GameObject)
            ),
            Is.False
        );
        Assert.That(Execute(DirectionAim(Vector3.right)).Accepted, Is.True);
    }


    [Test]
    public void Execution_DoesNotAcquireAutoTarget()
    {
        Assert.That(combatState.ManualTarget, Is.Null);

        Execute(DirectionAim(Vector3.right));

        Assert.That(combatState.ManualTarget, Is.Null);
        Assert.That(
            GetExecutionSource(),
            Does.Not.Contain("ShipAutoTargetSelector")
        );
    }


    [Test]
    public void ShipFireEligibility_RemainsReadOnlyQueryOwner()
    {
        Assert.That(
            typeof(ShipFireEligibility).GetMethod("TryExecuteBlindFire"),
            Is.Null
        );

        string source = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipFireEligibility.cs"
        ));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("SetAutoFireEnabled"));
        Assert.That(source, Does.Not.Contain("AssignManualTarget"));
        Assert.That(source, Does.Not.Contain("ClearManualTarget"));
    }


    [Test]
    public void Execution_DoesNotWriteMovementOrShipRootTransform()
    {
        shooterRoot.transform.SetPositionAndRotation(
            new Vector3(12f, 3f, -8f),
            Quaternion.Euler(0f, 35f, 0f)
        );
        Vector3 position = shooterRoot.transform.position;
        Quaternion rotation = shooterRoot.transform.rotation;
        BlindFireAim aim = DirectionAim(shooterRoot.transform.right);

        Execute(aim);

        Assert.That(shooterRoot.transform.position, Is.EqualTo(position));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(GetExecutionSource(), Does.Not.Contain(
            "ShipDestinationController"
        ));
        Assert.That(GetExecutionSource(), Does.Not.Contain("ShipTurning"));
        Assert.That(GetExecutionSource(), Does.Not.Contain("transform.position ="));
        Assert.That(GetExecutionSource(), Does.Not.Contain("transform.rotation ="));
    }


    [Test]
    public void Execution_CreatesNoProjectile()
    {
        Assert.That(GetExecutionSource(), Does.Not.Contain("Projectile"));
    }


    [Test]
    public void Execution_CreatesNoShotSample()
    {
        Assert.That(GetExecutionSource(), Does.Not.Contain("ShotSample"));
    }


    [Test]
    public void Execution_EmitsNoVfx()
    {
        string source = GetExecutionSource();

        Assert.That(source, Does.Not.Contain("CombatVFX"));
        Assert.That(source, Does.Not.Contain("MuzzleFire"));
    }


    [Test]
    public void Execution_AppliesNoDamage()
    {
        Assert.That(GetExecutionSource(), Does.Not.Contain("Damage"));
        Assert.That(GetExecutionSource(), Does.Not.Contain("Integrity"));
    }


    [Test]
    public void ExecutionResult_IsImmutableReadOnlyValue()
    {
        Type type = typeof(BlindFireExecutionResult);

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
        Assert.That(
            typeof(BlindFireExecutionFailure).IsDefined(
                typeof(FlagsAttribute),
                false
            ),
            Is.True
        );
    }


    [Test]
    public void OppositeSideReloadTimer_RemainsUnchangedByCommit()
    {
        BlindFireAim starboardAim = DirectionAim(Vector3.right);
        combatState.TryCommitBroadsideFire(CombatSide.Port);
        AdvanceReloads(2f);
        float portRemaining = combatState.PortReloadRemainingSeconds;

        Execute(starboardAim);

        Assert.That(combatState.PortReloadRemainingSeconds, Is.EqualTo(
            portRemaining
        ));
    }


    [Test]
    public void AcceptedResult_PreservesExactAimBasisWithoutDispersion()
    {
        Vector3 supplied = new Vector3(4f, 8f, 0f);
        BlindFireAim aim = DirectionAim(supplied);

        BlindFireExecutionResult result = Execute(aim);

        Assert.That(result.Aim.WorldAimDirection, Is.EqualTo(Vector3.right));
        Assert.That(
            result.Eligibility.Aim.WorldAimDirection,
            Is.EqualTo(result.Aim.WorldAimDirection)
        );
        Assert.That(GetExecutionSource(), Does.Not.Contain("Random"));
        Assert.That(GetExecutionSource(), Does.Not.Contain("Dispersion"));
    }


    private BlindFireExecutionResult Execute(
        BlindFireAim aim,
        bool expectedAccepted = true
    )
    {
        bool accepted = command.TryExecuteBlindFire(
            aim,
            out BlindFireExecutionResult result
        );

        Assert.That(accepted, Is.EqualTo(expectedAccepted));
        Assert.That(result.Accepted, Is.EqualTo(expectedAccepted));
        return result;
    }


    private BlindFireAim PointAim(Vector3 worldPoint)
    {
        bool evaluated = eligibility.TryEvaluateBlindFireAtPoint(
            worldPoint,
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result.Aim;
    }


    private BlindFireAim DirectionAim(Vector3 worldDirection)
    {
        bool evaluated = eligibility.TryEvaluateBlindFireDirection(
            worldDirection,
            out BlindFireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result.Aim;
    }


    private void AdvanceReloads(float elapsedSeconds)
    {
        MethodInfo method = typeof(ShipCombatState).GetMethod(
            "AdvanceReloads",
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null);
        method.Invoke(combatState, new object[] { elapsedSeconds });
    }


    private void AssertRejectedWithoutReload(
        BlindFireExecutionResult result
    )
    {
        Assert.That(result.Accepted, Is.False);
        Assert.That(result.FailureReasons.HasFlag(
            BlindFireExecutionFailure.EligibilityRejected
        ), Is.True);
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
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


    private static string GetExecutionSource()
    {
        string path = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipBlindFireCommand.cs"
        );
        string source = File.ReadAllText(path);
        int start = source.IndexOf(
            "public bool TryExecuteBlindFire",
            StringComparison.Ordinal
        );
        int end = source.LastIndexOf('}');

        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return source.Substring(start, end - start);
    }
}
