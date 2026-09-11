using UnityEngine;

public static class FormationSuccessionCompatibility
{
    private const float PositionOrderEpsilon = 0.01f;


    public static bool IsCompatible(
        FormationMemberOrder memberOrder,
        Vector3 incomingFormationForward,
        float lateralTolerance
    )
    {
        if (memberOrder == null || memberOrder.Count < 2)
        {
            return false;
        }

        Vector3 forward = Vector3.ProjectOnPlane(
            incomingFormationForward,
            Vector3.up
        );
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 leadPosition = memberOrder.Lead.transform.position;
        float maximumLateralDeviation = Mathf.Max(0f, lateralTolerance);
        float previousProgress = 0f;

        for (int index = 0; index < memberOrder.Count; index++)
        {
            ShipDestinationController member = memberOrder.GetMember(index);
            if (member == null)
            {
                return false;
            }

            Vector3 offset = member.transform.position - leadPosition;
            offset.y = 0f;
            float lateralDeviation = Mathf.Abs(Vector3.Dot(offset, right));
            float progress = Vector3.Dot(offset, forward);

            if (lateralDeviation > maximumLateralDeviation)
            {
                return false;
            }

            if (index > 0 && previousProgress - progress
                <= PositionOrderEpsilon)
            {
                return false;
            }

            previousProgress = progress;
        }

        return true;
    }


    public static FormationManeuverStyle ResolveEffectiveStyle(
        FormationManeuverStyle requestedStyle,
        bool successionCompatible
    )
    {
        return requestedStyle == FormationManeuverStyle.InSuccession
            && successionCompatible
            ? FormationManeuverStyle.InSuccession
            : FormationManeuverStyle.Together;
    }
}
