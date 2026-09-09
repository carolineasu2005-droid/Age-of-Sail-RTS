using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DesignatedFormationLeadSelectionTests
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
    public void TrySetDesignatedFormationLead_RejectsEmptyAndSingleSelection()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController ship = CreateShip("Single");

        Assert.That(selection.TrySetDesignatedFormationLead(ship), Is.False);
        selection.AddSelection(ship);
        Assert.That(selection.TrySetDesignatedFormationLead(ship), Is.False);
        Assert.That(selection.HasDesignatedFormationLead, Is.False);
    }


    [Test]
    public void TrySetDesignatedFormationLead_AcceptsSelectedMemberWithoutChangingPrimary()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController primary = CreateShip("Primary");
        ShipDestinationController designated = CreateShip("Designated");
        selection.AddSelection(primary);
        selection.AddSelection(designated);

        Assert.That(selection.TrySetDesignatedFormationLead(designated), Is.True);
        Assert.That(selection.DesignatedFormationLead, Is.SameAs(designated));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(primary));
        Assert.That(selection.SelectedShips,
            Is.EqualTo(new[] { primary, designated }));
    }


    [Test]
    public void TrySetDesignatedFormationLead_RejectsUnselectedMember()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        ShipDestinationController outsider = CreateShip("Outsider");
        selection.AddSelection(first);
        selection.AddSelection(second);

        Assert.That(selection.TrySetDesignatedFormationLead(outsider), Is.False);
        Assert.That(selection.HasDesignatedFormationLead, Is.False);
    }


    [Test]
    public void TrySetDesignatedFormationLead_ReplacesAndClearsDesignation()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        selection.AddSelection(first);
        selection.AddSelection(second);

        Assert.That(selection.TrySetDesignatedFormationLead(first), Is.True);
        Assert.That(selection.TrySetDesignatedFormationLead(second), Is.True);
        Assert.That(selection.DesignatedFormationLead, Is.SameAs(second));

        selection.ClearDesignatedFormationLead();
        Assert.That(selection.HasDesignatedFormationLead, Is.False);
    }


    [Test]
    public void MembershipChangeAndInvalidMember_ClearDesignation()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        ShipDestinationController third = CreateShip("Third");
        selection.AddSelection(first);
        selection.AddSelection(second);
        selection.TrySetDesignatedFormationLead(second);

        selection.AddSelection(third);
        Assert.That(selection.HasDesignatedFormationLead, Is.False);

        selection.TrySetDesignatedFormationLead(second);
        second.gameObject.SetActive(false);
        Assert.That(selection.HasDesignatedFormationLead, Is.False);
    }


    [Test]
    public void FormationCommandDispatch_DoesNotClearDesignation()
    {
        ShipSelectionManager selection = CreateSelectionManager(
            out ShipCommandDispatcher dispatcher);
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        selection.AddSelection(first);
        selection.AddSelection(second);
        selection.TrySetDesignatedFormationLead(second);

        dispatcher.DispatchDestination(
            new Vector3(20f, 0f, 30f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );

        Assert.That(selection.DesignatedFormationLead, Is.SameAs(second));
    }


    [Test]
    public void DesignatedLead_LineAheadSnapshotPlacesDesignatedMemberFrontmost()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController front = CreateShip("Front");
        ShipDestinationController designatedMiddle = CreateShip("Middle");
        ShipDestinationController rearPrimary = CreateShip("Rear");
        front.transform.position = new Vector3(0f, 0f, 6f);
        designatedMiddle.transform.position = Vector3.zero;
        rearPrimary.transform.position = new Vector3(0f, 0f, -5f);
        selection.AddSelection(rearPrimary);
        selection.AddSelection(designatedMiddle);
        selection.AddSelection(front);
        selection.TrySetDesignatedFormationLead(designatedMiddle);
        FormationGeometrySnapshot captured = Capture(selection);

        FormationGeometrySnapshot lineAhead =
            FormationLayoutGenerator.CreateStandardLineAhead(
                FormationMemberOrder.CreateWithDesignatedLead(
                    captured,
                    selection.DesignatedFormationLead
                ),
                captured.FormationCenter,
                captured.FormationHeading
            );
        front.transform.position = new Vector3(0f, 0f, -30f);
        designatedMiddle.transform.position = new Vector3(0f, 0f, 30f);

        Assert.That(selection.PrimarySelectedShip, Is.SameAs(rearPrimary));
        Assert.That(lineAhead.Members[0].Ship, Is.SameAs(designatedMiddle));
        Assert.That(lineAhead.Members[0].LocalZ, Is.EqualTo(100f));
        Assert.That(lineAhead.Members[1].Ship, Is.SameAs(front));
        Assert.That(lineAhead.Members[2].Ship, Is.SameAs(rearPrimary));
    }


    [Test]
    public void MembershipChange_LineAheadFallsBackToAutomaticOrder()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController front = CreateShip("Front");
        ShipDestinationController designatedMiddle = CreateShip("Middle");
        ShipDestinationController rear = CreateShip("Rear");
        ShipDestinationController extra = CreateShip("Extra");
        front.transform.position = new Vector3(0f, 0f, 6f);
        designatedMiddle.transform.position = Vector3.zero;
        rear.transform.position = new Vector3(0f, 0f, -5f);
        extra.transform.position = new Vector3(0f, 0f, -10f);
        selection.AddSelection(rear);
        selection.AddSelection(designatedMiddle);
        selection.AddSelection(front);
        selection.TrySetDesignatedFormationLead(designatedMiddle);

        selection.AddSelection(extra);
        FormationMemberOrder order =
            FormationMemberOrder.CreateWithDesignatedLead(
                Capture(selection),
                selection.DesignatedFormationLead
            );

        Assert.That(selection.HasDesignatedFormationLead, Is.False);
        Assert.That(order.Lead, Is.SameAs(front));
    }


    private ShipSelectionManager CreateSelectionManager(
        out ShipCommandDispatcher dispatcher
    )
    {
        GameObject commandObject = new GameObject("Selection Manager");
        createdObjects.Add(commandObject);
        ShipSelectionManager selection =
            commandObject.AddComponent<ShipSelectionManager>();
        dispatcher = commandObject.AddComponent<ShipCommandDispatcher>();
        return selection;
    }


    private ShipDestinationController CreateShip(string name)
    {
        GameObject shipObject = new GameObject(name);
        createdObjects.Add(shipObject);
        return shipObject.AddComponent<ShipDestinationController>();
    }


    private static FormationGeometrySnapshot Capture(
        ShipSelectionManager selection
    )
    {
        bool captured = FormationGeometrySnapshot.TryCapture(
            selection.SelectedShips,
            selection.PrimarySelectedShip,
            out FormationGeometrySnapshot snapshot
        );

        Assert.That(captured, Is.True);
        return snapshot;
    }
}
