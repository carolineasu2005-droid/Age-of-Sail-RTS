using UnityEngine;

public class ShipTacking : MonoBehaviour
{
    private enum TackState
    {
        Idle,
        TurningIntoWind,
        CrossingNoGo,
        Recovering,
        Completed,
        Failed
    }

    [Header("References")]

    [SerializeField]
    private ShipSailingSpeed shipSailingSpeed;

    [SerializeField]
    private ShipTurning shipTurning;

    [SerializeField]
    private ShipHeadingController headingController;


    [Header("Tack Settings")]

    [SerializeField]
    [Min(0f)]
    private float tackYawAssistRate = 4f;

    [SerializeField]
    [Min(0f)]
    private float tackAbortSpeed = 0.25f;

    [SerializeField]
    [Min(0f)]
    private float tackAbortDelay = 1.5f;

    [SerializeField]
    [Range(0f, 180f)]
    private float tackExitAngle = 67.5f;

    [SerializeField]
    [Min(0f)]
    private float windCrossDeadZone = 1f;


    [Header("Runtime Debug")]

    [SerializeField]
    private TackState state = TackState.Idle;

    [SerializeField]
    private bool isActive;

    [SerializeField]
    private float targetHeading;

    [SerializeField]
    private TurnDirection turnDirection;

    [SerializeField]
    private float startSignedWindAngle;

    [SerializeField]
    private int startWindSide;

    [SerializeField]
    private float currentSignedWindAngle;

    [SerializeField]
    private float currentAbsoluteWindAngle;

    [SerializeField]
    private bool crossedWind;

    [SerializeField]
    private float entrySpeed;

    [SerializeField]
    private float noGoEntrySpeed;

    [SerializeField]
    private float windCrossSpeed;

    [SerializeField]
    private float minimumSpeedDuringTack;

    [SerializeField]
    private float exitSpeed;

    [SerializeField]
    private float tackElapsedTime;

    [SerializeField]
    private float turningIntoWindTime;

    [SerializeField]
    private float crossingNoGoTime;

    [SerializeField]
    private float recoveryTime;

    [SerializeField]
    private float lowSpeedTimer;

    [SerializeField]
    private bool yawAssistActive;

    [SerializeField]
    private float currentYawAssistRate;


    [Header("Debug Commands")]

    [SerializeField]
    [Range(0f, 360f)]
    private float debugTargetHeading;

    [SerializeField]
    private TurnDirection debugTurnDirection;

    [SerializeField]
    private bool debugStartTack;

    [SerializeField]
    private bool debugCancelTack;

    private void Awake()
    {
        if (shipSailingSpeed == null)
        {
            shipSailingSpeed = GetComponent<ShipSailingSpeed>();
        }

        if (shipTurning == null)
        {
            shipTurning = GetComponent<ShipTurning>();
        }

        if (headingController == null)
        {
            headingController = GetComponent<ShipHeadingController>();
        }
    }


    private void Update()
    {
        if (debugStartTack)
        {
            debugStartTack = false;
            StartTack(debugTargetHeading, debugTurnDirection);
        }

        if (debugCancelTack)
        {
            debugCancelTack = false;
            CancelTack();
        }

        UpdateWindTelemetry();

        if (!isActive || shipSailingSpeed == null)
        {
            ClearYawAssist();
            return;
        }

        tackElapsedTime += Time.deltaTime;
        UpdatePhaseTiming();
        minimumSpeedDuringTack = Mathf.Min(
            minimumSpeedDuringTack,
            shipSailingSpeed.CurrentSpeed
        );

        switch (state)
        {
            case TackState.TurningIntoWind:
                UpdateTurningIntoWind();
                break;
            case TackState.CrossingNoGo:
                UpdateCrossingNoGo();
                break;
            case TackState.Recovering:
                UpdateRecovering();
                break;
        }
    }


    public void StartTack(float requestedTargetHeading, TurnDirection requestedDirection)
    {
        if (isActive)
        {
            CancelTack();
        }

        targetHeading = Mathf.Repeat(requestedTargetHeading, 360f);
        turnDirection = requestedDirection;
        ClearYawAssist();

        tackElapsedTime = 0f;
        turningIntoWindTime = 0f;
        crossingNoGoTime = 0f;
        recoveryTime = 0f;
        lowSpeedTimer = 0f;
        crossedWind = false;
        noGoEntrySpeed = 0f;
        windCrossSpeed = 0f;
        entrySpeed = shipSailingSpeed != null
            ? shipSailingSpeed.CurrentSpeed
            : 0f;
        minimumSpeedDuringTack = entrySpeed;
        exitSpeed = 0f;

        if (shipSailingSpeed == null
            || shipTurning == null
            || headingController == null)
        {
            state = TackState.Failed;
            isActive = false;
            return;
        }

        startSignedWindAngle = shipSailingSpeed.RelativeWindAngleSigned;
        startWindSide = Mathf.Sign(startSignedWindAngle) > 0f ? 1 : -1;

        if (Mathf.Abs(startSignedWindAngle) <= windCrossDeadZone)
        {
            state = TackState.Failed;
            isActive = false;
            headingController.CancelHeadingCommand();
            return;
        }

        state = TackState.TurningIntoWind;
        isActive = true;

        headingController.SetTargetHeading(targetHeading, turnDirection);
    }


    public void CancelTack()
    {
        ClearYawAssist();

        if (headingController != null)
        {
            headingController.CancelHeadingCommand();
        }

        state = TackState.Idle;
        isActive = false;
        tackElapsedTime = 0f;
        lowSpeedTimer = 0f;
        crossedWind = false;
    }


    private void UpdateTurningIntoWind()
    {
        ClearYawAssist();

        if (shipSailingSpeed.IsInNoGoZone)
        {
            noGoEntrySpeed = shipSailingSpeed.CurrentSpeed;
            state = TackState.CrossingNoGo;
        }
    }


    private void UpdatePhaseTiming()
    {
        switch (state)
        {
            case TackState.TurningIntoWind:
                turningIntoWindTime += Time.deltaTime;
                break;
            case TackState.CrossingNoGo:
                crossingNoGoTime += Time.deltaTime;
                break;
            case TackState.Recovering:
                recoveryTime += Time.deltaTime;
                break;
        }
    }


    private void UpdateCrossingNoGo()
    {
        if (shipSailingSpeed.CurrentSpeed < tackAbortSpeed)
        {
            ClearYawAssist();
            lowSpeedTimer += Time.deltaTime;

            if (lowSpeedTimer >= tackAbortDelay)
            {
                FailTack();
            }

            return;
        }

        lowSpeedTimer = 0f;
        SetTackYawAssist();

        if (!crossedWind)
        {
            bool hasCrossedWind = startWindSide > 0
                ? currentSignedWindAngle <= -windCrossDeadZone
                : currentSignedWindAngle >= windCrossDeadZone;

            if (hasCrossedWind)
            {
                crossedWind = true;
                windCrossSpeed = shipSailingSpeed.CurrentSpeed;
            }
        }

        if (crossedWind
            && currentAbsoluteWindAngle >= tackExitAngle
            && shipSailingSpeed.PolarEfficiency > 0f)
        {
            exitSpeed = shipSailingSpeed.CurrentSpeed;
            ClearYawAssist();
            lowSpeedTimer = 0f;
            state = TackState.Recovering;
        }
    }


    private void UpdateRecovering()
    {
        ClearYawAssist();

        if (!headingController.IsActive)
        {
            state = TackState.Completed;
            isActive = false;
        }
    }


    private void SetTackYawAssist()
    {
        currentYawAssistRate = turnDirection == TurnDirection.Clockwise
            ? tackYawAssistRate
            : -tackYawAssistRate;
        yawAssistActive = true;
        shipTurning.SetManeuverYawAssistRate(currentYawAssistRate);
    }


    private void ClearYawAssist()
    {
        currentYawAssistRate = 0f;
        yawAssistActive = false;

        if (shipTurning != null)
        {
            shipTurning.ClearManeuverYawAssist();
        }
    }


    private void FailTack()
    {
        ClearYawAssist();

        if (headingController != null)
        {
            headingController.CancelHeadingCommand();
        }

        state = TackState.Failed;
        isActive = false;
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
