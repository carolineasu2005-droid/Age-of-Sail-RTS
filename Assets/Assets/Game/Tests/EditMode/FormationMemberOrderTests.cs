using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FormationMemberOrderTests
{
    private readonly List<GameObject> createdObjects = new();


    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
    }


    [Test]
    public void Constructor_SortsCapturedMembersFrontToBackAndSelectsLead()
    {
        ShipDestinationController rear = CreateShip("Rear", new Vector3(0f, 0f, -4f));
        ShipDestinationController front = CreateShip("Front", new Vector3(0f, 0f, 8f));
        ShipDestinationController middle = CreateShip("Middle", new Vector3(0f, 0f, 2f));

        FormationMemberOrder order = CreateOrder(rear, front, middle);

        Assert.That(order.OrderedMembers,
            Is.EqualTo(new[] { front, middle, rear }));
        Assert.That(order.Lead, Is.SameAs(front));
        Assert.That(order.GetMember(order.Count - 1), Is.SameAs(rear));
    }


    [Test]
    public void Constructor_EqualLocalZPreservesCapturedOrder()
    {
        ShipDestinationController first = CreateShip("First", new Vector3(-2f, 0f, 4f));
        ShipDestinationController second = CreateShip("Second", new Vector3(2f, 0f, 4f));
        ShipDestinationController rear = CreateShip("Rear", new Vector3(0f, 0f, -3f));

        FormationMemberOrder order = CreateOrder(second, first, rear);

        Assert.That(order.OrderedMembers,
            Is.EqualTo(new[] { second, first, rear }));
    }


    [Test]
    public void Constructor_DoesNotUseSelectionPrimaryOrShipIdentity()
    {
        ShipDestinationController heavyPrimary = CreateShip(
            "Heavy Primary", new Vector3(0f, 0f, -3f));
        ShipDestinationController light = CreateShip(
            "Light", new Vector3(0f, 0f, 6f));
        ShipDestinationController medium = CreateShip(
            "Medium", Vector3.zero);
        GameObject selectionObject = new GameObject("Selection Manager");
        createdObjects.Add(selectionObject);
        ShipSelectionManager selection =
            selectionObject.AddComponent<ShipSelectionManager>();
        selection.SelectSingle(heavyPrimary);

        FormationMemberOrder order = CreateOrder(heavyPrimary, light, medium);

        Assert.That(selection.PrimarySelectedShip, Is.SameAs(heavyPrimary));
        Assert.That(order.Lead, Is.SameAs(light));
        Assert.That(order.IndexOf(heavyPrimary), Is.EqualTo(2));
        Assert.That(order.IndexOf(medium), Is.EqualTo(1));
    }


    [Test]
    public void Constructor_DoesNotMutateCapturedOrderAndRemainsSnapshotStable()
    {
        ShipDestinationController first = CreateShip("First", new Vector3(0f, 0f, 1f));
        ShipDestinationController second = CreateShip("Second", new Vector3(0f, 0f, 5f));
        ShipDestinationController third = CreateShip("Third", new Vector3(0f, 0f, -2f));
        ShipDestinationController[] capturedOrder = { first, second, third };
        FormationGeometrySnapshot snapshot = Capture(capturedOrder);

        FormationMemberOrder order = new FormationMemberOrder(snapshot);
        first.transform.position = new Vector3(0f, 0f, 20f);
        second.transform.position = new Vector3(0f, 0f, -20f);

        Assert.That(snapshot.Members[0].Ship, Is.SameAs(first));
        Assert.That(snapshot.Members[1].Ship, Is.SameAs(second));
        Assert.That(snapshot.Members[2].Ship, Is.SameAs(third));
        Assert.That(capturedOrder,
            Is.EqualTo(new[] { first, second, third }));
        Assert.That(order.OrderedMembers,
            Is.EqualTo(new[] { second, first, third }));
    }


    [Test]
    public void CreateWithDesignatedLead_AbsentOrInvalidLeadUsesAutomaticOrder()
    {
        ShipDestinationController front = CreateShip("Front", new Vector3(0f, 0f, 6f));
        ShipDestinationController middle = CreateShip("Middle", Vector3.zero);
        ShipDestinationController rear = CreateShip("Rear", new Vector3(0f, 0f, -4f));
        ShipDestinationController outsider = CreateShip("Outsider", Vector3.right);
        FormationGeometrySnapshot snapshot = Capture(new[] { rear, middle, front });

        FormationMemberOrder automatic = FormationMemberOrder.CreateAutomatic(snapshot);
        FormationMemberOrder absent = FormationMemberOrder.CreateWithDesignatedLead(
            snapshot, null);
        FormationMemberOrder invalid = FormationMemberOrder.CreateWithDesignatedLead(
            snapshot, outsider);

        Assert.That(absent.OrderedMembers, Is.EqualTo(automatic.OrderedMembers));
        Assert.That(invalid.OrderedMembers, Is.EqualTo(automatic.OrderedMembers));
    }


    [Test]
    public void CreateWithDesignatedLead_FrontmostMemberPreservesAutomaticOrder()
    {
        ShipDestinationController front = CreateShip("Front", new Vector3(0f, 0f, 6f));
        ShipDestinationController middle = CreateShip("Middle", Vector3.zero);
        ShipDestinationController rear = CreateShip("Rear", new Vector3(0f, 0f, -4f));
        FormationGeometrySnapshot snapshot = Capture(new[] { rear, middle, front });

        FormationMemberOrder order = FormationMemberOrder.CreateWithDesignatedLead(
            snapshot, front);

        Assert.That(order.OrderedMembers, Is.EqualTo(new[] { front, middle, rear }));
        Assert.That(order.Lead, Is.SameAs(front));
    }


    [Test]
    public void CreateWithDesignatedLead_MiddleMemberMovesToLeadAndKeepsRelativeOrder()
    {
        ShipDestinationController front = CreateShip("Light", new Vector3(0f, 0f, 6f));
        ShipDestinationController designatedMiddle = CreateShip(
            "Medium", Vector3.zero);
        ShipDestinationController rearPrimary = CreateShip(
            "Heavy", new Vector3(0f, 0f, -4f));
        GameObject selectionObject = new GameObject("Selection Manager");
        createdObjects.Add(selectionObject);
        ShipSelectionManager selection =
            selectionObject.AddComponent<ShipSelectionManager>();
        selection.SelectSingle(rearPrimary);
        FormationGeometrySnapshot snapshot = Capture(
            new[] { rearPrimary, designatedMiddle, front });

        FormationMemberOrder order = FormationMemberOrder.CreateWithDesignatedLead(
            snapshot, designatedMiddle);

        Assert.That(selection.PrimarySelectedShip, Is.SameAs(rearPrimary));
        Assert.That(order.OrderedMembers,
            Is.EqualTo(new[] { designatedMiddle, front, rearPrimary }));
        Assert.That(order.Lead, Is.SameAs(designatedMiddle));
    }


    [Test]
    public void CreateWithDesignatedLead_RearmostMemberMovesToLeadAndRemainsStable()
    {
        ShipDestinationController front = CreateShip("Light", new Vector3(0f, 0f, 6f));
        ShipDestinationController middle = CreateShip("Medium", Vector3.zero);
        ShipDestinationController rear = CreateShip("Heavy", new Vector3(0f, 0f, -4f));
        FormationGeometrySnapshot snapshot = Capture(new[] { rear, middle, front });

        FormationMemberOrder order = FormationMemberOrder.CreateWithDesignatedLead(
            snapshot, rear);
        front.transform.position = new Vector3(0f, 0f, -30f);
        rear.transform.position = new Vector3(0f, 0f, 30f);

        Assert.That(order.OrderedMembers, Is.EqualTo(new[] { rear, front, middle }));
        Assert.That(order.Lead, Is.SameAs(rear));
    }


    private FormationMemberOrder CreateOrder(
        params ShipDestinationController[] ships
    )
    {
        return new FormationMemberOrder(Capture(ships));
    }


    private FormationGeometrySnapshot Capture(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        bool captured = FormationGeometrySnapshot.TryCapture(
            ships,
            ships[0],
            out FormationGeometrySnapshot snapshot
        );

        Assert.That(captured, Is.True);
        return snapshot;
    }


    private ShipDestinationController CreateShip(string name, Vector3 position)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        gameObject.transform.position = position;
        return gameObject.AddComponent<ShipDestinationController>();
    }
}
