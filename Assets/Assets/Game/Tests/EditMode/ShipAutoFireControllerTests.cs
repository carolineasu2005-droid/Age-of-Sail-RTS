using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipAutoFireControllerTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const string AIPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_CombatAI_v01.prefab";
    private const string IslandPrefabPath =
        "Assets/Assets/Game/Combat/Prefabs/PF_Debug_CombatIsland_v01.prefab";

    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (CombatProjectile projectile in Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(projectile.gameObject);
        }

        for (int index = created.Count - 1; index >= 0; index--)
        {
            if (created[index] != null)
            {
                Object.DestroyImmediate(created[index]);
            }
        }

        created.Clear();
    }

    [Test]
    public void AutoFireOff_DoesNotEvaluateOrCommit()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipAutoFireController controller = Controller(shooter);

        Assert.That(controller.TryRunAutoFire(0f), Is.False);
        Assert.That(controller.EvaluationCount, Is.Zero);
        AssertNoFire(shooter);
    }

    [Test]
    public void Cadence_FirstEnabledCallIsImmediateAndLaterCallsAreThrottled()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        ShipAutoFireController controller = Controller(shooter);
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);
        float interval = controller.EvaluationIntervalSeconds;

        Assert.That(controller.TryRunAutoFire(10f), Is.True);
        Assert.That(controller.TryRunAutoFire(10f + interval * 0.5f), Is.False);
        Assert.That(controller.TryRunAutoFire(10f + interval), Is.True);
        Assert.That(controller.EvaluationCount, Is.EqualTo(2));
        Assert.That(controller.LastEvaluationTime, Is.EqualTo(10f + interval));
    }

    [Test]
    public void InvalidCadence_FallsBackToFinitePositiveFoundationValue()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        ShipAutoFireController controller = Controller(shooter);
        FieldInfo intervalField = typeof(ShipAutoFireController).GetField(
            "evaluationIntervalSeconds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(intervalField, Is.Not.Null);
        intervalField.SetValue(controller, float.NaN);
        Assert.That(controller.EvaluationIntervalSeconds, Is.EqualTo(0.25f));
        intervalField.SetValue(controller, -1f);
        Assert.That(controller.EvaluationIntervalSeconds, Is.EqualTo(0.25f));
    }

    [Test]
    public void NoCandidates_PreservesModeAndReadiness()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(state.AutoFireEnabled, Is.True);
        Assert.That(Controller(shooter).HasLastSelection, Is.False);
        AssertNoFire(shooter);
    }

    [Test]
    public void ManualTargetAssignment_StopsAutoFire()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        Assert.That(state.AssignManualTarget(target), Is.True);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.False);
        Assert.That(state.AutoFireEnabled, Is.False);
        Assert.That(state.ManualTarget, Is.SameAs(target));
        AssertNoFire(shooter);
    }

    [TestCase(CombatSide.Port, -100f)]
    [TestCase(CombatSide.Starboard, 100f)]
    public void OneLegalSide_UsesExistingPhysicalCommand(
        CombatSide side, float targetX)
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * targetX);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        ShipAutoFireController controller = Controller(shooter);

        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        TargetedFireExecutionResult execution = side == CombatSide.Port
            ? controller.LastPortExecutionResult
            : controller.LastStarboardExecutionResult;
        Assert.That(execution.Accepted, Is.True);
        Assert.That(execution.TargetShipRoot, Is.SameAs(target));
        Assert.That(execution.BroadsideExecution.Side, Is.EqualTo(side));
        Assert.That(execution.BroadsideExecution.ShotCount, Is.EqualTo(13));
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(side == CombatSide.Port ? state.PortBroadsideState
            : state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(side == CombatSide.Port ? state.StarboardBroadsideState
            : state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
    }

    [Test]
    public void BothSides_FireInOneEvaluationAndReloadIndependently()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.left * 100f);
        CreateShip(2, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        ShipAutoFireController controller = Controller(shooter);
        AutoFireMuzzleOrderReceiver receiver =
            shooter.AddComponent<AutoFireMuzzleOrderReceiver>();
        FieldInfo receiverField = typeof(ShipBroadsideFireExecutor).GetField(
            "combatVFXReceiver", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(receiverField, Is.Not.Null);
        receiverField.SetValue(
            shooter.GetComponent<ShipBroadsideFireExecutor>(), receiver);

        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        Assert.That(controller.LastPortExecutionResult.Accepted, Is.True);
        Assert.That(controller.LastStarboardExecutionResult.Accepted, Is.True);
        Assert.That(controller.LastPortExecutionResult.BroadsideExecution.Side,
            Is.EqualTo(CombatSide.Port));
        Assert.That(controller.LastStarboardExecutionResult.BroadsideExecution.Side,
            Is.EqualTo(CombatSide.Starboard));
        Assert.That(ProjectileCount(), Is.EqualTo(26));
        Assert.That(receiver.Sides, Has.Count.EqualTo(26));
        for (int index = 0; index < 26; index++)
        {
            Assert.That(receiver.Sides[index], Is.EqualTo(index < 13
                ? CombatSide.Port : CombatSide.Starboard));
        }
        Assert.That(state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
    }

    [Test]
    public void ReloadingPort_DoesNotBlockReadyStarboard()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.left * 100f);
        CreateShip(2, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Assert.That(state.TryCommitBroadsideFire(CombatSide.Port), Is.True);
        state.SetAutoFireEnabled(true);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(Controller(shooter).LastPortCommandAttempted, Is.False);
        Assert.That(Controller(shooter).LastStarboardExecutionResult.Accepted,
            Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
    }

    [TestCase(600f, 0f)]
    [TestCase(0f, 100f)]
    public void IllegalRangeOrArc_NeverFires(float x, float z)
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, new Vector3(x, 0f, z));
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(Controller(shooter).HasLastSelection, Is.False);
        AssertNoFire(shooter);
    }

    [Test]
    public void PhysicalObstruction_NeverFires()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        GameObject island = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(IslandPrefabPath));
        created.Add(island);
        island.transform.position = Vector3.right * 50f;
        Physics.SyncTransforms();
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(Controller(shooter).HasLastSelection, Is.False);
        AssertNoFire(shooter);
    }

    [Test]
    public void SinkingTarget_IsRejectedBySelector()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(
            target, ShipCombatLifecycleState.Sinking);
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);
        Assert.That(ShipAutoTargetCandidateProvider.TryCollect(shooter,
            out IReadOnlyList<AutoTargetCandidate> candidates), Is.True);
        Assert.That(candidates, Has.Count.EqualTo(1));
        Assert.That(candidates[0].TargetShipRoot, Is.SameAs(target));

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(Controller(shooter).HasLastSelection, Is.False);
        AssertNoFire(shooter);
    }

    [Test]
    public void TargetedSuccess_LeavesAutoFireOn()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(Controller(shooter).LastStarboardExecutionResult.Accepted,
            Is.True);
        Assert.That(state.AutoFireEnabled, Is.True);
        Assert.That(state.ManualTarget, Is.Null);
    }

    [Test]
    public void ReloadCompletion_AllowsLaterAutomaticBroadside()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        ShipAutoFireController controller = Controller(shooter);
        float interval = controller.EvaluationIntervalSeconds;

        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(controller.TryRunAutoFire(interval), Is.True);
        Assert.That(controller.HasLastSelection, Is.False);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        AdvanceReload(state);
        Assert.That(controller.TryRunAutoFire(2f * interval), Is.True);
        Assert.That(controller.LastStarboardExecutionResult.Accepted, Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(26));
    }

    [Test]
    public void DisablingDuringReload_PreventsRepeatAfterReady()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        ShipAutoFireController controller = Controller(shooter);
        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        state.SetAutoFireEnabled(false);
        AdvanceReload(state);

        Assert.That(controller.TryRunAutoFire(1f), Is.False);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
    }

    [Test]
    public void Reenable_ResumesOnNextObservedCall()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipAutoFireController controller = Controller(shooter);
        state.SetAutoFireEnabled(true);
        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        AdvanceReload(state);
        state.SetAutoFireEnabled(false);
        Assert.That(controller.TryRunAutoFire(0.1f), Is.False);
        state.SetAutoFireEnabled(true);

        Assert.That(controller.TryRunAutoFire(0.11f), Is.True);
        Assert.That(controller.LastStarboardExecutionResult.Accepted, Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(26));
    }

    [Test]
    public void SourceCandidateHandoff_UsesExactIndexIdentityAndRelationship()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        GameObject other = CreateShip(1, Vector3.left * 100f);
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);
        var candidates = new List<AutoTargetCandidate>
        {
            new AutoTargetCandidate(other, true),
            new AutoTargetCandidate(target, true)
        };
        Assert.That(ShipAutoTargetSelector.TrySelect(shooter, candidates,
            out AutoTargetSelectionResult selection), Is.True);
        AutoTargetSideSelectionResult side = selection.StarboardSelection;
        Assert.That(side.CandidateIndex, Is.EqualTo(1));

        MethodInfo resolve = typeof(ShipAutoFireController).GetMethod(
            "TryResolveSourceCandidate",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(resolve, Is.Not.Null);
        object[] arguments = { candidates, side, null };
        Assert.That((bool)resolve.Invoke(null, arguments), Is.True);
        AutoTargetCandidate resolved = (AutoTargetCandidate)arguments[2];
        Assert.That(resolved.TargetShipRoot, Is.SameAs(target));
        Assert.That(resolved.RelationshipAllowsFire, Is.True);

        var wrongRoot = new List<AutoTargetCandidate>
        {
            candidates[0], new AutoTargetCandidate(other, true)
        };
        Assert.That((bool)resolve.Invoke(null,
            new object[] { wrongRoot, side, null }), Is.False);
        Assert.That((bool)resolve.Invoke(null,
            new object[] { new List<AutoTargetCandidate>(), side, null }),
            Is.False);
        var denied = new List<AutoTargetCandidate>
        {
            candidates[0], new AutoTargetCandidate(target, false)
        };
        object[] deniedArguments = { denied, side, null };
        Assert.That((bool)resolve.Invoke(null, deniedArguments), Is.True);
        AutoTargetCandidate deniedCandidate =
            (AutoTargetCandidate)deniedArguments[2];
        Assert.That(deniedCandidate.RelationshipAllowsFire, Is.False);
        var command = new ShipTargetedFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            shooter.GetComponent<ShipBroadsideFireExecutor>());
        Assert.That(command.TryExecuteWithRuntimeSeed(
            deniedCandidate.TargetShipRoot,
            deniedCandidate.RelationshipAllowsFire,
            out TargetedFireExecutionResult deniedResult), Is.False);
        Assert.That(deniedResult.Accepted, Is.False);
        AssertNoFire(shooter);
    }

    [Test]
    public void AutoFire_DoesNotWriteMovementOrRootPose()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipDestinationController destination =
            shooter.GetComponent<ShipDestinationController>();
        ShipHeadingController heading =
            shooter.GetComponent<ShipHeadingController>();
        ShipTurning turning = shooter.GetComponent<ShipTurning>();
        ShipSailingSpeed sailing = shooter.GetComponent<ShipSailingSpeed>();
        Vector3 position = shooter.transform.position;
        Quaternion rotation = shooter.transform.rotation;
        bool hadDestination = destination.HasDestination;
        ShipDestinationController.NavigationMode navigationMode =
            destination.CurrentNavigationMode;
        float targetHeading = heading.TargetHeading;
        float turningIntensity = turning.TurningIntensity;
        float effectiveSpeed = sailing.EffectiveTargetSpeed;
        bool formationCap = sailing.FormationSpeedCapActive;
        shooter.GetComponent<ShipCombatState>().SetAutoFireEnabled(true);

        Assert.That(Controller(shooter).TryRunAutoFire(0f), Is.True);
        Assert.That(destination.HasDestination, Is.EqualTo(hadDestination));
        Assert.That(destination.CurrentNavigationMode, Is.EqualTo(navigationMode));
        Assert.That(heading.TargetHeading, Is.EqualTo(targetHeading));
        Assert.That(turning.TurningIntensity, Is.EqualTo(turningIntensity));
        Assert.That(sailing.EffectiveTargetSpeed, Is.EqualTo(effectiveSpeed));
        Assert.That(sailing.FormationSpeedCapActive, Is.EqualTo(formationCap));
        Assert.That(shooter.transform.position, Is.EqualTo(position));
        Assert.That(shooter.transform.rotation, Is.EqualTo(rotation));
    }

    [Test]
    public void FormalPrefab_HasOneRootControllerAndGenericProxiesHaveNone()
    {
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath);
        GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            AIPrefabPath);
        Assert.That(basePrefab, Is.Not.Null);
        Assert.That(aiPrefab, Is.Not.Null);
        Assert.That(basePrefab.GetComponents<ShipAutoFireController>(),
            Has.Length.EqualTo(1));
        Assert.That(aiPrefab.GetComponents<ShipAutoFireController>(),
            Has.Length.EqualTo(1));
        Assert.That(basePrefab.GetComponent<ShipCombatState>().AutoFireEnabled,
            Is.False);
        Assert.That(aiPrefab.GetComponent<ShipCombatState>().AutoFireEnabled,
            Is.False);
        foreach (string name in new[] { "Light", "Medium", "Heavy" })
        {
            GameObject proxy = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Assets/Game/Ship/Proxy/PF_Proxy_" + name
                + "_v01.prefab");
            Assert.That(proxy, Is.Not.Null);
            Assert.That(proxy.GetComponent<ShipAutoFireController>(), Is.Null);
        }
    }

    [Test]
    public void SourceBoundary_UsesProviderSelectorAndTargetedCommandOnly()
    {
        string source = File.ReadAllText(
            "Assets/Assets/Game/Scripts/Combat/ShipAutoFireController.cs");
        Assert.That(source, Does.Contain("ShipAutoTargetCandidateProvider.TryCollect"));
        Assert.That(source, Does.Contain("ShipAutoTargetSelector.TrySelect"));
        Assert.That(source, Does.Contain("TryExecuteWithRuntimeSeed"));
        Assert.That(source, Does.Not.Contain("ShipSelectionManager"));
        Assert.That(source, Does.Not.Contain("CombatAIProfile"));
        Assert.That(source, Does.Not.Contain("SetDestination("));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire("));
    }

    private GameObject CreateShip(int team, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject ship = Object.Instantiate(prefab);
        created.Add(ship);
        ship.transform.position = position;
        CombatLifecycleTestUtility.EnsureOperational(ship);
        ship.GetComponent<CombatVFXPlaceholderReceiver>().VisualSpawningEnabled =
            false;
        SerializedObject affiliation = new SerializedObject(
            ship.GetComponent<ShipCombatAffiliation>());
        affiliation.FindProperty("teamId").intValue = team;
        affiliation.ApplyModifiedPropertiesWithoutUndo();
        Physics.SyncTransforms();
        return ship;
    }

    private static ShipAutoFireController Controller(GameObject ship)
    {
        ShipAutoFireController controller =
            ship.GetComponent<ShipAutoFireController>();
        Assert.That(controller, Is.Not.Null);
        return controller;
    }

    private static void AdvanceReload(ShipCombatState state)
    {
        MethodInfo advance = typeof(ShipCombatState).GetMethod(
            "AdvanceReloads", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(advance, Is.Not.Null);
        advance.Invoke(state,
            new object[] { state.BroadsideReloadDurationSeconds });
    }

    private static void AssertNoFire(GameObject shooter)
    {
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Assert.That(state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(ProjectileCount(), Is.Zero);
    }

    private static int ProjectileCount()
    {
        return Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include).Length;
    }
}
