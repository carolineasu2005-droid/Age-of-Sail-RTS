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

    public enum FormationManeuverState
    {
        None,
        Executing,
        Reforming,
        Failed
    }

    public enum FormationManeuverType
    {
        None,
        Normal,
        Tack,
        Wear,
        Complex
    }

    public enum FormationNavigationMode
    {
        Direct,
        BeatingUpwind
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
        internal float forwardError;

        [SerializeField]
        internal float desiredFormationSpeed;

        [SerializeField]
        internal float availableTargetSpeed;

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
    private float directFormationResumeThreshold = 50f;

    [SerializeField]
    [Range(0f, 180f)]
    private float formationCloseHauledHeadingAngle = 50f;

    [SerializeField]
    [Min(0f)]
    private float maximumFormationTackCorridorHalfWidth = 15f;

    [SerializeField]
    [Min(0f)]
    private float minimumFormationTackCorridorHalfWidth = 6f;

    [SerializeField]
    [Range(0f, 1f)]
    private float formationCorridorDistanceRatio = 0.08f;

    [SerializeField]
    [Range(0.5f, 1f)]
    private float formationCorridorSwitchFactor = 0.85f;

    [SerializeField]
    [Range(0.01f, 180f)]
    private float formationHeadingTurnRate = 12f;

    [SerializeField]
    [Range(0.01f, 1f)]
    private float formationCruiseFactor = 0.9f;

    [SerializeField]
    [Min(0f)]
    private float longitudinalSpeedDeadband = 4f;

    [SerializeField]
    [Min(0.01f)]
    private float laggingFullCatchUpDistance = 40f;

    [SerializeField]
    [Min(0.01f)]
    private float aheadFullSlowDistance = 40f;

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
    [Min(0f)]
    private float successionLateralTolerance = 10f;

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
    private Vector3 formationBoundsCenter;

    [SerializeField]
    private float boundsMinX;

    [SerializeField]
    private float boundsMaxX;

    [SerializeField]
    private float boundsMinZ;

    [SerializeField]
    private float boundsMaxZ;

    [SerializeField]
    private float formationHeading;

    [SerializeField]
    private float targetFormationHeading;

    [SerializeField]
    private float navigationHeading;

    [SerializeField]
    private float finalFormationHeading;

    [SerializeField]
    private bool hasExplicitFinalFormationHeading;

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
    private FormationNavigationMode formationNavigationMode;

    [SerializeField]
    private float currentBeatingLegHeading;

    [SerializeField]
    private float oppositeBeatingLegHeading;

    [SerializeField]
    private float currentBeatingLegCrossTrackSign;

    [SerializeField]
    private float formationCorridorHalfWidth;

    [SerializeField]
    private float formationCorridorCrossTrack;

    [SerializeField]
    private int formationTackSwitchCount;

    [SerializeField]
    private bool beatingLegSwitchRequested;

    [SerializeField]
    private bool automaticBeatingTackRequested;

    [SerializeField]
    private bool directResumeAvailable;

    [SerializeField]
    private bool finalAlignmentActive;

    [SerializeField]
    private bool finalAlignmentCompleted;

    [SerializeField]
    private Vector3 beatingRouteOrigin;

    [SerializeField]
    private Vector3 beatingRouteDestination;

    [SerializeField]
    private Vector3 beatingRouteDirection;

    [SerializeField]
    private Vector3 beatingRouteRight;

    [SerializeField]
    private FormationManeuverState formationManeuverState;

    [SerializeField]
    private FormationManeuverType formationManeuverType;

    [SerializeField]
    private float maneuverTargetHeading;

    [SerializeField]
    private TurnDirection maneuverTurnDirection;

    [SerializeField]
    private FormationManeuverStyle requestedManeuverStyle;

    [SerializeField]
    private FormationManeuverStyle effectiveManeuverStyle;

    [SerializeField]
    private bool successionCompatibleAtManeuverTrigger;

    [SerializeField]
    private bool hasManeuverGate;

    [SerializeField]
    private bool successionManeuverActive;

    [SerializeField]
    private List<FormationSuccessionMemberState> successionMemberStates = new();

    [SerializeField]
    private int maneuverValidMemberCount;

    [SerializeField]
    private int maneuverCompletedMemberCount;

    [SerializeField]
    private bool formationManeuverLocked;

    [SerializeField]
    private int reformingMemberCount;

    [SerializeField]
    private int consumedDispatchSequence;

    [SerializeField]
    private int currentDispatcherSequence;

    [SerializeField]
    private List<FormationMember> members = new();

    private readonly HashSet<ShipDestinationController> uniqueShips = new();

    private FormationMemberOrder formationMemberOrder;

    private FormationManeuverGate lastManeuverGate;


    public FormationState State => formationState;

    public bool IsActive => isActive;

    public int MemberCount => memberCount;

    public Vector3 FormationAnchorPosition => formationAnchorPosition;

    public Vector3 FormationBoundsCenter => formationBoundsCenter;

    public float BoundsMinX => boundsMinX;

    public float BoundsMaxX => boundsMaxX;

    public float BoundsMinZ => boundsMinZ;

    public float BoundsMaxZ => boundsMaxZ;

    public float FormationHeading => formationHeading;

    public float TargetFormationHeading => targetFormationHeading;

    public float NavigationHeading => navigationHeading;

    public float FinalFormationHeading => finalFormationHeading;

    public bool HasExplicitFinalFormationHeading
        => hasExplicitFinalFormationHeading;

    public Vector3 FormationForward => formationForward;

    public Vector3 FormationRight => formationRight;

    public Vector3 GroupDestination => groupDestination;

    public float DistanceAnchorToDestination => distanceToGroupDestination;

    public float DirectFormationSailingThreshold
        => directFormationSailingThreshold;

    public float TargetRelativeWindAngle => targetRelativeWindAngle;

    public FormationNavigationMode NavigationMode => formationNavigationMode;

    public float CurrentBeatingLegHeading => currentBeatingLegHeading;

    public float OppositeBeatingLegHeading => oppositeBeatingLegHeading;

    public float FormationCorridorHalfWidth => formationCorridorHalfWidth;

    public float FormationCorridorCrossTrack => formationCorridorCrossTrack;

    public int FormationTackSwitchCount => formationTackSwitchCount;

    public bool BeatingLegSwitchRequested => beatingLegSwitchRequested;

    public bool AutomaticBeatingTackRequested
        => automaticBeatingTackRequested;

    public bool DirectResumeAvailable => directResumeAvailable;

    public bool FinalAlignmentActive => finalAlignmentActive;

    public FormationManeuverState ManeuverState => formationManeuverState;

    public FormationManeuverType ManeuverType => formationManeuverType;

    public float ManeuverTargetHeading => maneuverTargetHeading;

    public TurnDirection ManeuverTurnDirection => maneuverTurnDirection;

    public int ManeuverValidMemberCount => maneuverValidMemberCount;

    public int ManeuverCompletedMemberCount => maneuverCompletedMemberCount;

    public bool FormationManeuverLocked => formationManeuverLocked;

    public int ReformingMemberCount => reformingMemberCount;

    public float SharedFormationTargetSpeed => sharedFormationTargetSpeed;

    public float SlowestCurrentMemberSpeed => slowestCurrentMemberSpeed;

    public float FormationCruiseFactor => formationCruiseFactor;

    public float LongitudinalSpeedDeadband => longitudinalSpeedDeadband;

    public float LaggingFullCatchUpDistance => laggingFullCatchUpDistance;

    public float AheadFullSlowDistance => aheadFullSlowDistance;

    public float SlotPositionDeadband => slotPositionDeadband;

    public float DirectSlotCorrectionAngle => directSlotCorrectionAngle;

    public float ManualSlotCorrectionAngle => manualSlotCorrectionAngle;

    public float SuccessionLateralTolerance => successionLateralTolerance;

    public FormationManeuverStyle RequestedManeuverStyle
        => requestedManeuverStyle;

    public FormationManeuverStyle EffectiveManeuverStyle
        => effectiveManeuverStyle;

    public bool SuccessionCompatibleAtManeuverTrigger
        => successionCompatibleAtManeuverTrigger;

    public bool HasManeuverGate => hasManeuverGate;

    public FormationManeuverGate LastManeuverGate => lastManeuverGate;

    public bool SuccessionManeuverActive => successionManeuverActive;

    public bool NormalSuccessionActive => successionManeuverActive
        && lastManeuverGate.ManeuverType
            == ShipManeuverPlanner.ManeuverType.NormalTurn;

    public IReadOnlyList<FormationSuccessionMemberState>
        SuccessionMemberStates => successionMemberStates;

    public FormationMemberOrder ActiveFormationMemberOrder
        => formationMemberOrder;

    public int ConsumedDispatchSequence => consumedDispatchSequence;

    public int CurrentDispatcherSequence => currentDispatcherSequence;


    public bool ContainsActiveMember(ShipDestinationController ship)
    {
        if (!isActive || ship == null)
        {
            return false;
        }

        foreach (FormationMember member in members)
        {
            if (IsMemberValid(member)
                && member.destinationController == ship)
            {
                return true;
            }
        }

        return false;
    }

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

        ClearFormationManeuverState();
        ClearFormationNavigationState();
        targetFormationHeading = 0f;
        finalFormationHeading = 0f;
        hasExplicitFinalFormationHeading = false;
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

        ShipDestinationController primarySelectedShip = selectionManager != null
            ? selectionManager.PrimarySelectedShip
            : null;

        if (!FormationGeometrySnapshot.TryCapture(
            validShips,
            primarySelectedShip,
            out FormationGeometrySnapshot geometrySnapshot
        ))
        {
            SetCaptureFailed();
            return false;
        }

        return CaptureFormationGeometry(geometrySnapshot);
    }


    private bool CaptureFormationGeometry(
        FormationGeometrySnapshot geometrySnapshot
    )
    {
        if (geometrySnapshot == null || geometrySnapshot.Members.Count < 2)
        {
            SetCaptureFailed();
            return false;
        }

        formationAnchorPosition = geometrySnapshot.FormationCenter;
        formationBoundsCenter = geometrySnapshot.FormationCenter;
        boundsMinX = geometrySnapshot.BoundsMinX;
        boundsMaxX = geometrySnapshot.BoundsMaxX;
        boundsMinZ = geometrySnapshot.BoundsMinZ;
        boundsMaxZ = geometrySnapshot.BoundsMaxZ;
        formationForward = geometrySnapshot.FormationForward;
        formationRight = geometrySnapshot.FormationRight;
        formationHeading = geometrySnapshot.FormationHeading;
        targetFormationHeading = formationHeading;
        formationMemberOrder = FormationMemberOrder.CreateWithDesignatedLead(
            geometrySnapshot,
            selectionManager != null
                ? selectionManager.DesignatedFormationLead
                : null
        );

        members.Clear();

        foreach (FormationGeometryMember geometryMember
                 in geometrySnapshot.Members)
        {
            ShipDestinationController ship = geometryMember.Ship;

            if (ship == null)
            {
                SetCaptureFailed();
                return false;
            }

            float localSlotX = geometryMember.LocalX;
            float localSlotZ = geometryMember.LocalZ;
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


    public bool TryCaptureSelectedFormationGeometry(
        out FormationGeometrySnapshot geometrySnapshot
    )
    {
        IReadOnlyList<ShipDestinationController> selectedShips =
            selectionManager != null
                ? selectionManager.SelectedShips
                : null;
        ShipDestinationController primarySelectedShip = selectionManager != null
            ? selectionManager.PrimarySelectedShip
            : null;

        return FormationGeometrySnapshot.TryCapture(
            selectedShips,
            primarySelectedShip,
            out geometrySnapshot
        );
    }


    public static Vector3 GetSlotWorldPosition(
        float localSlotX,
        float localSlotZ,
        Vector3 anchorPosition,
        float formationHeading
    )
    {
        return FormationGeometrySnapshot.GetSlotWorldPosition(
            anchorPosition,
            formationHeading,
            localSlotX,
            localSlotZ
        );
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
        FormationManeuverStyle nextRequestedManeuverStyle =
            commandDispatcher.PendingGroupRequestedManeuverStyle;
        bool hasExplicitFormationHeading =
            commandDispatcher.PendingGroupHasExplicitFormationHeading;
        float explicitFormationHeading =
            commandDispatcher.PendingGroupExplicitFormationHeading;
        FormationGeometrySnapshot pendingGeometrySnapshot =
            commandDispatcher.PendingGroupGeometrySnapshot;
        int nextDispatchSequence =
            commandDispatcher.PendingGroupDispatchSequence;
        commandDispatcher.ClearPendingGroupCommand();

        if (isActive)
        {
            CancelFormation();
        }

        bool captureSucceeded;

        if (hasExplicitFormationHeading)
        {
            captureSucceeded = CaptureFormationGeometry(
                pendingGeometrySnapshot
            );
        }
        else
        {
            IReadOnlyList<ShipDestinationController> selectedShips =
                selectionManager != null
                    ? selectionManager.SelectedShips
                    : null;
            captureSucceeded = CaptureCurrentFormation(selectedShips);
        }

        if (!captureSucceeded)
        {
            return;
        }

        consumedDispatchSequence = nextDispatchSequence;
        groupDestination = nextGroupDestination;
        UpdateDistanceToGroupDestination();
        initialGroupSelectionMode = nextSelectionMode;
        activeNavigationAssistMode = nextNavigationAssistMode;
        requestedManeuverStyle = nextRequestedManeuverStyle;
        hasExplicitFinalFormationHeading = hasExplicitFormationHeading;
        finalFormationHeading = hasExplicitFormationHeading
            ? Mathf.Repeat(explicitFormationHeading, 360f)
            : GetHeadingTowards(
                formationAnchorPosition,
                groupDestination,
                formationHeading
            );
        targetFormationHeading = finalFormationHeading;

        ConfigureFormationNavigation();

        if (formationNavigationMode == FormationNavigationMode.BeatingUpwind)
        {
            ClearFormationManeuverState();
        }
        else if (hasExplicitFinalFormationHeading)
        {
            ClearFormationManeuverState();
        }
        else
        {
            ConfigureFormationManeuver();
        }

        ClearMemberDestinations();
        isActive = true;
        formationState = FormationState.Moving;

        if (formationManeuverType == FormationManeuverType.Complex)
        {
            formationManeuverState = FormationManeuverState.Failed;
            isActive = false;
            ClearFormationMaximumTargetSpeeds();
            formationState = FormationState.Failed;
            return;
        }

        if (formationManeuverType == FormationManeuverType.Tack
            || formationManeuverType == FormationManeuverType.Wear)
        {
            StartCoordinatedManeuver();
        }
    }


    private void UpdateActiveFormation()
    {
        if (formationManeuverState == FormationManeuverState.None)
        {
            UpdateFormationNavigation();
        }

        if (!isActive)
        {
            return;
        }

        if (formationManeuverState == FormationManeuverState.Executing)
        {
            if (successionManeuverActive)
            {
                UpdateFormationHeadingForSuccession();
            }
            else
            {
                UpdateFormationHeadingFromMembers();
            }
        }
        else
        {
            UpdateFormationHeading(Time.deltaTime);
        }

        UpdateSharedFormationSpeed();
        MoveFormationAnchor(Time.deltaTime);
        UpdateCurrentSlotWorldPositions();

        UpdateLongitudinalStationKeeping();

        if (formationManeuverState == FormationManeuverState.Executing)
        {
            UpdateCoordinatedManeuverExecution();
            return;
        }

        UpdateMemberGuidance();

        if (formationManeuverState == FormationManeuverState.Reforming)
        {
            UpdateReformingProgress();

            if (formationManeuverState == FormationManeuverState.Reforming)
            {
                return;
            }
        }

        if (distanceToGroupDestination <= formationArrivalRadius)
        {
            if (hasExplicitFinalFormationHeading && !finalAlignmentActive)
            {
                if (!finalAlignmentCompleted)
                {
                    BeginFinalFormationAlignment();
                }
                else
                {
                    CompleteFormation();
                }
            }
            else if (!finalAlignmentActive)
            {
                CompleteFormation();
            }
        }
    }


    private void UpdateFormationHeading(float deltaTime)
    {
        formationHeading = Mathf.MoveTowardsAngle(
            formationHeading,
            navigationHeading,
            formationHeadingTurnRate * deltaTime
        );
        formationForward = HeadingToDirection(formationHeading);
        formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;
    }


    private void ConfigureFormationManeuver()
    {
        ConfigureFormationManeuverForHeading(targetFormationHeading);
    }


    private void CaptureManeuverGate(
        ShipManeuverPlanner.ManeuverType plannerManeuver
    )
    {
        FormationMemberOrder memberOrder = formationMemberOrder;
        if (memberOrder == null || memberOrder.Count != members.Count)
        {
            memberOrder = CreateCurrentMemberOrder();
            formationMemberOrder = memberOrder;
        }

        ShipDestinationController lead = memberOrder != null
            ? memberOrder.Lead
            : null;
        Vector3 gatePoint = lead != null
            ? lead.transform.position
            : formationAnchorPosition;
        successionCompatibleAtManeuverTrigger =
            formationManeuverState != FormationManeuverState.Reforming
            && !formationManeuverLocked
            && FormationSuccessionCompatibility.IsCompatible(
                memberOrder,
                formationForward,
                successionLateralTolerance
            );
        effectiveManeuverStyle = IsSuccessionSupportedManeuver(plannerManeuver)
            ? FormationSuccessionCompatibility.ResolveEffectiveStyle(
                requestedManeuverStyle,
                successionCompatibleAtManeuverTrigger
            )
            : FormationManeuverStyle.Together;
        lastManeuverGate = new FormationManeuverGate(
            gatePoint,
            formationHeading,
            formationForward,
            maneuverTargetHeading,
            maneuverTurnDirection,
            plannerManeuver,
            memberOrder,
            requestedManeuverStyle,
            sharedFormationTargetSpeed,
            consumedDispatchSequence
        );
        hasManeuverGate = true;
    }


    private FormationMemberOrder CreateCurrentMemberOrder()
    {
        List<ShipDestinationController> orderedMembers =
            new List<ShipDestinationController>(members.Count);

        foreach (FormationMember member in members)
        {
            if (IsMemberValid(member))
            {
                orderedMembers.Add(member.destinationController);
            }
        }

        return orderedMembers.Count > 0
            ? new FormationMemberOrder(orderedMembers)
            : null;
    }


    private void StartCoordinatedManeuver()
    {
        ShipManeuverPlanner.ManeuverType plannerManeuver =
            ToPlannerManeuverType(formationManeuverType);

        if (plannerManeuver != ShipManeuverPlanner.ManeuverType.NormalTurn
            && plannerManeuver != ShipManeuverPlanner.ManeuverType.Tack
            && plannerManeuver != ShipManeuverPlanner.ManeuverType.Wear)
        {
            return;
        }

        if (IsSuccessionSupportedManeuver(plannerManeuver)
            && requestedManeuverStyle == FormationManeuverStyle.InSuccession)
        {
            UpdateSharedFormationSpeed();
        }

        CaptureManeuverGate(plannerManeuver);

        if (ShouldExecuteSuccessionManeuver(plannerManeuver))
        {
            StartSuccessionManeuver();
            return;
        }

        formationManeuverState = FormationManeuverState.Executing;
        formationManeuverLocked = true;
        ClearFormationMaximumTargetSpeeds();
        maneuverValidMemberCount = 0;
        maneuverCompletedMemberCount = 0;
        reformingMemberCount = 0;

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            maneuverValidMemberCount++;
            if (member.maneuverPlanner == null)
            {
                FailFormationManeuver();
                return;
            }
        }

        if (maneuverValidMemberCount == 0)
        {
            FailFormationManeuver();
            return;
        }

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            member.maneuverPlanner.ExecuteCoordinatedManeuverCommand(
                maneuverTargetHeading,
                maneuverTurnDirection,
                plannerManeuver
            );
            member.initialAlignmentCommandHandled = true;
        }

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            float memberHeading = GetHeading(
                member.destinationController.transform.forward
            );
            bool headingAlreadyAligned = Mathf.Abs(Mathf.DeltaAngle(
                memberHeading,
                maneuverTargetHeading
            )) <= slotHeadingCommandThreshold;

            if (automaticBeatingTackRequested
                && !member.maneuverPlanner.IsActive
                && !headingAlreadyAligned)
            {
                FailFormationManeuver();
                return;
            }
        }
    }


    private void UpdateCoordinatedManeuverExecution()
    {
        if (successionManeuverActive)
        {
            UpdateSuccessionManeuverExecution();
            return;
        }

        maneuverValidMemberCount = 0;
        maneuverCompletedMemberCount = 0;

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            if (member.maneuverPlanner == null)
            {
                FailFormationManeuver();
                return;
            }

            maneuverValidMemberCount++;
            if (HasMemberManeuverFailed(member))
            {
                FailFormationManeuver();
                return;
            }

            float memberHeading = GetHeading(
                member.destinationController.transform.forward
            );
            bool maneuverComplete = IsMemberManeuverComplete(member);
            bool headingAligned = Mathf.Abs(Mathf.DeltaAngle(
                memberHeading,
                maneuverTargetHeading
            )) <= slotHeadingCommandThreshold;

            if (maneuverComplete && headingAligned)
            {
                maneuverCompletedMemberCount++;
            }
        }

        if (maneuverValidMemberCount == 0)
        {
            FailFormationManeuver();
            return;
        }

        if (maneuverCompletedMemberCount < maneuverValidMemberCount)
        {
            return;
        }

        RebaseFormationAnchorFromMembers();
        formationManeuverState = FormationManeuverState.Reforming;
        formationManeuverLocked = false;
        reformingMemberCount = maneuverValidMemberCount;
    }


    private static bool IsSuccessionSupportedManeuver(
        ShipManeuverPlanner.ManeuverType plannerManeuver
    )
    {
        return plannerManeuver == ShipManeuverPlanner.ManeuverType.NormalTurn
            || plannerManeuver == ShipManeuverPlanner.ManeuverType.Tack
            || plannerManeuver == ShipManeuverPlanner.ManeuverType.Wear;
    }


    private bool ShouldExecuteSuccessionManeuver(
        ShipManeuverPlanner.ManeuverType plannerManeuver
    )
    {
        return IsSuccessionSupportedManeuver(plannerManeuver)
            && effectiveManeuverStyle == FormationManeuverStyle.InSuccession;
    }


    private void StartSuccessionManeuver()
    {
        FormationMemberOrder memberOrder = lastManeuverGate.MemberOrder;
        if (!CanStartSuccessionManeuver(memberOrder))
        {
            FailFormationManeuver();
            return;
        }

        formationManeuverState = FormationManeuverState.Executing;
        formationManeuverLocked = true;
        successionManeuverActive = true;
        maneuverValidMemberCount = 0;
        maneuverCompletedMemberCount = 0;
        reformingMemberCount = 0;
        successionMemberStates.Clear();

        for (int index = 0; index < memberOrder.Count; index++)
        {
            ShipDestinationController ship = memberOrder.GetMember(index);
            TryGetFormationMember(ship, out FormationMember member);

            member.maneuverPlanner.CancelCurrentManeuver();
            member.initialAlignmentCommandHandled = true;
            successionMemberStates.Add(FormationSuccessionMemberState.Waiting);
            maneuverValidMemberCount++;
        }

        if (maneuverValidMemberCount == 0)
        {
            FailFormationManeuver();
            return;
        }

        StartEligibleSuccessionMembers();
        ApplySuccessionReferenceSpeedCaps();
    }


    private bool CanStartSuccessionManeuver(FormationMemberOrder memberOrder)
    {
        if (memberOrder == null || memberOrder.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < memberOrder.Count; index++)
        {
            ShipDestinationController ship = memberOrder.GetMember(index);
            if (!TryGetFormationMember(ship, out FormationMember member)
                || !IsSuccessionMemberUsable(member)
                || member.maneuverPlanner == null
                || !member.maneuverPlanner.CanExecuteCoordinatedManeuver(
                    lastManeuverGate.ManeuverType
                ))
            {
                return false;
            }
        }

        return true;
    }


    private void UpdateSuccessionManeuverExecution()
    {
        maneuverValidMemberCount = 0;
        maneuverCompletedMemberCount = 0;

        FormationMemberOrder memberOrder = lastManeuverGate.MemberOrder;
        if (memberOrder == null
            || successionMemberStates.Count != memberOrder.Count)
        {
            FailFormationManeuver();
            return;
        }

        for (int index = 0; index < memberOrder.Count; index++)
        {
            ShipDestinationController ship = memberOrder.GetMember(index);
            if (!TryGetFormationMember(ship, out FormationMember member)
                || !IsSuccessionMemberUsable(member)
                || member.maneuverPlanner == null)
            {
                successionMemberStates[index] =
                    FormationSuccessionMemberState.Completed;
                continue;
            }

            maneuverValidMemberCount++;

            switch (successionMemberStates[index])
            {
                case FormationSuccessionMemberState.Waiting:
                    MaintainWaitingSuccessionMember(member);
                    break;
                case FormationSuccessionMemberState.Maneuvering:
                    if (HasMemberManeuverFailed(member))
                    {
                        FailFormationManeuver();
                        return;
                    }

                    if (IsMemberManeuverComplete(member))
                    {
                        successionMemberStates[index] =
                            FormationSuccessionMemberState.Completed;
                    }
                    break;
                case FormationSuccessionMemberState.Completed:
                    MaintainCompletedSuccessionMember(member);
                    maneuverCompletedMemberCount++;
                    break;
            }
        }

        StartEligibleSuccessionMembers();
        ApplySuccessionReferenceSpeedCaps();
        maneuverCompletedMemberCount = CountCompletedSuccessionMembers();

        if (!AreAllSuccessionMembersCompleted())
        {
            return;
        }

        RebaseFormationAnchorFromMembers();
        successionManeuverActive = false;
        formationManeuverState = FormationManeuverState.Reforming;
        formationManeuverLocked = false;
        reformingMemberCount = maneuverValidMemberCount;
    }


    private void StartEligibleSuccessionMembers()
    {
        while (true)
        {
            int nextIndex = FormationSuccessionMath.GetNextEligibleMemberIndex(
                successionMemberStates
            );
            if (nextIndex < 0)
            {
                return;
            }

            if (nextIndex > 0)
            {
                FormationMemberOrder memberOrder = lastManeuverGate.MemberOrder;
                ShipDestinationController ship = memberOrder.GetMember(nextIndex);
                if (!FormationSuccessionMath.HasReachedGate(
                    ship.transform.position,
                    lastManeuverGate
                ))
                {
                    return;
                }
            }

            StartSuccessionMember(nextIndex);
        }
    }


    private void StartSuccessionMember(int memberIndex)
    {
        ShipDestinationController ship = lastManeuverGate.MemberOrder.GetMember(
            memberIndex
        );
        if (!TryGetFormationMember(ship, out FormationMember member)
            || !IsSuccessionMemberUsable(member)
            || member.maneuverPlanner == null)
        {
            successionMemberStates[memberIndex] =
                FormationSuccessionMemberState.Completed;
            return;
        }

        member.maneuverPlanner.ExecuteCoordinatedManeuverCommand(
            lastManeuverGate.TargetHeading,
            lastManeuverGate.TurnDirection,
            lastManeuverGate.ManeuverType
        );
        member.initialAlignmentCommandHandled = true;
        successionMemberStates[memberIndex] =
            IsMemberManeuverComplete(member)
                ? FormationSuccessionMemberState.Completed
                : FormationSuccessionMemberState.Maneuvering;
    }


    private void MaintainWaitingSuccessionMember(FormationMember member)
    {
        MaintainSuccessionHeading(
            member,
            lastManeuverGate.IncomingFormationHeading
        );
    }


    private void MaintainCompletedSuccessionMember(FormationMember member)
    {
        MaintainSuccessionHeading(member, lastManeuverGate.TargetHeading);
    }


    private void MaintainSuccessionHeading(
        FormationMember member,
        float targetHeading
    )
    {
        if (member.maneuverPlanner == null || member.maneuverPlanner.IsActive)
        {
            return;
        }

        float currentHeading = GetHeading(
            member.destinationController.transform.forward
        );
        if (Mathf.Abs(Mathf.DeltaAngle(currentHeading, targetHeading))
            <= slotHeadingCommandThreshold)
        {
            return;
        }

        member.maneuverPlanner.ExecuteCoordinatedManeuverCommand(
            targetHeading,
            ResolveAutoTurnDirection(currentHeading, targetHeading),
            ShipManeuverPlanner.ManeuverType.NormalTurn
        );
    }


    private bool IsMemberManeuverComplete(FormationMember member)
    {
        switch (lastManeuverGate.ManeuverType)
        {
            case ShipManeuverPlanner.ManeuverType.Tack:
            {
                ShipTacking tacking = member.destinationController.GetComponent<
                    ShipTacking
                >();
                return tacking != null && tacking.IsCompleted;
            }
            case ShipManeuverPlanner.ManeuverType.Wear:
            {
                ShipWearing wearing = member.destinationController.GetComponent<
                    ShipWearing
                >();
                return wearing != null && wearing.IsCompleted;
            }
        }

        if (member.maneuverPlanner == null || member.maneuverPlanner.IsActive)
        {
            return false;
        }

        float memberHeading = GetHeading(
            member.destinationController.transform.forward
        );
        return Mathf.Abs(Mathf.DeltaAngle(
            memberHeading,
            lastManeuverGate.TargetHeading
        )) <= slotHeadingCommandThreshold;
    }


    private bool HasMemberManeuverFailed(FormationMember member)
    {
        switch (lastManeuverGate.ManeuverType)
        {
            case ShipManeuverPlanner.ManeuverType.Tack:
            {
                ShipTacking tacking = member.destinationController.GetComponent<
                    ShipTacking
                >();
                return tacking == null || tacking.HasFailed;
            }
            case ShipManeuverPlanner.ManeuverType.Wear:
            {
                ShipWearing wearing = member.destinationController.GetComponent<
                    ShipWearing
                >();
                return wearing == null || wearing.HasFailed;
            }
            default:
                return false;
        }
    }


    private bool AreAllSuccessionMembersCompleted()
    {
        foreach (FormationSuccessionMemberState state in successionMemberStates)
        {
            if (state != FormationSuccessionMemberState.Completed)
            {
                return false;
            }
        }

        return true;
    }


    private int CountCompletedSuccessionMembers()
    {
        int completedCount = 0;

        foreach (FormationSuccessionMemberState state in successionMemberStates)
        {
            if (state == FormationSuccessionMemberState.Completed)
            {
                completedCount++;
            }
        }

        return completedCount;
    }


    private void ApplySuccessionReferenceSpeedCaps()
    {
        foreach (FormationMember member in members)
        {
            if (!IsSuccessionMemberUsable(member)
                || member.sailingSpeed == null)
            {
                continue;
            }

            int memberIndex = lastManeuverGate.MemberOrder != null
                ? lastManeuverGate.MemberOrder.IndexOf(
                    member.destinationController
                )
                : -1;
            bool activeSpecialManeuver = memberIndex >= 0
                && memberIndex < successionMemberStates.Count
                && successionMemberStates[memberIndex]
                    == FormationSuccessionMemberState.Maneuvering
                && (lastManeuverGate.ManeuverType
                    == ShipManeuverPlanner.ManeuverType.Tack
                    || lastManeuverGate.ManeuverType
                        == ShipManeuverPlanner.ManeuverType.Wear);
            if (activeSpecialManeuver)
            {
                member.sailingSpeed.ClearFormationMaximumTargetSpeed();
                continue;
            }

            float cap = Mathf.Min(
                lastManeuverGate.FormationReferenceSpeed,
                Mathf.Max(0f, member.sailingSpeed.AvailableTargetSpeed)
            );
            member.sailingSpeed.SetFormationMaximumTargetSpeed(cap);
        }
    }


    private bool TryGetFormationMember(
        ShipDestinationController ship,
        out FormationMember formationMember
    )
    {
        foreach (FormationMember member in members)
        {
            if (member.destinationController == ship)
            {
                formationMember = member;
                return true;
            }
        }

        formationMember = null;
        return false;
    }


    private static bool IsSuccessionMemberUsable(
        FormationMember member
    )
    {
        return IsMemberValid(member)
            && member.destinationController.isActiveAndEnabled;
    }


    private void RebaseFormationAnchorFromMembers()
    {
        Vector3 impliedAnchorSum = Vector3.zero;
        int validMemberCount = 0;

        foreach (FormationMember member in members)
        {
            if (!IsSuccessionMemberUsable(member))
            {
                continue;
            }

            Vector3 impliedAnchor = member.destinationController.transform
                .position - formationRight * member.localSlotX
                - formationForward * member.localSlotZ;
            impliedAnchorSum += impliedAnchor;
            validMemberCount++;
        }

        if (validMemberCount == 0)
        {
            return;
        }

        formationAnchorPosition = impliedAnchorSum / validMemberCount;
        formationBoundsCenter = formationAnchorPosition;
    }


    private void UpdateFormationHeadingForSuccession()
    {
        formationHeading = lastManeuverGate.TargetHeading;
        formationForward = HeadingToDirection(lastManeuverGate.TargetHeading);
        formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;
    }


    private void UpdateFormationHeadingFromMembers()
    {
        Vector3 forwardSum = Vector3.zero;

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                member.destinationController.transform.forward,
                Vector3.up
            );

            if (horizontalForward.sqrMagnitude > DirectionThresholdSquared)
            {
                forwardSum += horizontalForward.normalized;
            }
        }

        if (forwardSum.sqrMagnitude <= DirectionThresholdSquared)
        {
            return;
        }

        formationForward = forwardSum.normalized;
        formationHeading = GetHeading(formationForward);
        formationRight = Vector3.Cross(
            Vector3.up,
            formationForward
        ).normalized;
    }


    private void UpdateReformingProgress()
    {
        reformingMemberCount = 0;

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member))
            {
                continue;
            }

            float desiredMemberHeading = GetDesiredMemberHeading(member);
            float memberHeading = GetHeading(
                member.destinationController.transform.forward
            );
            bool positionReformed = member.distanceToSlot
                <= formationAlignmentDistance;
            bool headingReformed = Mathf.Abs(Mathf.DeltaAngle(
                memberHeading,
                desiredMemberHeading
            )) <= slotHeadingCommandThreshold;

            if (!positionReformed || !headingReformed)
            {
                reformingMemberCount++;
            }
        }

        if (reformingMemberCount > 0)
        {
            return;
        }

        formationManeuverState = FormationManeuverState.None;
        formationManeuverLocked = false;

        if (automaticBeatingTackRequested)
        {
            CommitAutomaticBeatingLegSwitch();
        }

        if (finalAlignmentActive)
        {
            finalAlignmentActive = false;
            finalAlignmentCompleted = true;
        }
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
                member.sailingSpeed.GetWindLimitedTargetSpeed()
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
        anchorMoveSpeed = CalculateFormationAnchorMoveSpeed(
            formationManeuverState,
            sharedFormationTargetSpeed,
            slowestCurrentMemberSpeed
        );

    }


    private void MoveFormationAnchor(float deltaTime)
    {
        if (formationNavigationMode == FormationNavigationMode.BeatingUpwind)
        {
            float anchorHeading = formationManeuverState
                == FormationManeuverState.Executing
                ? formationHeading
                : navigationHeading;
            formationAnchorPosition += HeadingToDirection(anchorHeading)
                * anchorMoveSpeed * deltaTime;
            UpdateDistanceToGroupDestination();
            return;
        }

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


    private void UpdateLongitudinalStationKeeping()
    {
        if (successionManeuverActive)
        {
            ApplySuccessionReferenceSpeedCaps();
            return;
        }

        if (!ShouldApplyLongitudinalStationKeeping(formationManeuverState))
        {
            ClearFormationMaximumTargetSpeeds();
            return;
        }

        foreach (FormationMember member in members)
        {
            if (!IsMemberValid(member) || member.sailingSpeed == null)
            {
                continue;
            }

            member.forwardError = CalculateFormationForwardError(
                member.currentSlotWorldPosition,
                member.destinationController.transform.position,
                formationForward
            );
            member.availableTargetSpeed = Mathf.Max(
                0f,
                member.sailingSpeed.AvailableTargetSpeed
            );
            member.desiredFormationSpeed = CalculateDesiredFormationSpeed(
                member.forwardError,
                sharedFormationTargetSpeed,
                member.availableTargetSpeed,
                longitudinalSpeedDeadband,
                laggingFullCatchUpDistance,
                aheadFullSlowDistance
            );
            member.sailingSpeed.SetFormationMaximumTargetSpeed(
                member.desiredFormationSpeed
            );
        }
    }


    public static bool ShouldApplyLongitudinalStationKeeping(
        FormationManeuverState maneuverState
    )
    {
        return maneuverState != FormationManeuverState.Executing;
    }


    public static float CalculateFormationAnchorMoveSpeed(
        FormationManeuverState maneuverState,
        float formationReferenceSpeed,
        float slowestMemberSpeed
    )
    {
        float referenceSpeed = Mathf.Max(0f, formationReferenceSpeed);
        if (maneuverState == FormationManeuverState.Reforming)
        {
            return referenceSpeed;
        }

        return Mathf.Min(referenceSpeed, Mathf.Max(0f, slowestMemberSpeed));
    }


    public static float CalculateFormationForwardError(
        Vector3 slotWorldPosition,
        Vector3 shipWorldPosition,
        Vector3 formationForward
    )
    {
        return Vector3.Dot(
            slotWorldPosition - shipWorldPosition,
            formationForward
        );
    }


    public static float CalculateReformingDesiredHeading(
        float formationHeading,
        float forwardError,
        float lateralError,
        float lateralDeadband,
        float maximumCorrectionAngle
    )
    {
        float normalizedFormationHeading = Mathf.Repeat(
            formationHeading,
            360f
        );
        if (Mathf.Abs(lateralError) <= Mathf.Max(0f, lateralDeadband))
        {
            return normalizedFormationHeading;
        }

        float forwardRecoveryDistance = Mathf.Max(0f, forwardError);
        float lateralCorrectionAngle = Mathf.Atan2(
            lateralError,
            forwardRecoveryDistance
        ) * Mathf.Rad2Deg;
        float correctionLimit = Mathf.Max(0f, maximumCorrectionAngle);

        return Mathf.Repeat(
            normalizedFormationHeading + Mathf.Clamp(
                lateralCorrectionAngle,
                -correctionLimit,
                correctionLimit
            ),
            360f
        );
    }


    public static float CalculateDesiredFormationSpeed(
        float forwardError,
        float formationReferenceSpeed,
        float availableTargetSpeed,
        float deadband,
        float laggingFullCatchUpDistance,
        float aheadFullSlowDistance
    )
    {
        float availableSpeed = Mathf.Max(0f, availableTargetSpeed);
        float referenceSpeed = Mathf.Clamp(
            formationReferenceSpeed,
            0f,
            availableSpeed
        );
        float absoluteDeadband = Mathf.Max(0f, deadband);

        if (Mathf.Abs(forwardError) <= absoluteDeadband)
        {
            return referenceSpeed;
        }

        if (forwardError > absoluteDeadband)
        {
            float catchUpProgress = CalculateDistanceProgress(
                forwardError,
                absoluteDeadband,
                laggingFullCatchUpDistance
            );
            return Mathf.Clamp(
                Mathf.Lerp(referenceSpeed, availableSpeed, catchUpProgress),
                0f,
                availableSpeed
            );
        }

        float slowProgress = CalculateDistanceProgress(
            -forwardError,
            absoluteDeadband,
            aheadFullSlowDistance
        );
        return Mathf.Clamp(
            Mathf.Lerp(referenceSpeed, 0f, slowProgress),
            0f,
            availableSpeed
        );
    }


    private static float CalculateDistanceProgress(
        float absoluteForwardError,
        float deadband,
        float fullResponseDistance
    )
    {
        float fullDistance = Mathf.Max(deadband, fullResponseDistance);
        if (fullDistance <= deadband)
        {
            return 1f;
        }

        return Mathf.InverseLerp(
            deadband,
            fullDistance,
            absoluteForwardError
        );
    }


    private void UpdateMemberGuidance()
    {
        if (formationManeuverLocked)
        {
            return;
        }

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

            if (member.maneuverPlanner == null)
            {
                continue;
            }

            if (formationManeuverState == FormationManeuverState.Reforming
                && activeNavigationAssistMode
                    == WindNavigationAssistMode.Assisted
                && member.maneuverPlanner.IsActive
                && member.maneuverPlanner.CurrentManeuver
                    == ShipManeuverPlanner.ManeuverType.NormalTurn)
            {
                float targetHeadingChange = Mathf.Abs(Mathf.DeltaAngle(
                    member.maneuverPlanner.TargetHeading,
                    desiredMemberHeading
                ));
                if (targetHeadingChange > slotHeadingCommandThreshold)
                {
                    TurnDirection retargetDirection =
                        ResolveFormationTurnDirection(
                            currentShipHeading,
                            desiredMemberHeading,
                            false
                        );
                    member.maneuverPlanner.RetargetActiveNormalTurn(
                        desiredMemberHeading,
                        retargetDirection
                    );
                }

                continue;
            }

            if (member.maneuverPlanner.IsActive)
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

            if (formationManeuverState == FormationManeuverState.Reforming)
            {
                float forwardError = Vector3.Dot(
                    slotOffset,
                    formationForward
                );
                float lateralError = Vector3.Dot(
                    slotOffset,
                    formationRight
                );
                return CalculateReformingDesiredHeading(
                    formationHeading,
                    forwardError,
                    lateralError,
                    slotPositionDeadband,
                    directSlotCorrectionAngle
                );
            }

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
        if (formationManeuverLocked
            || formationManeuverState == FormationManeuverState.Reforming)
        {
            return;
        }

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


    private void ConfigureFormationNavigation()
    {
        ClearFormationNavigationState();

        float destinationBearing =
            WindBeatingNavigationMath.GetHorizontalBearing(
                formationAnchorPosition,
                groupDestination,
                formationHeading
            );
        navigationHeading = destinationBearing;

        if (activeNavigationAssistMode != WindNavigationAssistMode.Assisted
            || globalWind == null)
        {
            targetRelativeWindAngle = 0f;
            return;
        }

        float windFromHeading = GetHeading(globalWind.WindFromDirection);
        targetRelativeWindAngle =
            WindBeatingNavigationMath.GetAbsoluteBearingRelativeToWind(
                destinationBearing,
                windFromHeading
            );

        if (!WindBeatingNavigationMath.ShouldEnterBeating(
            targetRelativeWindAngle,
            directFormationSailingThreshold
        ))
        {
            return;
        }

        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            WindBeatingNavigationMath.GetCloseHauledCandidates(
                windFromHeading,
                formationCloseHauledHeadingAngle
            );
        currentBeatingLegHeading = SelectInitialFormationBeatingLeg(
            destinationBearing,
            candidates
        );
        oppositeBeatingLegHeading =
            WindBeatingNavigationMath.GetOppositeCloseHauledHeading(
                currentBeatingLegHeading,
                candidates
            );
        WindBeatingNavigationMath.RouteReference routeReference =
            WindBeatingNavigationMath.CalculateRouteReference(
                formationAnchorPosition,
                groupDestination
            );
        beatingRouteOrigin = formationAnchorPosition;
        beatingRouteDestination = groupDestination;
        beatingRouteDirection = routeReference.Direction;
        beatingRouteRight = routeReference.Right;
        currentBeatingLegCrossTrackSign =
            WindBeatingNavigationMath.CalculateLegCrossTrackSign(
                currentBeatingLegHeading,
                beatingRouteRight
            );
        navigationHeading = currentBeatingLegHeading;
        formationNavigationMode = FormationNavigationMode.BeatingUpwind;
    }


    private void UpdateFormationNavigation()
    {
        if (formationNavigationMode != FormationNavigationMode.BeatingUpwind)
        {
            navigationHeading = GetDirectFormationNavigationHeading();
            directResumeAvailable = false;
            return;
        }

        if (activeNavigationAssistMode != WindNavigationAssistMode.Assisted
            || globalWind == null)
        {
            ExitBeatingNavigationToDirect(false);
            return;
        }

        float destinationBearing =
            WindBeatingNavigationMath.GetHorizontalBearing(
                formationAnchorPosition,
                groupDestination,
                formationHeading
            );
        float windFromHeading = GetHeading(globalWind.WindFromDirection);
        targetRelativeWindAngle =
            WindBeatingNavigationMath.GetAbsoluteBearingRelativeToWind(
                destinationBearing,
                windFromHeading
            );
        UpdateFormationCorridorData();

        directResumeAvailable = WindBeatingNavigationMath.ShouldResumeDirect(
            targetRelativeWindAngle,
            directFormationResumeThreshold
        );

        if (directResumeAvailable)
        {
            ExitBeatingNavigationToDirect(true);
            return;
        }

        navigationHeading = currentBeatingLegHeading;

        if (!beatingLegSwitchRequested
            && WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
                formationCorridorCrossTrack,
                currentBeatingLegCrossTrackSign,
                formationCorridorHalfWidth,
                formationCorridorSwitchFactor
            ))
        {
            beatingLegSwitchRequested = true;
            BeginAutomaticBeatingTack();
        }
    }


    private void BeginAutomaticBeatingTack()
    {
        if (activeNavigationAssistMode != WindNavigationAssistMode.Assisted
            || formationNavigationMode
                != FormationNavigationMode.BeatingUpwind
            || formationManeuverState != FormationManeuverState.None
            || !beatingLegSwitchRequested)
        {
            return;
        }

        if (!TryGetAutomaticBeatingTackDirection(
            out TurnDirection tackDirection
        ))
        {
            FailFormationManeuver();
            return;
        }

        automaticBeatingTackRequested = true;
        formationManeuverType = FormationManeuverType.Tack;
        maneuverTargetHeading = oppositeBeatingLegHeading;
        maneuverTurnDirection = tackDirection;
        navigationHeading = maneuverTargetHeading;
        StartCoordinatedManeuver();
    }


    private bool TryGetAutomaticBeatingTackDirection(
        out TurnDirection tackDirection
    )
    {
        tackDirection = TurnDirection.Clockwise;

        if (globalWind == null)
        {
            return false;
        }

        ShipManeuverPlanner.ManeuverType clockwiseManeuver =
            ShipManeuverPlanner.ClassifyDirectedArc(
                formationHeading,
                oppositeBeatingLegHeading,
                TurnDirection.Clockwise,
                globalWind.WindFromDirection
            );
        if (clockwiseManeuver == ShipManeuverPlanner.ManeuverType.Tack)
        {
            return true;
        }

        ShipManeuverPlanner.ManeuverType counterClockwiseManeuver =
            ShipManeuverPlanner.ClassifyDirectedArc(
                formationHeading,
                oppositeBeatingLegHeading,
                TurnDirection.CounterClockwise,
                globalWind.WindFromDirection
            );
        if (counterClockwiseManeuver
            != ShipManeuverPlanner.ManeuverType.Tack)
        {
            return false;
        }

        tackDirection = TurnDirection.CounterClockwise;
        return true;
    }


    private void CommitAutomaticBeatingLegSwitch()
    {
        float completedLegHeading = currentBeatingLegHeading;
        currentBeatingLegHeading = oppositeBeatingLegHeading;
        oppositeBeatingLegHeading = completedLegHeading;
        currentBeatingLegCrossTrackSign =
            WindBeatingNavigationMath.CalculateLegCrossTrackSign(
                currentBeatingLegHeading,
                beatingRouteRight
            );
        navigationHeading = currentBeatingLegHeading;
        beatingLegSwitchRequested = false;
        automaticBeatingTackRequested = false;
        formationTackSwitchCount++;
    }


    private void ExitBeatingNavigationToDirect(bool shouldCoordinateManeuver)
    {
        ClearBeatingRouteState();
        formationNavigationMode = FormationNavigationMode.Direct;
        navigationHeading = GetDirectFormationNavigationHeading();

        if (!shouldCoordinateManeuver)
        {
            return;
        }

        ConfigureFormationManeuverForHeading(navigationHeading);

        if (formationManeuverType == FormationManeuverType.Complex)
        {
            FailFormationManeuver();
            return;
        }

        if (formationManeuverType == FormationManeuverType.Normal
            || formationManeuverType == FormationManeuverType.Tack
            || formationManeuverType == FormationManeuverType.Wear)
        {
            StartCoordinatedManeuver();
        }
    }


    private void BeginFinalFormationAlignment()
    {
        finalAlignmentActive = true;
        navigationHeading = finalFormationHeading;
        ConfigureFormationManeuverForHeading(finalFormationHeading);

        if (formationManeuverType == FormationManeuverType.None)
        {
            finalAlignmentActive = false;
            finalAlignmentCompleted = true;
            CompleteFormation();
            return;
        }

        if (formationManeuverType == FormationManeuverType.Complex)
        {
            FailFormationManeuver();
            return;
        }

        StartCoordinatedManeuver();
    }


    private void ConfigureFormationManeuverForHeading(float targetHeading)
    {
        formationManeuverState = FormationManeuverState.None;
        formationManeuverLocked = false;
        maneuverTargetHeading = Mathf.Repeat(targetHeading, 360f);
        maneuverTurnDirection = ResolveRequestedFormationTurnDirection(
            formationHeading,
            maneuverTargetHeading
        );
        maneuverValidMemberCount = 0;
        maneuverCompletedMemberCount = 0;
        reformingMemberCount = 0;

        ShipManeuverPlanner.ManeuverType classifiedManeuver =
            ShipManeuverPlanner.ClassifyDirectedArc(
                formationHeading,
                maneuverTargetHeading,
                maneuverTurnDirection,
                globalWind != null
                    ? globalWind.WindFromDirection
                    : Vector3.zero
            );
        formationManeuverType = ToFormationManeuverType(
            classifiedManeuver
        );

        if (formationManeuverType == FormationManeuverType.None
            && Mathf.Abs(Mathf.DeltaAngle(
                formationHeading,
                maneuverTargetHeading
            )) > slotHeadingCommandThreshold)
        {
            formationManeuverType = FormationManeuverType.Normal;
        }
    }


    private void UpdateFormationCorridorData()
    {
        formationCorridorHalfWidth =
            WindBeatingNavigationMath.CalculateDynamicCorridorHalfWidth(
                distanceToGroupDestination,
                formationCorridorDistanceRatio,
                minimumFormationTackCorridorHalfWidth,
                maximumFormationTackCorridorHalfWidth
            );
        formationCorridorCrossTrack =
            WindBeatingNavigationMath.CalculateCrossTrackDistance(
                formationAnchorPosition,
                beatingRouteOrigin,
                beatingRouteRight
            );
    }


    private float GetDirectFormationNavigationHeading()
    {
        return WindBeatingNavigationMath.GetHorizontalBearing(
            formationAnchorPosition,
            groupDestination,
            formationHeading
        );
    }


    private float SelectInitialFormationBeatingLeg(
        float destinationBearing,
        WindBeatingNavigationMath.CloseHauledCandidates candidates
    )
    {
        if (initialGroupSelectionMode
            == ShipDestinationController.TurnSelectionMode.ForceClockwise)
        {
            return WindBeatingNavigationMath
                .SelectCloseHauledHeadingForDirectedArc(
                    formationHeading,
                    candidates,
                    TurnDirection.Clockwise
                );
        }

        if (initialGroupSelectionMode
            == ShipDestinationController.TurnSelectionMode
                .ForceCounterClockwise)
        {
            return WindBeatingNavigationMath
                .SelectCloseHauledHeadingForDirectedArc(
                    formationHeading,
                    candidates,
                    TurnDirection.CounterClockwise
                );
        }

        return WindBeatingNavigationMath
            .SelectInitialAutoCloseHauledHeading(
                destinationBearing,
                formationHeading,
                candidates
            );
    }


    private void ClearFormationNavigationState()
    {
        formationNavigationMode = FormationNavigationMode.Direct;
        navigationHeading = 0f;
        targetRelativeWindAngle = 0f;
        directResumeAvailable = false;
        finalAlignmentActive = false;
        finalAlignmentCompleted = false;
        formationTackSwitchCount = 0;
        ClearBeatingRouteState();
    }


    private void ClearBeatingRouteState()
    {
        currentBeatingLegHeading = 0f;
        oppositeBeatingLegHeading = 0f;
        currentBeatingLegCrossTrackSign = 0f;
        formationCorridorHalfWidth = 0f;
        formationCorridorCrossTrack = 0f;
        beatingLegSwitchRequested = false;
        automaticBeatingTackRequested = false;
        beatingRouteOrigin = Vector3.zero;
        beatingRouteDestination = Vector3.zero;
        beatingRouteDirection = Vector3.zero;
        beatingRouteRight = Vector3.zero;
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


    private TurnDirection ResolveRequestedFormationTurnDirection(
        float currentHeading,
        float requestedTargetHeading
    )
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

        return ResolveAutoTurnDirection(
            currentHeading,
            requestedTargetHeading
        );
    }


    private static FormationManeuverType ToFormationManeuverType(
        ShipManeuverPlanner.ManeuverType maneuverType
    )
    {
        return maneuverType switch
        {
            ShipManeuverPlanner.ManeuverType.NormalTurn
                => FormationManeuverType.Normal,
            ShipManeuverPlanner.ManeuverType.Tack
                => FormationManeuverType.Tack,
            ShipManeuverPlanner.ManeuverType.Wear
                => FormationManeuverType.Wear,
            ShipManeuverPlanner.ManeuverType.Complex
                => FormationManeuverType.Complex,
            _ => FormationManeuverType.None
        };
    }


    private static ShipManeuverPlanner.ManeuverType ToPlannerManeuverType(
        FormationManeuverType maneuverType
    )
    {
        return maneuverType switch
        {
            FormationManeuverType.Normal
                => ShipManeuverPlanner.ManeuverType.NormalTurn,
            FormationManeuverType.Tack
                => ShipManeuverPlanner.ManeuverType.Tack,
            FormationManeuverType.Wear
                => ShipManeuverPlanner.ManeuverType.Wear,
            FormationManeuverType.Complex
                => ShipManeuverPlanner.ManeuverType.Complex,
            _ => ShipManeuverPlanner.ManeuverType.None
        };
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


    private void SetCaptureFailed()
    {
        ClearFormationMaximumTargetSpeeds();
        ClearFormationManeuverState();
        ClearFormationNavigationState();
        members.Clear();
        memberCount = 0;
        isActive = false;
        formationState = FormationState.Failed;
    }


    private void ClearFormationManeuverState()
    {
        formationManeuverState = FormationManeuverState.None;
        formationManeuverType = FormationManeuverType.None;
        maneuverTargetHeading = 0f;
        maneuverTurnDirection = TurnDirection.Clockwise;
        maneuverValidMemberCount = 0;
        maneuverCompletedMemberCount = 0;
        formationManeuverLocked = false;
        reformingMemberCount = 0;
        successionManeuverActive = false;
        successionMemberStates.Clear();
    }


    private void FailFormationManeuver()
    {
        foreach (FormationMember member in members)
        {
            if (member != null && member.maneuverPlanner != null
                && member.maneuverPlanner.IsActive)
            {
                member.maneuverPlanner.CancelCurrentManeuver();
            }
        }

        formationManeuverState = FormationManeuverState.Failed;
        formationManeuverLocked = false;
        successionManeuverActive = false;
        successionMemberStates.Clear();
        isActive = false;
        ClearFormationMaximumTargetSpeeds();
        formationState = FormationState.Failed;
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
