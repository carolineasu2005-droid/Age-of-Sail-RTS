using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FormationPlacementCommandTests
{
    private readonly List<Object> createdObjects = new();


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.Destroy(createdObject);
            }
        }

        yield return null;
        createdObjects.Clear();
    }


    [UnityTest]
    public IEnumerator RightDragPlacement_StoresMouseDownCenterAndReleaseHeading()
    {
        ShipCommandDispatcher dispatcher = CreateDispatcher();
        FormationGeometrySnapshot snapshot = CreateSnapshot();
        Vector3 targetCenter = new Vector3(20f, 0f, 30f);

        dispatcher.DispatchFormationPlacement(
            targetCenter,
            135f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );

        Assert.That(dispatcher.PendingGroupDestination, Is.EqualTo(targetCenter));
        Assert.That(dispatcher.PendingGroupHasExplicitFormationHeading, Is.True);
        Assert.That(dispatcher.PendingGroupExplicitFormationHeading,
            Is.EqualTo(135f).Within(0.001f));
        yield return null;
    }


    [UnityTest]
    public IEnumerator RightDragPlacement_PreservesCapturedLocalSlotsAcrossHeadings()
    {
        ShipCommandDispatcher dispatcher = CreateDispatcher();
        FormationGeometrySnapshot snapshot = CreateSnapshot();

        dispatcher.DispatchFormationPlacement(
            Vector3.zero,
            45f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );
        FormationGeometrySnapshot firstPayload =
            dispatcher.PendingGroupGeometrySnapshot;

        dispatcher.DispatchFormationPlacement(
            new Vector3(50f, 0f, -20f),
            225f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );

        Assert.That(dispatcher.PendingGroupGeometrySnapshot,
            Is.SameAs(firstPayload));
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            Assert.That(dispatcher.PendingGroupGeometrySnapshot.Members[index].LocalX,
                Is.EqualTo(snapshot.Members[index].LocalX).Within(0.001f));
            Assert.That(dispatcher.PendingGroupGeometrySnapshot.Members[index].LocalZ,
                Is.EqualTo(snapshot.Members[index].LocalZ).Within(0.001f));
        }
        yield return null;
    }


    [UnityTest]
    public IEnumerator RightDragPlacement_ReconstructsSlotsFromTargetCenterAndHeading()
    {
        FormationGeometrySnapshot snapshot = CreateSnapshot();
        Vector3 targetCenter = new Vector3(10f, 0f, 20f);

        foreach (FormationGeometryMember member in snapshot.Members)
        {
            Vector3 reconstructed = FormationGeometrySnapshot.GetSlotWorldPosition(
                targetCenter,
                90f,
                member.LocalX,
                member.LocalZ
            );
            Vector3 expected = targetCenter + Vector3.right * member.LocalZ
                + Vector3.back * member.LocalX;

            Assert.That(reconstructed.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(reconstructed.z, Is.EqualTo(expected.z).Within(0.001f));
        }
        yield return null;
    }


    [UnityTest]
    public IEnumerator NormalGroupDestination_DoesNotRequireExplicitFinalHeading()
    {
        GameObject commandObject = CreateObject("Command");
        ShipSelectionManager selection = commandObject.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher = commandObject.AddComponent<ShipCommandDispatcher>();
        selection.AddSelection(CreateShip("First", Vector3.zero));
        selection.AddSelection(CreateShip("Second", Vector3.right));

        dispatcher.DispatchDestination(
            new Vector3(30f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );

        Assert.That(dispatcher.GroupCommandPending, Is.True);
        Assert.That(dispatcher.PendingGroupHasExplicitFormationHeading, Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator NewCommand_ReplacesOldExplicitPlacementPayload()
    {
        ShipCommandDispatcher dispatcher = CreateDispatcher();
        FormationGeometrySnapshot snapshot = CreateSnapshot();
        dispatcher.DispatchFormationPlacement(
            new Vector3(10f, 0f, 10f),
            180f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );

        dispatcher.DispatchFormationPlacement(
            new Vector3(-10f, 0f, 5f),
            90f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Manual
        );

        Assert.That(dispatcher.PendingGroupDestination,
            Is.EqualTo(new Vector3(-10f, 0f, 5f)));
        Assert.That(dispatcher.PendingGroupExplicitFormationHeading,
            Is.EqualTo(90f).Within(0.001f));
        Assert.That(dispatcher.PendingGroupNavigationAssistMode,
            Is.EqualTo(WindNavigationAssistMode.Manual));
        yield return null;
    }


    private ShipCommandDispatcher CreateDispatcher()
    {
        return CreateObject("Dispatcher").AddComponent<ShipCommandDispatcher>();
    }


    private FormationGeometrySnapshot CreateSnapshot()
    {
        ShipDestinationController first = CreateShip("First", new Vector3(-3f, 0f, 0f));
        ShipDestinationController second = CreateShip("Second", new Vector3(2f, 0f, 4f));
        bool captured = FormationGeometrySnapshot.TryCapture(
            new[] { first, second },
            first,
            out FormationGeometrySnapshot snapshot
        );

        Assert.That(captured, Is.True);
        return snapshot;
    }


    private ShipDestinationController CreateShip(string name, Vector3 position)
    {
        GameObject ship = CreateObject(name);
        ship.transform.position = position;
        return ship.AddComponent<ShipDestinationController>();
    }


    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
