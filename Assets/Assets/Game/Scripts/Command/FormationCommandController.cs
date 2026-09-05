using System.Collections.Generic;
using UnityEngine;

public class FormationCommandController : MonoBehaviour
{
    public enum FormationState
    {
        None,
        Captured,
        Moving,
        Completed,
        BlockedUpwind,
        Failed
    }


    [System.Serializable]
    private class FormationMember
    {
        [SerializeField]
        internal ShipDestinationController destinationController;

        [SerializeField]
        internal ShipManeuverPlanner maneuverPlanner;

        [SerializeField]
        internal ShipSailingSpeed sailingSpeed;

        [SerializeField]
        internal float localSlotX;

        [SerializeField]
        internal float localSlotZ;

        [SerializeField]
        internal Vector3 currentSlotWorldPosition;

        [SerializeField]
        internal bool initialAlignmentCommandHandled;

        [SerializeField]
        internal float distanceToSlot;

        [SerializeField]
        internal float desiredMemberHeading;

        [SerializeField]
        internal float headingError;

        [SerializeField]
        internal float rawSlotBearing;

        [SerializeField]
        internal float rawSlotHeadingError;

        [SerializeField]
        internal float limitedSlotHeadingError;

        [SerializeField]
        internal float directLimitedSlotHeadingError;

        [SerializeField]
        internal float directDesiredMemberHeading;

        [SerializeField]
        internal float manualLimitedSlotHeadingError;

        [SerializeField]
        internal float manualDesiredMemberHeading;

        [SerializeField]
        internal bool automaticSpecialManeuverSuppressed;

        [SerializeField]
        internal WindNavigationAssistMode
            automaticSpecialManeuverSuppressionMode;


        public FormationMember(
            ShipDestinationController destinationController,
            ShipManeuverPlanner maneuverPlanner,
            ShipSailingSpeed sailingSpeed,
            float localSlotX,
            float localSlotZ,
            Vector3 currentSlotWorldPosition
        )
        {
            this.destinationController = destinationController;
            this.maneuverPlanner = maneuverPlanner;
            this.sailingSpeed = sailingSpeed;
            this.localSlotX = localSlotX;
            this.localSlotZ = localSlotZ;
            this.currentSlotWorldPosition = currentSlotWorldPosition;
        }
    }


    private const float DirectionThresholdSquared = 0.0001f;
    private const float ManeuverClassificationEpsilon = 0.1f;


    [Header("References")]

    [SerializeField]
    private ShipSelectionManager selectionManager;

    [SerializeField]
    private ShipCommandDispatcher commandDispatcher;

    [SerializeField]
    private GlobalWind globalWind;


    [Header("Formation Movement Settings")]

    [SerializeField]
    [Range(0.01f, 180f)]
    private float directFormationSailingThreshold = 45f;

    [SerializeField]
    [Range(0.01f, 180f)]
    private float formationHeadingTurnRate = 12f;

    [SerializeField]
    [Range(0.01f, 1f)]
    private float formationCruiseFactor = 0.9f;

    [SerializeField]
    [Min(0.01f)]
    private float slotPositionDeadband = 3f;

    [SerializeField]
    [Range(0.01f, 180f)]
    private float slotHeadingCommandThreshold = 4f;

    [SerializeField]
    [Range(0f, 60f)]
    private float directSlotCorrectionAngle = 30f;

    [SerializeField]
    [Range(0f, 30f)]
    private float manualSlotCorrectionAngle = 12f;

    [SerializeField]
    [Min(0.01f)]
    private float formationAlignmentDistance = 5f;

    [SerializeField]
    [Min(0.01f)]
    private float formationArrivalRadius = 8f;


    [Header("Runtime Debug")]

    [SerializeField]
    private FormationState formationState;

    [SerializeField]
    private bool isActive;

    [SerializeField]
    private int memberCount;

    [SerializeField]
    private Vector3 formationAnchorPosition;

    [SerializeField]
    private float formationHeading;

    [SerializeField]
    private float targetFormationHeading;

    [SerializeField]
    private Vector3 formationForward;

    [SerializeField]
    private Vector3 formationRight;

    [SerializeField]
    private Vector3 groupDestination;

    [SerializeField]
    private ShipDestinationController.TurnSelectionMode
        initialGroupSelectionMode;

    [SerializeField]
    private WindNavigationAssistMode activeNavigationAssistMode =
        WindNavigationAssistMode.Assisted;

    [SerializeField]
    private float sharedFormationTargetSpeed;

    [SerializeField]
    private float slowestCurrentMemberSpeed;

    [SerializeField]
    private float anchorMoveSpeed;

    [SerializeField]
    private float distanceToGroupDestination;

    [SerializeField]
    private float targetRelativeWindAngle;

    [SerializeField]
    private int consumedDispatchSequence;

    [SerializeField]
    private int currentDispatcherSequence;

    [SerializeField]
    private List<FormationMember> members = new();

    private readonly HashSet<ShipDestinationController> uniqueShips = new();


    public FormationState State => formationState;

    public bool IsActive => isActive;

    public int MemberCount => memberCount;

    public Vector3 FormationAnchorPosition => formationAnchorPosition;

    public float FormationHeading => formationHeading;

    public float TargetFormationHeading => targetFormationHeading;

    public Vector3 FormationForward => formationForward;

    public Vector3 FormationRight => formationRight;

    public Vector3 GroupDestination => groupDestination;

    public float DistanceAnchorToDestination => distanceToGroupDestination;

    public float DirectFormationSailingThreshold
        => directFormationSailingThreshold;

    public float TargetRelativeWindAngle => targetRelativeWindAngle;

    public float SharedFormationTargetSpeed => sharedFormationTargetSpeed;

    public float SlowestCurrentMemberSpeed => slowestCurrentMemberSpeed;

    public float FormationCruiseFactor => formationCruiseFactor;

    public float DirectSlotCorrectionAngle => directSlotCorrectionAngle;

    public float ManualSlotCorrectionAngle => manualSlotCorrectionAngle;

    public int ConsumedDispatchSequence => consumedDispatchSequence;

    public int CurrentDispatcherSequence => currentDispatcherSequence;

    public ShipDestinationController.TurnSelectionMode
        InitialGroupSelectionMode => initialGroupSelectionMode;

    public WindNavigationAssistMode ActiveNavigationAssistMode
        => activeNavigationAssistMode;


    private void Awake()
    {
        if (selectionManager == null)
        {
            selectionManager = GetComponent<ShipSelectionManager>();
        }

        if (commandDispatcher == null)
        {
            commandDispatcher = GetComponent<ShipCommandDispatcher>();
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


    private void OnEnable()
    {
        if (commandDispatcher != null)
        {
            commandDispatcher.DispatchSequenceChanged
                += HandleDispatchSequenceChanged;
        }
    }


    private void OnDisable()
    {
        if (commandDispatcher != null)
        {
            commandDispatcher.DispatchSequenceChanged
                -= HandleDispatchSequenceChanged;
        }
    }


    private void Update()
    {
        UpdateCurrentDispatcherSequence();
        TryConsumePendingGroupCommand();

        if (!isActive || formationState != FormationState.Moving)
        {
            return;
        }

        if (currentDispatcherSequence > consumedDispatchSequence)
        {
            CancelFormation();
            return;
        }

        UpdateActiveFormation();
    }


    public void CancelFormation()
    {
        isActive = false;
        ClearFormationMaximumTargetSpeeds();

        foreach (FormationMember member in members)
        {
            if (member.maneuverPlanner != null
                && member.maneuverPlanner.IsActive)
            {
                member.maneuverPlanner.CancelCurrentManeuver();
            }
        }

        formationState = FormationState.None;
    }


    private void HandleDispatchSequenceChanged(int dispatchSequence)
    {
        currentDispatcherSequence = dispatchSequence;

        if (isActive && dispatchSequence > consumedDispatchSequence)
        {
            CancelFormation();
        }
    }


    private void UpdateCurrentDispatcherSequence()
    {
        currentDispatcherSequence = commandDispatcher != null
            ? commandDispatcher.DispatchSequence
            : 0;
    }


    public bool CaptureCurrentFormation(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        if (!TryGetValidShips(ships, out List<ShipDestinationController> validShips))
        {
            SetCaptureFailed();
            return false;
        }

        formationAnchorPosition = CalculateAveragePosition(validShips);
        formationForward = CalculateInitialFormationForward(validShips);
        formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;
        formationHeading = GetHeading(formationForward);
        targetFormationHeading = formationHeading;

        members.Clear();

        foreach (ShipDestinationController ship in validShips)
        {
            Vector3 worldOffset = ship.transform.position
                - formationAnchorPosition;
            float localSlotX = Vector3.Dot(worldOffset, formationRight);
            float localSlotZ = Vector3.Dot(worldOffset, formationForward);
            Vector3 currentSlotWorldPosition = GetSlotWorldPosition(
                localSlotX,
                localSlotZ,
                formationAnchorPosition,
                formationHeading
            );

            members.Add(new FormationMember(
                ship,
                ship.GetComponent<ShipManeuverPlanner>(),
                ship.GetComponent<ShipSailingSpeed>(),
                localSlotX,
                localSlotZ,
                currentSlotWorldPosition
            ));
        }

        memberCount = members.Count;
        isActive = true;
        formationState = FormationState.Captured;
        return true;
    }


    public static Vector3 GetSlotWorldPosition(
        float localSlotX,
        float localSlotZ,
        Vector3 anchorPosition,
        float formationHeading
    )
    {
        Vector3 slotForward = HeadingToDirection(formationHeading);
        Vector3 slotRight = Vector3.Cross(
            Vector3.up,
            slotForward
        ).normalized;

        return anchorPosition
            + slotRight * localSlotX
            + slotForward * localSlotZ;
    }


    private void TryConsumePendingGroupCommand()
    {
        if (commandDispatcher == null
            || !commandDispatcher.GroupCommandPending)
        {
            return;
        }

        Vector3 nextGroupDestination =
            commandDispatcher.PendingGroupDestination;
        ShipDestinationController.TurnSelectionMode nextSelectionMode =
            commandDispatcher.PendingGroupSelectionMode;
        WindNavigationAssistMode nextNavigationAssistMode =
            commandDispatcher.PendingGroupNavigationAssistMode;
        int nextDispatchSequence =
            commandDispatcher.PendingGroupDispatchSequence;
        commandDispatcher.ClearPendingGroupCommand();

        if (isActive)
        {
            CancelFormation();
        }

        IReadOnlyList<ShipDestinationController> selectedShips =
            selectionManager != null
                ? selectionManager.SelectedShips
                : null;

        if (!CaptureCurrentFormation(selectedShips))
        {
            return;
        }

        consumedDispatchSequence = nextDispatchSequence;
        groupDestination = nextGroupDestination;
        UpdateDistanceToGroupDestination();
        initialGroupSelectionMode = nextSelectionMode;
        activeNavigationAssistMode = nextNavigationAssistMode;
        targetFormationHeading = GetHeadingTowards(
            formationAnchorPosition,
            groupDestination,
            formationHeading
        );

        if (IsTargetBlockedUpwind())
        {
            ClearFormationMaximumTargetSpeeds();
            isActive = false;
            formationState = FormationState.BlockedUpwind;
            return;
        }

        ClearMemberDestinations();
        isActive = true;
        formationState = FormationState.Moving;
    }


    private void UpdateActiveFormation()
    {
        UpdateFormationHeading(Time.deltaTime);
        UpdateSharedFormationSpeed();
        MoveFormationAnchor(Time.deltaTime);
        UpdateCurrentSlotWorldPositions();
        UpdateMemberGuidance();

        if (distanceToGroupDestination <= formationArrivalRadius)
        {
            CompleteFormation();
        }
    }


    private void UpdateFormationHeading(float deltaTime)
    {
        formationHeading = Mathf.MoveTowardsAngle(
            formationHeading,
            targetFormationHeading,
            formationHeadingTurnRate * deltaTime
        );
        formationForward = HeadingToDirection(formationHeading);
        formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;
    }


    private void UpdateSharedFormationSpeed()
    {
        sharedFormationTargetSpeed = 0f;
        slowestCurrentMemberSpeed = 0f;
        anchorMoveSpeed = 0f;

        bool hasSpeedData = false;
        float minimumPolarTargetSpeed = 0f;
        float minimumCurrentSpeed = 0f;

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member) || member.sailingSpeed == null)
            {
                continue;
            }

            float polarTargetSpeed = Mathf.Max(
                0f,
                member.sailingSpeed.PolarTargetSpeed
            );
            float currentSpeed = Mathf.Max(
                0f,
                member.sailingSpeed.CurrentSpeed
            );

            if (!hasSpeedData)
            {
                minimumPolarTargetSpeed = polarTargetSpeed;
                minimumCurrentSpeed = currentSpeed;
                hasSpeedData = true;
            }
            else
            {
                minimumPolarTargetSpeed = Mathf.Min(
                    minimumPolarTargetSpeed,
                    polarTargetSpeed
                );
                minimumCurrentSpeed = Mathf.Min(
                    minimumCurrentSpeed,
                    currentSpeed
                );
            }
        }

        if (!hasSpeedData)
        {
            return;
        }

        sharedFormationTargetSpeed = Mathf.Max(
            0f,
            minimumPolarTargetSpeed * formationCruiseFactor
        );
        slowestCurrentMemberSpeed = minimumCurrentSpeed;
        anchorMoveSpeed = Mathf.Max(
            0f,
            Mathf.Min(
                sharedFormationTargetSpeed,
                slowestCurrentMemberSpeed
            )
        );

        foreach (FormationMember member in members)
        {
            if (IsMemberValid(member) && member.sailingSpeed != null)
            {
                member.sailingSpeed.SetFormationMaximumTargetSpeed(
                    sharedFormationTargetSpeed
                );
            }
        }
    }


    private void MoveFormationAnchor(float deltaTime)
    {
        Vector3 horizontalDestination = groupDestination;
        horizontalDestination.y = formationAnchorPosition.y;
        formationAnchorPosition = Vector3.MoveTowards(
            formationAnchorPosition,
            horizontalDestination,
            anchorMoveSpeed * deltaTime
        );

        UpdateDistanceToGroupDestination();
    }


    private void UpdateDistanceToGroupDestination()
    {
        Vector3 horizontalDestination = groupDestination;
        horizontalDestination.y = formationAnchorPosition.y;

        Vector3 horizontalOffset = horizontalDestination
            - formationAnchorPosition;
        horizontalOffset.y = 0f;
        distanceToGroupDestination = horizontalOffset.magnitude;
    }


    private void UpdateCurrentSlotWorldPositions()
    {
        foreach (FormationMember member in members)
        {
            member.currentSlotWorldPosition = formationAnchorPosition
                + formationRight * member.localSlotX
                + formationForward * member.localSlotZ;
        }
    }


    private void UpdateMemberGuidance()
    {
        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            ShipDestinationController ship = member.destinationController;
            float desiredMemberHeading = GetDesiredMemberHeading(member);
            float currentShipHeading = GetHeading(ship.transform.forward);
            bool isInitialAlignmentCommand =
                !member.initialAlignmentCommandHandled;
            float headingError = Mathf.Abs(Mathf.DeltaAngle(
                currentShipHeading,
                desiredMemberHeading
            ));
            member.desiredMemberHeading = desiredMemberHeading;
            member.headingError = headingError;

            if (member.maneuverPlanner == null
                || member.maneuverPlanner.IsActive)
            {
                continue;
            }

            if (headingError <= slotHeadingCommandThreshold)
            {
                member.initialAlignmentCommandHandled = true;
                continue;
            }

            TurnDirection turnDirection = ResolveFormationTurnDirection(
                currentShipHeading,
                desiredMemberHeading,
                isInitialAlignmentCommand
            );

            if ((activeNavigationAssistMode == WindNavigationAssistMode.Direct
                    || activeNavigationAssistMode
                        == WindNavigationAssistMode.Manual)
                && !isInitialAlignmentCommand
                && !TryResolveRestrictedAutomaticHeading(
                    member,
                    currentShipHeading,
                    ref desiredMemberHeading,
                    out turnDirection
                ))
            {
                member.desiredMemberHeading = desiredMemberHeading;
                member.headingError = Mathf.Abs(Mathf.DeltaAngle(
                    currentShipHeading,
                    desiredMemberHeading
                ));
                continue;
            }

            headingError = Mathf.Abs(Mathf.DeltaAngle(
                currentShipHeading,
                desiredMemberHeading
            ));
            member.desiredMemberHeading = desiredMemberHeading;
            member.headingError = headingError;

            if (headingError <= slotHeadingCommandThreshold)
            {
                member.initialAlignmentCommandHandled = true;
                continue;
            }

            member.maneuverPlanner.ExecuteHeadingCommand(
                desiredMemberHeading,
                turnDirection
            );
            member.initialAlignmentCommandHandled = true;
        }
    }


    private float GetDesiredMemberHeading(FormationMember member)
    {
        Vector3 slotOffset = member.currentSlotWorldPosition
            - member.destinationController.transform.position;
        slotOffset.y = 0f;
        member.distanceToSlot = slotOffset.magnitude;

        float rawSlotBearing = GetHeadingTowards(
            member.destinationController.transform.position,
            member.currentSlotWorldPosition,
            formationHeading
        );

        if (activeNavigationAssistMode
            == WindNavigationAssistMode.Assisted)
        {
            member.rawSlotBearing = 0f;
            member.rawSlotHeadingError = 0f;
            member.limitedSlotHeadingError = 0f;
            member.directLimitedSlotHeadingError = 0f;
            member.directDesiredMemberHeading = 0f;
            member.manualLimitedSlotHeadingError = 0f;
            member.manualDesiredMemberHeading = 0f;
            member.automaticSpecialManeuverSuppressed = false;
            member.automaticSpecialManeuverSuppressionMode =
                WindNavigationAssistMode.Assisted;

            return member.distanceToSlot > slotPositionDeadband
                ? rawSlotBearing
                : formationHeading;
        }

        member.rawSlotBearing = rawSlotBearing;
        member.rawSlotHeadingError = Mathf.DeltaAngle(
            formationHeading,
            rawSlotBearing
        );
        member.automaticSpecialManeuverSuppressed = false;
        member.automaticSpecialManeuverSuppressionMode =
            activeNavigationAssistMode;

        if (activeNavigationAssistMode == WindNavigationAssistMode.Direct)
        {
            member.limitedSlotHeadingError = 0f;
            member.directLimitedSlotHeadingError = member.distanceToSlot
                > slotPositionDeadband
                ? Mathf.Clamp(
                    member.rawSlotHeadingError,
                    -directSlotCorrectionAngle,
                    directSlotCorrectionAngle
                )
                : 0f;
            member.directDesiredMemberHeading = Mathf.Repeat(
                formationHeading + member.directLimitedSlotHeadingError,
                360f
            );
            member.manualLimitedSlotHeadingError = 0f;
            member.manualDesiredMemberHeading = 0f;

            return member.directDesiredMemberHeading;
        }

        member.limitedSlotHeadingError = member.distanceToSlot
            > slotPositionDeadband
            ? Mathf.Clamp(
                member.rawSlotHeadingError,
                -manualSlotCorrectionAngle,
                manualSlotCorrectionAngle
            )
            : 0f;
        member.directLimitedSlotHeadingError = 0f;
        member.directDesiredMemberHeading = 0f;
        member.manualLimitedSlotHeadingError =
            member.limitedSlotHeadingError;
        member.manualDesiredMemberHeading = Mathf.Repeat(
            formationHeading + member.manualLimitedSlotHeadingError,
            360f
        );

        return member.manualDesiredMemberHeading;
    }


    private bool TryResolveRestrictedAutomaticHeading(
        FormationMember member,
        float currentShipHeading,
        ref float desiredMemberHeading,
        out TurnDirection turnDirection
    )
    {
        turnDirection = ResolveAutoTurnDirection(
            currentShipHeading,
            desiredMemberHeading
        );

        if (!WouldAutomaticCorrectionRequireSpecialManeuver(
            currentShipHeading,
            desiredMemberHeading,
            turnDirection
        ))
        {
            return true;
        }

        member.automaticSpecialManeuverSuppressed = true;
        desiredMemberHeading = formationHeading;
        SetRestrictedDesiredMemberHeading(member, desiredMemberHeading);

        float slotCorrectionAngle = activeNavigationAssistMode
            == WindNavigationAssistMode.Direct
            ? directSlotCorrectionAngle
            : manualSlotCorrectionAngle;

        float formationHeadingError = Mathf.Abs(Mathf.DeltaAngle(
            currentShipHeading,
            formationHeading
        ));

        if (formationHeadingError > slotCorrectionAngle)
        {
            return false;
        }

        turnDirection = ResolveAutoTurnDirection(
            currentShipHeading,
            desiredMemberHeading
        );

        return !WouldAutomaticCorrectionRequireSpecialManeuver(
            currentShipHeading,
            desiredMemberHeading,
            turnDirection
        );
    }


    private void SetRestrictedDesiredMemberHeading(
        FormationMember member,
        float desiredMemberHeading
    )
    {
        if (activeNavigationAssistMode == WindNavigationAssistMode.Direct)
        {
            member.directDesiredMemberHeading = desiredMemberHeading;
            return;
        }

        member.manualDesiredMemberHeading = desiredMemberHeading;
    }


    private bool WouldAutomaticCorrectionRequireSpecialManeuver(
        float currentHeading,
        float targetHeading,
        TurnDirection turnDirection
    )
    {
        if (globalWind == null)
        {
            return false;
        }

        float commandedArc = CalculateDirectedArc(
            currentHeading,
            targetHeading,
            turnDirection
        );

        if (commandedArc <= ManeuverClassificationEpsilon)
        {
            return false;
        }

        float windFromHeading = GetHeading(globalWind.WindFromDirection);
        float downwindHeading = Mathf.Repeat(windFromHeading + 180f, 360f);

        return IsHeadingInsideDirectedArc(
                   CalculateDirectedArc(
                       currentHeading,
                       windFromHeading,
                       turnDirection
                   ),
                   commandedArc
               )
               || IsHeadingInsideDirectedArc(
                   CalculateDirectedArc(
                       currentHeading,
                       downwindHeading,
                       turnDirection
                   ),
                   commandedArc
               );
    }


    private static float CalculateDirectedArc(
        float fromHeading,
        float toHeading,
        TurnDirection turnDirection
    )
    {
        return turnDirection == TurnDirection.Clockwise
            ? Mathf.Repeat(toHeading - fromHeading, 360f)
            : Mathf.Repeat(fromHeading - toHeading, 360f);
    }


    private static bool IsHeadingInsideDirectedArc(
        float headingDistance,
        float commandedArc
    )
    {
        return headingDistance > ManeuverClassificationEpsilon
            && headingDistance
                < commandedArc - ManeuverClassificationEpsilon;
    }


    private void CompleteFormation()
    {
        isActive = false;
        formationState = FormationState.Completed;
        ClearFormationMaximumTargetSpeeds();

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member) || member.maneuverPlanner == null)
            {
                continue;
            }

            float currentShipHeading = GetHeading(
                member.destinationController.transform.forward
            );
            float headingError = Mathf.Abs(Mathf.DeltaAngle(
                currentShipHeading,
                targetFormationHeading
            ));

            if (headingError > slotHeadingCommandThreshold)
            {
                TurnDirection turnDirection = ResolveAutoTurnDirection(
                    currentShipHeading,
                    targetFormationHeading
                );

                if ((activeNavigationAssistMode
                        == WindNavigationAssistMode.Direct
                        || activeNavigationAssistMode
                            == WindNavigationAssistMode.Manual)
                    && WouldAutomaticCorrectionRequireSpecialManeuver(
                        currentShipHeading,
                        targetFormationHeading,
                        turnDirection
                    ))
                {
                    member.automaticSpecialManeuverSuppressed = true;
                    member.desiredMemberHeading = formationHeading;
                    SetRestrictedDesiredMemberHeading(
                        member,
                        formationHeading
                    );
                    continue;
                }

                member.maneuverPlanner.ExecuteHeadingCommand(
                    targetFormationHeading,
                    turnDirection
                );
            }
        }
    }


    private void ClearMemberDestinations()
    {
        foreach (FormationMember member in members)
        {
            if (IsMemberValid(member))
            {
                member.destinationController.ClearDestination();
            }
        }
    }


    private void ClearFormationMaximumTargetSpeeds()
    {
        foreach (FormationMember member in members)
        {
            if (member.sailingSpeed != null)
            {
                member.sailingSpeed.ClearFormationMaximumTargetSpeed();
            }
        }
    }


    private bool IsTargetBlockedUpwind()
    {
        if (globalWind == null)
        {
            targetRelativeWindAngle = 0f;
            return false;
        }

        float windFromHeading = GetHeading(globalWind.WindFromDirection);
        targetRelativeWindAngle = Mathf.Abs(Mathf.DeltaAngle(
            targetFormationHeading,
            windFromHeading
        ));

        return activeNavigationAssistMode == WindNavigationAssistMode.Assisted
            && targetRelativeWindAngle < directFormationSailingThreshold;
    }


    private TurnDirection ResolveFormationTurnDirection(
        float currentHeading,
        float targetHeading,
        bool isInitialAlignmentCommand
    )
    {
        if (isInitialAlignmentCommand)
        {
            if (initialGroupSelectionMode
                == ShipDestinationController.TurnSelectionMode.ForceClockwise)
            {
                return TurnDirection.Clockwise;
            }

            if (initialGroupSelectionMode
                == ShipDestinationController.TurnSelectionMode
                    .ForceCounterClockwise)
            {
                return TurnDirection.CounterClockwise;
            }
        }

        return ResolveAutoTurnDirection(currentHeading, targetHeading);
    }


    private static TurnDirection ResolveAutoTurnDirection(
        float currentHeading,
        float targetHeading
    )
    {
        float clockwiseArc = Mathf.Repeat(
            targetHeading - currentHeading,
            360f
        );
        float counterClockwiseArc = Mathf.Repeat(
            currentHeading - targetHeading,
            360f
        );

        return clockwiseArc <= counterClockwiseArc
            ? TurnDirection.Clockwise
            : TurnDirection.CounterClockwise;
    }


    private static bool IsMemberValid(FormationMember member)
    {
        return member != null && member.destinationController != null;
    }


    private bool TryGetValidShips(
        IReadOnlyList<ShipDestinationController> ships,
        out List<ShipDestinationController> validShips
    )
    {
        validShips = new List<ShipDestinationController>();

        if (ships == null)
        {
            return false;
        }

        uniqueShips.Clear();

        foreach (ShipDestinationController ship in ships)
        {
            if (ship == null || !uniqueShips.Add(ship))
            {
                continue;
            }

            validShips.Add(ship);
        }

        return validShips.Count >= 2;
    }


    private Vector3 CalculateInitialFormationForward(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        Vector3 forwardSum = Vector3.zero;

        foreach (ShipDestinationController ship in ships)
        {
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                ship.transform.forward,
                Vector3.up
            );

            if (horizontalForward.sqrMagnitude > DirectionThresholdSquared)
            {
                forwardSum += horizontalForward.normalized;
            }
        }

        if (forwardSum.sqrMagnitude > DirectionThresholdSquared)
        {
            return forwardSum.normalized;
        }

        ShipDestinationController primarySelectedShip = selectionManager != null
            ? selectionManager.PrimarySelectedShip
            : null;
        Vector3 fallbackForward = GetHorizontalForward(primarySelectedShip);

        if (fallbackForward.sqrMagnitude <= DirectionThresholdSquared)
        {
            fallbackForward = GetHorizontalForward(ships[0]);
        }

        return fallbackForward.sqrMagnitude > DirectionThresholdSquared
            ? fallbackForward.normalized
            : Vector3.forward;
    }


    private static Vector3 CalculateAveragePosition(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        Vector3 totalPosition = Vector3.zero;

        foreach (ShipDestinationController ship in ships)
        {
            totalPosition += ship.transform.position;
        }

        return totalPosition / ships.Count;
    }


    private void SetCaptureFailed()
    {
        ClearFormationMaximumTargetSpeeds();
        members.Clear();
        memberCount = 0;
        isActive = false;
        formationState = FormationState.Failed;
    }


    private static Vector3 GetHorizontalForward(
        ShipDestinationController ship
    )
    {
        if (ship == null)
        {
            return Vector3.zero;
        }

        return Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up);
    }


    private static float GetHeading(Vector3 forward)
    {
        return Mathf.Repeat(
            Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
            360f
        );
    }


    private static float GetHeadingTowards(
        Vector3 fromPosition,
        Vector3 toPosition,
        float fallbackHeading
    )
    {
        Vector3 horizontalOffset = toPosition - fromPosition;
        horizontalOffset.y = 0f;

        return horizontalOffset.sqrMagnitude > DirectionThresholdSquared
            ? GetHeading(horizontalOffset)
            : fallbackHeading;
    }


    private static Vector3 HeadingToDirection(float heading)
    {
        return Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
    }


    private void OnDrawGizmos()
    {
        if (members == null || members.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(formationAnchorPosition, 0.75f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(formationAnchorPosition, groupDestination);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            formationAnchorPosition,
            formationAnchorPosition + formationForward * 8f
        );

        foreach (FormationMember member in members)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(member.currentSlotWorldPosition, 0.4f);

            if (!IsMemberValid(member))
            {
                continue;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawLine(
                member.destinationController.transform.position,
                member.currentSlotWorldPosition
            );
        }
    }
}
