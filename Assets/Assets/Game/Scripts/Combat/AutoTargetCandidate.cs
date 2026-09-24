using UnityEngine;

public readonly struct AutoTargetCandidate
{
    public AutoTargetCandidate(
        GameObject targetShipRoot,
        bool relationshipAllowsFire
    )
    {
        TargetShipRoot = targetShipRoot;
        RelationshipAllowsFire = relationshipAllowsFire;
    }


    public GameObject TargetShipRoot { get; }

    public bool RelationshipAllowsFire { get; }
}
