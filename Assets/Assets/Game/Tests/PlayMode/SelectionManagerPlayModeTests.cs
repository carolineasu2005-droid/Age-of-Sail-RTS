using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SelectionManagerPlayModeTests
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
    public IEnumerator SingleSelection_SelectSingleSetsPrimary()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        ShipDestinationController ship = CreateShip("Single", Vector3.zero);

        selection.SelectSingle(ship);
        yield return null;

        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(ship));
    }


    [UnityTest]
    public IEnumerator ShiftToggle_TogglesMembershipWithoutReplacingSelection()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        ShipDestinationController first = CreateShip("First", Vector3.zero);
        ShipDestinationController second = CreateShip("Second", Vector3.right);

        selection.SelectSingle(first);
        selection.ToggleSelection(second);
        selection.ToggleSelection(first);
        yield return null;

        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(second));
    }


    [UnityTest]
    public IEnumerator BoxSelection_NormalizedRectangleContainsMatchingShips()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        Camera camera = CreateCamera();
        ShipDestinationController left = CreateShip("Left", new Vector3(-2f, 0f, 0f));
        ShipDestinationController right = CreateShip("Right", new Vector3(2f, 0f, 0f));
        CreateShip("Outside", new Vector3(20f, 0f, 0f));

        int count = selection.SelectShipsInScreenRect(
            camera,
            GetNormalizedRect(camera, left, right),
            false
        );
        yield return null;

        Assert.That(count, Is.EqualTo(2));
        Assert.That(selection.SelectedShips, Does.Contain(left));
        Assert.That(selection.SelectedShips, Does.Contain(right));
    }


    [UnityTest]
    public IEnumerator NonShiftBox_ReplacesExistingSelection()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        Camera camera = CreateCamera();
        ShipDestinationController oldShip = CreateShip("Old", new Vector3(-8f, 0f, 0f));
        ShipDestinationController selected = CreateShip("Selected", Vector3.zero);
        selection.SelectSingle(oldShip);

        selection.SelectShipsInScreenRect(camera,
            GetNormalizedRect(camera, selected, selected), false);
        yield return null;

        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(selected));
    }


    [UnityTest]
    public IEnumerator ShiftBox_AddsMatchesToExistingSelection()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        Camera camera = CreateCamera();
        ShipDestinationController first = CreateShip("First", new Vector3(-3f, 0f, 0f));
        ShipDestinationController second = CreateShip("Second", new Vector3(3f, 0f, 0f));
        selection.SelectSingle(first);

        selection.SelectShipsInScreenRect(camera,
            GetNormalizedRect(camera, second, second), true);
        yield return null;

        Assert.That(selection.SelectedCount, Is.EqualTo(2));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(first));
    }


    [UnityTest]
    public IEnumerator EmptyNonShiftBox_ClearsSelection()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        Camera camera = CreateCamera();
        selection.SelectSingle(CreateShip("Selected", Vector3.zero));

        selection.SelectShipsInScreenRect(camera,
            new Rect(-100f, -100f, 10f, 10f), false);
        yield return null;

        Assert.That(selection.SelectedCount, Is.EqualTo(0));
    }


    [UnityTest]
    public IEnumerator EmptyShiftBox_PreservesSelection()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        Camera camera = CreateCamera();
        ShipDestinationController selected = CreateShip("Selected", Vector3.zero);
        selection.SelectSingle(selected);

        selection.SelectShipsInScreenRect(camera,
            new Rect(-100f, -100f, 10f, 10f), true);
        yield return null;

        Assert.That(selection.SelectedCount, Is.EqualTo(1));
        Assert.That(selection.PrimarySelectedShip, Is.SameAs(selected));
    }


    [UnityTest]
    public IEnumerator FreshBoxSelection_UsesDeterministicScreenOrderForPrimary()
    {
        ShipSelectionManager selection = CreateSelectionManager();
        Camera camera = CreateCamera();
        ShipDestinationController left = CreateShip("Left", new Vector3(-2f, 0f, 0f));
        ShipDestinationController right = CreateShip("Right", new Vector3(2f, 0f, 0f));

        selection.SelectShipsInScreenRect(camera,
            GetNormalizedRect(camera, left, right), false);
        yield return null;

        Assert.That(selection.PrimarySelectedShip, Is.SameAs(left));
    }


    [UnityTest]
    public IEnumerator SelectionMembershipChange_ClearsArmedLineAheadTemplate()
    {
        GameObject commandObject = CreateObject("Line Ahead Command Input");
        ShipSelectionManager selection =
            commandObject.AddComponent<ShipSelectionManager>();
        commandObject.AddComponent<ShipCommandDispatcher>();
        ShipPlayerCommandInput input =
            commandObject.AddComponent<ShipPlayerCommandInput>();
        ShipDestinationController first = CreateShip("First", Vector3.zero);
        ShipDestinationController second = CreateShip("Second", Vector3.right);
        ShipDestinationController third = CreateShip("Third", Vector3.forward);

        yield return null;

        selection.AddSelection(first);
        selection.AddSelection(second);
        Assert.That(selection.SelectedCount, Is.EqualTo(2));

        input.ToggleLineAheadTemplate();
        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.LineAhead));

        selection.AddSelection(third);
        yield return null;

        Assert.That(input.PendingTemplate,
            Is.EqualTo(ShipPlayerCommandInput.PendingFormationTemplate.None));
    }


    private ShipSelectionManager CreateSelectionManager()
    {
        return CreateObject("Selection Manager")
            .AddComponent<ShipSelectionManager>();
    }


    private Camera CreateCamera()
    {
        Camera camera = CreateObject("Selection Camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.transform.position = new Vector3(0f, 10f, 0f);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return camera;
    }


    private ShipDestinationController CreateShip(string name, Vector3 position)
    {
        GameObject ship = CreateObject(name);
        ship.transform.position = position;
        return ship.AddComponent<ShipDestinationController>();
    }


    private static Rect GetNormalizedRect(
        Camera camera,
        ShipDestinationController first,
        ShipDestinationController second
    )
    {
        Vector3 firstScreen = camera.WorldToScreenPoint(first.transform.position);
        Vector3 secondScreen = camera.WorldToScreenPoint(second.transform.position);
        float minimumX = Mathf.Min(firstScreen.x, secondScreen.x) - 5f;
        float maximumX = Mathf.Max(firstScreen.x, secondScreen.x) + 5f;
        float minimumY = Mathf.Min(firstScreen.y, secondScreen.y) - 5f;
        float maximumY = Mathf.Max(firstScreen.y, secondScreen.y) + 5f;
        return Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
    }


    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
