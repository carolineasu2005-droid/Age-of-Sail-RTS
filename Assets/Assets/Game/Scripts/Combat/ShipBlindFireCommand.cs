public sealed class ShipBlindFireCommand
{
    private readonly ShipFireEligibility fireEligibility;
    private readonly ShipCombatState combatState;
    private readonly ShipBroadsideFireExecutor broadsideExecutor;


    public ShipBlindFireCommand(
        ShipFireEligibility fireEligibility,
        ShipCombatState combatState,
        ShipBroadsideFireExecutor broadsideExecutor
    )
    {
        this.fireEligibility = fireEligibility;
        this.combatState = combatState;
        this.broadsideExecutor = broadsideExecutor;
    }


    public bool TryExecuteBlindFire(
        BlindFireAim aim,
        out BlindFireExecutionResult result
    )
    {
        return TryExecuteInternal(aim, 0u, false, out result);
    }


    public bool TryExecuteBlindFire(
        BlindFireAim aim,
        uint broadsideSeed,
        out BlindFireExecutionResult result
    )
    {
        return TryExecuteInternal(
            aim,
            broadsideSeed,
            true,
            out result
        );
    }


    private bool TryExecuteInternal(
        BlindFireAim aim,
        uint broadsideSeed,
        bool useExplicitSeed,
        out BlindFireExecutionResult result
    )
    {
        result = default;

        if (!aim.IsValid)
        {
            result = new BlindFireExecutionResult(
                false,
                null,
                aim,
                default,
                FireAimBasisFailure.InvalidGeometry,
                default,
                BlindFireExecutionFailure.InvalidAim
            );
            return false;
        }

        if (!HasValidDependencies())
        {
            result = new BlindFireExecutionResult(
                false,
                null,
                aim,
                default,
                FireAimBasisFailure.InvalidGeometry,
                default,
                BlindFireExecutionFailure.EligibilityUnavailable
            );
            return false;
        }

        bool evaluated = aim.HasWorldAimPoint
            ? fireEligibility.TryEvaluateBlindFireAtPoint(
                aim.WorldAimPoint.Value,
                out BlindFireEligibilityResult eligibility
            )
            : fireEligibility.TryEvaluateBlindFireDirection(
                aim.WorldAimDirection,
                out eligibility
            );

        if (!evaluated)
        {
            result = new BlindFireExecutionResult(
                false,
                null,
                aim,
                default,
                FireAimBasisFailure.InvalidGeometry,
                default,
                BlindFireExecutionFailure.EligibilityUnavailable
            );
            return false;
        }

        if (!eligibility.CanBlindFire || !eligibility.Side.HasValue)
        {
            result = new BlindFireExecutionResult(
                false,
                eligibility.Side,
                eligibility.Aim,
                eligibility,
                FireAimBasisFailure.BlindFireEligibilityRejected,
                default,
                BlindFireExecutionFailure.EligibilityRejected
            );
            return false;
        }

        if (!eligibility.Aim.HasWorldAimPoint)
        {
            result = new BlindFireExecutionResult(
                false,
                eligibility.Side,
                eligibility.Aim,
                eligibility,
                FireAimBasisFailure.FiniteAimPointRequired,
                default,
                BlindFireExecutionFailure.FiniteAimPointRequired
            );
            return false;
        }

        if (!ShipFireAimBasisBuilder.TryBuildBlindFirePoint(
            eligibility,
            out FireAimBasis aimBasis,
            out FireAimBasisFailure aimBasisFailure
        ))
        {
            result = new BlindFireExecutionResult(
                false,
                eligibility.Side,
                eligibility.Aim,
                eligibility,
                aimBasisFailure,
                default,
                BlindFireExecutionFailure.AimBasisUnavailable
            );
            return false;
        }

        bool accepted = useExplicitSeed
            ? broadsideExecutor.TryExecute(
                aimBasis,
                broadsideSeed,
                out BroadsideFireExecutionResult broadsideExecution
            )
            : broadsideExecutor.TryExecuteWithRuntimeSeed(
                aimBasis,
                out broadsideExecution
            );

        if (!accepted)
        {
            BlindFireExecutionFailure failure =
                BlindFireExecutionFailure.BroadsideExecutionRejected;

            if (broadsideExecution.FailureReasons.HasFlag(
                BroadsideFireExecutionFailure.BroadsideCommitFailed
            ))
            {
                failure |= BlindFireExecutionFailure
                    .BroadsideCommitFailed;
            }

            result = new BlindFireExecutionResult(
                false,
                eligibility.Side,
                eligibility.Aim,
                eligibility,
                FireAimBasisFailure.None,
                broadsideExecution,
                failure
            );
            return false;
        }

        combatState.SetAutoFireEnabled(false);
        result = new BlindFireExecutionResult(
            true,
            eligibility.Side,
            eligibility.Aim,
            eligibility,
            FireAimBasisFailure.None,
            broadsideExecution,
            BlindFireExecutionFailure.None
        );
        return true;
    }


    private bool HasValidDependencies()
    {
        return fireEligibility != null
            && combatState != null
            && broadsideExecutor != null
            && fireEligibility.gameObject == combatState.gameObject
            && broadsideExecutor.gameObject == combatState.gameObject;
    }
}
