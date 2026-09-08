using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FormationNavigationPlayModeTests
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
    public IEnumerator AssistedDirectDestination_UsesDirectNavigation()
    {
        FormationFixture fixture = CreateFixture();
        fixture.Dispatch(new Vector3(100f, 0f, 100f),
            WindNavigationAssistMode.Assisted);

        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.Direct));
    }


    [UnityTest]
    public IEnumerator AssistedUpwindDestination_UsesSharedBeatingNavigation()
    {
        FormationFixture fixture = CreateFixture();
        fixture.Dispatch(new Vector3(0f, 0f, 100f),
            WindNavigationAssistMode.Assisted);

        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.BeatingUpwind));
        Assert.That(fixture.Formation.CurrentBeatingLegHeading,
            Is.EqualTo(50f).Or.EqualTo(310f).Within(0.001f));
        Assert.That(fixture.Formation.OppositeBeatingLegHeading,
            Is.EqualTo(50f).Or.EqualTo(310f).Within(0.001f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(
            fixture.Formation.CurrentBeatingLegHeading,
            fixture.Formation.OppositeBeatingLegHeading
        )), Is.EqualTo(100f).Within(0.001f));
    }


    [UnityTest]
    public IEnumerator DirectUpwindDestination_NeverUsesBeatingNavigation()
    {
        FormationFixture fixture = CreateFixture();
        fixture.Dispatch(new Vector3(0f, 0f, 100f),
            WindNavigationAssistMode.Direct);

        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.Direct));
        Assert.That(fixture.Formation.AutomaticBeatingTackRequested, Is.False);
    }


    [UnityTest]
    public IEnumerator ManualUpwindDestination_NeverUsesBeatingNavigation()
    {
        FormationFixture fixture = CreateFixture();
        fixture.Dispatch(new Vector3(0f, 0f, 100f),
            WindNavigationAssistMode.Manual);

        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.Direct));
        Assert.That(fixture.Formation.AutomaticBeatingTackRequested, Is.False);
    }


    [UnityTest]
    public IEnumerator NewCommandDuringBeating_ReplacesRouteOwnership()
    {
        FormationFixture fixture = CreateFixture();
        fixture.Dispatch(new Vector3(0f, 0f, 100f),
            WindNavigationAssistMode.Assisted);
        yield return WaitForCommandConsumption(fixture);
        int firstSequence = fixture.Dispatcher.DispatchSequence;

        fixture.Dispatch(new Vector3(100f, 0f, 100f),
            WindNavigationAssistMode.Assisted);
        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Dispatcher.DispatchSequence,
            Is.GreaterThan(firstSequence));
        Assert.That(fixture.Formation.ConsumedDispatchSequence,
            Is.EqualTo(fixture.Dispatcher.DispatchSequence));
        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.Direct));
        Assert.That(fixture.Formation.BeatingLegSwitchRequested, Is.False);
    }


    [UnityTest]
    public IEnumerator ExplicitFormationHeading_RemainsSeparateFromTravelHeading()
    {
        FormationFixture fixture = CreateFixture();
        FormationGeometrySnapshot snapshot;
        Assert.That(FormationGeometrySnapshot.TryCapture(
            fixture.Ships,
            fixture.Ships[0],
            out snapshot
        ), Is.True);

        fixture.Dispatcher.DispatchFormationPlacement(
            new Vector3(100f, 0f, 0f),
            180f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );
        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Formation.HasExplicitFinalFormationHeading, Is.True);
        Assert.That(fixture.Formation.FinalFormationHeading,
            Is.EqualTo(180f).Within(0.001f));
        Assert.That(fixture.Formation.NavigationHeading,
            Is.EqualTo(90f).Within(0.001f));
    }


    [UnityTest]
    public IEnumerator ExplicitFinalHeading_BeatingRouteDoesNotOverwriteFinalHeading()
    {
        FormationFixture fixture = CreateFixture();
        FormationGeometrySnapshot snapshot;
        Assert.That(FormationGeometrySnapshot.TryCapture(
            fixture.Ships,
            fixture.Ships[0],
            out snapshot
        ), Is.True);

        fixture.Dispatcher.DispatchFormationPlacement(
            new Vector3(0f, 0f, 100f),
            180f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );
        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.BeatingUpwind));
        Assert.That(fixture.Formation.FinalFormationHeading,
            Is.EqualTo(180f).Within(0.001f));
        Assert.That(fixture.Formation.NavigationHeading,
            Is.Not.EqualTo(fixture.Formation.FinalFormationHeading));
    }


    private FormationFixture CreateFixture()
    {
        GameObject windObject = CreateObject("Test Wind");
        GlobalWind wind = windObject.AddComponent<GlobalWind>();
        wind.windFromDegrees = 0f;

        GameObject commandObject = CreateObject("Test Formation Command");
        ShipSelectionManager selection = commandObject.AddComponent<ShipSelectionManager>();
        ShipCommandDispatcher dispatcher = commandObject.AddComponent<ShipCommandDispatcher>();
        FormationCommandController formation = commandObject.AddComponent<FormationCommandController>();

        ShipDestinationController light = CreateShip("Light", new Vector3(-4f, 0f, 0f), wind);
        ShipDestinationController medium = CreateShip("Medium", Vector3.zero, wind);
        ShipDestinationController heavy = CreateShip("Heavy", new Vector3(4f, 0f, 0f), wind);
        ShipDestinationController[] ships = { light, medium, heavy };

        foreach (ShipDestinationController ship in ships)
        {
            selection.AddSelection(ship);
        }

        return new FormationFixture(
            formation,
            dispatcher,
            ships
        );
    }


    private ShipDestinationController CreateShip(
        string name,
        Vector3 position,
        GlobalWind wind
    )
    {
        GameObject shipObject = CreateObject(name);
        shipObject.transform.position = position;
        ShipSailingSpeed sailingSpeed = shipObject.AddComponent<ShipSailingSpeed>();
        shipObject.AddComponent<ShipTurning>();
        shipObject.AddComponent<ShipHeadingController>();
        shipObject.AddComponent<ShipTacking>();
        shipObject.AddComponent<ShipWearing>();
        ShipManeuverPlanner maneuverPlanner =
            shipObject.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController destinationController =
            shipObject.AddComponent<ShipDestinationController>();

        SailPolarProfile polarProfile =
            ScriptableObject.CreateInstance<SailPolarProfile>();
        createdObjects.Add(polarProfile);
        SetPrivateField(sailingSpeed, "globalWind", wind);
        SetPrivateField(sailingSpeed, "sailPolarProfile", polarProfile);
        SetPrivateField(maneuverPlanner, "globalWind", wind);
        SetPrivateField(destinationController, "globalWind", wind);
        return destinationController;
    }


    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }


    private static IEnumerator WaitForCommandConsumption(FormationFixture fixture)
    {
        const int maximumFrames = 10;

        for (int frame = 0; frame < maximumFrames; frame++)
        {
            if (fixture.Formation.ConsumedDispatchSequence
                == fixture.Dispatcher.DispatchSequence)
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail("Formation command was not consumed within the frame timeout.");
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
            $"Required runtime field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }


    private readonly struct FormationFixture
    {
        public FormationCommandController Formation { get; }

        public ShipCommandDispatcher Dispatcher { get; }

        public ShipDestinationController[] Ships { get; }


        public FormationFixture(
            FormationCommandController formation,
            ShipCommandDispatcher dispatcher,
            ShipDestinationController[] ships
        )
        {
            Formation = formation;
            Dispatcher = dispatcher;
            Ships = ships;
        }


        public void Dispatch(
            Vector3 destination,
            WindNavigationAssistMode navigationMode
        )
        {
            Dispatcher.DispatchDestination(
                destination,
                ShipDestinationController.TurnSelectionMode.Auto,
                navigationMode
            );
        }
    }
}
