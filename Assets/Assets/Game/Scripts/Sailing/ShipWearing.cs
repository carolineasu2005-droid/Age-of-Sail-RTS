using UnityEngine;

public class ShipWearing : MonoBehaviour
{
    private enum WearState
    {
        Idle,
        TurningDownwind,
        CrossingDownwind,
        Recovering,
        Completed,
        Failed
    }

    [Header("References")]

    [SerializeField]
    private ShipSailingSpeed shipSailingSpeed;

    [SerializeField]
    private ShipHeadingController headingController;


    [Header("Wear Settings")]

    [SerializeField]
    [Range(90f, 180f)]
    private float downwindCrossThreshold = 170f;


    [Header("Runtime Debug")]

    [SerializeField]
    private WearState state = WearState.Idle;

    [SerializeField]
    private bool isActive;

    [SerializeField]
    private float targetHeading;

    [SerializeField]
    private TurnDirection turnDirection;

    [SerializeField]
    private float startSignedWindAngle;

    [SerializeField]
    private float previousSignedWindAngle;

    [SerializeField]
    private float currentSignedWindAngle;

    [SerializeField]
    private float currentAbsoluteWindAngle;

    [SerializeField]
    private bool crossedDownwind;

    [SerializeField]
    private float entrySpeed;

    [SerializeField]
    private float downwindCrossSpeed;

    [SerializeField]
    private float minimumSpeedDuringWear;

    [SerializeField]
    private float maximumSpeedDuringWear;

    [SerializeField]
    private float completionSpeed;

    [SerializeField]
    private float timeToDownwindCross;

    [SerializeField]
    private float recoveryTime;

    [SerializeField]
    private float totalWearTime;


    [Header("Debug Commands")]

    [SerializeField]
    [Range(0f, 360f)]
    private float debugTargetHeading;

    [SerializeField]
    private TurnDirection debugTurnDirection;

    [SerializeField]
    private bool debugStartWear;

    [SerializeField]
    private bool debugCancelWear;

    private void Awake()
    {
        if (shipSailingSpeed == null)
        {
            shipSailingSpeed = GetComponent<ShipSailingSpeed>();
        }

        if (headingController == null)
        {
            headingController = GetComponent<ShipHeadingController>();
        }
    }


    private void Update()
    {
        if (debugStartWear)
        {
            debugStartWear = false;
            StartWear(debugTargetHeading, debugTurnDirection);
        }

        if (debugCancelWear)
        {
            debugCancelWear = false;
            CancelWear();
        }

        if (!isActive)
        {
            return;
        }

        UpdateWindTelemetry();

        float currentSpeed = shipSailingSpeed != null
            ? shipSailingSpeed.CurrentSpeed
            : 0f;
        minimumSpeedDuringWear = Mathf.Min(
            minimumSpeedDuringWear,
            currentSpeed
        );
        maximumSpeedDuringWear = Mathf.Max(
            maximumSpeedDuringWear,
            currentSpeed
        );
        totalWearTime += Time.deltaTime;

        if (!crossedDownwind)
        {
            timeToDownwindCross += Time.deltaTime;
        }

        if (state == WearState.Recovering)
        {
            recoveryTime += Time.deltaTime;
        }

        switch (state)
        {
            case WearState.TurningDownwind:
                UpdateTurningDownwind();
                break;
            case WearState.CrossingDownwind:
                UpdateCrossingDownwind();
                break;
            case WearState.Recovering:
                UpdateRecovering();
                break;
        }

        previousSignedWindAngle = currentSignedWindAngle;
    }


    public void StartWear(float requestedTargetHeading, TurnDirection requestedDirection)
    {
        if (isActive)
        {
            CancelWear();
        }

        targetHeading = Mathf.Repeat(requestedTargetHeading, 360f);
        turnDirection = requestedDirection;
        crossedDownwind = false;
        UpdateWindTelemetry();
        entrySpeed = shipSailingSpeed != null
            ? shipSailingSpeed.CurrentSpeed
            : 0f;
        minimumSpeedDuringWear = entrySpeed;
        maximumSpeedDuringWear = entrySpeed;
        downwindCrossSpeed = 0f;
        completionSpeed = 0f;
        timeToDownwindCross = 0f;
        recoveryTime = 0f;
        totalWearTime = 0f;
        startSignedWindAngle = currentSignedWindAngle;
        previousSignedWindAngle = currentSignedWindAngle;
        state = WearState.TurningDownwind;
        isActive = true;

        if (headingController != null)
        {
            headingController.SetTargetHeading(targetHeading, turnDirection);
        }
    }


    public void CancelWear()
    {
        if (headingController != null)
        {
            headingController.CancelHeadingCommand();
        }

        state = WearState.Idle;
        isActive = false;
    }


    private void UpdateTurningDownwind()
    {
        if (headingController == null || shipSailingSpeed == null)
        {
            state = WearState.Failed;
            isActive = false;
            return;
        }

        if (!headingController.IsActive)
        {
            state = WearState.Failed;
            isActive = false;
            return;
        }

        if (currentAbsoluteWindAngle >= downwindCrossThreshold)
        {
            state = WearState.CrossingDownwind;
        }
    }


    private void UpdateCrossingDownwind()
    {
        if (headingController == null)
        {
            state = WearState.Failed;
            isActive = false;
            return;
        }

        if (!crossedDownwind)
        {
            bool hasCrossedDownwind = turnDirection == TurnDirection.CounterClockwise
                ? previousSignedWindAngle >= downwindCrossThreshold
                    && currentSignedWindAngle <= -downwindCrossThreshold
                : previousSignedWindAngle <= -downwindCrossThreshold
                    && currentSignedWindAngle >= downwindCrossThreshold;

            if (hasCrossedDownwind)
            {
                crossedDownwind = true;
                downwindCrossSpeed = shipSailingSpeed != null
                    ? shipSailingSpeed.CurrentSpeed
                    : 0f;
                state = WearState.Recovering;
                return;
            }
        }

        if (!headingController.IsActive)
        {
            state = WearState.Failed;
            isActive = false;
        }
    }


    private void UpdateRecovering()
    {
        if (headingController == null)
        {
            state = WearState.Failed;
            isActive = false;
            return;
        }

        if (!headingController.IsActive)
        {
            completionSpeed = shipSailingSpeed != null
                ? shipSailingSpeed.CurrentSpeed
                : 0f;
            state = WearState.Completed;
            isActive = false;
        }
    }


    private void UpdateWindTelemetry()
    {
        if (shipSailingSpeed == null)
        {
            return;
        }

        currentSignedWindAngle = shipSailingSpeed.RelativeWindAngleSigned;
        currentAbsoluteWindAngle = shipSailingSpeed.RelativeWindAngleAbsolute;
    }
}
