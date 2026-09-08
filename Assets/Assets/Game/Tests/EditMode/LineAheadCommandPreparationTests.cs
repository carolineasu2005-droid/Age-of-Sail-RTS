using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LineAheadCommandPreparationTests
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
    public void ToggleLineAheadTemplate_EmptyOrSingleSelectionDoesNotArm()
    {
        ShipPlayerCommandInput input = CreateInput(out ShipSelectionManager selection,
            out _);

        input.ToggleLineAheadTemplate();
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));

        selection.AddSelection(CreateShip("Single"));
        input.ToggleLineAheadTemplate();
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));
    }


    [Test]
    public void ToggleLineAheadTemplate_MultiSelectionArmsAndSecondToggleCancels()
    {
        ShipPlayerCommandInput input = CreateInput(out ShipSelectionManager selection,
            out _);
        selection.AddSelection(CreateShip("First"));
        selection.AddSelection(CreateShip("Second"));

        Assert.That(selection.SelectedCount, Is.EqualTo(2));

        input.ToggleLineAheadTemplate();
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.LineAhead));

        input.ToggleLineAheadTemplate();
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));
    }


    [Test]
    public void DispatchFormationDestination_PreservesProvidedLineAheadSnapshot()
    {
        ShipPlayerCommandInput input = CreateInput(out ShipSelectionManager selection,
            out ShipCommandDispatcher dispatcher);
        ShipDestinationController front = CreateShip("Front");
        ShipDestinationController rear = CreateShip("Rear");
        selection.AddSelection(front);
        selection.AddSelection(rear);
        FormationGeometrySnapshot lineAhead =
            FormationLayoutGenerator.CreateStandardLineAhead(
                new FormationMemberOrder(new[] { front, rear }),
                Vector3.zero,
                0f
            );

        dispatcher.DispatchFormationDestination(
            new Vector3(30f, 0f, 40f),
            lineAhead,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );

        Assert.That(dispatcher.PendingGroupGeometrySnapshot, Is.SameAs(lineAhead));
        Assert.That(dispatcher.PendingGroupHasExplicitFormationHeading, Is.False);
        Assert.That(dispatcher.PendingGroupGeometrySnapshot.Members[0].LocalZ,
            Is.EqualTo(50f));
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));
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
        ShipPlayerCommandInput input =
            commandObject.AddComponent<ShipPlayerCommandInput>();
        SetPrivateField(input, "selectionManager", selection);
        SetPrivateField(input, "commandDispatcher", dispatcher);
        input.enabled = false;
        input.enabled = true;
        return input;
    }


    private ShipDestinationController CreateShip(string name)
    {
        GameObject shipObject = new GameObject(name);
        createdObjects.Add(shipObject);
        return shipObject.AddComponent<ShipDestinationController>();
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
            $"Required serialized field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }
}
