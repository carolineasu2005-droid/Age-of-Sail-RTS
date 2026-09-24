using System.Collections.Generic;
using UnityEngine;

public static class ShipAutoTargetSelector
{
    public static bool TrySelect(
        GameObject shooterShipRoot,
        IReadOnlyList<AutoTargetCandidate> candidates,
        out AutoTargetSelectionResult result
    )
    {
        result = default;

        if (shooterShipRoot == null
            || candidates == null
            || !TryGetShooterDependencies(
                shooterShipRoot,
                out ShipCombatState combatState,
                out ShipFireEligibility fireEligibility
            )
            || !combatState.AutoFireEnabled
            || combatState.ManualTarget != null)
        {
            return false;
        }

        AutoTargetSideSelectionResult portSelection = default;
        AutoTargetSideSelectionResult starboardSelection = default;

        for (int index = 0; index < candidates.Count; index++)
        {
            AutoTargetCandidate candidate = candidates[index];
            GameObject targetShipRoot = candidate.TargetShipRoot;

            if (targetShipRoot == null
                || targetShipRoot == shooterShipRoot
                || !fireEligibility.TryEvaluate(
                    targetShipRoot,
                    candidate.RelationshipAllowsFire,
                    out FireEligibilityResult eligibilityResult
                )
                || !eligibilityResult.CanFire
                || !ShipAutoTargetScorer.TryEvaluate(
                    shooterShipRoot,
                    targetShipRoot,
                    eligibilityResult,
                    out AutoTargetScoreResult score
                )
                || !score.Selectable)
            {
                continue;
            }

            AutoTargetSideSelectionResult candidateResult =
                new AutoTargetSideSelectionResult(
                    targetShipRoot,
                    eligibilityResult,
                    score,
                    index
                );

            switch (eligibilityResult.Side.Value)
            {
                case CombatSide.Port:
                    if (!portSelection.HasTarget
                        || IsBetter(candidateResult, portSelection))
                    {
                        portSelection = candidateResult;
                    }

                    break;

                case CombatSide.Starboard:
                    if (!starboardSelection.HasTarget
                        || IsBetter(candidateResult, starboardSelection))
                    {
                        starboardSelection = candidateResult;
                    }

                    break;
            }
        }

        result = new AutoTargetSelectionResult(
            portSelection,
            starboardSelection
        );
        return result.HasAnyTarget;
    }


    private static bool TryGetShooterDependencies(
        GameObject shooterShipRoot,
        out ShipCombatState combatState,
        out ShipFireEligibility fireEligibility
    )
    {
        combatState = shooterShipRoot.GetComponent<ShipCombatState>();
        fireEligibility =
            shooterShipRoot.GetComponent<ShipFireEligibility>();

        return combatState != null && fireEligibility != null;
    }


    private static bool IsBetter(
        AutoTargetSideSelectionResult candidate,
        AutoTargetSideSelectionResult current
    )
    {
        int scoreComparison = candidate.Score.FinalScore.CompareTo(
            current.Score.FinalScore
        );

        if (scoreComparison != 0)
        {
            return scoreComparison > 0;
        }

        int distanceComparison =
            candidate.FireEligibility.DistanceMeters.CompareTo(
                current.FireEligibility.DistanceMeters
            );

        if (distanceComparison != 0)
        {
            return distanceComparison < 0;
        }

        return candidate.CandidateIndex < current.CandidateIndex;
    }
}
