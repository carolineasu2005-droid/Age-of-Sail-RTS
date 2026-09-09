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

    public bool HasDestination => hasDestination;

    public NavigationMode CurrentNavigationMode => navigationMode;

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
            || !WindBeatingNavigationMath.ShouldEnterBeating(
                desiredBearingRelativeToWind,
                directSailingThreshold
            ))
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
        desiredBearing = WindBeatingNavigationMath.GetHorizontalBearing(
            transform.position,
            destination,
            currentHeading
        );
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
        desiredBearingRelativeToWind =
            WindBeatingNavigationMath.GetAbsoluteBearingRelativeToWind(
                desiredBearing,
                windFromHeading
            );
        currentDestinationBearingRelativeToWind =
            desiredBearingRelativeToWind;
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            WindBeatingNavigationMath.GetCloseHauledCandidates(
                windFromHeading,
                closeHauledHeadingAngle
            );
        positiveCloseHauledHeading = candidates.PositiveHeading;
        negativeCloseHauledHeading = candidates.NegativeHeading;
    }


    private void InitializeRouteReference()
    {
        routeOrigin = transform.position;
        routeDestination = destination;

        WindBeatingNavigationMath.RouteReference routeReference =
            WindBeatingNavigationMath.CalculateRouteReference(
                routeOrigin,
                routeDestination
            );
        routeDirection = routeReference.Direction;
        routeRight = routeReference.Right;
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

        if (WindBeatingNavigationMath.ShouldResumeDirect(
            currentDestinationBearingRelativeToWind,
            directResumeThreshold
        ))
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

        if (WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            crossTrackDistance,
            currentLegCrossTrackSign,
            dynamicCorridorHalfWidth,
            corridorSwitchFactor
        ))
        {
            SwitchToOppositeUpwindLeg();
        }
    }


    private void UpdateCorridorData()
    {
        dynamicCorridorHalfWidth =
            WindBeatingNavigationMath.CalculateDynamicCorridorHalfWidth(
                distanceToDestination,
                corridorDistanceRatio,
                minimumTackCorridorHalfWidth,
                maximumTackCorridorHalfWidth
            );
        crossTrackDistance =
            WindBeatingNavigationMath.CalculateCrossTrackDistance(
                transform.position,
                routeOrigin,
                routeRight
            );
    }


    private void SwitchToOppositeUpwindLeg()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            new WindBeatingNavigationMath.CloseHauledCandidates(
                positiveCloseHauledHeading,
                negativeCloseHauledHeading
            );
        float nextLegHeading =
            WindBeatingNavigationMath.GetOppositeCloseHauledHeading(
                currentLegHeading,
                candidates
            );
        TurnDirection direction = ResolveTurnDirection(
            currentHeading,
            nextLegHeading,
            TurnSelectionMode.Auto
        );

        currentLegHeading = nextLegHeading;
        currentLegCrossTrackSign =
            WindBeatingNavigationMath.CalculateLegCrossTrackSign(
                currentLegHeading,
                routeRight
            );
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
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            new WindBeatingNavigationMath.CloseHauledCandidates(
                positiveCloseHauledHeading,
                negativeCloseHauledHeading
            );

        switch (selectionMode)
        {
            case TurnSelectionMode.ForceClockwise:
                selectedHeading =
                    WindBeatingNavigationMath
                        .SelectCloseHauledHeadingForDirectedArc(
                            currentHeading,
                            candidates,
                            TurnDirection.Clockwise
                        );
                selectedDirection = TurnDirection.Clockwise;
                break;
            case TurnSelectionMode.ForceCounterClockwise:
                selectedHeading =
                    WindBeatingNavigationMath
                        .SelectCloseHauledHeadingForDirectedArc(
                            currentHeading,
                            candidates,
                            TurnDirection.CounterClockwise
                        );
                selectedDirection = TurnDirection.CounterClockwise;
                break;
            default:
                selectedHeading =
                    WindBeatingNavigationMath
                        .SelectInitialAutoCloseHauledHeading(
                            desiredBearing,
                            currentHeading,
                            candidates
                        );
                selectedDirection = ResolveTurnDirection(
                    currentHeading,
                    selectedHeading,
                    TurnSelectionMode.Auto
                );
                break;
        }

        currentLegHeading = selectedHeading;
        currentLegCrossTrackSign =
            WindBeatingNavigationMath.CalculateLegCrossTrackSign(
                currentLegHeading,
                routeRight
            );
        initialUpwindSelectionMode = selectionMode;
        IssueHeadingCommand(selectedHeading, selectedDirection);
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
            transform.position
                + WindBeatingNavigationMath.GetHeadingDirection(
                    currentLegHeading
                ) * 10f
        );
    }
}
