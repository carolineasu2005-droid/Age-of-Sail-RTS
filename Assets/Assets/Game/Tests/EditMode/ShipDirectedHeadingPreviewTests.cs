using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipDirectedHeadingPreviewTests
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

    [TestCase(TurnDirection.Clockwise, 110f)]
    [TestCase(TurnDirection.CounterClockwise, 90f)]
    public void CompletedTenthSecond_AdvancesOneSignedStep(
        TurnDirection direction, float expected)
    {
        Fixture fixture = CreateFixture(100f);
        Assert.That(fixture.Preview.BeginPreview(fixture.Ship, direction, 5d), Is.True);
        Assert.That(fixture.Preview.CompletedSteps, Is.Zero);
        AssertHeading(100f, fixture.Preview.TargetHeading);

        fixture.Preview.UpdatePreview(5.099d);
        Assert.That(fixture.Preview.CompletedSteps, Is.Zero);
        fixture.Preview.UpdatePreview(5.1d);
        Assert.That(fixture.Preview.CompletedSteps, Is.EqualTo(1));
        Assert.That(fixture.Preview.AngularDelta, Is.EqualTo(10f));
        AssertHeading(expected, fixture.Preview.TargetHeading);
    }

    [Test]
    public void SixTenths_ProducesExactlySixStepsFromInitialHeading()
    {
        Fixture fixture = CreateFixture(100f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);

        fixture.Preview.UpdatePreview(0.6d);

        Assert.That(fixture.Preview.CompletedSteps, Is.EqualTo(6));
        Assert.That(fixture.Preview.AngularDelta, Is.EqualTo(60f));
        AssertHeading(160f, fixture.Preview.TargetHeading);
    }

    [Test]
    public void ExplicitUnscaledTime_AdvancesWhileGameplayTimeIsPaused()
    {
        Fixture fixture = CreateFixture(0f);
        float previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 1d);
            fixture.Preview.UpdatePreview(1.2d);
            Assert.That(fixture.Preview.CompletedSteps, Is.EqualTo(2));
            AssertHeading(20f, fixture.Preview.TargetHeading);
        }
        finally
        {
            Time.timeScale = previousTimeScale;
        }
    }

    [Test]
    public void ShortRelease_ClearsGhostWithoutCommand()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);

        Assert.That(fixture.Preview.CommitPreview(0.099d, out _), Is.False);

        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Planner.CommandSequence, Is.Zero);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void PhysicalYawChange_DoesNotMoveFrozenTarget()
    {
        Fixture fixture = CreateFixture(100f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Ship.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

        fixture.Preview.UpdatePreview(0.2d);

        AssertHeading(100f, fixture.Preview.InitialHeading);
        AssertHeading(120f, fixture.Preview.TargetHeading);
        AssertHeading(135f, fixture.Ship.transform.eulerAngles.y);
    }

    [TestCase(350f, TurnDirection.Clockwise, 10f)]
    [TestCase(5f, TurnDirection.CounterClockwise, 345f)]
    public void TargetHeading_WrapsAcrossNorth(
        float start, TurnDirection direction, float expected)
    {
        Fixture fixture = CreateFixture(start);
        fixture.Preview.BeginPreview(fixture.Ship, direction, 0d);

        fixture.Preview.UpdatePreview(0.2d);

        AssertHeading(expected, fixture.Preview.TargetHeading);
    }

    [Test]
    public void LongHold_SaturatesAtThirtyFiveSteps()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);

        fixture.Preview.UpdatePreview(20d);

        Assert.That(fixture.Preview.CompletedSteps, Is.EqualTo(35));
        Assert.That(fixture.Preview.AngularDelta, Is.EqualTo(350f));
        AssertHeading(350f, fixture.Preview.TargetHeading);
    }

    [Test]
    public void BeginAndUpdates_DoNotDispatchMovement()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(0.1d);
        fixture.Preview.UpdatePreview(0.3d);

        Assert.That(fixture.Planner.CommandSequence, Is.Zero);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
        Assert.That(fixture.Heading.IsActive, Is.False);
    }

    [Test]
    public void Commit_UsesOneHighLevelDirectedHeadingCommand()
    {
        Fixture fixture = CreateFixture(100f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(0.3d);
        Assert.That(fixture.Planner.CommandSequence, Is.Zero);

        bool accepted = fixture.Preview.CommitPreview(0.6d, out var result);
        Assert.That(accepted, Is.True, $"Failure: {result.Failure}");

        Assert.That(result.Accepted, Is.True);
        AssertHeading(160f, result.TargetHeading);
        Assert.That(result.Direction, Is.EqualTo(TurnDirection.Clockwise));
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.EqualTo(1));
        Assert.That(fixture.Planner.CommandSequence, Is.EqualTo(1));
        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Preview.CommitPreview(0.7d, out _), Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.EqualTo(1));
    }

    [Test]
    public void ExplicitCancel_DoesNotDispatch()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(0.2d);

        fixture.Preview.CancelPreview();

        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Preview.PreviewShip, Is.Null);
        Assert.That(fixture.Preview.CommitPreview(0.3d, out _), Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void SelectionMembershipChange_CancelsImmediately()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);

        fixture.Selection.ClearSelection();

        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Preview.CommitPreview(0.2d, out _), Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void DisabledShip_CancelsOnUpdate()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Ship.enabled = false;

        Assert.That(fixture.Preview.UpdatePreview(0.2d), Is.False);
        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.CommitPreview(0.3d, out _), Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void DestroyedShip_CancelsOnUpdate()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        Object.DestroyImmediate(fixture.Ship.gameObject);

        Assert.That(fixture.Preview.UpdatePreview(0.2d), Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void ExplicitCancelAfterEditModeDisable_ClearsPreviewWithoutCommand()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Preview.enabled = false;
        fixture.Preview.CancelPreview();

        Assert.That(fixture.Preview.IsPreviewActive, Is.False);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void GhostPose_IsActiveOnlyForSessionAndFollowsCurrentRootPosition()
    {
        Fixture fixture = CreateFixture(45f);
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Ship.transform.position = new Vector3(8f, 2f, -3f);

        fixture.Preview.UpdatePreview(0.2d);

        Assert.That(fixture.Preview.IsGhostVisible, Is.True);
        Assert.That(fixture.Preview.GhostPosition,
            Is.EqualTo(fixture.Ship.transform.position));
        AssertHeading(fixture.Preview.TargetHeading,
            fixture.Preview.GhostRotation.eulerAngles.y);
        fixture.Preview.CancelPreview();
        Assert.That(fixture.Preview.IsGhostVisible, Is.False);
    }

    [Test]
    public void Classification_UsesLivePlannerQueryForFrozenTarget()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(1.8d);
        Assert.That(fixture.Preview.PreviewManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        fixture.Ship.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
        fixture.Wind.windFromDegrees = 270f;

        fixture.Preview.UpdatePreview(1.8d);
        bool available = fixture.Planner.TryPreviewDirectedHeading(
            fixture.Preview.TargetHeading, TurnDirection.Clockwise,
            out var expected);

        Assert.That(fixture.Preview.IsClassificationAvailable, Is.EqualTo(available));
        Assert.That(fixture.Preview.PreviewManeuver, Is.EqualTo(expected));
        Assert.That(fixture.Preview.PreviewManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Wear));
        AssertHeading(180f, fixture.Preview.TargetHeading);
    }

    [Test]
    public void ClassificationUnavailable_IsReportedWithoutIssuingCommand()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Wind.enabled = false;

        fixture.Preview.UpdatePreview(0.2d);

        Assert.That(fixture.Preview.IsPreviewActive, Is.True);
        Assert.That(fixture.Preview.IsClassificationAvailable, Is.False);
        Assert.That(fixture.Preview.PreviewManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    [Test]
    public void Preview_DoesNotMutateMovementOrShipPose()
    {
        Fixture fixture = CreateFixture(0f);
        fixture.Speed.DecreasePlayerSpeedOrder();
        fixture.Speed.SetPlayerStopSpeedCap(0f);
        fixture.Ship.SetDestination(new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual);
        Vector3 position = fixture.Ship.transform.position;
        Quaternion rotation = fixture.Ship.transform.rotation;
        float speed = fixture.Speed.CurrentSpeed;
        int sequence = fixture.Planner.CommandSequence;
        float headingTarget = fixture.Heading.TargetHeading;
        bool headingActive = fixture.Heading.IsActive;
        bool tackActive = fixture.Tacking.IsActive;
        bool wearActive = fixture.Wearing.IsActive;

        fixture.Preview.BeginPreview(fixture.Ship, TurnDirection.Clockwise, 0d);
        fixture.Preview.UpdatePreview(0.3d);

        Assert.That(fixture.Ship.transform.position, Is.EqualTo(position));
        Assert.That(fixture.Ship.transform.rotation, Is.EqualTo(rotation));
        Assert.That(fixture.Speed.CurrentSpeed, Is.EqualTo(speed));
        Assert.That(fixture.Speed.IsPlayerStopped, Is.True);
        Assert.That(fixture.Ship.HasDestination, Is.True);
        Assert.That(fixture.Planner.CommandSequence, Is.EqualTo(sequence));
        Assert.That(fixture.Heading.TargetHeading, Is.EqualTo(headingTarget));
        Assert.That(fixture.Heading.IsActive, Is.EqualTo(headingActive));
        Assert.That(fixture.Tacking.IsActive, Is.EqualTo(tackActive));
        Assert.That(fixture.Wearing.IsActive, Is.EqualTo(wearActive));
        Assert.That(fixture.Dispatcher.DispatchSequence, Is.Zero);
    }

    private Fixture CreateFixture(float heading)
    {
        GameObject commandRoot = CreateObject("Command");
        ShipSelectionManager selection = commandRoot.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher = commandRoot.AddComponent<ShipCommandDispatcher>();
        FormationCommandController formation = commandRoot.AddComponent<FormationCommandController>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(dispatcher, "formationCommandController", formation);
        ShipPlayerCommandInput input =
            commandRoot.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(input, "selectionManager", selection);
        SetPrivateField(input, "commandDispatcher", dispatcher);
        ShipDirectedHeadingPreviewController preview =
            commandRoot.AddComponent<ShipDirectedHeadingPreviewController>();

        GameObject shipRoot = CreateObject("Ship");
        shipRoot.transform.rotation = Quaternion.Euler(0f, heading, 0f);
        GlobalWind wind = shipRoot.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;
        ShipSailingSpeed speed = shipRoot.AddComponent<ShipSailingSpeed>();
        ShipTurning turning = shipRoot.AddComponent<ShipTurning>();
        ShipHeadingController headingController =
            shipRoot.AddComponent<ShipHeadingController>();
        ShipTacking tacking = shipRoot.AddComponent<ShipTacking>();
        ShipWearing wearing = shipRoot.AddComponent<ShipWearing>();
        ShipManeuverPlanner planner = shipRoot.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController ship =
            shipRoot.AddComponent<ShipDestinationController>();
        SetPrivateField(headingController, "shipTurning", turning);
        SetPrivateField(tacking, "shipSailingSpeed", speed);
        SetPrivateField(tacking, "shipTurning", turning);
        SetPrivateField(tacking, "headingController", headingController);
        SetPrivateField(wearing, "shipSailingSpeed", speed);
        SetPrivateField(wearing, "headingController", headingController);
        SetPrivateField(planner, "globalWind", wind);
        SetPrivateField(planner, "headingController", headingController);
        SetPrivateField(planner, "shipTacking", tacking);
        SetPrivateField(planner, "shipWearing", wearing);
        SetPrivateField(ship, "maneuverPlanner", planner);
        selection.SelectSingle(ship);
        return new Fixture(selection, dispatcher, preview, ship, wind, speed,
            headingController, tacking, wearing, planner);
    }

    private GameObject CreateObject(string name)
    {
        GameObject item = new(name);
        created.Add(item);
        return item;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void AssertHeading(float expected, float actual)
    {
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(expected, actual)),
            Is.LessThanOrEqualTo(0.001f),
            $"Expected heading {expected}°, got {actual}°.");
    }

    private readonly struct Fixture
    {
        public readonly ShipSelectionManager Selection;
        public readonly ShipCommandDispatcher Dispatcher;
        public readonly ShipDirectedHeadingPreviewController Preview;
        public readonly ShipDestinationController Ship;
        public readonly GlobalWind Wind;
        public readonly ShipSailingSpeed Speed;
        public readonly ShipHeadingController Heading;
        public readonly ShipTacking Tacking;
        public readonly ShipWearing Wearing;
        public readonly ShipManeuverPlanner Planner;

        public Fixture(ShipSelectionManager selection, ShipCommandDispatcher dispatcher,
            ShipDirectedHeadingPreviewController preview,
            ShipDestinationController ship, GlobalWind wind, ShipSailingSpeed speed,
            ShipHeadingController heading, ShipTacking tacking, ShipWearing wearing,
            ShipManeuverPlanner planner)
        {
            Selection = selection;
            Dispatcher = dispatcher;
            Preview = preview;
            Ship = ship;
            Wind = wind;
            Speed = speed;
            Heading = heading;
            Tacking = tacking;
            Wearing = wearing;
            Planner = planner;
        }
    }
}
