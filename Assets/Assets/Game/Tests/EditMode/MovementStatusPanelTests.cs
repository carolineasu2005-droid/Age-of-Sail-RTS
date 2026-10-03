using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class MovementStatusPanelTests
{
    private readonly List<Object> created = new();

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
    public void NoSelection_ShowsDisabledState()
    {
        Fixture fixture = CreateFixture();
        Assert.That(fixture.Panel.CurrentStatus.SelectionCount, Is.Zero);
        Assert.That(fixture.Panel.DisplayText, Does.Contain("No ship selected"));
        Assert.That(fixture.Panel.TryDecreaseSpeed(), Is.False);
        Assert.That(fixture.Panel.TryStop(), Is.False);
        Assert.That(fixture.Panel.BeginTurnHold(
            TurnDirection.Clockwise, 0d), Is.False);
    }

    [Test]
    public void SingleSelection_CopiesAuthoritativeMovementValues()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(33f);
        ship.Speed.DecreasePlayerSpeedOrder();
        SetPrivateField(ship.Speed, "currentSpeed", 4.2f);
        SetPrivateField(ship.Speed, "polarTargetSpeed", 5.6f);
        SetPrivateField(ship.Speed, "effectiveTargetSpeed", 3.4f);
        SetPrivateField(ship.Speed, "courseHeading", 25f);
        SetPrivateField(ship.Speed, "relativeWindAngleSigned", -48f);
        fixture.Selection.SelectSingle(ship.Destination);
        Refresh(fixture.Panel);

        MovementStatusSnapshot status = fixture.Panel.CurrentStatus;
        Assert.That(status.SelectionCount, Is.EqualTo(1));
        Assert.That(status.Ship, Is.SameAs(ship.Destination));
        Assert.That(status.PlayerSpeedOrder,
            Is.EqualTo(ship.Speed.PlayerSpeedOrderNormalized));
        Assert.That(status.CurrentSpeed, Is.EqualTo(ship.Speed.CurrentSpeed));
        Assert.That(status.AvailableTargetSpeed,
            Is.EqualTo(ship.Speed.AvailableTargetSpeed));
        Assert.That(status.EffectiveTargetSpeed,
            Is.EqualTo(ship.Speed.EffectiveTargetSpeed));
        Assert.That(status.PlayerStopped, Is.EqualTo(ship.Speed.IsPlayerStopped));
        Assert.That(status.PhysicalHeading, Is.EqualTo(33f).Within(0.001f));
        Assert.That(status.CourseHeading, Is.EqualTo(ship.Speed.CourseHeading));
        Assert.That(status.RelativeWindAngleSigned,
            Is.EqualTo(ship.Speed.RelativeWindAngleSigned));
        Assert.That(fixture.Panel.DisplayText,
            Does.Contain("Speed Order: 75%")
                .And.Contain("Current Speed: 4.2")
                .And.Contain("Available Target: 5.6")
                .And.Contain("Effective Target: 3.4")
                .And.Contain("Relative Wind: -48"));
    }

    [Test]
    public void MultiSelection_DisablesEveryControl()
    {
        Fixture fixture = CreateFixture();
        ShipFixture first = CreateShip(0f);
        ShipFixture second = CreateShip(0f);
        fixture.Selection.SelectSingle(first.Destination);
        fixture.Selection.AddSelection(second.Destination);
        Refresh(fixture.Panel);

        Assert.That(fixture.Panel.CurrentStatus.SelectionCount, Is.EqualTo(2));
        Assert.That(fixture.Panel.CurrentStatus.HasControllableShip, Is.False);
        Assert.That(fixture.Panel.DisplayText,
            Does.Contain("Select exactly one ship"));
        Assert.That(fixture.Panel.TryDecreaseSpeed(), Is.False);
        Assert.That(fixture.Panel.TryIncreaseSpeed(), Is.False);
        Assert.That(fixture.Panel.TryStop(), Is.False);
        Assert.That(fixture.Panel.BeginTurnHold(
            TurnDirection.Clockwise, 0d), Is.False);
        Assert.That(first.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
        Assert.That(second.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
    }

    [Test]
    public void PlannerClassificationAndActivity_AreDisplayedSeparately()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        SetPrivateField(ship.Planner, "currentManeuver",
            ShipManeuverPlanner.ManeuverType.Tack);
        SetPrivateField(ship.Planner, "isActive", false);
        Refresh(fixture.Panel);

        Assert.That(fixture.Panel.CurrentStatus.PlannerManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(fixture.Panel.CurrentStatus.PlannerActive, Is.False);
        Assert.That(fixture.Panel.DisplayText,
            Does.Contain("Planner: Tack (Inactive)"));
    }

    [Test]
    public void SpeedButtons_DelegateOnceEachToPlayerInput()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);

        Assert.That(fixture.Panel.TryDecreaseSpeed(), Is.True);
        Assert.That(ship.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(0.75f));
        Assert.That(fixture.Panel.TryIncreaseSpeed(), Is.True);
        Assert.That(ship.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
        Assert.That(ship.Planner.CommandSequence, Is.Zero);
    }

    [Test]
    public void StopButton_UsesExistingStopCommand()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);

        Assert.That(fixture.Panel.TryStop(), Is.True);

        Assert.That(ship.Speed.IsPlayerStopped, Is.True);
        Assert.That(fixture.Dispatcher.LastDispatchResult,
            Is.EqualTo(ShipCommandDispatcher.DispatchResult.StopSelectedShips));
        Assert.That(fixture.Panel.CurrentStatus.PlayerStopped, Is.True);
    }

    [Test]
    public void SpeedPlusAfterStop_ClearsStopWithoutSpeedSnap()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        SetPrivateField(ship.Speed, "currentSpeed", 3f);
        fixture.Panel.TryStop();
        Assert.That(ship.Speed.IsPlayerStopped, Is.True);

        Assert.That(fixture.Panel.TryIncreaseSpeed(), Is.True);

        Assert.That(ship.Speed.IsPlayerStopped, Is.False);
        Assert.That(ship.Speed.CurrentSpeed, Is.EqualTo(3f));
        Assert.That(ship.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(1f));
    }

    [TestCase(TurnDirection.Clockwise, 60f)]
    [TestCase(TurnDirection.CounterClockwise, 300f)]
    public void TurnMouseDown_CapturesOneDirectionAndReleaseCommitsOnce(
        TurnDirection direction, float target)
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        Vector2 buttonPoint = TurnPoint(fixture.Panel, direction);
        Assert.That(fixture.Panel.HandlePointerEvent(
            EventType.MouseDown, 0, buttonPoint, 0d), Is.True);
        Assert.That(fixture.Panel.HandlePointerEvent(
            EventType.MouseDown, 0,
            TurnPoint(fixture.Panel, Opposite(direction)), 0.1d), Is.True);
        Assert.That(fixture.Panel.TryDecreaseSpeed(), Is.False);
        Assert.That(fixture.Panel.TryStop(), Is.False);

        fixture.Preview.UpdatePreview(0.6d);
        Refresh(fixture.Panel);
        Assert.That(fixture.Preview.IsGhostVisible, Is.True);
        Assert.That(fixture.Preview.Direction, Is.EqualTo(direction));
        Assert.That(fixture.Preview.CompletedSteps, Is.EqualTo(6));
        Assert.That(fixture.Panel.CurrentStatus.PreviewDelta, Is.EqualTo(60f));
        Assert.That(fixture.Panel.CurrentStatus.PreviewTargetHeading,
            Is.EqualTo(target));
        Assert.That(fixture.Panel.CurrentStatus.PreviewManeuver,
            Is.EqualTo(fixture.Preview.PreviewManeuver));
        Assert.That(fixture.Panel.DisplayText,
            Does.Contain("Turn Preview").And.Contain("Predicted:"));
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
        Assert.That(ship.Planner.CommandSequence, Is.Zero);

        Assert.That(fixture.Panel.HandlePointerEvent(
            EventType.MouseUp, 0, new Vector2(999f, 999f), 0.6d), Is.True);
        Assert.That(fixture.Panel.IsTurnPointerCaptured, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.EqualTo(1));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(1));
        Assert.That(ship.Planner.TargetHeading, Is.EqualTo(target));
        Assert.That(fixture.Panel.HandlePointerEvent(
            EventType.MouseUp, 0, buttonPoint, 0.7d), Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.EqualTo(1));
    }

    [Test]
    public void ShortPress_ReleasesWithoutHeadingCommand()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        Vector2 point = TurnPoint(fixture.Panel, TurnDirection.Clockwise);
        fixture.Panel.HandlePointerEvent(EventType.MouseDown, 0, point, 0d);

        fixture.Panel.HandlePointerEvent(EventType.MouseUp, 0, point, 0.099d);

        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
        Assert.That(ship.Planner.CommandSequence, Is.Zero);
    }

    [Test]
    public void SelectionChange_CancelsCaptureAndGhost()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        fixture.Panel.BeginTurnHold(TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(0.2d);

        fixture.Selection.ClearSelection();

        Assert.That(fixture.Panel.IsTurnPointerCaptured, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Panel.ReleaseTurnHold(0.3d), Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void ExplicitCancelAfterEditModeDisable_ClearsPanelHoldWithoutCommand()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        fixture.Panel.BeginTurnHold(TurnDirection.Clockwise, 0d);
        fixture.Panel.enabled = false;
        fixture.Panel.CancelTurnHold();

        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Panel.IsTurnPointerCaptured, Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void PanelRegion_BlocksWorldInputEvenWithoutSelection()
    {
        Fixture fixture = CreateFixture();
        Vector2 movementPoint = new(450f, Screen.height - 30f);
        Assert.That(fixture.Panel.PanelRect.Overlaps(
            new Rect(12f, 12f, 410f, 450f)), Is.False);
        Assert.That(fixture.Panel.ContainsScreenPoint(movementPoint), Is.True);
        Assert.That(fixture.Input.IsPointerOverStatusPanel(movementPoint), Is.True);
        Assert.That(fixture.Input.IsPointerOverStatusPanel(
            new Vector2(900f, Screen.height - 30f)), Is.False);
        Assert.That(fixture.Selection.SelectedCount, Is.Zero);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void PanelPreview_DoesNotWriteMovementState()
    {
        Fixture fixture = CreateFixture();
        ShipFixture ship = CreateShip(0f);
        fixture.Selection.SelectSingle(ship.Destination);
        Vector3 position = ship.Destination.transform.position;
        Quaternion rotation = ship.Destination.transform.rotation;
        float speed = ship.Speed.CurrentSpeed;
        float speedOrder = ship.Speed.PlayerSpeedOrderNormalized;
        int plannerSequence = ship.Planner.CommandSequence;
        fixture.Panel.BeginTurnHold(TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(0.3d);

        Assert.That(ship.Destination.transform.position, Is.EqualTo(position));
        Assert.That(ship.Destination.transform.rotation, Is.EqualTo(rotation));
        Assert.That(ship.Speed.CurrentSpeed, Is.EqualTo(speed));
        Assert.That(ship.Speed.PlayerSpeedOrderNormalized, Is.EqualTo(speedOrder));
        Assert.That(ship.Planner.CommandSequence, Is.EqualTo(plannerSequence));
        Assert.That(ship.Tacking.IsActive, Is.False);
        Assert.That(ship.Wearing.IsActive, Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void PrototypeCamera_HasOneWiredMovementPanelAndPreview()
    {
        string path = Path.Combine(Application.dataPath,
            "Scenes/Prototype/SailingPrototype_01_Speed.unity");
        string scene = File.ReadAllText(path);
        Assert.That(Regex.Matches(scene,
            "AgeOfSailRTS.Runtime::MovementStatusPanel").Count,
            Is.EqualTo(1));
        Assert.That(Regex.Matches(scene,
            "AgeOfSailRTS.Runtime::ShipDirectedHeadingPreviewController").Count,
            Is.EqualTo(1));
        Assert.That(scene, Does.Contain("- component: {fileID: 1123039786}"));
        Assert.That(scene, Does.Contain("- component: {fileID: 1123039787}"));
        Match panelBlock = Regex.Match(scene,
            @"--- !u!114 &1123039787\s+MonoBehaviour:(.*?)(?=--- !u!)",
            RegexOptions.Singleline);
        Match previewBlock = Regex.Match(scene,
            @"--- !u!114 &1123039786\s+MonoBehaviour:(.*?)(?=--- !u!)",
            RegexOptions.Singleline);
        Assert.That(panelBlock.Success, Is.True);
        Assert.That(previewBlock.Success, Is.True);
        Assert.That(panelBlock.Value,
            Does.Contain("m_GameObject: {fileID: 1123039775}")
                .And.Contain("selectionManager: {fileID: 1123039782}")
                .And.Contain("playerInput: {fileID: 1123039776}")
                .And.Contain("headingPreview: {fileID: 1123039786}"));
        Assert.That(previewBlock.Value,
            Does.Contain("m_GameObject: {fileID: 1123039775}")
                .And.Contain("selectionManager: {fileID: 1123039782}")
                .And.Contain("playerCommandInput: {fileID: 1123039776}"));
    }

    private Fixture CreateFixture()
    {
        GameObject host = CreateObject("Movement Panel Host");
        ShipSelectionManager selection = host.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher = host.AddComponent<ShipCommandDispatcher>();
        FormationCommandController formation =
            host.AddComponent<FormationCommandController>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(dispatcher, "formationCommandController", formation);
        ShipPlayerCommandInput input = host.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(input, "selectionManager", selection);
        SetPrivateField(input, "commandDispatcher", dispatcher);
        ShipDirectedHeadingPreviewController preview =
            host.AddComponent<ShipDirectedHeadingPreviewController>();
        MovementStatusPanel panel = host.AddComponent<MovementStatusPanel>();
        SetPrivateField(panel, "selectionManager", selection);
        SetPrivateField(panel, "playerInput", input);
        SetPrivateField(panel, "headingPreview", preview);
        System.Action handler = () => InvokePrivate(panel, "OnSelectionChanged");
        selection.SelectionMembershipChanged += handler;
        Refresh(panel);
        return new Fixture(selection, dispatcher, input, preview, panel);
    }

    private ShipFixture CreateShip(float heading)
    {
        GameObject root = CreateObject("Movement Ship");
        root.transform.rotation = Quaternion.Euler(0f, heading, 0f);
        GlobalWind wind = root.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;
        ShipSailingSpeed speed = root.AddComponent<ShipSailingSpeed>();
        ShipTurning turning = root.AddComponent<ShipTurning>();
        ShipHeadingController headingController =
            root.AddComponent<ShipHeadingController>();
        ShipTacking tack = root.AddComponent<ShipTacking>();
        ShipWearing wear = root.AddComponent<ShipWearing>();
        ShipManeuverPlanner planner = root.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController destination =
            root.AddComponent<ShipDestinationController>();
        SetPrivateField(headingController, "shipTurning", turning);
        SetPrivateField(tack, "shipSailingSpeed", speed);
        SetPrivateField(tack, "shipTurning", turning);
        SetPrivateField(tack, "headingController", headingController);
        SetPrivateField(wear, "shipSailingSpeed", speed);
        SetPrivateField(wear, "headingController", headingController);
        SetPrivateField(planner, "globalWind", wind);
        SetPrivateField(planner, "headingController", headingController);
        SetPrivateField(planner, "shipTacking", tack);
        SetPrivateField(planner, "shipWearing", wear);
        SetPrivateField(destination, "maneuverPlanner", planner);
        return new ShipFixture(destination, speed, planner, tack, wear);
    }

    private GameObject CreateObject(string name)
    {
        GameObject item = new(name);
        created.Add(item);
        return item;
    }

    private static Vector2 TurnPoint(MovementStatusPanel panel,
        TurnDirection direction)
    {
        Rect rect = panel.PanelRect;
        return new Vector2(direction == TurnDirection.Clockwise
            ? rect.xMax - 30f : rect.xMin + 30f, rect.y + 350f);
    }

    private static TurnDirection Opposite(TurnDirection direction)
    {
        return direction == TurnDirection.Clockwise
            ? TurnDirection.CounterClockwise : TurnDirection.Clockwise;
    }

    private static void Refresh(MovementStatusPanel panel)
    {
        InvokePrivate(panel, "RefreshStatus");
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private readonly struct Fixture
    {
        public readonly ShipSelectionManager Selection;
        public readonly ShipCommandDispatcher Dispatcher;
        public readonly ShipPlayerCommandInput Input;
        public readonly ShipDirectedHeadingPreviewController Preview;
        public readonly MovementStatusPanel Panel;

        public Fixture(ShipSelectionManager selection,
            ShipCommandDispatcher dispatcher, ShipPlayerCommandInput input,
            ShipDirectedHeadingPreviewController preview,
            MovementStatusPanel panel)
        {
            Selection = selection;
            Dispatcher = dispatcher;
            Input = input;
            Preview = preview;
            Panel = panel;
        }
    }

    private readonly struct ShipFixture
    {
        public readonly ShipDestinationController Destination;
        public readonly ShipSailingSpeed Speed;
        public readonly ShipManeuverPlanner Planner;
        public readonly ShipTacking Tacking;
        public readonly ShipWearing Wearing;

        public ShipFixture(ShipDestinationController destination,
            ShipSailingSpeed speed, ShipManeuverPlanner planner,
            ShipTacking tacking, ShipWearing wearing)
        {
            Destination = destination;
            Speed = speed;
            Planner = planner;
            Tacking = tacking;
            Wearing = wearing;
        }
    }
}
