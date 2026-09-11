using UnityEngine;

public readonly struct FormationManeuverGate
{
    public Vector3 GatePoint { get; }

    public float IncomingFormationHeading { get; }

    public Vector3 IncomingFormationForward { get; }

    public float TargetHeading { get; }

    public TurnDirection TurnDirection { get; }

    public ShipManeuverPlanner.ManeuverType ManeuverType { get; }

    public FormationMemberOrder MemberOrder { get; }

    public FormationManeuverStyle RequestedStyle { get; }

    public float FormationReferenceSpeed { get; }

    public int DispatchSequence { get; }


    public FormationManeuverGate(
        Vector3 gatePoint,
        float incomingFormationHeading,
        Vector3 incomingFormationForward,
        float targetHeading,
        TurnDirection turnDirection,
        ShipManeuverPlanner.ManeuverType maneuverType,
        FormationMemberOrder memberOrder,
        FormationManeuverStyle requestedStyle,
        float formationReferenceSpeed,
        int dispatchSequence = 0
    )
    {
        GatePoint = new Vector3(gatePoint.x, 0f, gatePoint.z);
        IncomingFormationHeading = Mathf.Repeat(incomingFormationHeading, 360f);
        Vector3 horizontalForward = Vector3.ProjectOnPlane(
            incomingFormationForward,
            Vector3.up
        );
        IncomingFormationForward = horizontalForward.sqrMagnitude > 0.0001f
            ? horizontalForward.normalized
            : Vector3.forward;
        TargetHeading = Mathf.Repeat(targetHeading, 360f);
        TurnDirection = turnDirection;
        ManeuverType = maneuverType;
        MemberOrder = memberOrder;
        RequestedStyle = requestedStyle;
        FormationReferenceSpeed = Mathf.Max(0f, formationReferenceSpeed);
        DispatchSequence = Mathf.Max(0, dispatchSequence);
    }


    public float GetProgress(Vector3 shipWorldPosition)
    {
        return FormationSuccessionMath.CalculateGateProgress(
            shipWorldPosition,
            GatePoint,
            IncomingFormationForward
        );
    }
}
