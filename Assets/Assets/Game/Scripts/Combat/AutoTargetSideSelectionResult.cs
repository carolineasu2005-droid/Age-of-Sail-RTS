using UnityEngine;

public readonly struct AutoTargetSideSelectionResult
{
    internal AutoTargetSideSelectionResult(
        GameObject targetShipRoot,
        FireEligibilityResult fireEligibility,
        AutoTargetScoreResult score,
        int candidateIndex
    )
    {
        HasTarget = true;
        TargetShipRoot = targetShipRoot;
        FireEligibility = fireEligibility;
        Score = score;
        CandidateIndex = candidateIndex;
    }


    public bool HasTarget { get; }

    public GameObject TargetShipRoot { get; }

    public FireEligibilityResult FireEligibility { get; }

    public AutoTargetScoreResult Score { get; }

    public int CandidateIndex { get; }
}
