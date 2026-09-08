using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class FormationMemberOrder
{
    private const float LocalZComparisonTolerance = 0.001f;

    private readonly ReadOnlyCollection<ShipDestinationController> orderedMembers;
    private readonly Dictionary<ShipDestinationController, int> memberIndices;


    public int Count => orderedMembers.Count;

    public ShipDestinationController Lead => Count > 0 ? orderedMembers[0] : null;

    public IReadOnlyList<ShipDestinationController> OrderedMembers => orderedMembers;


    public FormationMemberOrder(FormationGeometrySnapshot geometrySnapshot)
        : this(CreateOrderedMembers(geometrySnapshot))
    {
    }


    public FormationMemberOrder(
        IReadOnlyList<ShipDestinationController> membersInOrder
    )
    {
        if (membersInOrder == null)
        {
            throw new ArgumentNullException(nameof(membersInOrder));
        }

        List<ShipDestinationController> members =
            new List<ShipDestinationController>(membersInOrder.Count);
        memberIndices = new Dictionary<ShipDestinationController, int>(
            membersInOrder.Count
        );

        for (int index = 0; index < membersInOrder.Count; index++)
        {
            ShipDestinationController ship = membersInOrder[index];

            if (ship == null || memberIndices.ContainsKey(ship))
            {
                throw new ArgumentException(
                    "Formation member order requires unique non-null members.",
                    nameof(membersInOrder)
                );
            }

            members.Add(ship);
            memberIndices.Add(ship, index);
        }

        orderedMembers = members.AsReadOnly();
    }


    public ShipDestinationController GetMember(int index)
    {
        return orderedMembers[index];
    }


    public int IndexOf(ShipDestinationController member)
    {
        return member != null && memberIndices.TryGetValue(member, out int index)
            ? index
            : -1;
    }


    private static List<FormationGeometryMember> CreateFrontToBackOrder(
        IReadOnlyList<FormationGeometryMember> capturedMembers
    )
    {
        List<FormationGeometryMember> ordered =
            new List<FormationGeometryMember>(capturedMembers.Count);

        foreach (FormationGeometryMember capturedMember in capturedMembers)
        {
            int insertIndex = ordered.Count;

            for (int index = 0; index < ordered.Count; index++)
            {
                if (capturedMember.LocalZ
                    > ordered[index].LocalZ + LocalZComparisonTolerance)
                {
                    insertIndex = index;
                    break;
                }
            }

            ordered.Insert(insertIndex, capturedMember);
        }

        return ordered;
    }


    private static List<ShipDestinationController> CreateOrderedMembers(
        FormationGeometrySnapshot geometrySnapshot
    )
    {
        if (geometrySnapshot == null)
        {
            throw new ArgumentNullException(nameof(geometrySnapshot));
        }

        List<FormationGeometryMember> sortedMembers =
            CreateFrontToBackOrder(geometrySnapshot.Members);
        List<ShipDestinationController> orderedMembers =
            new List<ShipDestinationController>(sortedMembers.Count);

        foreach (FormationGeometryMember member in sortedMembers)
        {
            orderedMembers.Add(member.Ship);
        }

        return orderedMembers;
    }
}
