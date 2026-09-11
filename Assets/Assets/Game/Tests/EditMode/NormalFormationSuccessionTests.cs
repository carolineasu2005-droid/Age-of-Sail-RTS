using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NormalFormationSuccessionTests
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
    public void TogetherNormalManeuver_UsesExistingCoordinatedExecution()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.Together,
            true
        );

        Assert.That(fixture.Formation.NormalSuccessionActive, Is.False);
        Assert.That(fixture.Formation.ManeuverState,
            Is.EqualTo(FormationCommandController.FormationManeuverState.Executing));
        Assert.That(fixture.LeadPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
    }


    [Test]
    public void Reforming_ActiveNormalTurnRetargetsAcrossDeadbandWithoutNewCommand()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.Together,
            true,
            FormationCommandController.FormationManeuverType.Normal,
            false
        );
        GlobalWind wind = CreateObject("Wind").AddComponent<GlobalWind>();
        ShipHeadingController headingController = fixture.Second.GetComponent<
            ShipHeadingController
        >();
        SetPrivateField(fixture.SecondPlanner, "globalWind", wind);
        SetPrivateField(
            fixture.Formation,
            "formationManeuverState",
            FormationCommandController.FormationManeuverState.Reforming
        );
        SetPrivateField(
            fixture.Formation,
            "activeNavigationAssistMode",
            WindNavigationAssistMode.Assisted
        );

        fixture.Second.transform.position = new Vector3(-10f, 0f, 0f);
        InvokePrivate(fixture.Formation, "UpdateMemberGuidance");
        int commandSequence = fixture.SecondPlanner.CommandSequence;

        Assert.That(fixture.SecondPlanner.IsActive, Is.True);
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(fixture.SecondPlanner.TargetHeading,
            Is.EqualTo(30f).Within(0.001f));
        Assert.That(headingController.IsActive, Is.True);
        Assert.That(headingController.TargetHeading,
            Is.EqualTo(30f).Within(0.001f));

        fixture.Second.transform.SetPositionAndRotation(
            new Vector3(-2f, 0f, 0f),
            Quaternion.Euler(0f, 10f, 0f)
        );
        InvokePrivate(fixture.Formation, "UpdateMemberGuidance");

        Assert.That(fixture.SecondPlanner.IsActive, Is.True);
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(fixture.SecondPlanner.TargetHeading,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(fixture.SecondPlanner.PlannedTurnDirection,
            Is.EqualTo(TurnDirection.CounterClockwise));
        Assert.That(headingController.IsActive, Is.True);
        Assert.That(headingController.TargetHeading,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(fixture.SecondPlanner.CommandSequence,
            Is.EqualTo(commandSequence));

        fixture.Second.transform.position = new Vector3(10f, 0f, 0f);
        InvokePrivate(fixture.Formation, "UpdateMemberGuidance");

        Assert.That(fixture.SecondPlanner.IsActive, Is.True);
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
        Assert.That(fixture.SecondPlanner.TargetHeading,
            Is.EqualTo(330f).Within(0.001f));
        Assert.That(headingController.IsActive, Is.True);
        Assert.That(headingController.TargetHeading,
            Is.EqualTo(330f).Within(0.001f));
        Assert.That(fixture.SecondPlanner.CommandSequence,
            Is.EqualTo(commandSequence));
        Assert.That(fixture.Second.GetComponent<ShipTacking>().IsActive,
            Is.False);
        Assert.That(fixture.Second.GetComponent<ShipWearing>().IsActive,
            Is.False);
    }


    [Test]
    public void InSuccessionIncompatibleColumn_FallsBackToTogether()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            false
        );

        Assert.That(fixture.Formation.EffectiveManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
        Assert.That(fixture.Formation.NormalSuccessionActive, Is.False);
    }


    [Test]
    public void CompatibleInSuccession_FreezesGateAndStartsOnlyOrderZero()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true
        );
        FormationManeuverGate gate = fixture.Formation.LastManeuverGate;

        Assert.That(fixture.Formation.NormalSuccessionActive, Is.True);
        Assert.That(gate.GatePoint, Is.EqualTo(new Vector3(0f, 0f, 20f)));
        Assert.That(gate.TargetHeading, Is.EqualTo(90f));
        Assert.That(gate.TurnDirection, Is.EqualTo(TurnDirection.Clockwise));
        Assert.That(gate.MemberOrder.Lead, Is.SameAs(fixture.Lead));
        Assert.That(fixture.Formation.SuccessionMemberStates[0],
            Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        Assert.That(fixture.Formation.SuccessionMemberStates[1],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));

        fixture.Lead.transform.position += Vector3.right * 50f;
        Assert.That(fixture.Formation.LastManeuverGate.GatePoint,
            Is.EqualTo(new Vector3(0f, 0f, 20f)));
    }


    [Test]
    public void InSuccession_NextMemberStartsOnlyAfterPriorStartedAndGateCrossed()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true
        );

        fixture.Third.transform.position = new Vector3(0f, 0f, 21f);
        InvokePrivate(fixture.Formation, "UpdateSuccessionManeuverExecution");

        Assert.That(fixture.Formation.SuccessionMemberStates[1],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));
        Assert.That(fixture.Formation.SuccessionMemberStates[2],
            Is.EqualTo(FormationSuccessionMemberState.Waiting));

        fixture.Second.transform.position = new Vector3(0f, 0f, 20f);
        InvokePrivate(fixture.Formation, "UpdateSuccessionManeuverExecution");

        Assert.That(fixture.Formation.SuccessionMemberStates[1],
            Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
    }


    [Test]
    public void InSuccession_AllComplete_RebasesAnchorThenRestoresReforming()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true
        );

        fixture.Lead.transform.SetPositionAndRotation(
            new Vector3(10f, 0f, 20f), Quaternion.Euler(0f, 90f, 0f)
        );
        fixture.Second.transform.SetPositionAndRotation(
            new Vector3(10f, 0f, 20f), Quaternion.Euler(0f, 90f, 0f)
        );
        fixture.Third.transform.SetPositionAndRotation(
            new Vector3(10f, 0f, 20f), Quaternion.Euler(0f, 90f, 0f)
        );
        AdvancePlanner(fixture.LeadPlanner);
        InvokePrivate(fixture.Formation, "UpdateSuccessionManeuverExecution");

        Assert.That(fixture.Formation.NormalSuccessionActive, Is.False);
        Assert.That(fixture.Formation.ManeuverState,
            Is.EqualTo(FormationCommandController.FormationManeuverState.Reforming));
        Assert.That(fixture.Formation.FormationAnchorPosition,
            Is.EqualTo(new Vector3(10f, 0f, 20f)));
        Assert.That(fixture.Formation.ActiveFormationMemberOrder.Lead,
            Is.SameAs(fixture.Lead));
    }


    [Test]
    public void InSuccession_CancellationClearsFrozenSpeedCaps()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true
        );

        Assert.That(fixture.LeadSpeed.FormationSpeedCapActive, Is.True);

        fixture.Formation.CancelFormation();

        Assert.That(fixture.Formation.NormalSuccessionActive, Is.False);
        Assert.That(fixture.LeadSpeed.FormationSpeedCapActive, Is.False);
    }


    [Test]
    public void TackTogether_UsesExistingCoordinatedExecution()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.Together,
            true,
            FormationCommandController.FormationManeuverType.Tack
        );

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.LeadPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
    }


    [Test]
    public void TackInSuccession_LeadStartsAndReleasesOnlyItsFormationCap()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true,
            FormationCommandController.FormationManeuverType.Tack
        );

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.True);
        Assert.That(fixture.Formation.LastManeuverGate.ManeuverType,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Tack));
        Assert.That(fixture.Formation.SuccessionMemberStates[0],
            Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        Assert.That(fixture.Lead.GetComponent<ShipTacking>().IsActive, Is.True);
        Assert.That(fixture.LeadSpeed.FormationSpeedCapActive, Is.False);
        Assert.That(fixture.SecondSpeed.FormationSpeedCapActive, Is.True);
    }


    [Test]
    public void WearTogether_UsesExistingCoordinatedExecution()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.Together,
            true,
            FormationCommandController.FormationManeuverType.Wear
        );

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.LeadPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Wear));
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Wear));
    }


    [Test]
    public void WearInSuccession_LeadStartsAndReleasesOnlyItsFormationCap()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true,
            FormationCommandController.FormationManeuverType.Wear
        );

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.True);
        Assert.That(fixture.Formation.LastManeuverGate.ManeuverType,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.Wear));
        Assert.That(fixture.Formation.SuccessionMemberStates[0],
            Is.EqualTo(FormationSuccessionMemberState.Maneuvering));
        Assert.That(fixture.Lead.GetComponent<ShipWearing>().IsActive, Is.True);
        Assert.That(fixture.LeadSpeed.FormationSpeedCapActive, Is.False);
        Assert.That(fixture.SecondSpeed.FormationSpeedCapActive, Is.True);
    }


    [Test]
    public void TackInSuccession_PrevalidatesAllOrderedMembersBeforeLeadStarts()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true,
            FormationCommandController.FormationManeuverType.Tack,
            false
        );
        ShipTacking invalidTacking = fixture.Third.GetComponent<ShipTacking>();
        SetPrivateField(invalidTacking, "headingController", null);

        Assert.That(fixture.Formation.ActiveFormationMemberOrder.Count,
            Is.EqualTo(3));
        Assert.That(fixture.Formation.ActiveFormationMemberOrder.Lead,
            Is.SameAs(fixture.Lead));
        Assert.That(fixture.Formation.ActiveFormationMemberOrder.GetMember(2),
            Is.SameAs(fixture.Third));
        Assert.That(fixture.LeadPlanner.CanExecuteCoordinatedManeuver(
            ShipManeuverPlanner.ManeuverType.Tack
        ), Is.True);
        Assert.That(fixture.ThirdPlanner.CanExecuteCoordinatedManeuver(
            ShipManeuverPlanner.ManeuverType.Tack
        ), Is.False);
        Assert.That(fixture.LeadPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
        Assert.That(fixture.ThirdPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));

        InvokePrivate(fixture.Formation, "StartCoordinatedManeuver");

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.State,
            Is.EqualTo(FormationCommandController.FormationState.Failed));
        AssertNoMemberManeuverStarted(fixture);
        AssertNoSuccessionStateOrSpeedCaps(fixture);
    }


    [TestCase(0)]
    [TestCase(1)]
    public void TackInSuccession_InvalidLeadOrMiddleMember_PreventsAllMembersFromStarting(
        int invalidMemberIndex
    )
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true,
            FormationCommandController.FormationManeuverType.Tack,
            false
        );
        ShipDestinationController invalidShip = GetOrderedShip(
            fixture,
            invalidMemberIndex
        );
        ShipTacking invalidTacking = invalidShip.GetComponent<ShipTacking>();
        SetPrivateField(invalidTacking, "headingController", null);

        Assert.That(fixture.Formation.ActiveFormationMemberOrder.GetMember(
            invalidMemberIndex
        ), Is.SameAs(invalidShip));
        Assert.That(GetPlanner(fixture, invalidMemberIndex)
            .CanExecuteCoordinatedManeuver(ShipManeuverPlanner.ManeuverType.Tack),
            Is.False);
        AssertNoMemberManeuverStarted(fixture);

        InvokePrivate(fixture.Formation, "StartCoordinatedManeuver");

        Assert.That(fixture.Formation.State,
            Is.EqualTo(FormationCommandController.FormationState.Failed));
        AssertNoMemberManeuverStarted(fixture);
        AssertNoSuccessionStateOrSpeedCaps(fixture);
    }


    [Test]
    public void ComplexManeuver_NeverEntersSuccession()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true,
            FormationCommandController.FormationManeuverType.Complex
        );

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.ManeuverState,
            Is.Not.EqualTo(FormationCommandController.FormationManeuverState.Executing));
    }


    [Test]
    public void WearInSuccession_PrevalidatesAllOrderedMembersBeforeLeadStarts()
    {
        SuccessionFixture fixture = CreateFixture(
            FormationManeuverStyle.InSuccession,
            true,
            FormationCommandController.FormationManeuverType.Wear,
            false
        );
        ShipWearing invalidWearing = fixture.Third.GetComponent<ShipWearing>();
        SetPrivateField(invalidWearing, "headingController", null);

        Assert.That(fixture.LeadPlanner.CanExecuteCoordinatedManeuver(
            ShipManeuverPlanner.ManeuverType.Wear
        ), Is.True);
        Assert.That(fixture.ThirdPlanner.CanExecuteCoordinatedManeuver(
            ShipManeuverPlanner.ManeuverType.Wear
        ), Is.False);
        Assert.That(fixture.LeadPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));

        InvokePrivate(fixture.Formation, "StartCoordinatedManeuver");

        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.State,
            Is.EqualTo(FormationCommandController.FormationState.Failed));
        AssertNoMemberManeuverStarted(fixture);
        AssertNoSuccessionStateOrSpeedCaps(fixture);
    }


    private SuccessionFixture CreateFixture(
        FormationManeuverStyle requestedStyle,
        bool compatible,
        FormationCommandController.FormationManeuverType maneuverType =
            FormationCommandController.FormationManeuverType.Normal,
        bool startManeuver = true
    )
    {
        GameObject commandObject = CreateObject("Formation Command");
        ShipSelectionManager selection = commandObject.AddComponent<
            ShipSelectionManager
        >();
        ShipCommandDispatcher dispatcher = commandObject.AddComponent<
            ShipCommandDispatcher
        >();
        FormationCommandController formation = commandObject.AddComponent<
            FormationCommandController
        >();
        SetPrivateField(dispatcher, "selectionManager", selection);
        SetPrivateField(dispatcher, "formationCommandController", formation);
        SetPrivateField(formation, "selectionManager", selection);
        SetPrivateField(formation, "commandDispatcher", dispatcher);

        ShipDestinationController lead = CreateShip(
            "Lead", new Vector3(0f, 0f, 20f), out ShipManeuverPlanner leadPlanner,
            out ShipSailingSpeed leadSpeed
        );
        ShipDestinationController second = CreateShip(
            "Second", compatible ? Vector3.zero : new Vector3(11f, 0f, 0f),
            out ShipManeuverPlanner secondPlanner, out ShipSailingSpeed secondSpeed
        );
        ShipDestinationController third = CreateShip(
            "Third", new Vector3(0f, 0f, -20f),
            out ShipManeuverPlanner thirdPlanner, out ShipSailingSpeed thirdSpeed
        );
        selection.AddSelection(lead);
        selection.AddSelection(second);
        selection.AddSelection(third);

        Assert.That(formation.CaptureCurrentFormation(selection.SelectedShips),
            Is.True);
        SetPrivateField(formation, "requestedManeuverStyle", requestedStyle);
        SetPrivateField(formation, "formationManeuverType", maneuverType);
        SetPrivateField(formation, "maneuverTargetHeading", 90f);
        SetPrivateField(formation, "maneuverTurnDirection", TurnDirection.Clockwise);
        if (startManeuver)
        {
            InvokePrivate(formation, "StartCoordinatedManeuver");
        }

        return new SuccessionFixture(
            formation,
            lead,
            second,
            third,
            leadPlanner,
            secondPlanner,
            thirdPlanner,
            leadSpeed,
            secondSpeed,
            thirdSpeed
        );
    }


    private static ShipDestinationController GetOrderedShip(
        SuccessionFixture fixture,
        int index
    )
    {
        return index switch
        {
            0 => fixture.Lead,
            1 => fixture.Second,
            2 => fixture.Third,
            _ => throw new System.ArgumentOutOfRangeException(nameof(index))
        };
    }


    private static ShipManeuverPlanner GetPlanner(
        SuccessionFixture fixture,
        int index
    )
    {
        return index switch
        {
            0 => fixture.LeadPlanner,
            1 => fixture.SecondPlanner,
            2 => fixture.ThirdPlanner,
            _ => throw new System.ArgumentOutOfRangeException(nameof(index))
        };
    }


    private static void AssertNoMemberManeuverStarted(SuccessionFixture fixture)
    {
        Assert.That(fixture.LeadPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
        Assert.That(fixture.SecondPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
        Assert.That(fixture.ThirdPlanner.CurrentManeuver,
            Is.EqualTo(ShipManeuverPlanner.ManeuverType.None));
    }


    private static void AssertNoSuccessionStateOrSpeedCaps(
        SuccessionFixture fixture
    )
    {
        Assert.That(fixture.Formation.SuccessionManeuverActive, Is.False);
        Assert.That(fixture.Formation.SuccessionMemberStates, Is.Empty);
        Assert.That(fixture.LeadSpeed.FormationSpeedCapActive, Is.False);
        Assert.That(fixture.SecondSpeed.FormationSpeedCapActive, Is.False);
        Assert.That(fixture.ThirdSpeed.FormationSpeedCapActive, Is.False);
    }


    private ShipDestinationController CreateShip(
        string name,
        Vector3 position,
        out ShipManeuverPlanner maneuverPlanner,
        out ShipSailingSpeed sailingSpeed
    )
    {
        GameObject shipObject = CreateObject(name);
        shipObject.transform.position = position;
        sailingSpeed = shipObject.AddComponent<ShipSailingSpeed>();
        ShipTurning turning = shipObject.AddComponent<ShipTurning>();
        ShipHeadingController heading = shipObject.AddComponent<ShipHeadingController>();
        ShipTacking tacking = shipObject.AddComponent<ShipTacking>();
        ShipWearing wearing = shipObject.AddComponent<ShipWearing>();
        maneuverPlanner = shipObject.AddComponent<ShipManeuverPlanner>();
        ShipDestinationController destination = shipObject.AddComponent<
            ShipDestinationController
        >();
        SetPrivateField(sailingSpeed, "polarTargetSpeed", 4f);
        SetPrivateField(sailingSpeed, "relativeWindAngleSigned", 30f);
        SetPrivateField(sailingSpeed, "relativeWindAngleAbsolute", 30f);
        SetPrivateField(turning, "shipSailingSpeed", sailingSpeed);
        SetPrivateField(heading, "shipTurning", turning);
        SetPrivateField(tacking, "shipSailingSpeed", sailingSpeed);
        SetPrivateField(tacking, "shipTurning", turning);
        SetPrivateField(tacking, "headingController", heading);
        SetPrivateField(wearing, "shipSailingSpeed", sailingSpeed);
        SetPrivateField(wearing, "headingController", heading);
        SetPrivateField(maneuverPlanner, "headingController", heading);
        SetPrivateField(maneuverPlanner, "shipTacking", tacking);
        SetPrivateField(maneuverPlanner, "shipWearing", wearing);
        SetPrivateField(destination, "maneuverPlanner", maneuverPlanner);
        return destination;
    }


    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }


    private static void AdvancePlanner(ShipManeuverPlanner planner)
    {
        ShipHeadingController heading = planner.GetComponent<ShipHeadingController>();
        InvokePrivate(heading, "Update");
        InvokePrivate(planner, "Update");
    }


    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(method, Is.Not.Null,
            $"Required runtime method '{methodName}' was not found.");
        method.Invoke(target, null);
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


    private readonly struct SuccessionFixture
    {
        public FormationCommandController Formation { get; }
        public ShipDestinationController Lead { get; }
        public ShipDestinationController Second { get; }
        public ShipDestinationController Third { get; }
        public ShipManeuverPlanner LeadPlanner { get; }
        public ShipManeuverPlanner SecondPlanner { get; }
        public ShipManeuverPlanner ThirdPlanner { get; }
        public ShipSailingSpeed LeadSpeed { get; }
        public ShipSailingSpeed SecondSpeed { get; }
        public ShipSailingSpeed ThirdSpeed { get; }


        public SuccessionFixture(
            FormationCommandController formation,
            ShipDestinationController lead,
            ShipDestinationController second,
            ShipDestinationController third,
            ShipManeuverPlanner leadPlanner,
            ShipManeuverPlanner secondPlanner,
            ShipManeuverPlanner thirdPlanner,
            ShipSailingSpeed leadSpeed,
            ShipSailingSpeed secondSpeed,
            ShipSailingSpeed thirdSpeed
        )
        {
            Formation = formation;
            Lead = lead;
            Second = second;
            Third = third;
            LeadPlanner = leadPlanner;
            SecondPlanner = secondPlanner;
            ThirdPlanner = thirdPlanner;
            LeadSpeed = leadSpeed;
            SecondSpeed = secondSpeed;
            ThirdSpeed = thirdSpeed;
        }
    }
}
