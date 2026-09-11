using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FormationSuccessionFoundationTests
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
    public void RequestedStyle_DefaultsToTogether()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);

        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
    }


    [Test]
    public void RequestedStyle_MultiSelectionTogglesBothDirections()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        selection.AddSelection(CreateShip("First"));
        selection.AddSelection(CreateShip("Second"));

        selection.ToggleRequestedFormationManeuverStyle();
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));

        selection.ToggleRequestedFormationManeuverStyle();
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
    }


    [Test]
    public void RequestedStyle_EmptyAndSingleSelectionCannotToggle()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        selection.ToggleRequestedFormationManeuverStyle();
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));

        selection.AddSelection(CreateShip("Single"));
        selection.ToggleRequestedFormationManeuverStyle();
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
    }


    [Test]
    public void RequestedStyle_PrimaryChangeWithoutMembershipPreservesStyle()
    {
        ShipSelectionManager selection = CreateSelectionManager(out _);
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        selection.AddSelection(first);
        selection.AddSelection(second);
        selection.ToggleRequestedFormationManeuverStyle();

        SetPrivateField(selection, "primarySelectedShip", second);

        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));
    }


    [Test]
    public void RequestedStyle_MembershipChangeResetsButLeadAndStopDoNot()
    {
        ShipSelectionManager selection = CreateSelectionManager(
            out ShipCommandDispatcher dispatcher
        );
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        ShipDestinationController third = CreateShip("Third");
        selection.AddSelection(first);
        selection.AddSelection(second);
        selection.ToggleRequestedFormationManeuverStyle();
        Assert.That(selection.TrySetDesignatedFormationLead(second), Is.True);

        dispatcher.DispatchStopSelectedShips();
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));
        Assert.That(selection.DesignatedFormationLead, Is.SameAs(second));

        selection.AddSelection(third);
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
    }


    [Test]
    public void GroupCommand_CapturesRequestedStyleImmutably()
    {
        ShipSelectionManager selection = CreateSelectionManager(
            out ShipCommandDispatcher dispatcher
        );
        selection.AddSelection(CreateShip("First"));
        selection.AddSelection(CreateShip("Second"));
        selection.ToggleRequestedFormationManeuverStyle();

        dispatcher.DispatchDestination(
            new Vector3(50f, 0f, 0f),
            ShipDestinationController.TurnSelectionMode.Auto,
            WindNavigationAssistMode.Assisted
        );
        Assert.That(dispatcher.PendingGroupRequestedManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));

        selection.ToggleRequestedFormationManeuverStyle();
        Assert.That(selection.RequestedFormationManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.Together));
        Assert.That(dispatcher.PendingGroupRequestedManeuverStyle,
            Is.EqualTo(FormationManeuverStyle.InSuccession));
    }


    [Test]
    public void ManeuverGate_UsesSignedHorizontalProgressAtAllHeadings()
    {
        FormationManeuverGate gate = new FormationManeuverGate(
            Vector3.zero,
            90f,
            Vector3.right,
            180f,
            TurnDirection.Clockwise,
            ShipManeuverPlanner.ManeuverType.NormalTurn,
            null,
            FormationManeuverStyle.InSuccession,
            3.6f
        );

        Assert.That(gate.GetProgress(new Vector3(-2f, 0f, 0f)),
            Is.LessThan(0f));
        Assert.That(gate.GetProgress(Vector3.zero), Is.EqualTo(0f));
        Assert.That(gate.GetProgress(new Vector3(2f, 0f, 0f)),
            Is.GreaterThan(0f));
        Assert.That(FormationSuccessionMath.HasReachedGate(
            new Vector3(2f, 0f, 0f), gate
        ), Is.True);
    }


    [Test]
    public void ManeuverGate_ProgressIsNegativeBeforeGate()
    {
        FormationManeuverGate gate = CreateForwardGate();

        Assert.That(gate.GetProgress(new Vector3(0f, 0f, -1f)),
            Is.LessThan(0f));
    }


    [Test]
    public void ManeuverGate_ProgressIsZeroAtGate()
    {
        FormationManeuverGate gate = CreateForwardGate();

        Assert.That(gate.GetProgress(Vector3.zero), Is.EqualTo(0f));
    }


    [Test]
    public void ManeuverGate_ProgressIsPositiveAfterCrossingGate()
    {
        FormationManeuverGate gate = CreateForwardGate();

        Assert.That(gate.GetProgress(new Vector3(0f, 0f, 1f)),
            Is.GreaterThan(0f));
    }


    [Test]
    public void ManeuverGate_RotatedIncomingForwardUsesHorizontalDotProduct()
    {
        FormationManeuverGate gate = new FormationManeuverGate(
            Vector3.zero,
            90f,
            Vector3.right,
            180f,
            TurnDirection.Clockwise,
            ShipManeuverPlanner.ManeuverType.NormalTurn,
            null,
            FormationManeuverStyle.InSuccession,
            3.6f
        );

        Assert.That(gate.GetProgress(new Vector3(2f, 100f, 0f)),
            Is.GreaterThan(0f));
    }


    [Test]
    public void Compatibility_LineAheadAndSmallLateralDeviationAreCompatible()
    {
        FormationMemberOrder perfectLine = CreateOrder(
            new Vector3(0f, 0f, 20f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, -20f)
        );
        FormationMemberOrder adjustedLine = CreateOrder(
            new Vector3(0f, 0f, 20f),
            new Vector3(4f, 0f, 0f),
            new Vector3(-5f, 0f, -20f)
        );

        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            perfectLine, Vector3.forward, 10f
        ), Is.True);
        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            adjustedLine, Vector3.forward, 10f
        ), Is.True);
    }


    [Test]
    public void Compatibility_PerfectLineAheadIsCompatible()
    {
        FormationMemberOrder perfectLine = CreateOrder(
            new Vector3(0f, 0f, 20f),
            Vector3.zero,
            new Vector3(0f, 0f, -20f)
        );

        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            perfectLine, Vector3.forward, 10f
        ), Is.True);
    }


    [Test]
    public void Compatibility_SmallLateralDeviationWithinToleranceIsCompatible()
    {
        FormationMemberOrder adjustedLine = CreateOrder(
            new Vector3(0f, 0f, 20f),
            new Vector3(4f, 0f, 0f),
            new Vector3(-5f, 0f, -20f)
        );

        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            adjustedLine, Vector3.forward, 10f
        ), Is.True);
    }


    [Test]
    public void Compatibility_ExcessiveLateralDeviationAndWrongPhysicalOrderFail()
    {
        FormationMemberOrder lateralDeviation = CreateOrder(
            new Vector3(0f, 0f, 20f),
            new Vector3(11f, 0f, 0f),
            new Vector3(0f, 0f, -20f)
        );
        FormationMemberOrder wrongOrder = CreateOrder(
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 20f),
            new Vector3(0f, 0f, -20f)
        );

        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            lateralDeviation, Vector3.forward, 10f
        ), Is.False);
        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            wrongOrder, Vector3.forward, 10f
        ), Is.False);
    }


    [Test]
    public void Compatibility_DoesNotReorderDesignatedLeadThatIsNotPhysicallyLeading()
    {
        ShipDestinationController physicalLead = CreateShip("Physical Lead");
        ShipDestinationController designatedMiddle = CreateShip("Designated Middle");
        ShipDestinationController rear = CreateShip("Rear");
        physicalLead.transform.position = new Vector3(0f, 0f, 20f);
        designatedMiddle.transform.position = Vector3.zero;
        rear.transform.position = new Vector3(0f, 0f, -20f);
        FormationMemberOrder designatedOrder = new FormationMemberOrder(
            new[] { designatedMiddle, physicalLead, rear }
        );

        Assert.That(designatedOrder.Lead, Is.SameAs(designatedMiddle));
        Assert.That(FormationSuccessionCompatibility.IsCompatible(
            designatedOrder, Vector3.forward, 10f
        ), Is.False);
    }


    [Test]
    public void Compatibility_DoesNotMutateStableMemberOrder()
    {
        ShipDestinationController first = CreateShip("First");
        ShipDestinationController second = CreateShip("Second");
        ShipDestinationController third = CreateShip("Third");
        first.transform.position = new Vector3(0f, 0f, 0f);
        second.transform.position = new Vector3(0f, 0f, 20f);
        third.transform.position = new Vector3(0f, 0f, -20f);
        FormationMemberOrder order = new FormationMemberOrder(
            new[] { first, second, third }
        );

        FormationSuccessionCompatibility.IsCompatible(
            order, Vector3.forward, 10f
        );

        Assert.That(order.GetMember(0), Is.SameAs(first));
        Assert.That(order.GetMember(1), Is.SameAs(second));
        Assert.That(order.GetMember(2), Is.SameAs(third));
    }


    [Test]
    public void EffectiveStyle_UsesTogetherWhenInSuccessionIsIncompatible()
    {
        Assert.That(FormationSuccessionCompatibility.ResolveEffectiveStyle(
            FormationManeuverStyle.InSuccession, false
        ), Is.EqualTo(FormationManeuverStyle.Together));
        Assert.That(FormationSuccessionCompatibility.ResolveEffectiveStyle(
            FormationManeuverStyle.InSuccession, true
        ), Is.EqualTo(FormationManeuverStyle.InSuccession));
    }


    [Test]
    public void EffectiveStyle_TogetherRequestAlwaysRemainsTogether()
    {
        Assert.That(FormationSuccessionCompatibility.ResolveEffectiveStyle(
            FormationManeuverStyle.Together, true
        ), Is.EqualTo(FormationManeuverStyle.Together));
    }


    [Test]
    public void Eligibility_AllowsOnlyTheNextStableOrderMemberAfterStart()
    {
        FormationSuccessionMemberState[] states =
        {
            FormationSuccessionMemberState.Waiting,
            FormationSuccessionMemberState.Waiting,
            FormationSuccessionMemberState.Waiting
        };

        Assert.That(FormationSuccessionMath.GetNextEligibleMemberIndex(states),
            Is.EqualTo(0));
        states[0] = FormationSuccessionMemberState.Maneuvering;
        Assert.That(FormationSuccessionMath.GetNextEligibleMemberIndex(states),
            Is.EqualTo(1));
        Assert.That(FormationSuccessionMath.CanStartMember(2, states), Is.False);
    }


    private ShipSelectionManager CreateSelectionManager(
        out ShipCommandDispatcher dispatcher
    )
    {
        GameObject commandObject = new GameObject("Selection Manager");
        createdObjects.Add(commandObject);
        ShipSelectionManager selection = commandObject.AddComponent<
            ShipSelectionManager
        >();
        dispatcher = commandObject.AddComponent<ShipCommandDispatcher>();
        SetPrivateField(dispatcher, "selectionManager", selection);
        return selection;
    }


    private static FormationManeuverGate CreateForwardGate()
    {
        return new FormationManeuverGate(
            Vector3.zero,
            0f,
            Vector3.forward,
            90f,
            TurnDirection.Clockwise,
            ShipManeuverPlanner.ManeuverType.NormalTurn,
            null,
            FormationManeuverStyle.InSuccession,
            3.6f
        );
    }


    private FormationMemberOrder CreateOrder(params Vector3[] positions)
    {
        List<ShipDestinationController> members =
            new List<ShipDestinationController>(positions.Length);

        for (int index = 0; index < positions.Length; index++)
        {
            ShipDestinationController ship = CreateShip($"Ship {index}");
            ship.transform.position = positions[index];
            members.Add(ship);
        }

        return new FormationMemberOrder(members);
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
            $"Required runtime field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }
}
