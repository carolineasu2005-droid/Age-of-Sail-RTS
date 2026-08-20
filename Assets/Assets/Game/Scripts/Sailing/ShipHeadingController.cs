using UnityEngine;

[RequireComponent(typeof(ShipTurning))]
public class ShipHeadingController : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private ShipTurning shipTurning;


    [Header("Heading Settings")]

    [SerializeField]
    [Min(0.1f)]
    private float rudderEaseAngle = 20f;

    [SerializeField]
    [Min(0.1f)]
    private float headingTolerance = 0.5f;


    [Header("Runtime Debug")]

    [SerializeField]
    private bool isActive;

    [SerializeField]
    private float targetHeading;

    [SerializeField]
    private float currentHeading;

    [SerializeField]
    private TurnDirection turnDirection;

    [SerializeField]
    private float commandedArc;

    [SerializeField]
    private float accumulatedTurnAngle;

    [SerializeField]
    private float remainingTurnAngle;

    [SerializeField]
    private float rudderCommandOutput;

    [SerializeField]
    private float actualHeadingError;


    [Header("Debug Commands")]

    [SerializeField]
    [Range(0f, 360f)]
    private float debugTargetHeading;

    [SerializeField]
    private TurnDirection debugTurnDirection;

    [SerializeField]
    private bool debugExecuteCommand;

    [SerializeField]
    private bool debugCancelCommand;

    private float previousHeading;

    public bool IsActive => isActive;

    public float TargetHeading => targetHeading;

    private void Awake()
    {
        if (shipTurning == null)
        {
            shipTurning = GetComponent<ShipTurning>();
        }
    }


    private void Update()
    {
        if (debugExecuteCommand)
        {
            debugExecuteCommand = false;
            SetTargetHeading(debugTargetHeading, debugTurnDirection);
        }

        if (debugCancelCommand)
        {
            debugCancelCommand = false;
            CancelHeadingCommand();
        }

        currentHeading = NormalizeHeading(transform.eulerAngles.y);
        actualHeadingError = Mathf.Abs(
            Mathf.DeltaAngle(currentHeading, targetHeading)
        );

        if (!isActive || shipTurning == null)
        {
            return;
        }

        float signedYawDelta =
            Mathf.DeltaAngle(previousHeading, currentHeading);

        if (turnDirection == TurnDirection.Clockwise)
        {
            accumulatedTurnAngle += Mathf.Max(0f, signedYawDelta);
        }
        else
        {
            accumulatedTurnAngle += Mathf.Max(0f, -signedYawDelta);
        }

        previousHeading = currentHeading;
        remainingTurnAngle =
            Mathf.Max(0f, commandedArc - accumulatedTurnAngle);

        if (remainingTurnAngle <= headingTolerance)
        {
            remainingTurnAngle = 0f;
            rudderCommandOutput = 0f;
            shipTurning.SetRudderCommand(0f);
            isActive = false;
            return;
        }

        float rudderMagnitude = Mathf.Clamp01(
            remainingTurnAngle / rudderEaseAngle
        );

        rudderCommandOutput = turnDirection == TurnDirection.Clockwise
            ? rudderMagnitude
            : -rudderMagnitude;

        shipTurning.SetRudderCommand(rudderCommandOutput);
    }


    public void SetTargetHeading(float targetHeading, TurnDirection direction)
    {
        this.targetHeading = NormalizeHeading(targetHeading);
        turnDirection = direction;
        currentHeading = NormalizeHeading(transform.eulerAngles.y);

        commandedArc = direction == TurnDirection.Clockwise
            ? Mathf.Repeat(this.targetHeading - currentHeading, 360f)
            : Mathf.Repeat(currentHeading - this.targetHeading, 360f);

        if (Mathf.Abs(Mathf.DeltaAngle(currentHeading, this.targetHeading))
            <= headingTolerance)
        {
            accumulatedTurnAngle = 0f;
            remainingTurnAngle = 0f;
            rudderCommandOutput = 0f;
            isActive = false;
            if (shipTurning != null)
            {
                shipTurning.SetRudderCommand(0f);
            }

            return;
        }

        previousHeading = currentHeading;
        accumulatedTurnAngle = 0f;
        remainingTurnAngle = commandedArc;
        isActive = true;
    }


    public void CancelHeadingCommand()
    {
        isActive = false;
        rudderCommandOutput = 0f;

        if (shipTurning != null)
        {
            shipTurning.SetRudderCommand(0f);
        }
    }


    private static float NormalizeHeading(float heading)
    {
        return Mathf.Repeat(heading, 360f);
    }
}
