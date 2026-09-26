using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipLifecycleFireEligibilityTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";

    private readonly System.Collections.Generic.List<GameObject> created =
        new System.Collections.Generic.List<GameObject>();


    [TearDown]
    public void TearDown()
    {
        foreach (CombatProjectile projectile in
            UnityEngine.Object.FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ))
        {
            UnityEngine.Object.DestroyImmediate(projectile.gameObject);
        }

        for (int index = created.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(created[index]);
        }

        created.Clear();
    }


    [Test]
    public void OperationalShooter_LifecycleAllowsTargetedAndBlindFireQueries()
    {
        GameObject shooter = CreateShip("Operational Shooter", Vector3.zero);
        GameObject target = CreateShip(
            "Operational Target",
            Vector3.right * 100f
        );
        ShipFireEligibility eligibility =
            shooter.GetComponent<ShipFireEligibility>();

        Assert.That(eligibility.TryEvaluate(
            target,
            true,
            out FireEligibilityResult targeted
        ), Is.True);
        Assert.That(targeted.LifecycleAllowsFire, Is.True);
        Assert.That(targeted.TargetLifecycleLegal, Is.True);
        Assert.That(targeted.CanFire, Is.True);

        Assert.That(eligibility.TryEvaluateBlindFireAtPoint(
            Vector3.right * 100f,
            out BlindFireEligibilityResult blind
        ), Is.True);
        Assert.That(blind.LifecycleAllowsFire, Is.True);
        Assert.That(blind.CanBlindFire, Is.True);
    }


    [TestCase(ShipCombatLifecycleState.CombatDisabled)]
    [TestCase(ShipCombatLifecycleState.Sinking)]
    public void NonOperationalShooter_TargetedCommandRejectsWithoutSideEffects(
        ShipCombatLifecycleState lifecycle
    )
    {
        GameObject shooter = CreateShip("Rejected Shooter", Vector3.zero);
        GameObject target = CreateShip(
            "Target",
            Vector3.right * 100f
        );
        SetLifecycle(shooter, lifecycle);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipTargetedFireCommand command = new ShipTargetedFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            shooter.GetComponent<ShipBroadsideFireExecutor>()
        );
        int projectileCount = FindProjectileCount();

        bool accepted = command.TryExecute(
            target,
            true,
            123u,
            out TargetedFireExecutionResult result
        );

        Assert.That(accepted, Is.False);
        Assert.That(result.Eligibility.LifecycleAllowsFire, Is.False);
        Assert.That(
            result.Eligibility.FailureReasons.HasFlag(
                FireEligibilityFailure.LifecycleDisallowsFire
            ),
            Is.True
        );
        Assert.That(result.BroadsideExecution.ShotCount, Is.Zero);
        AssertReloadUnchanged(state);
        Assert.That(FindProjectileCount(), Is.EqualTo(projectileCount));
    }


    [TestCase(ShipCombatLifecycleState.CombatDisabled)]
    [TestCase(ShipCombatLifecycleState.Sinking)]
    public void NonOperationalShooter_BlindCommandRejectsWithoutSideEffects(
        ShipCombatLifecycleState lifecycle
    )
    {
        GameObject shooter = CreateShip("Blind Shooter", Vector3.zero);
        SetLifecycle(shooter, lifecycle);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipBlindFireCommand command = new ShipBlindFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            state,
            shooter.GetComponent<ShipBroadsideFireExecutor>()
        );
        Assert.That(
            shooter.GetComponent<ShipFireEligibility>()
                .TryEvaluateBlindFireAtPoint(
                    Vector3.right * 100f,
                    out BlindFireEligibilityResult preview
                ),
            Is.True
        );
        BlindFireAim aim = preview.Aim;
        int projectileCount = FindProjectileCount();

        bool accepted = command.TryExecuteBlindFire(
            aim,
            456u,
            out BlindFireExecutionResult result
        );

        Assert.That(accepted, Is.False);
        Assert.That(result.Eligibility.LifecycleAllowsFire, Is.False);
        Assert.That(
            result.Eligibility.FailureReasons.HasFlag(
                BlindFireEligibilityFailure.LifecycleDisallowsFire
            ),
            Is.True
        );
        Assert.That(result.BroadsideExecution.ShotCount, Is.Zero);
        AssertReloadUnchanged(state);
        Assert.That(FindProjectileCount(), Is.EqualTo(projectileCount));
    }


    [TestCase(ShipCombatLifecycleState.Operational, true)]
    [TestCase(ShipCombatLifecycleState.CombatDisabled, true)]
    [TestCase(ShipCombatLifecycleState.Sinking, false)]
    public void TargetLifecycle_DeterminesNewTargetLegality(
        ShipCombatLifecycleState targetLifecycle,
        bool expectedLegal
    )
    {
        GameObject shooter = CreateShip("Shooter", Vector3.zero);
        GameObject target = CreateShip(
            "Lifecycle Target",
            Vector3.right * 100f
        );
        SetLifecycle(target, targetLifecycle);
        ShipFireEligibility eligibility =
            shooter.GetComponent<ShipFireEligibility>();

        Assert.That(eligibility.TryEvaluate(
            target,
            true,
            out FireEligibilityResult result
        ), Is.True);
        Assert.That(result.TargetLifecycleLegal, Is.EqualTo(expectedLegal));
        Assert.That(result.TargetLegal, Is.EqualTo(expectedLegal));
        Assert.That(result.CanFire, Is.EqualTo(expectedLegal));
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.TargetLifecycleIllegal
            ),
            Is.EqualTo(!expectedLegal)
        );
    }


    [Test]
    public void ManualTarget_RemainsStoredAfterTargetSinksButCannotFire()
    {
        GameObject shooter = CreateShip("Manual Shooter", Vector3.zero);
        GameObject target = CreateShip(
            "Manual Target",
            Vector3.right * 100f
        );
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Assert.That(state.AssignManualTarget(target), Is.True);
        SetLifecycle(target, ShipCombatLifecycleState.Sinking);

        Assert.That(state.ManualTarget, Is.SameAs(target));
        Assert.That(
            shooter.GetComponent<ShipFireEligibility>().TryEvaluate(
                target,
                true,
                out FireEligibilityResult eligibility
            ),
            Is.True
        );
        Assert.That(eligibility.CanFire, Is.False);
        Assert.That(eligibility.TargetLifecycleLegal, Is.False);

        ShipTargetedFireCommand command = new ShipTargetedFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            shooter.GetComponent<ShipBroadsideFireExecutor>()
        );
        Assert.That(command.TryExecute(
            target,
            true,
            789u,
            out TargetedFireExecutionResult result
        ), Is.False);
        Assert.That(result.BroadsideExecution.ShotCount, Is.Zero);
        Assert.That(state.ManualTarget, Is.SameAs(target));
        AssertReloadUnchanged(state);
        Assert.That(FindProjectileCount(), Is.Zero);
    }


    [Test]
    public void AutoTarget_IncludesDisabledAndExcludesSinkingCandidates()
    {
        GameObject shooter = CreateShip("Auto Shooter", Vector3.zero);
        GameObject disabled = CreateShip(
            "Disabled Candidate",
            Vector3.right * 100f
        );
        GameObject sinking = CreateShip(
            "Sinking Candidate",
            Vector3.left * 100f
        );
        SetLifecycle(disabled, ShipCombatLifecycleState.CombatDisabled);
        SetLifecycle(sinking, ShipCombatLifecycleState.Sinking);
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);

        bool selected = ShipAutoTargetSelector.TrySelect(
            shooter,
            new[]
            {
                new AutoTargetCandidate(disabled, true),
                new AutoTargetCandidate(sinking, true)
            },
            out AutoTargetSelectionResult result
        );

        Assert.That(selected, Is.True);
        Assert.That(result.StarboardSelection.HasTarget, Is.True);
        Assert.That(
            result.StarboardSelection.TargetShipRoot,
            Is.SameAs(disabled)
        );
        Assert.That(result.PortSelection.HasTarget, Is.False);
    }


    [Test]
    public void CombatDisabledTarget_DoesNotChangeAutoTargetScoreFactors()
    {
        GameObject shooter = CreateShip("Score Shooter", Vector3.zero);
        GameObject target = CreateShip(
            "Score Target",
            Vector3.right * 100f
        );
        ShipFireEligibility evaluator =
            shooter.GetComponent<ShipFireEligibility>();
        FireEligibilityResult operationalEligibility = Evaluate(
            evaluator,
            target
        );
        Assert.That(ShipAutoTargetScorer.TryEvaluate(
            shooter,
            target,
            operationalEligibility,
            out AutoTargetScoreResult operationalScore
        ), Is.True);

        SetLifecycle(target, ShipCombatLifecycleState.CombatDisabled);
        FireEligibilityResult disabledEligibility = Evaluate(
            evaluator,
            target
        );
        Assert.That(ShipAutoTargetScorer.TryEvaluate(
            shooter,
            target,
            disabledEligibility,
            out AutoTargetScoreResult disabledScore
        ), Is.True);

        Assert.That(
            disabledScore.ExposureNormalized,
            Is.EqualTo(operationalScore.ExposureNormalized)
        );
        Assert.That(
            disabledScore.RangeQualityNormalized,
            Is.EqualTo(operationalScore.RangeQualityNormalized)
        );
        Assert.That(
            disabledScore.VisibilityQualityNormalized,
            Is.EqualTo(operationalScore.VisibilityQualityNormalized)
        );
        Assert.That(
            disabledScore.FinalScore,
            Is.EqualTo(operationalScore.FinalScore)
        );
    }


    [Test]
    public void LowerOperationalIntegrity_DoesNotChangeErvScore()
    {
        GameObject shooter = CreateShip(
            "Integrity Independent Score Shooter",
            Vector3.zero
        );
        GameObject target = CreateShip(
            "Integrity Independent Score Target",
            Vector3.right * 100f
        );
        ShipFireEligibility evaluator =
            shooter.GetComponent<ShipFireEligibility>();
        FireEligibilityResult fullEligibility = Evaluate(evaluator, target);
        Assert.That(ShipAutoTargetScorer.TryEvaluate(
            shooter,
            target,
            fullEligibility,
            out AutoTargetScoreResult fullScore
        ), Is.True);

        ShipIntegrity integrity = target.GetComponent<ShipIntegrity>();
        Assert.That(
            integrity.TryApplyIntegrityLoss(500f, out _),
            Is.True
        );
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
        FireEligibilityResult reducedEligibility = Evaluate(
            evaluator,
            target
        );
        Assert.That(ShipAutoTargetScorer.TryEvaluate(
            shooter,
            target,
            reducedEligibility,
            out AutoTargetScoreResult reducedScore
        ), Is.True);

        Assert.That(
            reducedScore.ExposureNormalized,
            Is.EqualTo(fullScore.ExposureNormalized)
        );
        Assert.That(
            reducedScore.RangeQualityNormalized,
            Is.EqualTo(fullScore.RangeQualityNormalized)
        );
        Assert.That(
            reducedScore.VisibilityQualityNormalized,
            Is.EqualTo(fullScore.VisibilityQualityNormalized)
        );
        Assert.That(
            reducedScore.FinalScore,
            Is.EqualTo(fullScore.FinalScore)
        );
    }


    [Test]
    public void LifecycleTransition_DoesNotDisableOrMutateMovementState()
    {
        GameObject ship = CreateShip(
            "Movement Isolation Ship",
            new Vector3(12f, 0f, -8f)
        );
        ship.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
        ShipSailingSpeed sailing = ship.GetComponent<ShipSailingSpeed>();
        ShipTurning turning = ship.GetComponent<ShipTurning>();
        ShipDestinationController destination =
            ship.GetComponent<ShipDestinationController>();
        Vector3 position = ship.transform.position;
        Quaternion rotation = ship.transform.rotation;
        float currentSpeed = sailing.CurrentSpeed;
        bool hasDestination = destination.HasDestination;
        ShipDestinationController.NavigationMode navigationMode =
            destination.CurrentNavigationMode;

        SetLifecycle(ship, ShipCombatLifecycleState.Sinking);

        Assert.That(ship.activeSelf, Is.True);
        Assert.That(sailing.enabled, Is.True);
        Assert.That(turning.enabled, Is.True);
        Assert.That(destination.enabled, Is.True);
        Assert.That(ship.GetComponent<ShipCombatState>().enabled, Is.True);
        Assert.That(ship.GetComponent<ShipFireEligibility>().enabled, Is.True);
        Assert.That(ship.transform.position, Is.EqualTo(position));
        Assert.That(ship.transform.rotation, Is.EqualTo(rotation));
        Assert.That(sailing.CurrentSpeed, Is.EqualTo(currentSpeed));
        Assert.That(destination.HasDestination, Is.EqualTo(hasDestination));
        Assert.That(
            destination.CurrentNavigationMode,
            Is.EqualTo(navigationMode)
        );
    }


    [Test]
    public void LifecycleRules_AreCentralizedOutsideCommandsScoringAndExecutor()
    {
        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string eligibilitySource = File.ReadAllText(Path.Combine(
            combatRoot,
            "ShipFireEligibility.cs"
        ));

        Assert.That(eligibilitySource, Does.Contain("ShipIntegrity"));
        Assert.That(eligibilitySource, Does.Contain(
            "ShipCombatLifecycleState.Operational"
        ));
        Assert.That(eligibilitySource, Does.Contain("IsSinking"));

        foreach (string fileName in new[]
        {
            "ShipTargetedFireCommand.cs",
            "ShipBlindFireCommand.cs",
            "ShipAutoTargetScorer.cs",
            "ShipAutoTargetSelector.cs",
            "ShipBroadsideFireExecutor.cs"
        })
        {
            string source = File.ReadAllText(Path.Combine(
                combatRoot,
                fileName
            ));
            Assert.That(source, Does.Not.Contain("LifecycleState"), fileName);
            Assert.That(source, Does.Not.Contain("CurrentIntegrity"), fileName);
            Assert.That(source, Does.Not.Contain("IsSinking"), fileName);
        }
    }


    private GameObject CreateShip(string name, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        CombatLifecycleTestUtility.EnsureOperational(instance);
        instance.name = name;
        instance.transform.position = position;
        CombatVFXPlaceholderReceiver receiver =
            instance.GetComponent<CombatVFXPlaceholderReceiver>();

        if (receiver != null)
        {
            receiver.VisualSpawningEnabled = false;
        }

        created.Add(instance);
        Physics.SyncTransforms();
        return instance;
    }


    private static void SetLifecycle(
        GameObject ship,
        ShipCombatLifecycleState lifecycle
    )
    {
        ShipIntegrity integrity =
            CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(
                ship,
                lifecycle
            );
        Assert.That(integrity.LifecycleState, Is.EqualTo(lifecycle));
    }


    private static FireEligibilityResult Evaluate(
        ShipFireEligibility evaluator,
        GameObject target
    )
    {
        Physics.SyncTransforms();
        Assert.That(evaluator.TryEvaluate(
            target,
            true,
            out FireEligibilityResult result
        ), Is.True);
        Assert.That(result.CanFire, Is.True);
        return result;
    }


    private static void AssertReloadUnchanged(ShipCombatState state)
    {
        Assert.That(
            state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(state.PortReloadRemainingSeconds, Is.Zero);
        Assert.That(state.StarboardReloadRemainingSeconds, Is.Zero);
    }


    private static int FindProjectileCount()
    {
        return UnityEngine.Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include
        ).Length;
    }
}
