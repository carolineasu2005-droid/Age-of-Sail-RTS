using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FormationLayoutGeneratorTests
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


    [TestCase(1, new[] { 0f })]
    [TestCase(2, new[] { 50f, -50f })]
    [TestCase(3, new[] { 100f, 0f, -100f })]
    [TestCase(4, new[] { 150f, 50f, -50f, -150f })]
    [TestCase(5, new[] { 200f, 100f, 0f, -100f, -200f })]
    public void CreateStandardLineAhead_UsesCenteredPrototypeSlots(
        int memberCount,
        float[] expectedLocalZ
    )
    {
        FormationGeometrySnapshot layout = CreateLayout(memberCount);

        Assert.That(layout.Members.Count, Is.EqualTo(memberCount));
        for (int index = 0; index < memberCount; index++)
        {
            Assert.That(layout.Members[index].LocalX, Is.EqualTo(0f));
            Assert.That(layout.Members[index].LocalZ,
                Is.EqualTo(expectedLocalZ[index]).Within(0.001f));
        }
    }


    [TestCase(3)]
    [TestCase(4)]
    public void CreateStandardLineAhead_RemainsCenteredForOddAndEvenCounts(
        int memberCount
    )
    {
        FormationGeometrySnapshot layout = CreateLayout(memberCount);

        Assert.That((layout.BoundsMinZ + layout.BoundsMaxZ) * 0.5f,
            Is.EqualTo(0f).Within(0.001f));
    }


    [Test]
    public void CreateStandardLineAhead_UsesExactAdjacentSpacingAndOrderIdentity()
    {
        ShipDestinationController light = CreateShip("Light");
        ShipDestinationController medium = CreateShip("Medium");
        ShipDestinationController heavy = CreateShip("Heavy");
        FormationMemberOrder order = new FormationMemberOrder(
            new[] { light, medium, heavy }
        );

        FormationGeometrySnapshot layout =
            FormationLayoutGenerator.CreateStandardLineAhead(
                order,
                Vector3.zero,
                0f
            );

        Assert.That(layout.Members[0].Ship, Is.SameAs(light));
        Assert.That(layout.Members[1].Ship, Is.SameAs(medium));
        Assert.That(layout.Members[2].Ship, Is.SameAs(heavy));
        Assert.That(layout.Members[0].LocalZ - layout.Members[1].LocalZ,
            Is.EqualTo(FormationLayoutGenerator.StandardLineAheadSpacing));
        Assert.That(layout.Members[1].LocalZ - layout.Members[2].LocalZ,
            Is.EqualTo(FormationLayoutGenerator.StandardLineAheadSpacing));
    }


    [Test]
    public void CreateStandardLineAhead_RotatedHeadingReconstructsAlongFormationForward()
    {
        FormationGeometrySnapshot layout = CreateLayout(
            3,
            new Vector3(10f, 0f, 20f),
            90f
        );

        Vector3 frontWorldPosition = FormationGeometrySnapshot.GetSlotWorldPosition(
            layout.FormationCenter,
            layout.FormationHeading,
            layout.Members[0].LocalX,
            layout.Members[0].LocalZ
        );
        Vector3 rearWorldPosition = FormationGeometrySnapshot.GetSlotWorldPosition(
            layout.FormationCenter,
            layout.FormationHeading,
            layout.Members[2].LocalX,
            layout.Members[2].LocalZ
        );

        Assert.That(frontWorldPosition.x, Is.EqualTo(110f).Within(0.001f));
        Assert.That(frontWorldPosition.z, Is.EqualTo(20f).Within(0.001f));
        Assert.That(rearWorldPosition.x, Is.EqualTo(-90f).Within(0.001f));
        Assert.That(rearWorldPosition.z, Is.EqualTo(20f).Within(0.001f));
    }


    [Test]
    public void CreateStandardLineAhead_DoesNotUseSelectionPrimary()
    {
        ShipDestinationController heavyPrimary = CreateShip("Heavy");
        ShipDestinationController lightLead = CreateShip("Light");
        GameObject selectionObject = new GameObject("Selection Manager");
        createdObjects.Add(selectionObject);
        ShipSelectionManager selection =
            selectionObject.AddComponent<ShipSelectionManager>();
        selection.SelectSingle(heavyPrimary);
        FormationMemberOrder order = new FormationMemberOrder(
            new[] { lightLead, heavyPrimary }
        );

        FormationGeometrySnapshot layout =
            FormationLayoutGenerator.CreateStandardLineAhead(
                order,
                Vector3.zero,
                0f
            );

        Assert.That(selection.PrimarySelectedShip, Is.SameAs(heavyPrimary));
        Assert.That(layout.Members[0].Ship, Is.SameAs(lightLead));
        Assert.That(layout.Members[0].LocalZ, Is.EqualTo(50f));
    }


    private FormationGeometrySnapshot CreateLayout(
        int memberCount,
        Vector3? center = null,
        float heading = 0f
    )
    {
        List<ShipDestinationController> members =
            new List<ShipDestinationController>(memberCount);

        for (int index = 0; index < memberCount; index++)
        {
            members.Add(CreateShip($"Ship {index}"));
        }

        return FormationLayoutGenerator.CreateStandardLineAhead(
            new FormationMemberOrder(members),
            center ?? Vector3.zero,
            heading
        );
    }


    private ShipDestinationController CreateShip(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<ShipDestinationController>();
    }
}
