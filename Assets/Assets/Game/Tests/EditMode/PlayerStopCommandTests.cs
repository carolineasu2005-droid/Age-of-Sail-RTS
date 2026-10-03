using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerStopCommandTests
{
    private readonly List<Object> createdObjects = new();


    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
    }


    [Test]
    public void StopSelectedShips_EmptySelectionDoesNotStopShips()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out _, out _
        );
        ShipSailingSpeed speed = CreateShip("Unselected", out _);

        dispatcher.DispatchStopSelectedShips();

        Assert.That(dispatcher.LastDispatchResult,
            Is.EqualTo(ShipCommandDispatcher.DispatchResult.NoSelection));
        Assert.That(speed.IsPlayerStopped, Is.False);
    }


    [Test]
    public void StopSelectedShips_SingleSelectionAppliesZeroSpeedCapWithoutSpeedSnap()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        SetPrivateField(speed, "currentSpeed", 2.5f);
        selection.AddSelection(ship);

        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(selection.SelectedShips[0], Is.SameAs(ship));
        Assert.That(speed.IsPlayerStopped, Is.False);

        dispatcher.DispatchStopSelectedShips();

        Assert.That(speed.IsPlayerStopped, Is.True);
        Assert.That(speed.PlayerStopSpeedCap, Is.EqualTo(0f));
        Assert.That(speed.CurrentSpeed, Is.EqualTo(2.5f));
        Assert.That(ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, false, 0f, true, speed.PlayerStopSpeedCap
        ), Is.EqualTo(0f));
    }


    [Test]
    public void StopSelectedShips_MultipleSelectionStopsOnlySelectedShips()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed firstSpeed = CreateShip("First", out ShipDestinationController first);
        ShipSailingSpeed secondSpeed = CreateShip("Second", out ShipDestinationController second);
        ShipSailingSpeed unselectedSpeed = CreateShip("Unselected", out _);
        selection.AddSelection(first);
        selection.AddSelection(second);

        Assert.That(selection.SelectedCount, Is.EqualTo(2));
        Assert.That(firstSpeed.IsPlayerStopped, Is.False);
        Assert.That(secondSpeed.IsPlayerStopped, Is.False);

        dispatcher.DispatchStopSelectedShips();

        Assert.That(firstSpeed.IsPlayerStopped, Is.True);
        Assert.That(secondSpeed.IsPlayerStopped, Is.True);
        Assert.That(unselectedSpeed.IsPlayerStopped, Is.False);
    }


    [Test]
    public void StopSelectedShips_CancelsDestinationAndHeadingGuidance()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        CreateShip("Selected", out ShipDestinationController ship);
        ShipHeadingController heading = ship.GetComponent<ShipHeadingController>();
        ship.SetDestination(
            new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual
        );
        heading.SetTargetHeading(90f, TurnDirection.Clockwise);
        selection.AddSelection(ship);

        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(ship.HasDestination, Is.True);
        Assert.That(heading.IsActive, Is.True);

        dispatcher.DispatchStopSelectedShips();

        Assert.That(ship.HasDestination, Is.False);
        Assert.That(ship.CurrentNavigationMode,
            Is.EqualTo(ShipDestinationController.NavigationMode.None));
        Assert.That(heading.IsActive, Is.False);
    }


    [Test]
    public void NewSingleDestination_ClearsPlayerStop()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        speed.DecreasePlayerSpeedOrder();
        selection.AddSelection(ship);
        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(speed.IsPlayerStopped, Is.False);
        dispatcher.DispatchStopSelectedShips();
        Assert.That(speed.IsPlayerStopped, Is.True);

        dispatcher.DispatchDestination(
            new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual
        );

        Assert.That(speed.IsPlayerStopped, Is.False);
        Assert.That(ship.HasDestination, Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
    }


    [Test]
    public void NewGroupDestination_ClearsStopsForCommandedMembers()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed firstSpeed = CreateShip("First", out ShipDestinationController first);
        ShipSailingSpeed secondSpeed = CreateShip("Second", out ShipDestinationController second);
        selection.AddSelection(first);
        selection.AddSelection(second);
        Assert.That(selection.SelectedCount, Is.EqualTo(2));
        dispatcher.DispatchStopSelectedShips();
        Assert.That(firstSpeed.IsPlayerStopped, Is.True);
        Assert.That(secondSpeed.IsPlayerStopped, Is.True);

        dispatcher.DispatchDestination(
            new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Direct
        );

        Assert.That(firstSpeed.IsPlayerStopped, Is.False);
        Assert.That(secondSpeed.IsPlayerStopped, Is.False);
        Assert.That(dispatcher.GroupCommandPending, Is.True);
    }


    [Test]
    public void NewFormationPlacement_ClearsStopsForSnapshotMembers()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed firstSpeed = CreateShip("First", out ShipDestinationController first);
        ShipSailingSpeed secondSpeed = CreateShip("Second", out ShipDestinationController second);
        selection.AddSelection(first);
        selection.AddSelection(second);
        dispatcher.DispatchStopSelectedShips();
        Assert.That(FormationGeometrySnapshot.TryCapture(
            selection.SelectedShips,
            selection.PrimarySelectedShip,
            out FormationGeometrySnapshot snapshot
        ), Is.True);

        dispatcher.DispatchFormationPlacement(
            new Vector3(50f, 0f, 0f),
            90f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Direct
        );

        Assert.That(firstSpeed.IsPlayerStopped, Is.False);
        Assert.That(secondSpeed.IsPlayerStopped, Is.False);
    }


    [Test]
    public void StopSelectedShips_CancelsWholeFormationButStopsOnlySelectedMember()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection,
            out FormationCommandController formation
        );
        ShipSailingSpeed firstSpeed = CreateShip("First", out ShipDestinationController first);
        ShipSailingSpeed secondSpeed = CreateShip("Second", out ShipDestinationController second);
        selection.AddSelection(first);
        selection.AddSelection(second);
        Assert.That(formation.CaptureCurrentFormation(selection.SelectedShips), Is.True);
        selection.SelectSingle(first);

        Assert.That(formation.IsActive, Is.True);
        Assert.That(formation.ContainsActiveMember(first), Is.True);
        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(firstSpeed.IsPlayerStopped, Is.False);
        Assert.That(secondSpeed.IsPlayerStopped, Is.False);

        dispatcher.DispatchStopSelectedShips();

        Assert.That(formation.IsActive, Is.False);
        Assert.That(firstSpeed.IsPlayerStopped, Is.True);
        Assert.That(secondSpeed.IsPlayerStopped, Is.False);
    }


    [Test]
    public void StopSelectedShips_PersistsAcrossDeselection()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        selection.AddSelection(ship);
        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(speed.IsPlayerStopped, Is.False);

        dispatcher.DispatchStopSelectedShips();
        Assert.That(speed.IsPlayerStopped, Is.True);
        selection.ClearSelection();

        Assert.That(speed.IsPlayerStopped, Is.True);
    }


    [Test]
    public void StopSelectedShips_ClearsPendingLineAheadWithoutChangingDesignatedLead()
    {
        ShipPlayerCommandInput input = CreateInput(
            out ShipSelectionManager selection, out _
        );
        CreateShip("First", out ShipDestinationController first);
        CreateShip("Lead", out ShipDestinationController lead);
        selection.AddSelection(first);
        selection.AddSelection(lead);
        Assert.That(selection.TrySetDesignatedFormationLead(lead), Is.True);
        input.ToggleLineAheadTemplate();

        input.StopSelectedShips();

        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));
        Assert.That(selection.DesignatedFormationLead, Is.SameAs(lead));
    }

    [Test]
    public void PlayerSpeedOrder_DefaultAndDecreaseSequence()
    {
        ShipSailingSpeed speed = CreateShip("Speed Order", out _);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));

        speed.DecreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        speed.DecreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.5f));
        speed.DecreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.25f));
        speed.DecreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.25f));
    }

    [Test]
    public void PlayerSpeedOrder_IncreaseSequenceStopsAtFull()
    {
        ShipSailingSpeed speed = CreateShip("Speed Order", out _);
        for (int i = 0; i < 3; i++)
        {
            speed.DecreasePlayerSpeedOrder();
        }

        speed.IncreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.5f));
        speed.IncreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        speed.IncreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
        speed.IncreasePlayerSpeedOrder();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
    }

    [Test]
    public void SpeedOrderCommand_ClearsStopWithoutSpeedSnapAndPersistsThroughStop()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        selection.SelectSingle(ship);
        SetPrivateField(speed, "currentSpeed", 2.5f);

        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(speed.CurrentSpeed, Is.EqualTo(2.5f));
        dispatcher.DispatchStopSelectedShips();
        Assert.That(speed.IsPlayerStopped, Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.True);
        Assert.That(speed.IsPlayerStopped, Is.False);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.5f));
        Assert.That(speed.CurrentSpeed, Is.EqualTo(2.5f));
        selection.ClearSelection();
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.5f));
    }

    [Test]
    public void SpeedOrderCommand_AtBoundStillClearsStop()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        selection.SelectSingle(ship);
        dispatcher.DispatchStopSelectedShips();

        Assert.That(dispatcher.TryIncreaseSelectedSpeedOrder(), Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
        Assert.That(speed.IsPlayerStopped, Is.False);
    }

    [Test]
    public void SpeedOrderCommand_PreservesDestinationHeadingPoseSelectionAndSequence()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        ShipHeadingController heading = ship.GetComponent<ShipHeadingController>();
        ShipManeuverPlanner planner = ship.GetComponent<ShipManeuverPlanner>();
        ShipTacking tacking = ship.GetComponent<ShipTacking>();
        ShipWearing wearing = ship.GetComponent<ShipWearing>();
        ship.SetDestination(new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        heading.SetTargetHeading(90f, TurnDirection.Clockwise);
        SetPrivateField(planner, "isActive", true);
        SetPrivateField(tacking, "isActive", true);
        SetPrivateField(wearing, "isActive", true);
        selection.SelectSingle(ship);
        Vector3 position = ship.transform.position;
        Quaternion rotation = ship.transform.rotation;
        int sequence = dispatcher.DispatchSequence;

        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(ship.HasDestination, Is.True);
        Assert.That(heading.IsActive, Is.True);
        Assert.That(planner.IsActive, Is.True);
        Assert.That(tacking.IsActive, Is.True);
        Assert.That(wearing.IsActive, Is.True);
        Assert.That(ship.transform.position, Is.EqualTo(position));
        Assert.That(ship.transform.rotation, Is.EqualTo(rotation));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(ship));
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(sequence));
    }

    [Test]
    public void SpeedOrderCommand_RejectsNoSelectionMultipleSelectionAndMissingSpeed()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed firstSpeed = CreateShip("First", out ShipDestinationController first);
        ShipSailingSpeed secondSpeed = CreateShip("Second", out ShipDestinationController second);
        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.False);
        selection.AddSelection(first);
        selection.AddSelection(second);
        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.False);
        Assert.That(firstSpeed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
        Assert.That(secondSpeed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));

        GameObject invalidRoot = new GameObject("Missing Speed");
        createdObjects.Add(invalidRoot);
        ShipDestinationController invalidShip = invalidRoot.AddComponent<ShipDestinationController>();
        selection.SelectSingle(invalidShip);
        Assert.That(dispatcher.TryIncreaseSelectedSpeedOrder(), Is.False);
        Assert.That(firstSpeed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));

        selection.SelectSingle(first);
        first.enabled = false;
        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.False);
        Assert.That(firstSpeed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
    }

    [Test]
    public void SpeedOrderCommand_RejectsActiveFormationMemberWithoutMutation()
    {
        ShipCommandDispatcher dispatcher = CreateCommandObjects(
            out ShipSelectionManager selection,
            out FormationCommandController formation
        );
        ShipSailingSpeed speed = CreateShip("First", out ShipDestinationController first);
        CreateShip("Second", out ShipDestinationController second);
        selection.AddSelection(first);
        selection.AddSelection(second);
        Assert.That(formation.CaptureCurrentFormation(selection.SelectedShips), Is.True);
        selection.SelectSingle(first);
        speed.SetPlayerStopSpeedCap(0f);
        int sequence = dispatcher.DispatchSequence;

        Assert.That(dispatcher.TryDecreaseSelectedSpeedOrder(), Is.False);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
        Assert.That(speed.IsPlayerStopped, Is.True);
        Assert.That(formation.IsActive, Is.True);
        Assert.That(formation.ContainsActiveMember(first), Is.True);
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(first));
        Assert.That(dispatcher.DispatchSequence, Is.EqualTo(sequence));
    }

    [Test]
    public void SpeedOrderInput_ForwardsThroughDispatcher()
    {
        ShipPlayerCommandInput input = CreateInput(
            out ShipSelectionManager selection, out _
        );
        ShipSailingSpeed speed = CreateShip("Selected", out ShipDestinationController ship);
        selection.SelectSingle(ship);

        Assert.That(input.TryDecreaseSelectedSpeedOrder(), Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(input.TryIncreaseSelectedSpeedOrder(), Is.True);
        Assert.That(speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
    }


    private ShipCommandDispatcher CreateCommandObjects(
        out ShipSelectionManager selection,
        out FormationCommandController formation
    )
    {
        GameObject commandObject = new GameObject("Command Objects");
        createdObjects.Add(commandObject);
        selection = commandObject.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher = commandObject.AddComponent<ShipCommandDispatcher>();
        formation = commandObject.AddComponent<FormationCommandController>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(
            dispatcher,
            "formationCommandController",
            formation
        );
        return dispatcher;
    }


    private ShipPlayerCommandInput CreateInput(
        out ShipSelectionManager selection,
        out ShipCommandDispatcher dispatcher
    )
    {
        GameObject commandObject = new GameObject("Command Input");
        createdObjects.Add(commandObject);
        selection = commandObject.AddComponent<ShipSelectionManager>();
        dispatcher = commandObject.AddComponent<ShipCommandDispatcher>();
        ShipPlayerCommandInput input = commandObject.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(input, "selectionManager", selection);
        SetPrivateField(input, "commandDispatcher", dispatcher);
        input.enabled = false;
        input.enabled = true;
        return input;
    }


    private ShipSailingSpeed CreateShip(
        string name,
        out ShipDestinationController ship
    )
    {
        GameObject shipObject = new GameObject(name);
        createdObjects.Add(shipObject);
        ShipSailingSpeed speed = shipObject.AddComponent<ShipSailingSpeed>();
        ShipTurning turning = shipObject.AddComponent<ShipTurning>();
        ShipHeadingController heading = shipObject.AddComponent<
            ShipHeadingController
        >();
        ShipTacking tacking = shipObject.AddComponent<ShipTacking>();
        ShipWearing wearing = shipObject.AddComponent<ShipWearing>();
        ShipManeuverPlanner maneuverPlanner = shipObject.AddComponent<
            ShipManeuverPlanner
        >();
        ship = shipObject.AddComponent<ShipDestinationController>();
        SetPrivateField(heading, "shipTurning", turning);
        SetPrivateField(tacking, "shipSailingSpeed", speed);
        SetPrivateField(tacking, "shipTurning", turning);
        SetPrivateField(tacking, "headingController", heading);
        SetPrivateField(wearing, "shipSailingSpeed", speed);
        SetPrivateField(wearing, "headingController", heading);
        SetPrivateField(maneuverPlanner, "headingController", heading);
        SetPrivateField(maneuverPlanner, "shipTacking", tacking);
        SetPrivateField(maneuverPlanner, "shipWearing", wearing);
        SetPrivateField(ship, "maneuverPlanner", maneuverPlanner);
        return speed;
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null,
            $"Required runtime field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }
}
