using System.Collections.Generic;
using UnityEngine;

public enum FormationSuccessionMemberState
{
    Waiting,
    Maneuvering,
    Completed
}


public static class FormationSuccessionMath
{
    public static float CalculateGateProgress(
        Vector3 shipWorldPosition,
        Vector3 gatePoint,
        Vector3 incomingFormationForward
    )
    {
        Vector3 horizontalOffset = shipWorldPosition - gatePoint;
        horizontalOffset.y = 0f;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(
            incomingFormationForward,
            Vector3.up
        );

        return horizontalForward.sqrMagnitude > 0.0001f
            ? Vector3.Dot(horizontalOffset, horizontalForward.normalized)
            : 0f;
    }


    public static bool HasReachedGate(
        Vector3 shipWorldPosition,
        FormationManeuverGate maneuverGate
    )
    {
        return maneuverGate.GetProgress(shipWorldPosition) >= 0f;
    }


    public static bool CanStartMember(
        int memberIndex,
        IReadOnlyList<FormationSuccessionMemberState> memberStates
    )
    {
        if (memberStates == null
            || memberIndex < 0
            || memberIndex >= memberStates.Count
            || memberStates[memberIndex]
                != FormationSuccessionMemberState.Waiting)
        {
            return false;
        }

        return memberIndex == 0
            || memberStates[memberIndex - 1]
                != FormationSuccessionMemberState.Waiting;
    }


    public static int GetNextEligibleMemberIndex(
        IReadOnlyList<FormationSuccessionMemberState> memberStates
    )
    {
        if (memberStates == null)
        {
            return -1;
        }

        for (int index = 0; index < memberStates.Count; index++)
        {
            if (CanStartMember(index, memberStates))
            {
                return index;
            }
        }

        return -1;
    }
}
