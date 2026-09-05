using UnityEngine;

public class ShipDestinationController : MonoBehaviour
{
    public enum TurnSelectionMode
    {
        Auto,
        ForceClockwise,
        ForceCounterClockwise
    }

    public enum NavigationMode
    {
        None,
        Direct,
        BeatingUpwind,
        Blocked
    }

    [Header("References")]

    [SerializeField]
    private ShipManeuverPlanner maneuverPlanner;

    [SerializeField]
    private GlobalWind globalWind;


    [Header("Navigation Settings")]

    [SerializeField]
    [Min(0f)]
    private float arrivalRadius = 5f;

    [SerializeField]
    [Min(0f)]
    private float replanHeadingThreshold = 3f;

    [SerializeField]
    [Range(0f, 180f)]
    private float directSailingThreshold = 45f;

    [SerializeField]
    [Range(0f, 180f)]
    private float directResumeThreshold = 50f;

    [SerializeField]
    [Range(0f, 180f)]
    private float closeHauledHeadingAngle = 50f;

    [SerializeField]
    [Min(0f)]
    private float maximumTackCorridorHalfWidth = 15f;

    [SerializeField]
    [Min(0f)]
    private float minimumTackCorridorHalfWidth = 6f;

    [SerializeField]
    [Range(0f, 1f)]
    private float corridorDistanceRatio = 0.08f;

    [SerializeField]
    [Range(0.5f, 1f)]
    private float corridorSwitchFactor = 0.85f;


    [Header("Runtime Debug")]

    [SerializeField]
    private bool hasDestination;

    [SerializeField]
    private Vector3 destination;

    [SerializeField]
    private bool reachedDestination;

    [SerializeField]
    private float distanceToDestination;

    [SerializeField]
    private float desiredBearing;

    [SerializeField]
    private float currentDestinationBearing;

    [SerializeField]
    private float currentHeading;

    [SerializeField]
    private NavigationMode navigationMode;

    [SerializeField]
    private WindNavigationAssistMode activeNavigationAssistMode =
        WindNavigationAssistMode.Assisted;

    [SerializeField]
    private float windFromHeading;

    [SerializeField]
    private float desiredBearingRelativeToWind;

    [SerializeField]
    private float currentDestinationBearingRelativeToWind;

    [SerializeField]
    private float positiveCloseHauledHeading;

    [SerializeField]
    private float negativeCloseHauledHeading;

    [SerializeField]
    private float currentLegHeading;

    [SerializeField]
    private float currentLegCrossTrackSign;

    [SerializeField]
    private float crossTrackDistance;

    [SerializeField]
    private float dynamicCorridorHalfWidth;

    [SerializeField]
    private int tackSwitchCount;

    [SerializeField]
    private Vector3 lastTackSwitchPosition;

    [SerializeField]
    private Vector3 routeOrigin;

    [SerializeField]
    private Vector3 routeDestination;

    [SerializeField]
    private Vector3 routeDirection;

    [SerializeField]
    private Vector3 routeRight;

    [SerializeField]
    private TurnSelectionMode initialSelectionMode;

    [SerializeField]
    private TurnSelectionMode initialUpwindSelectionMode;

    [SerializeField]
    private float lastCommandedHeading;

    [SerializeField]
    private TurnDirection lastCommandedDirection;

    [SerializeField]
    private bool navigationBlocked;

    private void Awake()
    {
        if (maneuverPlanner == null)
        {
            maneuverPlanner = GetComponent<ShipManeuverPlanner>();
        }

        if (globalWind == null)
        {
            globalWind = GetComponent<GlobalWind>();

            if (globalWind == null)
            {
                globalWind = FindFirstObjectByType<GlobalWind>();
            }
        }
    }


    private void Update()
    {
        if (!hasDestination)
        {
            return;
        }

        UpdateDestinationData();

        if (distanceToDestination <= arrivalRadius)
        {
            reachedDestination = true;
            hasDestination = false;
            navigationMode = NavigationMode.None;

            if (maneuverPlanner != null && maneuverPlanner.IsActive)
            {
                maneuverPlanner.CancelCurrentManeuver();
            }

            return;
        }

        if (activeNavigationAssistMode != WindNavigationAssistMode.Assisted
            && navigationMode == NavigationMode.BeatingUpwind)
        {
            ClearAssistedNavigationState();
            navigationMode = NavigationMode.Direct;
        }

        if (navigationMode == NavigationMode.BeatingUpwind)
        {
            UpdateBeatingUpwindNavigation();
            return;
        }

        if (maneuverPlanner == null
            || maneuverPlanner.IsActive
            || navigationBlocked)
        {
            return;
        }

        float headingError = Mathf.Abs(
            Mathf.DeltaAngle(currentHeading, desiredBearing)
        );

        if (headingError > replanHeadingThreshold)
        {
            IssueHeadingCommand(desiredBearing, TurnSelectionMode.Auto);
        }
    }


    public void SetDestination(
        Vector3 worldDestination,
        TurnSelectionMode selectionMode,
        WindNavigationAssistMode navigationAssistMode
    )
    {
        ResetPreviousNavigationCommand();
        destination = worldDestination;
        hasDestination = true;
        initialSelectionMode = selectionMode;
        activeNavigationAssistMode = navigationAssistMode;

        UpdateDestinationData();

        if (distanceToDestination <= arrivalRadius)
        {
            reachedDestination = true;
            hasDestination = false;
            navigationMode = NavigationMode.None;
            return;
        }

        if (activeNavigationAssistMode != WindNavigationAssistMode.Assisted)
        {
            navigationMode = NavigationMode.Direct;
            IssueHeadingCommand(desiredBearing, selectionMode);
            return;
        }

        UpdateWindNavigationData();
        InitializeRouteReference();

        if (globalWind == null
            || desiredBearingRelativeToWind >= directSailingThreshold)
        {
            navigationMode = NavigationMode.Direct;
            IssueHeadingCommand(desiredBearing, selectionMode);
            return;
        }

        navigationMode = NavigationMode.BeatingUpwind;
        SelectAndCommandInitialUpwindLeg(selectionMode);
    }


    public void ClearDestination()
    {
        ResetPreviousNavigationCommand();
    }


    private void ResetPreviousNavigationCommand()
    {
        if (maneuverPlanner != null && maneuverPlanner.IsActive)
        {
            maneuverPlanner.CancelCurrentManeuver();
        }

        hasDestination = false;
        reachedDestination = false;
        navigationBlocked = false;
        navigationMode = NavigationMode.None;
        activeNavigationAssistMode = WindNavigationAssistMode.Assisted;
        destination = Vector3.zero;
        distanceToDestination = 0f;
        desiredBearing = 0f;
        currentDestinationBearing = 0f;
        ClearAssistedNavigationState();
        initialSelectionMode = TurnSelectionMode.Auto;
        lastCommandedHeading = 0f;
        lastCommandedDirection = TurnDirection.Clockwise;
    }


    private void ClearAssistedNavigationState()
    {
        navigationBlocked = false;
        windFromHeading = 0f;
        desiredBearingRelativeToWind = 0f;
        currentDestinationBearingRelativeToWind = 0f;
        positiveCloseHauledHeading = 0f;
        negativeCloseHauledHeading = 0f;
        currentLegHeading = 0f;
        currentLegCrossTrackSign = 0f;
        crossTrackDistance = 0f;
        dynamicCorridorHalfWidth = 0f;
        tackSwitchCount = 0;
        lastTackSwitchPosition = Vector3.zero;
        routeOrigin = Vector3.zero;
        routeDestination = Vector3.zero;
        routeDirection = Vector3.zero;
        routeRight = Vector3.zero;
        initialUpwindSelectionMode = TurnSelectionMode.Auto;
    }


    private void UpdateDestinationData()
    {
        Vector3 horizontalOffset = destination - transform.position;
        horizontalOffset.y = 0f;

        distanceToDestination = horizontalOffset.magnitude;
        currentHeading = GetHeading(transform.forward);
        desiredBearing = distanceToDestination > 0.0001f
            ? GetHeading(horizontalOffset)
            : currentHeading;
        currentDestinationBearing = desiredBearing;
    }


    private void UpdateWindNavigationData()
    {
        if (globalWind == null)
        {
            windFromHeading = 0f;
            desiredBearingRelativeToWind = 0f;
            currentDestinationBearingRelativeToWind = 0f;
            return;
        }

        windFromHeading = GetHeading(globalWind.WindFromDirection);
        desiredBearingRelativeToWind = Mathf.Abs(
            Mathf.DeltaAngle(desiredBearing, windFromHeading)
        );
        currentDestinationBearingRelativeToWind =
            desiredBearingRelativeToWind;
        positiveCloseHauledHeading = NormalizeHeading(
            windFromHeading + closeHauledHeadingAngle
        );
        negativeCloseHauledHeading = NormalizeHeading(
            windFromHeading - closeHauledHeadingAngle
        );
    }


    private void InitializeRouteReference()
    {
        routeOrigin = transform.position;
        routeDestination = destination;

        Vector3 horizontalRoute = routeDestination - routeOrigin;
        horizontalRoute.y = 0f;
        routeDirection = horizontalRoute.sqrMagnitude > 0.0001f
            ? horizontalRoute.normalized
            : Vector3.zero;
        routeRight = Vector3.Cross(Vector3.up, routeDirection);
    }


    private void UpdateBeatingUpwindNavigation()
    {
        if (activeNavigationAssistMode != WindNavigationAssistMode.Assisted)
        {
            ClearAssistedNavigationState();
            navigationMode = NavigationMode.Direct;
            return;
        }

        UpdateWindNavigationData();
        UpdateCorridorData();

        if (currentDestinationBearingRelativeToWind
            >= directResumeThreshold)
        {
            navigationMode = NavigationMode.Direct;
            IssueHeadingCommand(desiredBearing, TurnSelectionMode.Auto);
            return;
        }

        if (maneuverPlanner == null
            || maneuverPlanner.IsActive
            || navigationBlocked)
        {
            return;
        }

        if (crossTrackDistance * currentLegCrossTrackSign
            >= dynamicCorridorHalfWidth * corridorSwitchFactor)
        {
            SwitchToOppositeUpwindLeg();
        }
    }


    private void UpdateCorridorData()
    {
        dynamicCorridorHalfWidth = Mathf.Clamp(
            distanceToDestination * corridorDistanceRatio,
            minimumTackCorridorHalfWidth,
            maximumTackCorridorHalfWidth
        );

        Vector3 horizontalOffset = transform.position - routeOrigin;
        horizontalOffset.y = 0f;
        crossTrackDistance = Vector3.Dot(horizontalOffset, routeRight);
    }


    private void SwitchToOppositeUpwindLeg()
    {
        float nextLegHeading = Mathf.Abs(Mathf.DeltaAngle(
            currentLegHeading,
            positiveCloseHauledHeading
        )) <= 0.1f
            ? negativeCloseHauledHeading
            : positiveCloseHauledHeading;
        TurnDirection direction = ResolveTurnDirection(
            currentHeading,
            nextLegHeading,
            TurnSelectionMode.Auto
        );

        currentLegHeading = nextLegHeading;
        currentLegCrossTrackSign = Mathf.Sign(Vector3.Dot(
            HeadingToDirection(currentLegHeading),
            routeRight
        ));
        tackSwitchCount++;
        lastTackSwitchPosition = transform.position;
        IssueHeadingCommand(nextLegHeading, direction);
    }


    private void SelectAndCommandInitialUpwindLeg(
        TurnSelectionMode selectionMode
    )
    {
        float selectedHeading = positiveCloseHauledHeading;
        TurnDirection selectedDirection;

        switch (selectionMode)
        {
            case TurnSelectionMode.ForceClockwise:
                selectedHeading = SelectHeadingForDirectedArc(
                    positiveCloseHauledHeading,
                    negativeCloseHauledHeading,
                    TurnDirection.Clockwise
                );
                selectedDirection = TurnDirection.Clockwise;
                break;
            case TurnSelectionMode.ForceCounterClockwise:
                selectedHeading = SelectHeadingForDirectedArc(
                    positiveCloseHauledHeading,
                    negativeCloseHauledHeading,
                    TurnDirection.CounterClockwise
                );
                selectedDirection = TurnDirection.CounterClockwise;
                break;
            default:
                selectedHeading = SelectHeadingForAutoUpwindLeg();
                selectedDirection = ResolveTurnDirection(
                    currentHeading,
                    selectedHeading,
                    TurnSelectionMode.Auto
                );
                break;
        }

        currentLegHeading = selectedHeading;
        currentLegCrossTrackSign = Mathf.Sign(Vector3.Dot(
            HeadingToDirection(currentLegHeading),
            routeRight
        ));
        initialUpwindSelectionMode = selectionMode;
        IssueHeadingCommand(selectedHeading, selectedDirection);
    }


    private float SelectHeadingForAutoUpwindLeg()
    {
        float positiveAlignment = Mathf.Abs(Mathf.DeltaAngle(
            desiredBearing,
            positiveCloseHauledHeading
        ));
        float negativeAlignment = Mathf.Abs(Mathf.DeltaAngle(
            desiredBearing,
            negativeCloseHauledHeading
        ));

        if (!Mathf.Approximately(positiveAlignment, negativeAlignment))
        {
            return positiveAlignment < negativeAlignment
                ? positiveCloseHauledHeading
                : negativeCloseHauledHeading;
        }

        float positiveTurn = Mathf.Abs(Mathf.DeltaAngle(
            currentHeading,
            positiveCloseHauledHeading
        ));
        float negativeTurn = Mathf.Abs(Mathf.DeltaAngle(
            currentHeading,
            negativeCloseHauledHeading
        ));

        return positiveTurn <= negativeTurn
            ? positiveCloseHauledHeading
            : negativeCloseHauledHeading;
    }


    private float SelectHeadingForDirectedArc(
        float firstHeading,
        float secondHeading,
        TurnDirection direction
    )
    {
        float firstArc = CalculateDirectedArc(
            currentHeading,
            firstHeading,
            direction
        );
        float secondArc = CalculateDirectedArc(
            currentHeading,
            secondHeading,
            direction
        );

        return firstArc <= secondArc ? firstHeading : secondHeading;
    }


    private void IssueHeadingCommand(
        float heading,
        TurnSelectionMode selectionMode
    )
    {
        if (maneuverPlanner == null)
        {
            return;
        }

        TurnDirection direction = ResolveTurnDirection(
            currentHeading,
            heading,
            selectionMode
        );

        IssueHeadingCommand(heading, direction);
    }


    private void IssueHeadingCommand(
        float heading,
        TurnDirection direction
    )
    {
        if (maneuverPlanner == null)
        {
            return;
        }

        lastCommandedHeading = heading;
        lastCommandedDirection = direction;
        maneuverPlanner.ExecuteHeadingCommand(heading, direction);
        navigationBlocked =
            maneuverPlanner.CurrentManeuver
            == ShipManeuverPlanner.ManeuverType.Complex;

        if (navigationBlocked)
        {
            navigationMode = NavigationMode.Blocked;
        }
    }


    private static TurnDirection ResolveTurnDirection(
        float fromHeading,
        float toHeading,
        TurnSelectionMode selectionMode
    )
    {
        if (selectionMode == TurnSelectionMode.ForceClockwise)
        {
            return TurnDirection.Clockwise;
        }

        if (selectionMode == TurnSelectionMode.ForceCounterClockwise)
        {
            return TurnDirection.CounterClockwise;
        }

        float clockwiseArc = Mathf.Repeat(toHeading - fromHeading, 360f);
        float counterClockwiseArc = Mathf.Repeat(
            fromHeading - toHeading,
            360f
        );

        return clockwiseArc <= counterClockwiseArc
            ? TurnDirection.Clockwise
            : TurnDirection.CounterClockwise;
    }


    private static float CalculateDirectedArc(
        float fromHeading,
        float toHeading,
        TurnDirection direction
    )
    {
        return direction == TurnDirection.Clockwise
            ? Mathf.Repeat(toHeading - fromHeading, 360f)
            : Mathf.Repeat(fromHeading - toHeading, 360f);
    }


    private static Vector3 HeadingToDirection(float heading)
    {
        return Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
    }


    private static float NormalizeHeading(float heading)
    {
        return Mathf.Repeat(heading, 360f);
    }


    private static float GetHeading(Vector3 direction)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            direction,
            Vector3.up
        );

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Repeat(
            Mathf.Atan2(horizontalDirection.x, horizontalDirection.z)
            * Mathf.Rad2Deg,
            360f
        );
    }


    private void OnDrawGizmos()
    {
        if (!hasDestination)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, destination);
        Gizmos.DrawSphere(destination, 0.75f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(routeOrigin, routeDestination);

        if (navigationMode != NavigationMode.BeatingUpwind)
        {
            return;
        }

        Vector3 positiveBoundaryOffset = routeRight
            * dynamicCorridorHalfWidth;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            routeOrigin + positiveBoundaryOffset,
            routeDestination + positiveBoundaryOffset
        );
        Gizmos.DrawLine(
            routeOrigin - positiveBoundaryOffset,
            routeDestination - positiveBoundaryOffset
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            transform.position,
            transform.position + HeadingToDirection(currentLegHeading) * 10f
        );
    }
}
