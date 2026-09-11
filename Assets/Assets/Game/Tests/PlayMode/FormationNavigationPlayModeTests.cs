using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FormationNavigationPlayModeTests
{
    private const float AcceleratedTimeScale = 12f;
    private const int HardFrameGuard = 100000;

    private readonly List<Object> createdObjects = new();
    private float originalTimeScale;
    private bool acceleratedSimulationEnabled;


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (acceleratedSimulationEnabled)
        {
            Time.timeScale = originalTimeScale;
            acceleratedSimulationEnabled = false;
        }

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
    public IEnumerator AssistedCompatibleCorridorSwitch_StartsTackInSuccession()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 50f);
        fixture.Selection.ToggleRequestedFormationManeuverStyle();
        fixture.Dispatch(new Vector3(0f, 0f, 100f),
            WindNavigationAssistMode.Assisted);
        yield return WaitForCommandConsumption(fixture);

        yield return WaitForSuccessionStart(fixture, 24f);

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.True);
        Assert.That(fixture.Formation.LastManeuverGate.ManeuverType,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(fixture.Formation.SuccessionMemberStates[0],
            Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        Assert.That(fixture.Formation.FormationTackSwitchCount, Is.EqualTo(0));
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


    [UnityTest]
    public IEnumerator NormalInSuccession_ThreeShipsCrossGateAndTurnInStableOrder()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 0f);
        fixture.Selection.ToggleRequestedFormationManeuverStyle();

        FormationGeometrySnapshot snapshot;
        Assert.That(FormationGeometrySnapshot.TryCapture(
            fixture.Ships,
            fixture.Ships[0],
            out snapshot
        ), Is.True);
        fixture.Dispatcher.DispatchFormationPlacement(
            Vector3.zero,
            90f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.ForceClockwise,
            WindNavigationAssistMode.Direct
        );

        yield return WaitForSuccessionStart(fixture, 8f);

        FormationMemberOrder order = fixture.Formation.ActiveFormationMemberOrder;
        Assert.That(order.Lead, Is.SameAs(fixture.Ships[0]));
        Assert.That(fixture.Formation.SuccessionMemberStates[0],
            Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        Assert.That(fixture.Formation.SuccessionMemberStates[1],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));
        yield return null;
        yield return null;
        AssertWaitingMemberCanReachGate(fixture, 1);

        yield return WaitForMemberState(
            fixture,
            1,
            FormationSuccessionMemberState.Maneuvering,
            30f
        );
        Assert.That(fixture.Formation.SuccessionMemberStates[2],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));

        yield return WaitForMemberState(
            fixture,
            2,
            FormationSuccessionMemberState.Maneuvering,
            30f
        );
        Assert.That(order.Lead, Is.SameAs(fixture.Ships[0]));
        Assert.That(order.GetMember(1), Is.SameAs(fixture.Ships[1]));
        Assert.That(order.GetMember(2), Is.SameAs(fixture.Ships[2]));
    }


    [UnityTest]
    public IEnumerator TackInSuccession_MixedProfilesStartInOrderAndUseOwnManeuvers()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 50f);
        ApplyDistinctProfiles(fixture);
        yield return null;
        fixture.Selection.ToggleRequestedFormationManeuverStyle();
        AssertTackPrevalidationPrerequisites(
            fixture,
            310f,
            TurnDirection.CounterClockwise,
            Vector3.forward
        );
        fixture.Dispatcher.DispatchDestination(
            HeadingDirection(310f) * 100f,
            ShipDestinationController.TurnSelectionMode.ForceCounterClockwise,
            WindNavigationAssistMode.Direct
        );

        yield return WaitForSuccessionStart(fixture, 8f);

        Assert.That(fixture.Formation.LastManeuverGate.ManeuverType,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(fixture.Ships[0].GetComponent<ShipTacking>().IsActive, Is.True);
        Assert.That(fixture.Ships[0].GetComponent<ShipSailingSpeed>()
            .FormationSpeedCapActive, Is.False);

        yield return WaitForMemberState(
            fixture,
            1,
            FormationSuccessionMemberState.Maneuvering,
            30f
        );
        Assert.That(fixture.Ships[1].GetComponent<ShipTacking>().IsActive, Is.True);
        Assert.That(fixture.Formation.SuccessionMemberStates[2],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));
    }


    [UnityTest]
    public IEnumerator WearInSuccession_MixedProfilesCrossSharedGateInStableOrder()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 130f);
        ApplyDistinctProfiles(fixture);
        fixture.Selection.ToggleRequestedFormationManeuverStyle();
        fixture.Dispatcher.DispatchDestination(
            HeadingDirection(230f) * 100f,
            ShipDestinationController.TurnSelectionMode.ForceClockwise,
            WindNavigationAssistMode.Direct
        );

        yield return WaitForSuccessionStart(fixture, 8f);

        Assert.That(fixture.Formation.LastManeuverGate.ManeuverType,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Wear));
        Assert.That(fixture.Ships[0].GetComponent<ShipWearing>().IsActive, Is.True);
        yield return null;
        yield return null;
        AssertWaitingMemberCanReachGate(fixture, 1);

        yield return WaitForMemberState(
            fixture,
            1,
            FormationSuccessionMemberState.Maneuvering,
            30f
        );
        Assert.That(fixture.Ships[1].GetComponent<ShipWearing>().IsActive, Is.True);
        Assert.That(fixture.Formation.SuccessionMemberStates[2],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));
    }


    [UnityTest]
    public IEnumerator LatestCommandWins_DuringActiveSuccessionCancelsOldOwnership()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        yield return StartNormalSuccession(fixture);
        int oldSequence = fixture.Dispatcher.DispatchSequence;
        FormationManeuverGate oldGate = fixture.Formation.LastManeuverGate;
        Vector3[] positionsBeforeReplacement = GetPositions(fixture.Ships);

        fixture.Dispatch(new Vector3(100f, 0f, 0f),
            WindNavigationAssistMode.Direct);
        Assert.That(fixture.Ships[0].transform.position,
            Is.EqualTo(positionsBeforeReplacement[0]).Within(0.001f));
        yield return WaitForCommandConsumption(fixture);

        Assert.That(fixture.Dispatcher.DispatchSequence, Is.GreaterThan(oldSequence));
        Assert.That(fixture.Formation.ConsumedDispatchSequence,
            Is.EqualTo(fixture.Dispatcher.DispatchSequence));
        if (fixture.Formation.SuccessionManeuverActive)
        {
            Assert.That(fixture.Formation.LastManeuverGate.DispatchSequence,
                Is.EqualTo(fixture.Dispatcher.DispatchSequence));
            Assert.That(fixture.Formation.SuccessionMemberStates[0],
                Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        }
    }


    [UnityTest]
    public IEnumerator PlayerStop_DuringSuccessionCancelsRouteAndPreventsResume()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        yield return StartNormalSuccession(fixture);
        Vector3[] positionsBeforeStop = GetPositions(fixture.Ships);

        fixture.Dispatcher.DispatchStopSelectedShips();
        for (int index = 0; index < fixture.Ships.Length; index++)
        {
            Assert.That(fixture.Ships[index].transform.position,
                Is.EqualTo(positionsBeforeStop[index]).Within(0.001f));
        }

        yield return null;
        yield return null;

        Assert.That(fixture.Formation.IsActive, Is.False);
        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.Direct));
        for (int index = 0; index < fixture.Ships.Length; index++)
        {
            ShipSailingSpeed speed = fixture.Ships[index].GetComponent<
                ShipSailingSpeed
            >();
            Assert.That(speed.IsPlayerStopped, Is.True);
        }
    }


    [UnityTest]
    public IEnumerator AssistedBeating_TwoSuccessionTackCyclesPreserveOrderAndSlots()
    {
        EnableAcceleratedSimulation();
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 50f);
        fixture.Selection.ToggleRequestedFormationManeuverStyle();
        FormationGeometrySnapshot initialGeometry;
        Assert.That(FormationGeometrySnapshot.TryCapture(
            fixture.Ships,
            fixture.Ships[0],
            out initialGeometry
        ), Is.True);
        fixture.Dispatch(new Vector3(0f, 0f, 240f),
            WindNavigationAssistMode.Assisted);

        yield return WaitForSuccessionStart(fixture, 24f);
        ShipDestinationController[] originalOrder = GetOrderMembers(
            fixture.Formation.ActiveFormationMemberOrder
        );
        int initialSwitchCount = fixture.Formation.FormationTackSwitchCount;

        yield return WaitForSwitchCount(
            fixture,
            initialSwitchCount + 1,
            60f
        );
        AssertStableOrderAndGeometry(fixture, originalOrder, initialGeometry);
        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.BeatingUpwind));
        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);

        FormationManeuverGate firstGate = fixture.Formation.LastManeuverGate;
        yield return WaitForSwitchCount(
            fixture,
            initialSwitchCount + 2,
            80f
        );

        Assert.That(fixture.Formation.FormationTackSwitchCount,
            Is.EqualTo(initialSwitchCount + 2));
        Assert.That(fixture.Formation.LastManeuverGate,
            Is.Not.SameAs(firstGate));
        AssertStableOrderAndGeometry(fixture, originalOrder, initialGeometry);
        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.BeatingUpwind));
    }


    [UnityTest]
    public IEnumerator RequestedInSuccession_IncompatibleEventFallsBackTogetherWithoutOverwritingRequestedStyle()
    {
        EnableAcceleratedSimulation(2f);
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 50f, Vector3.zero, 20f);
        fixture.Selection.ToggleRequestedFormationManeuverStyle();
        fixture.Dispatch(new Vector3(0f, 0f, 240f),
            WindNavigationAssistMode.Assisted);

        yield return WaitForCorridorApproach(fixture, 24f);
        FormationMemberOrder activeOrder =
            fixture.Formation.ActiveFormationMemberOrder;
        int commandSequence = fixture.Dispatcher.DispatchSequence;
        Assert.That(fixture.Formation.RequestedManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));
        fixture.Ships[1].transform.position += fixture.Ships[1].transform.right
            * 10.25f;
        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            fixture.Formation.ActiveFormationMemberOrder,
            fixture.Formation.FormationForward,
            fixture.Formation.SuccessionLateralTolerance
        ), Is.False);
        yield return WaitForEffectiveManeuverStyle(
            fixture,
            FormationManeuverStyle.Together,
            12f
        );
        Assert.That(fixture.Formation.HasManeuverGate, Is.True);
        Assert.That(fixture.Formation.EffectiveManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
        Assert.That(fixture.Formation.SuccessionCompatibleAtManeuverTrigger,
            Is.False);
        Assert.That(fixture.Formation.RequestedManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));
        Assert.That(fixture.Formation.LastManeuverGate.RequestedStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));
        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.IsActive, Is.True);
        Assert.That(fixture.Formation.LastManeuverGate.DispatchSequence,
            Is.EqualTo(commandSequence));
        Assert.That(fixture.Formation.LastManeuverGate.MemberOrder,
            Is.SameAs(activeOrder));
        Assert.That(fixture.Formation.ActiveFormationMemberOrder,
            Is.SameAs(activeOrder));
        Assert.That(fixture.Formation.ConsumedDispatchSequence,
            Is.EqualTo(commandSequence));
        Assert.That(fixture.Formation.CurrentDispatcherSequence,
            Is.EqualTo(commandSequence));
        Assert.That(fixture.Dispatcher.DispatchSequence,
            Is.EqualTo(commandSequence));
    }


    [UnityTest]
    public IEnumerator TogetherTack_DisplacedFormation_ReformingConvergence()
    {
        EnableAcceleratedSimulation(2f);
        FormationFixture fixture = CreateFixture();
        ConfigureColumn(fixture, 50f, Vector3.zero, 20f);
        FormationGeometrySnapshot initialGeometry;
        Assert.That(FormationGeometrySnapshot.TryCapture(
            fixture.Ships,
            fixture.Ships[0],
            out initialGeometry
        ), Is.True);
        fixture.Dispatch(new Vector3(0f, 0f, 240f),
            WindNavigationAssistMode.Assisted);

        yield return WaitForCorridorApproach(fixture, 24f);
        Assert.That(fixture.Formation.RequestedManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
        fixture.Ships[1].transform.position += fixture.Ships[1].transform.right
            * 10.25f;
        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            fixture.Formation.ActiveFormationMemberOrder,
            fixture.Formation.FormationForward,
            fixture.Formation.SuccessionLateralTolerance
        ), Is.False);
        yield return WaitForEffectiveManeuverStyle(
            fixture,
            FormationManeuverStyle.Together,
            12f
        );
        yield return WaitForManeuverState(
            fixture,
            FormationCommandController.FormationManeuverState.Reforming,
            24f
        );
        AssertTogetherSpecialManeuverCompleted(
            fixture,
            ShipManeuverPlanner.ManeuverType.Tack
        );
        yield return WaitForSwitchCountWithReformingDiagnostics(
            fixture,
            initialGeometry,
            1,
            // The observed 79.816s exit retains a deterministic finite margin.
            120f
        );

        Assert.That(fixture.Formation.NavigationMode,
            Is.EqualTo(FormationCommandController.FormationNavigationMode.BeatingUpwind));
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
            selection,
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
        shipObject.SetActive(false);
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
        shipObject.SetActive(true);
        return destinationController;
    }


    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }


    private void EnableAcceleratedSimulation(float timeScale = AcceleratedTimeScale)
    {
        if (acceleratedSimulationEnabled)
        {
            return;
        }

        originalTimeScale = Time.timeScale;
        Time.timeScale = timeScale;
        acceleratedSimulationEnabled = true;
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


    private static IEnumerator WaitForSuccessionStart(
        FormationFixture fixture,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            if (fixture.Formation.SuccessionManeuverActive)
            {
                yield break;
            }

            if (Time.time - startedAt >= maximumSimulatedSeconds)
            {
                string timeoutDetails = GetTimeoutDetails(
                    startedAt, startedAtRealTime, frame, "simulated timeout"
                );
                Assert.Fail($"Succession did not start within {maximumSimulatedSeconds} "
                    + $"simulated seconds. {timeoutDetails} "
                    + GetFormationDiagnostics(fixture));
            }

            yield return null;
        }

        Assert.Fail("Succession start exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + GetFormationDiagnostics(fixture));
    }


    private IEnumerator StartNormalSuccession(FormationFixture fixture)
    {
        ConfigureColumn(fixture, 0f);
        fixture.Selection.ToggleRequestedFormationManeuverStyle();
        FormationGeometrySnapshot snapshot;
        Assert.That(FormationGeometrySnapshot.TryCapture(
            fixture.Ships,
            fixture.Ships[0],
            out snapshot
        ), Is.True);
        fixture.Dispatcher.DispatchFormationPlacement(
            Vector3.zero,
            90f,
            snapshot,
            ShipDestinationController.TurnSelectionMode.ForceClockwise,
            WindNavigationAssistMode.Direct
        );
        yield return WaitForSuccessionStart(fixture, 8f);
    }


    private static Vector3[] GetPositions(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        Vector3[] positions = new Vector3[ships.Count];
        for (int index = 0; index < ships.Count; index++)
        {
            positions[index] = ships[index].transform.position;
        }

        return positions;
    }


    private static IEnumerator WaitForMemberState(
        FormationFixture fixture,
        int memberIndex,
        FormationSuccessionMemberState expectedState,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            if (fixture.Formation.SuccessionMemberStates.Count > memberIndex
                && fixture.Formation.SuccessionMemberStates[memberIndex]
                    == expectedState)
            {
                yield break;
            }

            if (Time.time - startedAt >= maximumSimulatedSeconds)
            {
                string timeoutDetails = GetTimeoutDetails(
                    startedAt, startedAtRealTime, frame, "simulated timeout"
                );
                Assert.Fail($"Succession member {memberIndex} did not reach "
                    + $"{expectedState} within {maximumSimulatedSeconds} simulated "
                    + $"seconds. {timeoutDetails} "
                    + GetFormationDiagnostics(fixture));
            }

            yield return null;
        }

        Assert.Fail("Succession member wait exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + GetFormationDiagnostics(fixture));
    }


    private static IEnumerator WaitForSwitchCount(
        FormationFixture fixture,
        int expectedSwitchCount,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            if (fixture.Formation.FormationTackSwitchCount >= expectedSwitchCount)
            {
                yield break;
            }

            if (Time.time - startedAt >= maximumSimulatedSeconds)
            {
                string timeoutDetails = GetTimeoutDetails(
                    startedAt, startedAtRealTime, frame, "simulated timeout"
                );
                Assert.Fail($"Beating switch count did not reach {expectedSwitchCount} "
                    + $"within {maximumSimulatedSeconds} simulated seconds and "
                    + $"{frame} frames. {timeoutDetails} "
                    + GetFormationDiagnostics(fixture));
            }

            yield return null;
        }

        Assert.Fail("Beating switch wait exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + GetFormationDiagnostics(fixture));
    }


    private static IEnumerator WaitForSwitchCountWithReformingDiagnostics(
        FormationFixture fixture,
        FormationGeometrySnapshot initialGeometry,
        int expectedSwitchCount,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        ReformingDiagnosticTracker diagnostics =
            new ReformingDiagnosticTracker(fixture, initialGeometry);
        diagnostics.Observe(0f);

        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            float elapsed = Time.time - startedAt;
            diagnostics.Observe(elapsed);

            if (fixture.Formation.FormationTackSwitchCount >= expectedSwitchCount)
            {
                Debug.Log(diagnostics.BuildReport());
                yield break;
            }

            if (elapsed >= maximumSimulatedSeconds)
            {
                string timeoutDetails = GetTimeoutDetails(
                    startedAt, startedAtRealTime, frame, "simulated timeout"
                );
                Assert.Fail($"Beating switch count did not reach {expectedSwitchCount} "
                    + $"within {maximumSimulatedSeconds} simulated seconds and "
                    + $"{frame} frames. {timeoutDetails} "
                    + diagnostics.BuildReport());
            }

            yield return null;
        }

        Assert.Fail("Beating switch wait exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + diagnostics.BuildReport());
    }


    private static IEnumerator WaitForManeuverState(
        FormationFixture fixture,
        FormationCommandController.FormationManeuverState expectedState,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            if (fixture.Formation.ManeuverState == expectedState)
            {
                yield break;
            }

            if (Time.time - startedAt >= maximumSimulatedSeconds)
            {
                string timeoutDetails = GetTimeoutDetails(
                    startedAt, startedAtRealTime, frame, "simulated timeout"
                );
                Assert.Fail($"Formation maneuver state did not become {expectedState} "
                    + $"within {maximumSimulatedSeconds} simulated seconds and "
                    + $"{frame} frames. {timeoutDetails} "
                    + GetFormationDiagnostics(fixture));
            }

            yield return null;
        }

        Assert.Fail("Formation maneuver state wait exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + GetFormationDiagnostics(fixture));
    }


    private static IEnumerator WaitForEffectiveManeuverStyle(
        FormationFixture fixture,
        FormationManeuverStyle expectedStyle,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            if (fixture.Formation.ManeuverState
                    == FormationCommandController.FormationManeuverState.Executing
                && fixture.Formation.EffectiveManeuverStyle == expectedStyle)
            {
                yield break;
            }

            if (Time.time - startedAt >= maximumSimulatedSeconds)
            {
                string timeoutDetails = GetTimeoutDetails(
                    startedAt, startedAtRealTime, frame, "simulated timeout"
                );
                Assert.Fail($"Effective maneuver style did not become {expectedStyle} "
                    + $"within {maximumSimulatedSeconds} simulated seconds and "
                    + $"{frame} frames. {timeoutDetails} "
                    + GetFormationDiagnostics(fixture));
            }

            yield return null;
        }

        Assert.Fail("Effective style wait exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + GetFormationDiagnostics(fixture));
    }


    private static IEnumerator WaitForCorridorApproach(
        FormationFixture fixture,
        float maximumSimulatedSeconds
    )
    {
        float startedAt = Time.time;
        float startedAtRealTime = Time.realtimeSinceStartup;
        for (int frame = 0; frame < HardFrameGuard; frame++)
        {
            float threshold = fixture.Formation.FormationCorridorHalfWidth;
            float crossTrack = Mathf.Abs(fixture.Formation.FormationCorridorCrossTrack);
            if (fixture.Formation.NavigationMode
                    == FormationCommandController.FormationNavigationMode.BeatingUpwind
                && threshold > 0f
                && crossTrack >= threshold * 0.7f)
            {
                yield break;
            }

            if (Time.time - startedAt >= maximumSimulatedSeconds)
            {
                Assert.Fail("Formation did not approach a real corridor switch within "
                    + $"{maximumSimulatedSeconds} simulated seconds and {frame} frames. "
                    + GetTimeoutDetails(
                        startedAt, startedAtRealTime, frame, "simulated timeout"
                    ) + " " + GetFormationDiagnostics(fixture));
            }

            yield return null;
        }

        Assert.Fail("Corridor approach wait exceeded the hard frame guard. "
            + GetTimeoutDetails(
                startedAt, startedAtRealTime, HardFrameGuard, "frame guard"
            ) + " " + GetFormationDiagnostics(fixture));
    }


    private static void AssertTackPrevalidationPrerequisites(
        FormationFixture fixture,
        float targetHeading,
        TurnDirection turnDirection,
        Vector3 windFromDirection
    )
    {
        Assert.That(fixture.Selection.SelectedCount,
            Is.EqualTo(fixture.Ships.Length));
        for (int index = 0; index < fixture.Ships.Length; index++)
        {
            ShipDestinationController ship = fixture.Ships[index];
            ShipManeuverPlanner planner = ship.GetComponent<ShipManeuverPlanner>();
            Assert.That(fixture.Selection.SelectedShips[index], Is.SameAs(ship));
            Assert.That(ship.isActiveAndEnabled, Is.True);
            Assert.That(ship.GetComponent<ShipTacking>(), Is.Not.Null);
            Assert.That(ship.GetComponent<ShipHeadingController>(), Is.Not.Null);
            Assert.That(ship.GetComponent<ShipSailingSpeed>(), Is.Not.Null);
            Assert.That(planner, Is.Not.Null);
            Assert.That(Mathf.Abs(ship.GetComponent<ShipSailingSpeed>()
                .RelativeWindAngleSigned), Is.GreaterThan(0.1f));
            Assert.That(planner.CanExecuteCoordinatedManeuver(
                ShipManeuverPlanner.ManeuverType.Tack
            ), Is.True);
            Assert.That(ShipManeuverPlanner.ClassifyDirectedArc(
                ship.transform.eulerAngles.y,
                targetHeading,
                turnDirection,
                windFromDirection
            ), Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        }
    }


    private static void AssertWaitingMemberCanReachGate(
        FormationFixture fixture,
        int memberIndex
    )
    {
        ShipDestinationController ship = fixture.Formation.ActiveFormationMemberOrder
            .GetMember(memberIndex);
        ShipSailingSpeed speed = ship.GetComponent<ShipSailingSpeed>();
        float progress = fixture.Formation.LastManeuverGate.GetProgress(
            ship.transform.position
        );
        Assert.That(progress, Is.LessThan(0f));
        Assert.That(fixture.Formation.LastManeuverGate.FormationReferenceSpeed,
            Is.GreaterThan(0f));
        Assert.That(speed.IsPlayerStopped, Is.False);
        Assert.That(speed.FormationSpeedCapActive, Is.True);
        Assert.That(speed.FormationSpeedCap, Is.GreaterThan(0f));
        Assert.That(speed.EffectiveTargetSpeed, Is.GreaterThan(0f));
    }


    private static void AssertTogetherSpecialManeuverCompleted(
        FormationFixture fixture,
        ShipManeuverPlanner.ManeuverType expectedManeuver
    )
    {
        FormationManeuverGate gate = fixture.Formation.LastManeuverGate;
        Assert.That(fixture.Formation.EffectiveManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
        Assert.That(gate, Is.Not.Null);
        Assert.That(gate.ManeuverType, Is.EqualTo(expectedManeuver));
        Assert.That(gate.MemberOrder, Is.Not.Null);

        for (int index = 0; index < gate.MemberOrder.Count; index++)
        {
            ShipDestinationController ship = gate.MemberOrder.GetMember(index);
            Assert.That(ship, Is.Not.Null,
                $"Coordinated maneuver member {index} no longer exists.");

            switch (expectedManeuver)
            {
                case ShipManeuverPlanner.ManeuverType.Tack:
                {
                    ShipTacking tacking = ship.GetComponent<ShipTacking>();
                    Assert.That(tacking, Is.Not.Null);
                    Assert.That(tacking.IsActive, Is.False,
                        $"{ship.name} entered Reforming with active Tack state "
                        + $"{tacking.StateName}.");
                    Assert.That(tacking.IsCompleted, Is.True,
                        $"{ship.name} entered Reforming before Tack completed; "
                        + $"state {tacking.StateName}.");
                    break;
                }
                case ShipManeuverPlanner.ManeuverType.Wear:
                {
                    ShipWearing wearing = ship.GetComponent<ShipWearing>();
                    Assert.That(wearing, Is.Not.Null);
                    Assert.That(wearing.IsActive, Is.False,
                        $"{ship.name} entered Reforming with active Wear state "
                        + $"{wearing.StateName}.");
                    Assert.That(wearing.IsCompleted, Is.True,
                        $"{ship.name} entered Reforming before Wear completed; "
                        + $"state {wearing.StateName}.");
                    break;
                }
                default:
                    Assert.Fail($"Expected Tack or Wear, got {expectedManeuver}.");
                    break;
            }
        }
    }


    private static string GetFormationDiagnostics(FormationFixture fixture)
    {
        FormationCommandController formation = fixture.Formation;
        int nextEligible = FormationSuccessionMath.GetNextEligibleMemberIndex(
            formation.SuccessionMemberStates
        );
        return $"Formation state: {formation.State}; maneuver state: "
            + $"{formation.ManeuverState}; navigation mode: {formation.NavigationMode}; "
            + $"succession active: {formation.SuccessionManeuverActive}; next eligible: "
            + $"{nextEligible}; member states: "
            + string.Join(", ", formation.SuccessionMemberStates) + "; switch count: "
            + $"{formation.FormationTackSwitchCount}; corridor cross-track: "
            + $"{formation.FormationCorridorCrossTrack}; corridor half-width: "
            + $"{formation.FormationCorridorHalfWidth}.";
    }


    private static string GetReformingDiagnostics(
        FormationFixture fixture,
        FormationGeometrySnapshot initialGeometry
    )
    {
        FormationCommandController formation = fixture.Formation;
        string diagnostics = GetFormationDiagnostics(fixture)
            + $" Formation anchor: {formation.FormationAnchorPosition}; formation heading: "
            + $"{formation.FormationHeading:F2}; navigation heading: "
            + $"{formation.NavigationHeading:F2}; formation reference speed: "
            + $"{formation.SharedFormationTargetSpeed:F3}.";

        for (int index = 0; index < initialGeometry.Members.Count; index++)
        {
            FormationGeometryMember initialMember = initialGeometry.Members[index];
            ShipDestinationController ship = initialMember.Ship;
            Vector3 expectedSlot = FormationCommandController.GetSlotWorldPosition(
                initialMember.LocalX,
                initialMember.LocalZ,
                formation.FormationAnchorPosition,
                formation.FormationHeading
            );
            Vector3 errorToSlot = expectedSlot - ship.transform.position;
            errorToSlot.y = 0f;
            float forwardError = Vector3.Dot(
                errorToSlot,
                formation.FormationForward
            );
            float lateralError = Vector3.Dot(
                errorToSlot,
                formation.FormationRight
            );
            ShipHeadingController heading = ship.GetComponent<ShipHeadingController>();
            ShipSailingSpeed speed = ship.GetComponent<ShipSailingSpeed>();
            ShipManeuverPlanner planner = ship.GetComponent<ShipManeuverPlanner>();
            ShipTacking tacking = ship.GetComponent<ShipTacking>();
            ShipWearing wearing = ship.GetComponent<ShipWearing>();
            float currentHeading = ship.transform.eulerAngles.y;
            float rawSlotBearing = errorToSlot.sqrMagnitude > 0.0001f
                ? Mathf.Repeat(
                    Mathf.Atan2(errorToSlot.x, errorToSlot.z) * Mathf.Rad2Deg,
                    360f
                )
                : formation.FormationHeading;
            float desiredSlotHeading = formation.ManeuverState
                    == FormationCommandController.FormationManeuverState.Reforming
                && formation.ActiveNavigationAssistMode
                    == WindNavigationAssistMode.Assisted
                ? FormationCommandController.CalculateReformingDesiredHeading(
                    formation.FormationHeading,
                    forwardError,
                    lateralError,
                    formation.SlotPositionDeadband,
                    formation.DirectSlotCorrectionAngle
                )
                : errorToSlot.magnitude > formation.SlotPositionDeadband
                    ? rawSlotBearing
                    : formation.FormationHeading;
            float plannerTargetHeading = planner != null
                ? planner.TargetHeading
                : 0f;
            float headingError = heading != null
                ? Mathf.Abs(Mathf.DeltaAngle(
                    currentHeading,
                    plannerTargetHeading
                ))
                : 0f;

            diagnostics += $" Member {index} ({ship.name}): slot distance "
                + $"{errorToSlot.magnitude:F3}; production forward error "
                + $"{forwardError:F3}; lateral error-to-slot {lateralError:F3}; "
                + $"current heading {currentHeading:F2}; desired/raw slot heading "
                + $"{desiredSlotHeading:F2}/{rawSlotBearing:F2}; planner target heading "
                + $"{plannerTargetHeading:F2}; planner turn direction "
                + $"{planner.PlannedTurnDirection}; planner heading error "
                + $"{headingError:F2}; "
                + $"current speed "
                + $"{speed.CurrentSpeed:F3}; wind-limited target speed "
                + $"{speed.GetWindLimitedTargetSpeed():F3}; available target speed "
                + $"{speed.AvailableTargetSpeed:F3}; effective target speed "
                + $"{speed.EffectiveTargetSpeed:F3}; formation cap active "
                + $"{speed.FormationSpeedCapActive}; formation cap "
                + $"{speed.FormationSpeedCap:F3}; planner active {planner.IsActive}; "
                + $"planner maneuver {planner.CurrentManeuver}; tack state "
                + $"{tacking.StateName}; tack elapsed {tacking.ElapsedTime:F3}; "
                + $"wear state {wearing.StateName}; wear elapsed "
                + $"{wearing.ElapsedTime:F3}.";
        }

        return diagnostics;
    }


    private sealed class ReformingDiagnosticTracker
    {
        private readonly FormationFixture fixture;
        private readonly FormationGeometrySnapshot initialGeometry;
        private readonly ReformingMemberTrackingState[] memberStates;
        private readonly float formationAlignmentDistance;
        private readonly float slotHeadingCommandThreshold;
        private float lastObservationTime;
        private bool hasHeavyLateralDeadbandTime;
        private float heavyLateralDeadbandTime;
        private bool hasHeavyDesiredFormationHeadingTime;
        private float heavyDesiredFormationHeadingTime;
        private bool hasHeavyFormationRetargetTime;
        private float heavyFormationRetargetTime;
        private bool hasHeavyHeadingToleranceTime;
        private float heavyHeadingToleranceTime;
        private bool hasHeavyPositionToleranceTime;
        private float heavyPositionToleranceTime;
        private bool hasReformingExitTime;
        private float reformingExitTime;


        public ReformingDiagnosticTracker(
            FormationFixture fixture,
            FormationGeometrySnapshot initialGeometry
        )
        {
            this.fixture = fixture;
            this.initialGeometry = initialGeometry;
            memberStates = new ReformingMemberTrackingState[
                initialGeometry.Members.Count
            ];
            for (int index = 0; index < memberStates.Length; index++)
            {
                memberStates[index] = new ReformingMemberTrackingState();
            }

            formationAlignmentDistance = GetPrivateField<float>(
                fixture.Formation,
                "formationAlignmentDistance"
            );
            slotHeadingCommandThreshold = GetPrivateField<float>(
                fixture.Formation,
                "slotHeadingCommandThreshold"
            );
            lastObservationTime = Time.time;
        }


        public void Observe(float reformingElapsedTime)
        {
            float now = Time.time;
            float sampleDeltaTime = Mathf.Max(0f, now - lastObservationTime);
            lastObservationTime = now;
            FormationCommandController formation = fixture.Formation;
            bool reformingActive = formation.ManeuverState
                == FormationCommandController.FormationManeuverState.Reforming;

            for (int index = 0; index < initialGeometry.Members.Count; index++)
            {
                FormationGeometryMember initialMember =
                    initialGeometry.Members[index];
                ShipDestinationController ship = initialMember.Ship;
                Vector3 expectedSlot = FormationCommandController
                    .GetSlotWorldPosition(
                        initialMember.LocalX,
                        initialMember.LocalZ,
                        formation.FormationAnchorPosition,
                        formation.FormationHeading
                    );
                Vector3 errorToSlot = expectedSlot - ship.transform.position;
                errorToSlot.y = 0f;
                float forwardError = Vector3.Dot(
                    errorToSlot,
                    formation.FormationForward
                );
                float lateralError = Vector3.Dot(
                    errorToSlot,
                    formation.FormationRight
                );
                float desiredHeading = FormationCommandController
                    .CalculateReformingDesiredHeading(
                        formation.FormationHeading,
                        forwardError,
                        lateralError,
                        formation.SlotPositionDeadband,
                        formation.DirectSlotCorrectionAngle
                    );
                float headingError = Mathf.Abs(Mathf.DeltaAngle(
                    ship.transform.eulerAngles.y,
                    desiredHeading
                ));

                ReformingMemberTrackingState state = memberStates[index];
                float slotDistance = errorToSlot.magnitude;
                bool simultaneouslyWithinTolerances =
                    slotDistance <= formationAlignmentDistance
                    && headingError <= slotHeadingCommandThreshold;
                if (simultaneouslyWithinTolerances
                    && !state.HasFirstSimultaneousToleranceTime)
                {
                    state.HasFirstSimultaneousToleranceTime = true;
                    state.FirstSimultaneousToleranceTime = reformingElapsedTime;
                }
                else if (!simultaneouslyWithinTolerances
                    && state.WasSimultaneouslyWithinTolerances
                    && !state.HasDivergenceAfterToleranceTime
                    && reformingActive)
                {
                    state.HasDivergenceAfterToleranceTime = true;
                    state.DivergenceAfterToleranceTime = reformingElapsedTime;
                }

                state.WasSimultaneouslyWithinTolerances =
                    simultaneouslyWithinTolerances;

                ShipManeuverPlanner planner = ship.GetComponent<
                    ShipManeuverPlanner
                >();
                if (planner != null && !state.HasPlannerCommandBaseline)
                {
                    state.LastPlannerCommandSequence = planner.CommandSequence;
                    state.HasPlannerCommandBaseline = true;
                }
                else if (planner != null
                    && planner.CommandSequence != state.LastPlannerCommandSequence)
                {
                    int newCommandCount = Mathf.Max(
                        1,
                        planner.CommandSequence - state.LastPlannerCommandSequence
                    );
                    state.PlannerCommandCount += newCommandCount;
                    switch (planner.CurrentManeuver)
                    {
                        case ShipManeuverPlanner.ManeuverType.NormalTurn:
                            state.NormalCommandCount += newCommandCount;
                            break;
                        case ShipManeuverPlanner.ManeuverType.Tack:
                            state.TackCommandCount += newCommandCount;
                            break;
                        case ShipManeuverPlanner.ManeuverType.Wear:
                            state.WearCommandCount += newCommandCount;
                            break;
                    }

                    state.LastPlannerCommandSequence = planner.CommandSequence;
                }

                ShipTacking tacking = ship.GetComponent<ShipTacking>();
                state.CurrentTackActiveTime = tacking != null && tacking.IsActive
                    ? state.CurrentTackActiveTime + sampleDeltaTime
                    : 0f;
                state.LongestTackActiveTime = Mathf.Max(
                    state.LongestTackActiveTime,
                    state.CurrentTackActiveTime
                );

                ShipWearing wearing = ship.GetComponent<ShipWearing>();
                state.CurrentWearActiveTime = wearing != null && wearing.IsActive
                    ? state.CurrentWearActiveTime + sampleDeltaTime
                    : 0f;
                state.LongestWearActiveTime = Mathf.Max(
                    state.LongestWearActiveTime,
                    state.CurrentWearActiveTime
                );

                if (ship == fixture.Ships[2])
                {
                    ObserveHeavyMilestones(
                        reformingElapsedTime,
                        slotDistance,
                        lateralError,
                        desiredHeading,
                        headingError,
                        planner
                    );
                }
            }

            if (!hasReformingExitTime && !reformingActive)
            {
                hasReformingExitTime = true;
                reformingExitTime = reformingElapsedTime;
            }
        }


        public string BuildReport()
        {
            return GetMilestoneDiagnostics()
                + " Final state: "
                + GetReformingDiagnostics(fixture, initialGeometry)
                + GetTrackingDiagnostics();
        }


        private void ObserveHeavyMilestones(
            float reformingElapsedTime,
            float slotDistance,
            float lateralError,
            float desiredHeading,
            float headingError,
            ShipManeuverPlanner planner
        )
        {
            if (!hasHeavyLateralDeadbandTime
                && Mathf.Abs(lateralError) <= fixture.Formation.SlotPositionDeadband)
            {
                hasHeavyLateralDeadbandTime = true;
                heavyLateralDeadbandTime = reformingElapsedTime;
            }

            if (!hasHeavyDesiredFormationHeadingTime
                && Mathf.Abs(Mathf.DeltaAngle(
                    desiredHeading,
                    fixture.Formation.FormationHeading
                )) <= 0.001f)
            {
                hasHeavyDesiredFormationHeadingTime = true;
                heavyDesiredFormationHeadingTime = reformingElapsedTime;
            }

            if (!hasHeavyFormationRetargetTime
                && planner != null
                && planner.IsActive
                && planner.CurrentManeuver
                    == ShipManeuverPlanner.ManeuverType.NormalTurn
                && Mathf.Abs(Mathf.DeltaAngle(
                    planner.TargetHeading,
                    fixture.Formation.FormationHeading
                )) <= 0.001f)
            {
                hasHeavyFormationRetargetTime = true;
                heavyFormationRetargetTime = reformingElapsedTime;
            }

            if (!hasHeavyHeadingToleranceTime
                && headingError <= slotHeadingCommandThreshold)
            {
                hasHeavyHeadingToleranceTime = true;
                heavyHeadingToleranceTime = reformingElapsedTime;
            }

            if (!hasHeavyPositionToleranceTime
                && slotDistance <= formationAlignmentDistance)
            {
                hasHeavyPositionToleranceTime = true;
                heavyPositionToleranceTime = reformingElapsedTime;
            }
        }


        private string GetMilestoneDiagnostics()
        {
            string diagnostics = "Reforming milestones:";
            for (int index = 0; index < memberStates.Length; index++)
            {
                ReformingMemberTrackingState state = memberStates[index];
                string firstToleranceTime = FormatObservedTime(
                    state.HasFirstSimultaneousToleranceTime,
                    state.FirstSimultaneousToleranceTime
                );
                string divergenceTime = FormatObservedTime(
                    state.HasDivergenceAfterToleranceTime,
                    state.DivergenceAfterToleranceTime
                );
                diagnostics += $" Member {index}: first simultaneous "
                    + $"position/heading tolerance {firstToleranceTime}; "
                    + $"divergence after first tolerance {divergenceTime}.";
            }

            string lateralDeadbandTime = FormatObservedTime(
                hasHeavyLateralDeadbandTime,
                heavyLateralDeadbandTime
            );
            string desiredFormationHeadingTime = FormatObservedTime(
                hasHeavyDesiredFormationHeadingTime,
                heavyDesiredFormationHeadingTime
            );
            string formationRetargetTime = FormatObservedTime(
                hasHeavyFormationRetargetTime,
                heavyFormationRetargetTime
            );
            string headingToleranceTime = FormatObservedTime(
                hasHeavyHeadingToleranceTime,
                heavyHeadingToleranceTime
            );
            string positionToleranceTime = FormatObservedTime(
                hasHeavyPositionToleranceTime,
                heavyPositionToleranceTime
            );
            string exitTime = FormatObservedTime(
                hasReformingExitTime,
                reformingExitTime
            );
            diagnostics += " Heavy: first lateral deadband "
                + $"{lateralDeadbandTime}; first desired Formation Heading "
                + $"{desiredFormationHeadingTime}; first active NormalTurn "
                + $"target Formation Heading {formationRetargetTime}; first "
                + $"heading tolerance {headingToleranceTime}; first position "
                + $"tolerance {positionToleranceTime}. Reforming exit "
                + $"{exitTime}.";

            return diagnostics;
        }


        private static string FormatObservedTime(bool observed, float time)
        {
            return observed ? $"{time:F3}s" : "not observed";
        }


        private string GetTrackingDiagnostics()
        {
            string diagnostics = " Tracking:";
            for (int index = 0; index < memberStates.Length; index++)
            {
                ReformingMemberTrackingState state = memberStates[index];
                diagnostics += $" Member {index}: planner commands "
                    + $"{state.PlannerCommandCount} (Normal "
                    + $"{state.NormalCommandCount}, Tack {state.TackCommandCount}, "
                    + $"Wear {state.WearCommandCount}); longest continuous Tack "
                    + $"{state.LongestTackActiveTime:F3}s; longest continuous Wear "
                    + $"{state.LongestWearActiveTime:F3}s.";
            }

            return diagnostics;
        }
    }


    private sealed class ReformingMemberTrackingState
    {
        public int PlannerCommandCount;
        public int NormalCommandCount;
        public int TackCommandCount;
        public int WearCommandCount;
        public bool HasPlannerCommandBaseline;
        public int LastPlannerCommandSequence;
        public float CurrentTackActiveTime;
        public float LongestTackActiveTime;
        public float CurrentWearActiveTime;
        public float LongestWearActiveTime;
        public bool HasFirstSimultaneousToleranceTime;
        public float FirstSimultaneousToleranceTime;
        public bool WasSimultaneouslyWithinTolerances;
        public bool HasDivergenceAfterToleranceTime;
        public float DivergenceAfterToleranceTime;
    }


    private static string GetTimeoutDetails(
        float startedAtSimulationTime,
        float startedAtRealTime,
        int frameCount,
        string timeoutSource
    )
    {
        return $"Timeout source: {timeoutSource}; simulated elapsed: "
            + $"{Time.time - startedAtSimulationTime:F3}s; real elapsed: "
            + $"{Time.realtimeSinceStartup - startedAtRealTime:F3}s; frame count: "
            + $"{frameCount}.";
    }


    private static ShipDestinationController[] GetOrderMembers(
        FormationMemberOrder order
    )
    {
        ShipDestinationController[] members = new ShipDestinationController[
            order.Count
        ];
        for (int index = 0; index < order.Count; index++)
        {
            members[index] = order.GetMember(index);
        }

        return members;
    }


    private static void AssertStableOrderAndGeometry(
        FormationFixture fixture,
        IReadOnlyList<ShipDestinationController> expectedOrder,
        FormationGeometrySnapshot initialGeometry
    )
    {
        FormationMemberOrder order = fixture.Formation.ActiveFormationMemberOrder;
        Assert.That(order.Count, Is.EqualTo(expectedOrder.Count));
        for (int index = 0; index < order.Count; index++)
        {
            Assert.That(order.GetMember(index), Is.SameAs(expectedOrder[index]));
            FormationGeometryMember initialMember = initialGeometry.Members[index];
            Vector3 expectedSlot = FormationCommandController.GetSlotWorldPosition(
                initialMember.LocalX,
                initialMember.LocalZ,
                fixture.Formation.FormationAnchorPosition,
                fixture.Formation.FormationHeading
            );
            Assert.That(Vector3.Distance(
                initialMember.Ship.transform.position,
                expectedSlot
            ), Is.LessThanOrEqualTo(5.01f));
        }
    }


    private void ConfigureColumn(
        FormationFixture fixture,
        float heading,
        Vector3 center = default,
        float adjacentSpacing = 10f
    )
    {
        Vector3 forward = HeadingDirection(heading);
        for (int index = 0; index < fixture.Ships.Length; index++)
        {
            fixture.Ships[index].transform.SetPositionAndRotation(
                center + forward * (adjacentSpacing - index * adjacentSpacing),
                Quaternion.Euler(0f, heading, 0f)
            );
        }
    }


    private void ApplyDistinctProfiles(FormationFixture fixture)
    {
        float[] speeds = { 4f, 3.5f, 3f };
        for (int index = 0; index < fixture.Ships.Length; index++)
        {
            ShipMovementProfile profile = ScriptableObject.CreateInstance<
                ShipMovementProfile
            >();
            profile.baseMaxSpeed = speeds[index];
            createdObjects.Add(profile);
            fixture.Ships[index].GetComponent<ShipSailingSpeed>()
                .ApplyMovementProfile(profile);
            fixture.Ships[index].GetComponent<ShipTurning>()
                .ApplyMovementProfile(profile);
            fixture.Ships[index].GetComponent<ShipTacking>()
                .ApplyMovementProfile(profile);
        }
    }


    private static Vector3 HeadingDirection(float heading)
    {
        return Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
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


    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null,
            $"Required runtime field '{fieldName}' was not found.");
        return (T)field.GetValue(target);
    }

    private readonly struct FormationFixture
    {
        public FormationCommandController Formation { get; }

        public ShipCommandDispatcher Dispatcher { get; }

        public ShipSelectionManager Selection { get; }

        public ShipDestinationController[] Ships { get; }


        public FormationFixture(
            FormationCommandController formation,
            ShipCommandDispatcher dispatcher,
            ShipSelectionManager selection,
            ShipDestinationController[] ships
        )
        {
            Formation = formation;
            Dispatcher = dispatcher;
            Selection = selection;
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
