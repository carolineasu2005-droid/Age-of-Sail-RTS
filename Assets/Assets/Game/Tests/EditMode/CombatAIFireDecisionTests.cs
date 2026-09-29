using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatAIFireDecisionTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_CombatAI_Foundation.asset";
    private const string IslandPrefabPath =
        "Assets/Assets/Game/Combat/Prefabs/PF_Debug_CombatIsland_v01.prefab";

    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (CombatProjectile projectile in Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include
        ))
        {
            Object.DestroyImmediate(projectile.gameObject);
        }
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [TestCase(CombatSide.Port, -100f)]
    [TestCase(CombatSide.Starboard, 100f)]
    public void LegalSide_UsesTargetedPhysicalBroadside(
        CombatSide side,
        float targetX
    )
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * targetX);
        ShipCombatAIController ai = AddAI(shooter);
        ShipCombatState reload = shooter.GetComponent<ShipCombatState>();
        CombatVFXCountingReceiver receiver = CountMuzzleEvents(shooter);
        float targetIntegrity = target.GetComponent<ShipIntegrity>().CurrentIntegrity;
        ShipFireEligibility eligibility = shooter.GetComponent<ShipFireEligibility>();
        Assert.That(eligibility.TryEvaluate(target, true,
            out FireEligibilityResult playerVerdict), Is.True);
        Assert.That(playerVerdict.CanFire, Is.True);
        Assert.That(playerVerdict.Side, Is.EqualTo(side));

        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Engaging));
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(target));
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(receiver.MuzzleFireCount, Is.EqualTo(13));
        Assert.That(target.GetComponent<ShipIntegrity>().CurrentIntegrity,
            Is.EqualTo(targetIntegrity));
        Assert.That(side == CombatSide.Port
            ? reload.PortBroadsideState : reload.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(side == CombatSide.Port
            ? reload.StarboardBroadsideState : reload.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(ai.Think(0f), Is.False);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
    }

    [Test]
    public void BothLegalPolicy_PrefersPoseSideThenPortFallback()
    {
        Assert.That(CombatAIFireDecision.SelectSide(true, true, CombatSide.Port),
            Is.EqualTo(CombatSide.Port));
        Assert.That(CombatAIFireDecision.SelectSide(true, true, CombatSide.Starboard),
            Is.EqualTo(CombatSide.Starboard));
        Assert.That(CombatAIFireDecision.SelectSide(true, true, null),
            Is.EqualTo(CombatSide.Port));
        Assert.That(CombatAIFireDecision.SelectSide(true, false, CombatSide.Starboard),
            Is.EqualTo(CombatSide.Port));
        Assert.That(CombatAIFireDecision.SelectSide(false, true, CombatSide.Port),
            Is.EqualTo(CombatSide.Starboard));
        Assert.That(CombatAIFireDecision.SelectSide(false, false, null), Is.Null);
    }

    [TestCase(600f, 0f, FireEligibilityFailure.BeyondMaximumRange)]
    [TestCase(0f, 100f, FireEligibilityFailure.NoBroadsideArc)]
    public void PlayerVerdictRejected_AIHoldsFire(
        float targetX,
        float targetZ,
        FireEligibilityFailure reason
    )
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, new Vector3(targetX, 0f, targetZ));
        ShipCombatAIController ai = AddAI(shooter);
        CombatVFXCountingReceiver receiver = CountMuzzleEvents(shooter);
        Assert.That(shooter.GetComponent<ShipFireEligibility>().TryEvaluate(
            target, true, out FireEligibilityResult verdict), Is.True);
        Assert.That(verdict.FailureReasons.HasFlag(reason), Is.True);
        Assert.That(verdict.CanFire, Is.False);

        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Maneuvering));
        AssertNoPhysicalFire(shooter, receiver);
    }

    [Test]
    public void Reloading_HoldsFireWhileMovementDestinationContinues()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        ShipCombatAIController ai = AddAI(shooter);
        ShipCombatState reload = shooter.GetComponent<ShipCombatState>();
        CombatVFXCountingReceiver receiver = CountMuzzleEvents(shooter);
        ShipDestinationController destination =
            shooter.GetComponent<ShipDestinationController>();
        Assert.That(ai.Think(0f), Is.True);
        Assert.That(destination.HasDestination, Is.True);
        int fired = ProjectileCount();
        int muzzleEvents = receiver.MuzzleFireCount;
        float remaining = reload.StarboardReloadRemainingSeconds;
        Assert.That(shooter.GetComponent<ShipFireEligibility>().TryEvaluate(
            target, true, out FireEligibilityResult playerVerdict), Is.True);
        Assert.That(playerVerdict.FailureReasons.HasFlag(
            FireEligibilityFailure.BroadsideReloading), Is.True);

        Assert.That(ai.Think(0.25f), Is.True);
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Maneuvering));
        Assert.That(destination.HasDestination, Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(fired));
        Assert.That(receiver.MuzzleFireCount, Is.EqualTo(muzzleEvents));
        Assert.That(reload.StarboardReloadRemainingSeconds, Is.EqualTo(remaining));

        MethodInfo advance = typeof(ShipCombatState).GetMethod(
            "AdvanceReloads", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(advance, Is.Not.Null);
        advance.Invoke(reload, new object[] { reload.BroadsideReloadDurationSeconds });
        Assert.That(ai.Think(0.5f), Is.True);
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Engaging));
        Assert.That(ProjectileCount(), Is.EqualTo(fired + 13));
        Assert.That(receiver.MuzzleFireCount, Is.EqualTo(muzzleEvents + 13));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ShipObstruction_UsesPlayerHoldFire(int blockerTeam)
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        CreateShip(blockerTeam, Vector3.right * 50f);
        AssertBlockedAndNoFire(shooter, target);
    }

    [Test]
    public void StickyTarget_HostileThirdShipBlocksNextReadyBroadside()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        ShipCombatAIController ai = AddAI(shooter);
        CombatVFXCountingReceiver receiver = CountMuzzleEvents(shooter);
        ShipCombatState reload = shooter.GetComponent<ShipCombatState>();

        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(target));
        Assert.That(ProjectileCount(), Is.EqualTo(13));

        MethodInfo advance = typeof(ShipCombatState).GetMethod(
            "AdvanceReloads", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(advance, Is.Not.Null);
        advance.Invoke(reload, new object[] { reload.BroadsideReloadDurationSeconds });
        Assert.That(reload.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
        CreateShip(2, Vector3.right * 50f);
        Physics.SyncTransforms();

        Assert.That(ai.Think(0.25f), Is.True);
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(target));
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Maneuvering));
        Assert.That(ai.HasLastFireEligibility, Is.True);
        Assert.That(ai.LastFireEligibility.FailureReasons.HasFlag(
            FireEligibilityFailure.Obstructed), Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(receiver.MuzzleFireCount, Is.EqualTo(13));
        Assert.That(reload.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready));
    }

    [Test]
    public void WorldObstruction_UsesPlayerHoldFire()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IslandPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject island = Object.Instantiate(prefab);
        created.Add(island);
        island.transform.position = Vector3.right * 50f;
        Physics.SyncTransforms();
        AssertBlockedAndNoFire(shooter, target);
    }

    [Test]
    public void DisabledTargetRemainsLegal_SinkingTargetReacquires()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject first = CreateShip(1, Vector3.right * 100f);
        GameObject second = CreateShip(2, Vector3.left * 120f);
        CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(
            first, ShipCombatLifecycleState.CombatDisabled);
        ShipCombatAIController ai = AddAI(shooter);
        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(first));
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Engaging));

        CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(
            first, ShipCombatLifecycleState.Sinking);
        Assert.That(ai.Think(0.25f), Is.True);
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(second));
        Assert.That(ai.LastTargetChangeReason,
            Is.EqualTo(CombatAITargetChangeReason.Sinking));
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Engaging));
        Assert.That(ProjectileCount(), Is.EqualTo(26));
    }

    [Test]
    public void TargetBecomingFriendly_IsReleasedBeforeNextFire()
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject first = CreateShip(1, Vector3.right * 100f);
        GameObject second = CreateShip(2, Vector3.left * 120f);
        ShipCombatAIController ai = AddAI(shooter);
        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(first));

        SetTeam(first, 0);
        Assert.That(ai.Think(0.25f), Is.True);
        Assert.That(ai.CurrentTargetShipRoot, Is.SameAs(second));
        Assert.That(ai.LastTargetChangeReason,
            Is.EqualTo(CombatAITargetChangeReason.RelationshipChanged));
        Assert.That(ProjectileCount(), Is.EqualTo(26));
    }

    [TestCase(ShipCombatLifecycleState.CombatDisabled)]
    [TestCase(ShipCombatLifecycleState.Sinking)]
    public void InvalidShooter_IsCombatIncapable(
        ShipCombatLifecycleState lifecycle
    )
    {
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        ShipCombatAIController ai = AddAI(shooter);
        CombatLifecycleTestUtility.SetLifecycleThroughIntegrityLoss(shooter, lifecycle);
        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.State, Is.EqualTo(CombatAIState.CombatIncapable));
        Assert.That(ai.CurrentTargetShipRoot, Is.Null);
        Assert.That(ProjectileCount(), Is.Zero);
    }

    [Test]
    public void AISource_DelegatesCombatAndHasNoPhysicalOrDamageShortcut()
    {
        string source = File.ReadAllText(
            "Assets/Assets/Game/Scripts/Combat/ShipCombatAIController.cs");
        Assert.That(source, Does.Contain("ShipFireEligibility"));
        Assert.That(source, Does.Contain("TryEvaluate("));
        Assert.That(source, Does.Contain("ShipTargetedFireCommand"));
        Assert.That(source, Does.Contain("TryExecuteWithRuntimeSeed("));
        Assert.That(source, Does.Not.Contain("TryEvaluateBlindFire"));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("CombatProjectile.TrySpawn"));
        Assert.That(source, Does.Not.Contain("Instantiate("));
        Assert.That(source, Does.Not.Contain("CombatDamageResolver"));
        Assert.That(source, Does.Not.Contain("TryApplyIntegrityLoss"));
    }

    private void AssertBlockedAndNoFire(GameObject shooter, GameObject target)
    {
        CombatVFXCountingReceiver receiver = CountMuzzleEvents(shooter);
        ShipCombatAIController ai = AddAI(shooter);
        Assert.That(shooter.GetComponent<ShipFireEligibility>().TryEvaluate(
            target, true, out FireEligibilityResult verdict), Is.True);
        Assert.That(verdict.FailureReasons.HasFlag(
            FireEligibilityFailure.Obstructed), Is.True);
        Assert.That(verdict.CanFire, Is.False);
        Assert.That(ai.Think(0f), Is.True);
        Assert.That(ai.State, Is.EqualTo(CombatAIState.Maneuvering));
        AssertNoPhysicalFire(shooter, receiver);
    }

    private static void AssertNoPhysicalFire(
        GameObject shooter,
        CombatVFXCountingReceiver receiver
    )
    {
        ShipCombatState reload = shooter.GetComponent<ShipCombatState>();
        Assert.That(ProjectileCount(), Is.Zero);
        Assert.That(receiver.MuzzleFireCount, Is.Zero);
        Assert.That(reload.PortBroadsideState, Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(reload.StarboardBroadsideState, Is.EqualTo(BroadsideReloadState.Ready));
    }

    private GameObject CreateShip(int team, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject ship = Object.Instantiate(prefab);
        created.Add(ship);
        ship.transform.position = position;
        CombatLifecycleTestUtility.EnsureOperational(ship);
        ship.GetComponent<CombatVFXPlaceholderReceiver>().VisualSpawningEnabled = false;
        SetTeam(ship, team);
        Physics.SyncTransforms();
        return ship;
    }

    private static void SetTeam(GameObject ship, int team)
    {
        SerializedObject affiliation = new SerializedObject(
            ship.GetComponent<ShipCombatAffiliation>());
        affiliation.FindProperty("teamId").intValue = team;
        affiliation.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ShipCombatAIController AddAI(GameObject shooter)
    {
        ShipCombatAIController ai = shooter.AddComponent<ShipCombatAIController>();
        SerializedObject serialized = new SerializedObject(ai);
        serialized.FindProperty("profile").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<CombatAIProfile>(ProfilePath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return ai;
    }

    private static CombatVFXCountingReceiver CountMuzzleEvents(GameObject shooter)
    {
        CombatVFXCountingReceiver receiver =
            shooter.AddComponent<CombatVFXCountingReceiver>();
        FieldInfo field = typeof(ShipBroadsideFireExecutor).GetField(
            "combatVFXReceiver", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(shooter.GetComponent<ShipBroadsideFireExecutor>(), receiver);
        return receiver;
    }

    private static int ProjectileCount()
    {
        return Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include).Length;
    }
}
