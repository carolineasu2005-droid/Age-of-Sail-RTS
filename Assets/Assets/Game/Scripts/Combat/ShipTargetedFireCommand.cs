using UnityEngine;

public sealed class ShipTargetedFireCommand
{
    private readonly ShipFireEligibility fireEligibility;
    private readonly ShipBroadsideFireExecutor broadsideExecutor;


    public ShipTargetedFireCommand(
        ShipFireEligibility fireEligibility,
        ShipBroadsideFireExecutor broadsideExecutor
    )
    {
        this.fireEligibility = fireEligibility;
        this.broadsideExecutor = broadsideExecutor;
    }


    public bool TryExecute(
        GameObject targetShipRoot,
        bool relationshipAllowsFire,
        uint broadsideSeed,
        out TargetedFireExecutionResult result
    )
    {
        return TryExecuteInternal(
            targetShipRoot,
            relationshipAllowsFire,
            broadsideSeed,
            true,
            out result
        );
    }


    public bool TryExecuteWithRuntimeSeed(
        GameObject targetShipRoot,
        bool relationshipAllowsFire,
        out TargetedFireExecutionResult result
    )
    {
        return TryExecuteInternal(
            targetShipRoot,
            relationshipAllowsFire,
            0u,
            false,
            out result
        );
    }


    private bool TryExecuteInternal(
        GameObject targetShipRoot,
        bool relationshipAllowsFire,
        uint broadsideSeed,
        bool useExplicitSeed,
        out TargetedFireExecutionResult result
    )
    {
        result = default;

        if (!HasValidDependencies())
        {
            result = CreateRejected(
                targetShipRoot,
                default,
                FireAimBasisFailure.FireEligibilityRejected,
                default,
                TargetedFireExecutionFailure.InvalidDependencies
            );
            return false;
        }

        if (!fireEligibility.TryEvaluate(
            targetShipRoot,
            relationshipAllowsFire,
            out FireEligibilityResult eligibility
        ))
        {
            result = CreateRejected(
                targetShipRoot,
                default,
                FireAimBasisFailure.FireEligibilityRejected,
                default,
                TargetedFireExecutionFailure.EligibilityUnavailable
            );
            return false;
        }

        if (!eligibility.CanFire)
        {
            result = CreateRejected(
                targetShipRoot,
                eligibility,
                FireAimBasisFailure.FireEligibilityRejected,
                default,
                TargetedFireExecutionFailure.EligibilityRejected
            );
            return false;
        }

        if (!ShipFireAimBasisBuilder.TryBuildTargeted(
            fireEligibility.gameObject,
            targetShipRoot,
            eligibility,
            out FireAimBasis aimBasis,
            out FireAimBasisFailure aimFailure
        ))
        {
            result = CreateRejected(
                targetShipRoot,
                eligibility,
                aimFailure,
                default,
                TargetedFireExecutionFailure.AimBasisUnavailable
            );
            return false;
        }

        bool accepted = useExplicitSeed
            ? broadsideExecutor.TryExecute(
                aimBasis,
                broadsideSeed,
                out BroadsideFireExecutionResult execution
            )
            : broadsideExecutor.TryExecuteWithRuntimeSeed(
                aimBasis,
                out execution
            );
        result = new TargetedFireExecutionResult(
            accepted,
            targetShipRoot,
            eligibility,
            FireAimBasisFailure.None,
            execution,
            accepted
                ? TargetedFireExecutionFailure.None
                : TargetedFireExecutionFailure
                    .BroadsideExecutionRejected
        );
        return accepted;
    }


    private bool HasValidDependencies()
    {
        return fireEligibility != null
            && broadsideExecutor != null
            && fireEligibility.gameObject == broadsideExecutor.gameObject;
    }


    private static TargetedFireExecutionResult CreateRejected(
        GameObject targetShipRoot,
        FireEligibilityResult eligibility,
        FireAimBasisFailure aimFailure,
        BroadsideFireExecutionResult broadsideExecution,
        TargetedFireExecutionFailure failure
    )
    {
        return new TargetedFireExecutionResult(
            false,
            targetShipRoot,
            eligibility,
            aimFailure,
            broadsideExecution,
            failure
        );
    }
}
