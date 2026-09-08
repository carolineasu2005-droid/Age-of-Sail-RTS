using System;
using System.Collections.Generic;
using UnityEngine;

public static class FormationLayoutGenerator
{
    public const float StandardLineAheadSpacing = 100f;


    public static FormationGeometrySnapshot CreateStandardLineAhead(
        FormationMemberOrder memberOrder,
        Vector3 formationCenter,
        float formationHeading
    )
    {
        if (memberOrder == null)
        {
            throw new ArgumentNullException(nameof(memberOrder));
        }

        List<FormationGeometryMember> members =
            new List<FormationGeometryMember>(memberOrder.Count);

        for (int orderIndex = 0; orderIndex < memberOrder.Count; orderIndex++)
        {
            float localZ = ((memberOrder.Count - 1) * 0.5f - orderIndex)
                * StandardLineAheadSpacing;
            members.Add(new FormationGeometryMember(
                memberOrder.GetMember(orderIndex),
                0f,
                localZ
            ));
        }

        return FormationGeometrySnapshot.CreateFromLocalSlots(
            formationCenter,
            formationHeading,
            members
        );
    }
}
