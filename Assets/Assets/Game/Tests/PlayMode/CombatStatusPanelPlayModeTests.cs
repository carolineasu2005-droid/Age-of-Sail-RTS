using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CombatStatusPanelPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private readonly List<GameObject> created = new List<GameObject>();
    private ShipSelectionManager selection;
    private ShipPlayerCommandInput input;
    private CombatStatusPanel panel;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GameObject host = new GameObject("Combat Status Host");
        created.Add(host);
        selection = host.AddComponent<ShipSelectionManager>();
        host.AddComponent<ShipCommandDispatcher>();
        input = host.AddComponent<ShipPlayerCommandInput>();
        panel = host.AddComponent<CombatStatusPanel>();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject instance in created)
        {
            if (instance != null)
            {
                Object.Destroy(instance);
            }
        }

        yield return null;
        created.Clear();
    }

    [UnityTest]
    public IEnumerator FormalShips_BindSwitchAndDeselectWithoutFleetAggregation()
    {
        GameObject first = InstantiateCombatShip();
        GameObject second = InstantiateCombatShip();
        ShipDestinationController firstDestination =
            first.GetComponent<ShipDestinationController>();
        ShipDestinationController secondDestination =
            second.GetComponent<ShipDestinationController>();

        Assert.That(panel.CurrentStatus.HasSelection, Is.False);
        selection.SelectSingle(firstDestination);
        Assert.That(panel.CurrentStatus.ShipRoot, Is.SameAs(first));

        selection.AddSelection(secondDestination);
        Assert.That(panel.CurrentStatus.ShipRoot, Is.SameAs(first));

        selection.SelectSingle(secondDestination);
        Assert.That(panel.CurrentStatus.ShipRoot, Is.SameAs(second));

        selection.ClearSelection();
        Assert.That(panel.CurrentStatus.HasSelection, Is.False);
        Assert.That(panel.DisplayText, Is.Empty);
        yield return null;
    }

    [UnityTest]
    public IEnumerator FormalShip_IntegrityAndReloadChangesAppearInReadback()
    {
        GameObject ship = InstantiateCombatShip();
        selection.SelectSingle(ship.GetComponent<ShipDestinationController>());
        ShipIntegrity integrity = ship.GetComponent<ShipIntegrity>();
        ShipCombatState combat = ship.GetComponent<ShipCombatState>();
        Assert.That(integrity.IsInitialized, Is.True);

        Assert.That(integrity.TryApplyIntegrityLoss(100f, out _), Is.True);
        Assert.That(combat.TryCommitBroadsideFire(CombatSide.Port), Is.True);
        yield return null;

        Assert.That(panel.CurrentStatus.CurrentIntegrity,
            Is.EqualTo(integrity.CurrentIntegrity));
        Assert.That(panel.CurrentStatus.MaximumIntegrity,
            Is.EqualTo(integrity.MaximumIntegrity));
        Assert.That(panel.CurrentStatus.PortReloadState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(panel.CurrentStatus.StarboardReloadState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(panel.DisplayText,
            Does.Contain("Port: RELOADING").And.Contain("Starboard: READY"));
    }

    [UnityTest]
    public IEnumerator FormalPlayerShip_ControlsRouteThroughInputAndReadBack()
    {
        GameObject playerShip = InstantiateCombatShip(true);
        GameObject hostileShip = InstantiateCombatShip();
        hostileShip.transform.position = playerShip.transform.position
            + playerShip.transform.right * 200f;
        SetTeamId(playerShip, 1);
        SetTeamId(hostileShip, 2);
        selection.SelectSingle(
            playerShip.GetComponent<ShipDestinationController>());
        ShipCombatState state = playerShip.GetComponent<ShipCombatState>();

        Assert.That(panel.TryToggleAutoFire(), Is.True);
        Assert.That(state.AutoFireEnabled, Is.True);
        Assert.That(panel.CurrentStatus.AutoFireEnabled, Is.True);

        Assert.That(panel.TryBeginManualTarget(), Is.True);
        Assert.That(input.ManualTargetArmed, Is.True);
        Assert.That(input.TrySubmitArmedManualTarget(hostileShip), Is.True);
        yield return null;
        Assert.That(state.ManualTarget, Is.SameAs(hostileShip));
        Assert.That(state.AutoFireEnabled, Is.False);
        Assert.That(panel.CurrentStatus.ManualTarget,
            Is.SameAs(hostileShip));

        Assert.That(panel.TryClearManualTarget(), Is.True);
        Assert.That(state.ManualTarget, Is.Null);
        Assert.That(panel.CurrentStatus.ManualTarget, Is.Null);

        Assert.That(panel.TryBeginBlindFire(), Is.True);
        Vector3 worldPoint = playerShip.transform.position
            - playerShip.transform.right * 120f;
        input.TrySubmitArmedBlindFireAtWorldPoint(worldPoint,
            out BlindFirePlayerCommandResult result);
        yield return null;

        Assert.That(result.Attempted, Is.True);
        Assert.That(result.ShooterShipRoot, Is.SameAs(playerShip));
        Assert.That(result.WorldAimPoint, Is.EqualTo(worldPoint));
        Assert.That(result.Eligibility.Aim.WorldAimPoint,
            Is.EqualTo(worldPoint));
        Assert.That(input.BlindFireArmed, Is.False);
        Assert.That(state.ManualTarget, Is.Null);
        Assert.That(panel.CurrentStatus.ManualTarget, Is.Null);
        Assert.That(panel.CurrentStatus.LastBlindFirePointWorld,
            Is.EqualTo(worldPoint));
        Assert.That(panel.InteractionHint,
            Does.Contain("Last Blind:").And.Contain("Range")
                .And.Contain("Result:"));
    }

    private GameObject InstantiateCombatShip(
        bool keepDestinationEnabled = false)
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject ship = Object.Instantiate(prefab);
        created.Add(ship);

        ShipDestinationController destination =
            ship.GetComponent<ShipDestinationController>();
        Assert.That(destination, Is.Not.Null);
        destination.enabled = keepDestinationEnabled;
        ShipCombatAIController ai = ship.GetComponent<ShipCombatAIController>();
        if (ai != null)
        {
            ai.enabled = false;
        }

        return ship;
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        return null;
#endif
    }

    private static void SetTeamId(GameObject shipRoot, int teamId)
    {
        ShipCombatAffiliation affiliation =
            shipRoot.GetComponent<ShipCombatAffiliation>();
        Assert.That(affiliation, Is.Not.Null);
        FieldInfo field = typeof(ShipCombatAffiliation).GetField(
            "teamId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(affiliation, teamId);
    }
}
