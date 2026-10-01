using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatStatusReadModelTests
{
    private readonly List<Object> created = new List<Object>();
    private ShipSelectionManager selection;

    [SetUp]
    public void SetUp()
    {
        selection = CreateObject("Selection")
            .AddComponent<ShipSelectionManager>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }
        }

        created.Clear();
    }

    [Test]
    public void NoSelection_HasNoCombatPresentation()
    {
        CombatStatusSnapshot status = Capture();

        Assert.That(status.HasSelection, Is.False);
        Assert.That(CombatStatusFormatter.Format(status), Is.Empty);
    }

    [Test]
    public void PrimarySelectionChange_ReadsNewShip()
    {
        GameObject first = CreateCombatShip("First");
        GameObject second = CreateCombatShip("Second");
        selection.SelectSingle(first.GetComponent<ShipDestinationController>());
        Assert.That(Capture().ShipRoot, Is.SameAs(first));

        selection.SelectSingle(second.GetComponent<ShipDestinationController>());
        CombatStatusSnapshot status = Capture();

        Assert.That(status.ShipRoot, Is.SameAs(second));
        Assert.That(CombatStatusFormatter.Format(status),
            Does.StartWith("Second"));
    }

    [Test]
    public void AutoFire_ReadsAuthoritativeMode()
    {
        GameObject ship = SelectCombatShip();
        ShipCombatState combat = ship.GetComponent<ShipCombatState>();
        Assert.That(Capture().AutoFireEnabled, Is.False);

        combat.SetAutoFireEnabled(true);

        Assert.That(Capture().AutoFireEnabled, Is.True);
        Assert.That(CombatStatusFormatter.Format(Capture()),
            Does.Contain("Auto Fire: ON"));
    }

    [Test]
    public void ManualTarget_ReadsNoneAndAssignedIdentity()
    {
        GameObject ship = SelectCombatShip();
        GameObject target = CreateCombatShip("Enemy Target");
        ShipCombatState combat = ship.GetComponent<ShipCombatState>();
        Assert.That(Capture().ManualTarget, Is.Null);

        Assert.That(combat.AssignManualTarget(target), Is.True);
        CombatStatusSnapshot status = Capture();

        Assert.That(status.ManualTarget, Is.SameAs(target));
        Assert.That(CombatStatusFormatter.Format(status),
            Does.Contain("Manual Target: Enemy Target"));
    }

    [Test]
    public void BlindFire_FormatsIdleAndFrozenLastAimWithoutTarget()
    {
        Assert.That(CombatStatusFormatter.FormatBlindFire(false, null),
            Is.EqualTo("Blind Fire: Idle\nLast Blind Aim: None"));
        Assert.That(CombatStatusFormatter.FormatBlindFire(
            true, new Vector3(12f, 0f, -4f)),
            Is.EqualTo("Blind Fire: Armed\nLast Blind Aim: (12.0, 0.0, -4.0)"));
    }

    [Test]
    public void BlindFire_ReadsArmingAndLastValidFrozenPointFromInput()
    {
        GameObject ship = SelectCombatShip();
        AddIntegrity(ship);
        ship.AddComponent<ShipBroadsideFireExecutor>();
        selection.gameObject.AddComponent<ShipCommandDispatcher>();
        ShipPlayerCommandInput input =
            selection.gameObject.AddComponent<ShipPlayerCommandInput>();
        FieldInfo selectionField = typeof(ShipPlayerCommandInput).GetField(
            "selectionManager",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(selectionField, Is.Not.Null);
        selectionField.SetValue(input, selection);

        Assert.That(CombatStatusReadModel.Capture(
            selection, input, null, null).BlindFireArmed, Is.False);
        Assert.That(input.TryArmBlindFire(), Is.True);
        Assert.That(CombatStatusReadModel.Capture(
            selection, input, null, null).BlindFireArmed, Is.True);

        Vector3 point = new Vector3(50f, 0f, 0f);
        input.TryExecuteBlindFireAtWorldPoint(point, out _);
        CombatStatusSnapshot status = CombatStatusReadModel.Capture(
            selection, input, null, null);

        Assert.That(status.BlindFireArmed, Is.False);
        Assert.That(status.LastBlindFirePointWorld, Is.EqualTo(point));
        Assert.That(status.ManualTarget, Is.Null);
    }

    [Test]
    public void BroadsideReload_ReadbackKeepsPortAndStarboardIndependent()
    {
        GameObject ship = SelectCombatShip();
        ShipCombatState combat = ship.GetComponent<ShipCombatState>();
        Assert.That(combat.TryCommitBroadsideFire(CombatSide.Port), Is.True);

        CombatStatusSnapshot status = Capture();
        Assert.That(status.PortReloadState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(status.PortReloadRemainingSeconds,
            Is.EqualTo(combat.PortReloadRemainingSeconds));
        Assert.That(status.StarboardReloadState,
            Is.EqualTo(BroadsideReloadState.Ready));
        Assert.That(CombatStatusFormatter.Format(status),
            Does.Contain("Port: RELOADING").And.Contain("Starboard: READY"));

        Assert.That(combat.TryCommitBroadsideFire(CombatSide.Starboard),
            Is.True);
        Assert.That(Capture().StarboardReloadState,
            Is.EqualTo(BroadsideReloadState.Reloading));
    }

    [Test]
    public void Integrity_ReadsCurrentAndMaximumFromOwner()
    {
        GameObject ship = SelectCombatShip();
        ShipIntegrity integrity = AddIntegrity(ship);
        Assert.That(integrity.TryApplyIntegrityLoss(250f, out _), Is.True);

        CombatStatusSnapshot status = Capture();

        Assert.That(status.CurrentIntegrity, Is.EqualTo(9750f));
        Assert.That(status.MaximumIntegrity, Is.EqualTo(10000f));
        Assert.That(CombatStatusFormatter.Format(status),
            Does.Contain("Integrity: 9750 / 10000"));
    }

    [Test]
    public void Lifecycle_MapsOperationalDisabledAndSinking()
    {
        GameObject ship = SelectCombatShip();
        ShipIntegrity integrity = AddIntegrity(ship);
        Assert.That(Capture().LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational));

        Assert.That(integrity.TryApplyIntegrityLoss(8000f, out _), Is.True);
        Assert.That(Capture().LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.CombatDisabled));
        Assert.That(CombatStatusFormatter.Format(Capture()),
            Does.Contain("Lifecycle: Disabled"));

        Assert.That(integrity.TryApplyIntegrityLoss(1700f, out _), Is.True);
        Assert.That(Capture().LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking));
        Assert.That(CombatStatusFormatter.Format(Capture()),
            Does.Contain("Lifecycle: Sinking"));
    }

    [Test]
    public void EligibilityReasons_MapExistingGameplayFlagsToText()
    {
        Assert.That(CombatStatusFormatter.FormatFailure(
            FireEligibilityFailure.BroadsideReloading), Is.EqualTo("Reloading"));
        Assert.That(CombatStatusFormatter.FormatFailure(
            FireEligibilityFailure.NoBroadsideArc), Is.EqualTo("Wrong Arc"));
        Assert.That(CombatStatusFormatter.FormatFailure(
            FireEligibilityFailure.BeyondMaximumRange),
            Is.EqualTo("Out of Range"));
        Assert.That(CombatStatusFormatter.FormatFailure(
            FireEligibilityFailure.Obstructed), Is.EqualTo("Obstructed"));
        Assert.That(CombatStatusFormatter.FormatFailure(
            FireEligibilityFailure.TargetLifecycleIllegal),
            Is.EqualTo("Target Sinking"));
    }

    private CombatStatusSnapshot Capture()
    {
        return CombatStatusReadModel.Capture(selection, null, null, null);
    }

    private GameObject SelectCombatShip()
    {
        GameObject ship = CreateCombatShip("Selected Ship");
        selection.SelectSingle(ship.GetComponent<ShipDestinationController>());
        return ship;
    }

    private GameObject CreateCombatShip(string name)
    {
        GameObject ship = CreateObject(name);
        ship.AddComponent<ShipDestinationController>();
        ship.AddComponent<ShipCombatState>();
        ship.AddComponent<ShipFireEligibility>();
        return ship;
    }

    private ShipIntegrity AddIntegrity(GameObject ship)
    {
        ShipIntegrityProfile profile =
            ScriptableObject.CreateInstance<ShipIntegrityProfile>();
        created.Add(profile);
        ShipIntegrity integrity = ship.AddComponent<ShipIntegrity>();
        FieldInfo field = typeof(ShipIntegrity).GetField(
            "integrityProfile",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(integrity, profile);
        Assert.That(integrity.TryInitialize(), Is.True);
        return integrity;
    }

    private GameObject CreateObject(string name)
    {
        GameObject instance = new GameObject(name);
        created.Add(instance);
        return instance;
    }
}
