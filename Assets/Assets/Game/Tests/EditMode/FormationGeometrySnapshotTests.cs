using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FormationGeometrySnapshotTests
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
    public void TryCapture_UsesBoundsMidpointInsteadOfAveragePosition()
    {
        ShipDestinationController first = CreateShip("First", new Vector3(0f, 0f, 0f), 0f);
        ShipDestinationController second = CreateShip("Second", new Vector3(10f, 0f, 0f), 0f);
        ShipDestinationController third = CreateShip("Third", new Vector3(10f, 0f, 2f), 0f);

        FormationGeometrySnapshot snapshot = Capture(first, second, third);
        Vector3 average = (first.transform.position + second.transform.position
            + third.transform.position) / 3f;

        Assert.That(snapshot.FormationCenter.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(snapshot.FormationCenter.z, Is.EqualTo(1f).Within(0.001f));
        Assert.That(snapshot.FormationCenter, Is.Not.EqualTo(average));
    }


    [Test]
    public void TryCapture_UsesNormalizedHorizontalForwardSum()
    {
        ShipDestinationController first = CreateShip("First", Vector3.zero, 0f);
        ShipDestinationController second = CreateShip("Second", Vector3.right, 90f);

        FormationGeometrySnapshot snapshot = Capture(first, second);
        Vector3 expectedForward = new Vector3(1f, 0f, 1f).normalized;

        Assert.That(snapshot.FormationForward.x,
            Is.EqualTo(expectedForward.x).Within(0.001f));
        Assert.That(snapshot.FormationForward.z,
            Is.EqualTo(expectedForward.z).Within(0.001f));
        Assert.That(snapshot.FormationHeading, Is.EqualTo(45f).Within(0.001f));
    }


    [Test]
    public void TryCapture_UsesPrimaryHeadingWhenForwardSumIsNearZero()
    {
        ShipDestinationController primary = CreateShip("Primary", Vector3.zero, 90f);
        ShipDestinationController opposite = CreateShip("Opposite", Vector3.right, 270f);

        FormationGeometrySnapshot snapshot = Capture(primary, opposite);

        Assert.That(snapshot.FormationHeading, Is.EqualTo(90f).Within(0.001f));
        Assert.That(snapshot.FormationForward.x, Is.EqualTo(1f).Within(0.001f));
    }


    [Test]
    public void GetSlotWorldPosition_ReconstructsOriginalIrregularPositions()
    {
        ShipDestinationController first = CreateShip("First", new Vector3(-4f, 0f, 1f), 30f);
        ShipDestinationController second = CreateShip("Second", new Vector3(3f, 0f, 8f), 30f);
        ShipDestinationController third = CreateShip("Third", new Vector3(10f, 0f, -2f), 30f);
        FormationGeometrySnapshot snapshot = Capture(first, second, third);

        foreach (FormationGeometryMember member in snapshot.Members)
        {
            Vector3 reconstructed = FormationGeometrySnapshot.GetSlotWorldPosition(
                snapshot.FormationCenter,
                snapshot.FormationHeading,
                member.LocalX,
                member.LocalZ
            );

            Assert.That(reconstructed.x,
                Is.EqualTo(member.Ship.transform.position.x).Within(0.001f));
            Assert.That(reconstructed.z,
                Is.EqualTo(member.Ship.transform.position.z).Within(0.001f));
        }
    }


    [Test]
    public void TryCapture_PreservesIrregularAndAsymmetricLocalSpacing()
    {
        ShipDestinationController first = CreateShip("First", new Vector3(0f, 0f, 0f), 0f);
        ShipDestinationController second = CreateShip("Second", new Vector3(2f, 0f, 9f), 0f);
        ShipDestinationController third = CreateShip("Third", new Vector3(11f, 0f, 3f), 0f);
        FormationGeometrySnapshot snapshot = Capture(first, second, third);

        Assert.That(snapshot.Members[0].LocalX, Is.EqualTo(-5.5f).Within(0.001f));
        Assert.That(snapshot.Members[0].LocalZ, Is.EqualTo(-4.5f).Within(0.001f));
        Assert.That(snapshot.Members[1].LocalX, Is.EqualTo(-3.5f).Within(0.001f));
        Assert.That(snapshot.Members[1].LocalZ, Is.EqualTo(4.5f).Within(0.001f));
        Assert.That(snapshot.Members[2].LocalX, Is.EqualTo(5.5f).Within(0.001f));
        Assert.That(snapshot.Members[2].LocalZ, Is.EqualTo(-1.5f).Within(0.001f));
    }


    [Test]
    public void TryCapture_PreservesInputMemberOrder()
    {
        ShipDestinationController first = CreateShip("First", Vector3.zero, 0f);
        ShipDestinationController second = CreateShip("Second", Vector3.right, 0f);
        ShipDestinationController third = CreateShip("Third", Vector3.forward, 0f);
        FormationGeometrySnapshot snapshot = Capture(second, third, first);

        Assert.That(snapshot.Members[0].Ship, Is.SameAs(second));
        Assert.That(snapshot.Members[1].Ship, Is.SameAs(third));
        Assert.That(snapshot.Members[2].Ship, Is.SameAs(first));
    }


    private FormationGeometrySnapshot Capture(
        params ShipDestinationController[] ships
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


    private ShipDestinationController CreateShip(
        string name,
        Vector3 position,
        float heading
    )
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        gameObject.transform.position = position;
        gameObject.transform.rotation = Quaternion.Euler(0f, heading, 0f);
        return gameObject.AddComponent<ShipDestinationController>();
    }
}
