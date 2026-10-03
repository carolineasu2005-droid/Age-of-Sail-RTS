using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerDirectedHeadingCommandTests
{
    private readonly List<Object> created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null) Object.DestroyImmediate(item);
        }
        created.Clear();
    }

    [TestCase(405f, TurnDirection.Clockwise,
        ShipManeuverPlanner.ManeuverType.NormalTurn)]
    [TestCase(180f, TurnDirection.Clockwise,
        ShipManeuverPlanner.ManeuverType.Tack)]
    [TestCase(180f, TurnDirection.CounterClockwise,
        ShipManeuverPlanner.ManeuverType.Wear)]
    [TestCase(350f, TurnDirection.Clockwise,
        ShipManeuverPlanner.ManeuverType.Complex)]
    [TestCase(0f, TurnDirection.Clockwise,
        ShipManeuverPlanner.ManeuverType.None)]
    public void Preview_UsesExistingDirectedClassification(
        float target, TurnDirection direction,
        ShipManeuverPlanner.ManeuverType expected)
    {
        ShipFixture ship = CreateShip("Preview");
        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            target, direction, out var maneuver), Is.True);
        Assert.That(maneuver, Is.EqualTo(expected));
        Assert.That(maneuver, Is.EqualTo(
            ShipManeuverPlanner.ClassifyDirectedArc(
                ship.Destination.transform.eulerAngles.y,
                Mathf.Repeat(target, 360f), direction,
                ship.Wind.WindFromDirection)));
    }

    [Test]
    public void Preview_DoesNotMutateExistingManeuverHeadingOrRoot()
    {
        ShipFixture ship = CreateShip("Preview");
        ship.Destination.SetDestination(new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        ship.Planner.ExecuteHeadingCommand(20f, TurnDirection.Clockwise);
        int sequence = ship.Planner.CommandSequence;
        Vector3 position = ship.Destination.transform.position;
        Quaternion rotation = ship.Destination.transform.rotation;

        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            180f, TurnDirection.Clockwise, out var maneuver), Is.True);

        Assert.That(maneuver, Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(sequence));
        Assert.That(ship.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(ship.Planner.TargetHeading, Is.EqualTo(20f));
        Assert.That(ship.Heading.TargetHeading, Is.EqualTo(20f));
        Assert.That(ship.Heading.IsActive, Is.True);
        Assert.That(ship.Destination.HasDestination, Is.True);
        Assert.That(ship.Destination.transform.position, Is.EqualTo(position));
        Assert.That(ship.Destination.transform.rotation, Is.EqualTo(rotation));
    }

    [Test]
    public void Preview_WithoutResolvedWindFailsWithoutMutation()
    {
        ShipFixture ship = CreateShip("Preview");
        SetPrivateField(ship.Planner, "globalWind", null);

        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            45f, TurnDirection.Clockwise, out var maneuver), Is.False);
        Assert.That(maneuver, Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
        Assert.That(ship.Planner.CommandSequence, Is.Zero);
        Assert.That(ship.Heading.IsActive, Is.False);
    }

    [Test]
    public void Preview_UsesCurrentResolvedWind()
    {
        ShipFixture ship = CreateShip("Preview");
        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            180f, TurnDirection.Clockwise, out var first), Is.True);
        ship.Wind.windFromDegrees = 270f;
        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            180f, TurnDirection.Clockwise, out var second), Is.True);

        Assert.That(first, Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(second, Is.EqualTo(ShipManeuverPlanner.ManeuverType.Wear));
    }

    [Test]
    public void Preview_RejectsInvalidHeadingAndDirection()
    {
        ShipFixture ship = CreateShip("Preview");
        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            float.NaN, TurnDirection.Clockwise, out _), Is.False);
        Assert.That(ship.Planner.TryPreviewDirectedHeading(
            45f, (TurnDirection)99, out _), Is.False);
        Assert.That(ship.Planner.CommandSequence, Is.Zero);
    }

    [TestCase(45f, TurnDirection.Clockwise, 45f)]
    [TestCase(-45f, TurnDirection.CounterClockwise, 315f)]
    public void DirectedHeading_AcceptedCommandReachesPlannerExactlyOnce(
        float target, TurnDirection direction, float normalized)
    {
        CommandFixture command = CreateCommand();
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        ship.Speed.DecreasePlayerSpeedOrder();
        ship.Speed.SetPlayerStopSpeedCap(0f);
        Vector3 position = ship.Destination.transform.position;
        Quaternion rotation = ship.Destination.transform.rotation;

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            target, direction, out var result), Is.True);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.None));
        Assert.That(result.SelectedShip, Is.SameAs(ship.Destination));
        Assert.That(result.TargetHeading, Is.EqualTo(normalized));
        Assert.That(result.Direction, Is.EqualTo(direction));
        Assert.That(result.PreviewManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(result.ExecutedManeuver, Is.EqualTo(result.PreviewManeuver));
        Assert.That(ship.Planner.TargetHeading, Is.EqualTo(normalized));
        Assert.That(ship.Planner.PlannedTurnDirection, Is.EqualTo(direction));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(1));
        Assert.That(command.Dispatcher.DispatchSequence, Is.EqualTo(1));
        Assert.That(command.Dispatcher.LastDispatchResult,
            Is.EqualTo(ShipCommandDispatcher.DispatchResult.DirectedHeading));
        Assert.That(ship.Speed.IsPlayerStopped, Is.False);
        Assert.That(ship.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(ship.Destination.transform.position, Is.EqualTo(position));
        Assert.That(ship.Destination.transform.rotation, Is.EqualTo(rotation));
    }

    [Test]
    public void DirectedHeading_ReplacesDestinationAndPendingGroupCommand()
    {
        CommandFixture command = CreateCommand();
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        ship.Destination.SetDestination(new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        Assert.That(ship.Destination.HasDestination, Is.True);
        int previousPlannerSequence = ship.Planner.CommandSequence;
        SetPrivateField(command.Dispatcher, "groupCommandPending", true);

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            45f, TurnDirection.Clockwise, out _), Is.True);

        Assert.That(ship.Destination.HasDestination, Is.False);
        Assert.That(ship.Destination.CurrentNavigationMode,
            Is.EqualTo(ShipDestinationController.NavigationMode.None));
        Assert.That(command.Dispatcher.GroupCommandPending, Is.False);
        Assert.That(ship.Planner.CommandSequence,
            Is.EqualTo(previousPlannerSequence + 1));
        Assert.That(ship.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
    }

    [TestCase(TurnDirection.Clockwise,
        ShipManeuverPlanner.ManeuverType.Tack)]
    [TestCase(TurnDirection.CounterClockwise,
        ShipManeuverPlanner.ManeuverType.Wear)]
    public void DirectedHeading_PlannerSelectsSpecialManeuver(
        TurnDirection direction, ShipManeuverPlanner.ManeuverType expected)
    {
        CommandFixture command = CreateCommand();
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        SetPrivateField(ship.Speed, "relativeWindAngleSigned", 90f);
        Quaternion rotation = ship.Destination.transform.rotation;

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            180f, direction, out var result), Is.True);

        Assert.That(result.PreviewManeuver, Is.EqualTo(expected));
        Assert.That(result.ExecutedManeuver, Is.EqualTo(expected));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(1));
        Assert.That(ship.Tacking.IsActive,
            Is.EqualTo(expected == ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(ship.Wearing.IsActive,
            Is.EqualTo(expected == ShipManeuverPlanner.ManeuverType.Wear));
        Assert.That(ship.Destination.transform.rotation, Is.EqualTo(rotation));
    }

    [TestCase(TurnDirection.Clockwise,
        ShipManeuverPlanner.ManeuverType.Tack)]
    [TestCase(TurnDirection.CounterClockwise,
        ShipManeuverPlanner.ManeuverType.Wear)]
    public void DirectedHeading_ReplacesActiveSpecialManeuverThroughPlanner(
        TurnDirection oldDirection,
        ShipManeuverPlanner.ManeuverType oldManeuver)
    {
        CommandFixture command = CreateCommand();
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        SetPrivateField(ship.Speed, "relativeWindAngleSigned", 90f);
        ship.Planner.ExecuteHeadingCommand(180f, oldDirection);
        Assert.That(ship.Planner.CurrentManeuver, Is.EqualTo(oldManeuver));
        Assert.That(ship.Planner.IsActive, Is.True);
        int sequence = ship.Planner.CommandSequence;

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            45f, TurnDirection.Clockwise, out var result), Is.True);

        Assert.That(result.ExecutedManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(sequence + 1));
        Assert.That(ship.Tacking.IsActive, Is.False);
        Assert.That(ship.Wearing.IsActive, Is.False);
        Assert.That(ship.Heading.IsActive, Is.True);
    }

    [Test]
    public void DirectedHeading_ReplacesSelectedActiveFormationMember()
    {
        CommandFixture command = CreateCommand();
        ShipFixture first = CreateShip("First");
        ShipFixture second = CreateShip("Second");
        command.Selection.AddSelection(first.Destination);
        command.Selection.AddSelection(second.Destination);
        Assert.That(command.Formation.CaptureCurrentFormation(
            command.Selection.SelectedShips), Is.True);
        command.Selection.SelectSingle(first.Destination);
        Assert.That(command.Formation.ContainsActiveMember(first.Destination), Is.True);

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            45f, TurnDirection.Clockwise, out _), Is.True);

        Assert.That(command.Formation.IsActive, Is.False);
        Assert.That(first.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(command.Dispatcher.DispatchSequence, Is.EqualTo(1));
    }

    [TestCase(350f, ShipManeuverPlanner.ManeuverType.Complex)]
    [TestCase(0f, ShipManeuverPlanner.ManeuverType.None)]
    public void DirectedHeading_RejectedPreviewKeepsDestinationAndFormation(
        float target, ShipManeuverPlanner.ManeuverType expected)
    {
        CommandFixture command = CreateCommand();
        ShipFixture first = CreateShip("First");
        ShipFixture second = CreateShip("Second");
        command.Selection.AddSelection(first.Destination);
        command.Selection.AddSelection(second.Destination);
        Assert.That(command.Formation.CaptureCurrentFormation(
            command.Selection.SelectedShips), Is.True);
        command.Selection.SelectSingle(first.Destination);
        first.Destination.SetDestination(new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        int plannerSequence = first.Planner.CommandSequence;
        first.Speed.SetPlayerStopSpeedCap(0f);

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            target, TurnDirection.Clockwise, out var result), Is.False);

        Assert.That(result.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.UnsupportedManeuver));
        Assert.That(result.PreviewManeuver,
            Is.EqualTo(expected));
        Assert.That(command.Formation.IsActive, Is.True);
        Assert.That(first.Destination.HasDestination, Is.True);
        Assert.That(first.Speed.IsPlayerStopped, Is.True);
        Assert.That(first.Planner.CommandSequence, Is.EqualTo(plannerSequence));
        Assert.That(command.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void DirectedHeading_UnavailableOwnerRejectsBeforeCancellingDestination()
    {
        CommandFixture command = CreateCommand();
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        ship.Destination.SetDestination(new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        int sequence = ship.Planner.CommandSequence;
        SetPrivateField(ship.Planner, "shipTacking", null);

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            180f, TurnDirection.Clockwise, out var result), Is.False);

        Assert.That(result.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.OwnerUnavailable));
        Assert.That(result.PreviewManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(ship.Destination.HasDestination, Is.True);
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(sequence));
        Assert.That(command.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void DirectedHeading_RejectedComplexPreservesActiveTack()
    {
        CommandFixture command = CreateCommand();
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        SetPrivateField(ship.Speed, "relativeWindAngleSigned", 90f);
        ship.Planner.ExecuteHeadingCommand(180f, TurnDirection.Clockwise);
        Assert.That(ship.Tacking.IsActive, Is.True);
        int sequence = ship.Planner.CommandSequence;

        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            350f, TurnDirection.Clockwise, out var result), Is.False);

        Assert.That(result.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.UnsupportedManeuver));
        Assert.That(ship.Tacking.IsActive, Is.True);
        Assert.That(ship.Planner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(sequence));
        Assert.That(command.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void DirectedHeading_RequiresExactlyOneActiveShipAndRequiredComponents()
    {
        CommandFixture command = CreateCommand();
        ShipFixture first = CreateShip("First");
        ShipFixture second = CreateShip("Second");
        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            45f, TurnDirection.Clockwise, out var none), Is.False);
        Assert.That(none.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.NoSelection));
        command.Selection.AddSelection(first.Destination);
        command.Selection.AddSelection(second.Destination);
        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            45f, TurnDirection.Clockwise, out var multi), Is.False);
        Assert.That(multi.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.RequiresSingleSelection));
        command.Selection.SelectSingle(first.Destination);
        first.Planner.enabled = false;
        Assert.That(command.Dispatcher.TryDispatchDirectedHeading(
            45f, TurnDirection.Clockwise, out var invalid), Is.False);
        Assert.That(invalid.Failure,
            Is.EqualTo(ShipCommandDispatcher.DirectedHeadingFailure.MissingMovementComponents));
        Assert.That(first.Planner.CommandSequence, Is.Zero);
        Assert.That(command.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void DirectedHeading_InputFacadeForwardsResult()
    {
        CommandFixture command = CreateCommand();
        ShipPlayerCommandInput input = command.Dispatcher.gameObject
            .AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(input, "selectionManager", command.Selection);
        SetPrivateField(input, "commandDispatcher", command.Dispatcher);
        ShipFixture ship = CreateShip("Selected");
        command.Selection.SelectSingle(ship.Destination);
        SetPrivateField(input, "pendingFormationTemplate",
            ShipPlayerCommandInput.PendingFormationTemplate.LineAhead);

        bool accepted = input.TrySubmitDirectedHeading(
            45f, TurnDirection.Clockwise, out var result);
        Assert.That(accepted, Is.True, $"Failure: {result.Failure}");
        Assert.That(result.Accepted, Is.True);
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(1));
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));
    }

    private CommandFixture CreateCommand()
    {
        GameObject root = CreateObject("Command");
        ShipSelectionManager selection = root.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher = root.AddComponent<ShipCommandDispatcher>();
        FormationCommandController formation = root.AddComponent<FormationCommandController>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(dispatcher, "formationCommandController", formation);
        return new CommandFixture(selection, dispatcher, formation);
    }

    private ShipFixture CreateShip(string name)
    {
        GameObject root = CreateObject(name);
        GlobalWind wind = root.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;
        ShipSailingSpeed speed = root.AddComponent<ShipSailingSpeed>();
        ShipTurning turning = root.AddComponent<ShipTurning>();
        ShipHeadingController heading = root.AddComponent<ShipHeadingController>();
        ShipTacking tacking = root.AddComponent<ShipTacking>();
        ShipWearing wearing = root.AddComponent<ShipWearing>();
        ShipManeuverPlanner planner = root.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController destination = root.AddComponent<ShipDestinationController>();
        SetPrivateField(heading, "shipTurning", turning);
        SetPrivateField(tacking, "shipSailingSpeed", speed);
        SetPrivateField(tacking, "shipTurning", turning);
        SetPrivateField(tacking, "headingController", heading);
        SetPrivateField(wearing, "shipSailingSpeed", speed);
        SetPrivateField(wearing, "headingController", heading);
        SetPrivateField(planner, "globalWind", wind);
        SetPrivateField(planner, "headingController", heading);
        SetPrivateField(planner, "shipTacking", tacking);
        SetPrivateField(planner, "shipWearing", wearing);
        SetPrivateField(destination, "maneuverPlanner", planner);
        return new ShipFixture(wind, speed, heading, tacking, wearing,
            planner, destination);
    }

    private GameObject CreateObject(string name)
    {
        GameObject root = new GameObject(name);
        created.Add(root);
        return root;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private readonly struct CommandFixture
    {
        public readonly ShipSelectionManager Selection;
        public readonly ShipCommandDispatcher Dispatcher;
        public readonly FormationCommandController Formation;

        public CommandFixture(ShipSelectionManager selection,
            ShipCommandDispatcher dispatcher, FormationCommandController formation)
        {
            Selection = selection;
            Dispatcher = dispatcher;
            Formation = formation;
        }
    }

    private readonly struct ShipFixture
    {
        public readonly GlobalWind Wind;
        public readonly ShipSailingSpeed Speed;
        public readonly ShipHeadingController Heading;
        public readonly ShipTacking Tacking;
        public readonly ShipWearing Wearing;
        public readonly ShipManeuverPlanner Planner;
        public readonly ShipDestinationController Destination;

        public ShipFixture(GlobalWind wind, ShipSailingSpeed speed,
            ShipHeadingController heading, ShipTacking tacking,
            ShipWearing wearing, ShipManeuverPlanner planner,
            ShipDestinationController destination)
        {
            Wind = wind;
            Speed = speed;
            Heading = heading;
            Tacking = tacking;
            Wearing = wearing;
            Planner = planner;
            Destination = destination;
        }
    }
}
