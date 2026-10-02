using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShipAutoFireControllerPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private readonly List<GameObject> created = new List<GameObject>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (CombatProjectile projectile in Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include))
        {
            Object.Destroy(projectile.gameObject);
        }

        foreach (GameObject root in created)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        created.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator FormalShip_OneSideFiresThirteenAndRetainsAutoMode()
    {
#if UNITY_EDITOR
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        yield return null;
        Physics.SyncTransforms();
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipAutoFireController controller = shooter.GetComponent<ShipAutoFireController>();
        state.SetAutoFireEnabled(true);

        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        Assert.That(controller.LastAutoTargetSelectionResult.StarboardSelection
            .TargetShipRoot, Is.SameAs(target));
        Assert.That(controller.LastStarboardExecutionResult.Accepted, Is.True);
        Assert.That(controller.LastStarboardExecutionResult.BroadsideExecution
            .ShotCount, Is.EqualTo(13));
        Assert.That(controller.LastStarboardExecutionResult.BroadsideExecution
            .SpawnedProjectileCount, Is.EqualTo(13));
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(state.AutoFireEnabled, Is.True);
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator FormalShip_ReloadSuppressesThenAllowsRepeat()
    {
#if UNITY_EDITOR
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        yield return null;
        Physics.SyncTransforms();
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipAutoFireController controller = shooter.GetComponent<ShipAutoFireController>();
        state.SetAutoFireEnabled(true);
        float interval = controller.EvaluationIntervalSeconds;

        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(controller.TryRunAutoFire(interval), Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        AdvanceReload(state);
        Assert.That(controller.TryRunAutoFire(interval * 2f), Is.True);
        Assert.That(controller.LastStarboardExecutionResult.Accepted, Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(26));
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator FormalShip_TwoSidesFireTwentySixInOneEvaluation()
    {
#if UNITY_EDITOR
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.left * 100f);
        CreateShip(2, Vector3.right * 100f);
        yield return null;
        Physics.SyncTransforms();
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        ShipAutoFireController controller = shooter.GetComponent<ShipAutoFireController>();
        state.SetAutoFireEnabled(true);

        Assert.That(controller.TryRunAutoFire(0f), Is.True);
        Assert.That(controller.LastPortExecutionResult.Accepted, Is.True);
        Assert.That(controller.LastStarboardExecutionResult.Accepted, Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(26));
        Assert.That(state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator Deselection_DoesNotStopShipOwnedAutoFire()
    {
#if UNITY_EDITOR
        GameObject shooter = CreateShip(0, Vector3.zero);
        CreateShip(1, Vector3.right * 100f);
        GameObject selectionObject = new GameObject("Selection Manager");
        created.Add(selectionObject);
        ShipSelectionManager selection =
            selectionObject.AddComponent<ShipSelectionManager>();
        yield return null;
        Physics.SyncTransforms();
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        selection.SelectSingle(shooter.GetComponent<ShipDestinationController>());
        selection.ClearSelection();

        Assert.That(selection.PrimarySelectedShip, Is.Null);
        Assert.That(shooter.GetComponent<ShipAutoFireController>()
            .TryRunAutoFire(0f), Is.True);
        Assert.That(ProjectileCount(), Is.EqualTo(13));
        Assert.That(state.AutoFireEnabled, Is.True);
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator ManualTargetAssignment_InterruptsAutomaticFire()
    {
#if UNITY_EDITOR
        GameObject shooter = CreateShip(0, Vector3.zero);
        GameObject target = CreateShip(1, Vector3.right * 100f);
        yield return null;
        Physics.SyncTransforms();
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        state.SetAutoFireEnabled(true);
        Assert.That(state.AssignManualTarget(target), Is.True);

        Assert.That(shooter.GetComponent<ShipAutoFireController>()
            .TryRunAutoFire(0f), Is.False);
        Assert.That(state.AutoFireEnabled, Is.False);
        Assert.That(ProjectileCount(), Is.Zero);
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }

#if UNITY_EDITOR
    private GameObject CreateShip(int team, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject ship = Object.Instantiate(prefab);
        created.Add(ship);
        ship.transform.position = position;
        ShipIntegrity integrity = ship.GetComponent<ShipIntegrity>();
        Assert.That(integrity.TryInitialize(), Is.True);
        Assert.That(integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational));
        ship.GetComponent<CombatVFXPlaceholderReceiver>().VisualSpawningEnabled =
            false;
        FieldInfo teamField = typeof(ShipCombatAffiliation).GetField(
            "teamId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(teamField, Is.Not.Null);
        teamField.SetValue(ship.GetComponent<ShipCombatAffiliation>(), team);
        return ship;
    }

    private static void AdvanceReload(ShipCombatState state)
    {
        MethodInfo advance = typeof(ShipCombatState).GetMethod(
            "AdvanceReloads", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(advance, Is.Not.Null);
        advance.Invoke(state,
            new object[] { state.BroadsideReloadDurationSeconds });
    }

    private static int ProjectileCount()
    {
        return Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include).Length;
    }
#endif
}
