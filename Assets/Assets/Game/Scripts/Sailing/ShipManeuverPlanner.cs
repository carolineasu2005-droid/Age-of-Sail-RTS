using UnityEngine;

public class ShipManeuverPlanner : MonoBehaviour
{
    public enum ManeuverType
    {
        None,
        NormalTurn,
        Tack,
        Wear,
        Complex
    }

    private const float ClassificationEpsilon = 0.1f;

    [Header("References")]

    [SerializeField]
    private GlobalWind globalWind;

    [SerializeField]
    private ShipHeadingController headingController;

    [SerializeField]
    private ShipTacking shipTacking;

    [SerializeField]
    private ShipWearing shipWearing;


    [Header("Runtime Debug")]

    [SerializeField]
    private float currentHeading;

    [SerializeField]
    private float targetHeading;

    [SerializeField]
    private TurnDirection turnDirection;

    [SerializeField]
    private float commandedArc;

    [SerializeField]
    private float windFromHeading;

    [SerializeField]
    private float downwindHeading;

    [SerializeField]
    private float windFromDirectedDistance;

    [SerializeField]
    private float downwindDirectedDistance;

    [SerializeField]
    private bool crossesWindFrom;

    [SerializeField]
    private bool crossesDownwind;

    [SerializeField]
    private ManeuverType classifiedManeuver;

    [SerializeField]
    private bool isActive;

    [SerializeField]
    private ManeuverType currentManeuver;


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

    public bool IsActive => isActive;

    public ManeuverType CurrentManeuver => currentManeuver;

    private void Awake()
    {
        if (globalWind == null)
        {
            globalWind = GetComponent<GlobalWind>();
        }

        if (headingController == null)
        {
            headingController = GetComponent<ShipHeadingController>();
        }

        if (shipTacking == null)
        {
            shipTacking = GetComponent<ShipTacking>();
        }

        if (shipWearing == null)
        {
            shipWearing = GetComponent<ShipWearing>();
        }
    }


    private void Update()
    {
        if (debugExecuteCommand)
        {
            debugExecuteCommand = false;
            ExecuteHeadingCommand(debugTargetHeading, debugTurnDirection);
        }

        if (debugCancelCommand)
        {
            debugCancelCommand = false;
            CancelCurrentManeuver();
        }

        if (isActive && (headingController == null || !headingController.IsActive))
        {
            isActive = false;
        }
    }


    public ManeuverType ClassifyHeadingCommand(
        float requestedTargetHeading,
        TurnDirection requestedDirection
    )
    {
        currentHeading = NormalizeHeading(transform.eulerAngles.y);
        targetHeading = NormalizeHeading(requestedTargetHeading);
        turnDirection = requestedDirection;
        commandedArc = CalculateDirectedDistance(
            currentHeading,
            targetHeading,
            turnDirection
        );
        windFromHeading = 0f;
        downwindHeading = 0f;
        windFromDirectedDistance = 0f;
        downwindDirectedDistance = 0f;
        crossesWindFrom = false;
        crossesDownwind = false;
        classifiedManeuver = ManeuverType.None;

        if (commandedArc <= ClassificationEpsilon || globalWind == null)
        {
            return classifiedManeuver;
        }

        windFromHeading = HeadingFromDirection(globalWind.WindFromDirection);
        downwindHeading = NormalizeHeading(windFromHeading + 180f);
        windFromDirectedDistance = CalculateDirectedDistance(
            currentHeading,
            windFromHeading,
            turnDirection
        );
        downwindDirectedDistance = CalculateDirectedDistance(
            currentHeading,
            downwindHeading,
            turnDirection
        );
        crossesWindFrom = IsBoundaryInsideCommandedArc(
            windFromDirectedDistance
        );
        crossesDownwind = IsBoundaryInsideCommandedArc(
            downwindDirectedDistance
        );

        if (crossesWindFrom && crossesDownwind)
        {
            classifiedManeuver = ManeuverType.Complex;
        }
        else if (crossesWindFrom)
        {
            classifiedManeuver = ManeuverType.Tack;
        }
        else if (crossesDownwind)
        {
            classifiedManeuver = ManeuverType.Wear;
        }
        else
        {
            classifiedManeuver = ManeuverType.NormalTurn;
        }

        return classifiedManeuver;
    }


    public void ExecuteHeadingCommand(
        float requestedTargetHeading,
        TurnDirection requestedDirection
    )
    {
        if (isActive)
        {
            CancelCurrentManeuver();
        }

        ClassifyHeadingCommand(requestedTargetHeading, requestedDirection);
        currentManeuver = classifiedManeuver;
        isActive = false;

        switch (classifiedManeuver)
        {
            case ManeuverType.NormalTurn:
                if (headingController != null)
                {
                    headingController.SetTargetHeading(targetHeading, turnDirection);
                    isActive = headingController.IsActive;
                }
                break;
            case ManeuverType.Tack:
                if (shipTacking != null && headingController != null)
                {
                    shipTacking.StartTack(targetHeading, turnDirection);
                    isActive = headingController.IsActive;
                }
                break;
            case ManeuverType.Wear:
                if (shipWearing != null && headingController != null)
                {
                    shipWearing.StartWear(targetHeading, turnDirection);
                    isActive = headingController.IsActive;
                }
                break;
        }
    }


    public void CancelCurrentManeuver()
    {
        switch (currentManeuver)
        {
            case ManeuverType.NormalTurn:
                if (headingController != null)
                {
                    headingController.CancelHeadingCommand();
                }
                break;
            case ManeuverType.Tack:
                if (shipTacking != null)
                {
                    shipTacking.CancelTack();
                }
                break;
            case ManeuverType.Wear:
                if (shipWearing != null)
                {
                    shipWearing.CancelWear();
                }
                break;
        }

        isActive = false;
        currentManeuver = ManeuverType.None;
        classifiedManeuver = ManeuverType.None;
    }


    private float CalculateDirectedDistance(
        float fromHeading,
        float toHeading,
        TurnDirection direction
    )
    {
        return direction == TurnDirection.Clockwise
            ? Mathf.Repeat(toHeading - fromHeading, 360f)
            : Mathf.Repeat(fromHeading - toHeading, 360f);
    }


    private bool IsBoundaryInsideCommandedArc(float boundaryDistance)
    {
        return boundaryDistance > ClassificationEpsilon
            && boundaryDistance < commandedArc - ClassificationEpsilon;
    }


    private static float HeadingFromDirection(Vector3 direction)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            direction,
            Vector3.up
        );

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return NormalizeHeading(
            Mathf.Atan2(horizontalDirection.x, horizontalDirection.z)
            * Mathf.Rad2Deg
        );
    }


    private static float NormalizeHeading(float heading)
    {
        return Mathf.Repeat(heading, 360f);
    }
}
