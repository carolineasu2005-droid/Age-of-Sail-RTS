using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MovementPreviewDisablePlayModeTests
{
    private readonly List<GameObject> created = new();
    private ShipDirectedHeadingPreviewController preview;
    private MovementStatusPanel panel;
    private ShipSelectionManager selection;
    private ShipCommandDispatcher dispatcher;
    private ShipDestinationController ship;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GameObject command = CreateObject("Movement Preview Command");
        selection = command.AddComponent<ShipSelectionManager>();
        dispatcher = command.AddComponent<ShipCommandDispatcher>();
        FormationCommandController formation =
            command.AddComponent<FormationCommandController>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(dispatcher, "formationCommandController", formation);
        ShipPlayerCommandInput input =
            command.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(input, "selectionManager", selection);
        SetPrivateField(input, "commandDispatcher", dispatcher);
        preview = command.AddComponent<ShipDirectedHeadingPreviewController>();
        panel = command.AddComponent<MovementStatusPanel>();

        GameObject root = CreateObject("Movement Preview Ship");
        GlobalWind wind = root.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;
        ShipSailingSpeed speed = root.AddComponent<ShipSailingSpeed>();
        ShipTurning turning = root.AddComponent<ShipTurning>();
        ShipHeadingController heading = root.AddComponent<ShipHeadingController>();
        ShipTacking tack = root.AddComponent<ShipTacking>();
        ShipWearing wear = root.AddComponent<ShipWearing>();
        ShipManeuverPlanner planner = root.AddComponent<ShipManeuverPlanner>();
        ship = root.AddComponent<ShipDestinationController>();
        SetPrivateField(heading, "shipTurning", turning);
        SetPrivateField(tack, "shipSailingSpeed", speed);
        SetPrivateField(tack, "shipTurning", turning);
        SetPrivateField(tack, "headingController", heading);
        SetPrivateField(wear, "shipSailingSpeed", speed);
        SetPrivateField(wear, "headingController", heading);
        SetPrivateField(planner, "globalWind", wind);
        SetPrivateField(planner, "headingController", heading);
        SetPrivateField(planner, "shipTacking", tack);
        SetPrivateField(planner, "shipWearing", wear);
        SetPrivateField(ship, "maneuverPlanner", planner);
        selection.SelectSingle(ship);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject item in created)
        {
            if (item != null)
            {
                Object.Destroy(item);
            }
        }

        created.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisablingPreviewComponent_CancelsGhostWithoutCommand()
    {
        double start = Time.unscaledTimeAsDouble;
        Assert.That(preview.BeginPreview(ship, TurnDirection.Clockwise, start),
            Is.True);
        Assert.That(preview.UpdatePreview(start + 0.2d), Is.True);
        Assert.That(preview.IsGhostVisible, Is.True);

        preview.enabled = false;

        Assert.That(preview.IsPreviewActive, Is.False);
        Assert.That(preview.IsGhostVisible, Is.False);
        Assert.That(dispatcher.DispatchSequence, Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisablingPanel_CancelsOwnedHoldAndGhostWithoutCommand()
    {
        double start = Time.unscaledTimeAsDouble;
        Assert.That(panel.BeginTurnHold(TurnDirection.Clockwise, start),
            Is.True);
        Assert.That(preview.IsGhostVisible, Is.True);

        panel.enabled = false;

        Assert.That(panel.IsTurnPointerCaptured, Is.False);
        Assert.That(preview.IsPreviewActive, Is.False);
        Assert.That(preview.IsGhostVisible, Is.False);
        Assert.That(dispatcher.DispatchSequence, Is.Zero);
        yield return null;
    }

    private GameObject CreateObject(string name)
    {
        GameObject item = new(name);
        created.Add(item);
        return item;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
