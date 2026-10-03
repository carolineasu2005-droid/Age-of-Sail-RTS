using System.Collections.Generic;
using UnityEngine;

public class ShipCommandDispatcher : MonoBehaviour
{
    public enum DispatchResult
    {
        None,
        NoSelection,
        SingleShip,
        RequiresFormation,
        StopSelectedShips,
        DirectedHeading,
        Follow
    }

    public enum DirectedHeadingFailure
    {
        None,
        DispatcherUnavailable,
        NoSelection,
        RequiresSingleSelection,
        InvalidSelectedShip,
        MissingMovementComponents,
        InvalidRequest,
        PreviewUnavailable,
        UnsupportedManeuver,
        OwnerUnavailable
    }

    public enum FollowFailure
    {
        None, DispatcherUnavailable, NoSelection, MultipleSelection, InvalidFollower,
        InvalidTarget, Self, NotFriendly, UnknownRelationship, Cycle,
        MissingFollowComponents, TargetInvalidLifecycle
    }

    public readonly struct FollowCommandResult
    {
        public bool Accepted { get; }
        public bool IsNoOp { get; }
        public ShipDestinationController Follower { get; }
        public GameObject TargetShipRoot { get; }
        public FollowFailure Failure { get; }

        internal FollowCommandResult(bool accepted, bool isNoOp,
            ShipDestinationController follower, GameObject targetShipRoot, FollowFailure failure)
        {
            Accepted = accepted;
            IsNoOp = isNoOp;
            Follower = follower;
            TargetShipRoot = targetShipRoot;
            Failure = failure;
        }
    }

    public bool TryStartFollow(GameObject targetShipRoot, out FollowCommandResult result)
    {
        ShipDestinationController ship = null;
        FollowFailure failure = FollowFailure.None;
        if (!isActiveAndEnabled) failure = FollowFailure.DispatcherUnavailable;
        else if (selectionManager == null || selectionManager.SelectedCount == 0)
            failure = FollowFailure.NoSelection;
        else if (selectionManager.SelectedCount != 1) failure = FollowFailure.MultipleSelection;
        else
        {
            ship = selectionManager.PrimarySelectedShip;
            if (ship == null || !ship.isActiveAndEnabled) failure = FollowFailure.InvalidFollower;
            else if (targetShipRoot == null || !targetShipRoot.activeInHierarchy)
                failure = FollowFailure.InvalidTarget;
            else if (ship.gameObject == targetShipRoot) failure = FollowFailure.Self;
            else
            {
                CombatRelationship relationship = CombatRelationshipResolver.Resolve(
                    ship.GetComponent<ShipCombatAffiliation>(),
                    targetShipRoot.GetComponent<ShipCombatAffiliation>());
                if (relationship == CombatRelationship.Unknown) failure = FollowFailure.UnknownRelationship;
                else if (relationship != CombatRelationship.Friendly) failure = FollowFailure.NotFriendly;
                else
                {
                    ShipIntegrity integrity = targetShipRoot.GetComponent<ShipIntegrity>();
                    ShipFollowController follower = ship.GetComponent<ShipFollowController>();
                    ShipFollowController target = targetShipRoot.GetComponent<ShipFollowController>();
                    ShipFollowNavigationController navigation = ship.GetComponent<ShipFollowNavigationController>();
                    if (integrity != null && integrity.IsSinking)
                        failure = FollowFailure.TargetInvalidLifecycle;
                    else if (follower == null || target == null || navigation == null
                        || !navigation.isActiveAndEnabled
                        || !follower.IsMovementEntityValid() || !target.IsMovementEntityValid())
                        failure = FollowFailure.MissingFollowComponents;
                    else if (follower.WouldCreateCycle(target)) failure = FollowFailure.Cycle;
                    else if (!follower.CanBeginFollowRelationship(target)) failure = FollowFailure.InvalidTarget;
                    else
                    {
                        if (follower.IsFollowing && follower.FollowTarget == target
                            && follower.TargetTrailRecorder == target.GetComponent<ShipFollowTrailRecorder>()
                            && follower.TargetTrailRecorder.TryGetFollowerProgress(follower, out _, out _))
                        {
                            result = new FollowCommandResult(true, true, ship, targetShipRoot, FollowFailure.None);
                            return true;
                        }

                        // All preflight completes before touching existing Movement ownership.
                        FormationCommandController formation = GetFormationCommandController();
                        if (formation != null && formation.ContainsActiveMember(ship)) formation.CancelFormation();
                        ClearPendingGroupCommand();
                        ship.ClearDestination();
                        ship.GetComponent<ShipManeuverPlanner>().CancelCurrentManeuver();
                        ClearPlayerStop(ship);
                        // Validation/subscription is owned by the foundation, not duplicated here.
                        bool accepted = follower.TryBeginFollowRelationship(target);
                        if (!accepted)
                        {
                            result = new FollowCommandResult(false, false, ship, targetShipRoot, FollowFailure.InvalidTarget);
                            return false;
                        }
                        lastSelectedShipCount = 1;
                        lastSingleShip = ship;
                        lastDispatchResult = DispatchResult.Follow;
                        dispatchSequence++;
                        DispatchSequenceChanged?.Invoke(dispatchSequence);
                        result = new FollowCommandResult(true, false, ship, targetShipRoot, FollowFailure.None);
                        return true;
                    }
                }
            }
        }
        result = new FollowCommandResult(false, false, ship, targetShipRoot, failure);
        return false;
    }

    public readonly struct DirectedHeadingCommandResult
    {
        public bool Accepted { get; }
        public ShipDestinationController SelectedShip { get; }
        public float TargetHeading { get; }
        public TurnDirection Direction { get; }
        public ShipManeuverPlanner.ManeuverType PreviewManeuver { get; }
        public ShipManeuverPlanner.ManeuverType ExecutedManeuver { get; }
        public DirectedHeadingFailure Failure { get; }

        internal DirectedHeadingCommandResult(
            bool accepted,
            ShipDestinationController selectedShip,
            float targetHeading,
            TurnDirection direction,
            ShipManeuverPlanner.ManeuverType previewManeuver,
            ShipManeuverPlanner.ManeuverType executedManeuver,
            DirectedHeadingFailure failure
        )
        {
            Accepted = accepted;
            SelectedShip = selectedShip;
            TargetHeading = targetHeading;
            Direction = direction;
            PreviewManeuver = previewManeuver;
            ExecutedManeuver = executedManeuver;
            Failure = failure;
        }
    }


    [Header("References")]

    [SerializeField]
    private ShipSelectionManager selectionManager;

    [SerializeField]
    private FormationCommandController formationCommandController;


    [Header("Runtime Debug")]

    [SerializeField]
    private int dispatchSequence;

    [SerializeField]
    private DispatchResult lastDispatchResult;

    [SerializeField]
    private Vector3 lastWorldDestination;

    [SerializeField]
    private ShipDestinationController.TurnSelectionMode lastSelectionMode;

    [SerializeField]
    private WindNavigationAssistMode lastNavigationAssistMode =
        WindNavigationAssistMode.Assisted;

    [SerializeField]
    private int lastSelectedShipCount;

    [SerializeField]
    private ShipDestinationController lastSingleShip;

    [SerializeField]
    private bool groupCommandPending;

    [SerializeField]
    private Vector3 pendingGroupDestination;

    [SerializeField]
    private ShipDestinationController.TurnSelectionMode
        pendingGroupSelectionMode;

    [SerializeField]
    private WindNavigationAssistMode pendingGroupNavigationAssistMode =
        WindNavigationAssistMode.Assisted;

    [SerializeField]
    private FormationManeuverStyle pendingGroupRequestedManeuverStyle;

    [SerializeField]
    private bool pendingGroupHasExplicitFormationHeading;

    [SerializeField]
    private float pendingGroupExplicitFormationHeading;

    private FormationGeometrySnapshot pendingGroupGeometrySnapshot;

    [SerializeField]
    private int pendingGroupShipCount;

    [SerializeField]
    private int pendingGroupDispatchSequence;


    public event System.Action<int> DispatchSequenceChanged;

    public int DispatchSequence => dispatchSequence;

    public DispatchResult LastDispatchResult => lastDispatchResult;

    public Vector3 LastWorldDestination => lastWorldDestination;

    public ShipDestinationController.TurnSelectionMode LastSelectionMode
        => lastSelectionMode;

    public WindNavigationAssistMode LastNavigationAssistMode
        => lastNavigationAssistMode;

    public int LastSelectedShipCount => lastSelectedShipCount;

    public ShipDestinationController LastSingleShip => lastSingleShip;

    public bool GroupCommandPending => groupCommandPending;

    public Vector3 PendingGroupDestination => pendingGroupDestination;

    public ShipDestinationController.TurnSelectionMode
        PendingGroupSelectionMode => pendingGroupSelectionMode;

    public WindNavigationAssistMode PendingGroupNavigationAssistMode
        => pendingGroupNavigationAssistMode;

    public FormationManeuverStyle PendingGroupRequestedManeuverStyle
        => pendingGroupRequestedManeuverStyle;

    public bool PendingGroupHasExplicitFormationHeading
        => pendingGroupHasExplicitFormationHeading;

    public float PendingGroupExplicitFormationHeading
        => pendingGroupExplicitFormationHeading;

    public FormationGeometrySnapshot PendingGroupGeometrySnapshot
        => pendingGroupGeometrySnapshot;

    public int PendingGroupShipCount => pendingGroupShipCount;

    public int PendingGroupDispatchSequence => pendingGroupDispatchSequence;


    private void Awake()
    {
        if (selectionManager == null)
        {
            selectionManager = GetComponent<ShipSelectionManager>();
        }

        if (formationCommandController == null)
        {
            formationCommandController = GetComponent<
                FormationCommandController
            >();
        }
    }


    public void DispatchDestination(
        Vector3 worldDestination,
        ShipDestinationController.TurnSelectionMode selectionMode,
        WindNavigationAssistMode navigationAssistMode
    )
    {
        if (!IsFinite(worldDestination)) return;
        lastWorldDestination = worldDestination;
        lastSelectionMode = selectionMode;
        lastNavigationAssistMode = navigationAssistMode;
        lastSingleShip = null;

        if (selectionManager == null)
        {
            lastSelectedShipCount = 0;
            lastDispatchResult = DispatchResult.NoSelection;
            return;
        }

        IReadOnlyList<ShipDestinationController> selectedShips =
            selectionManager.SelectedShips;
        lastSelectedShipCount = selectedShips.Count;

        if (lastSelectedShipCount == 0)
        {
            lastDispatchResult = DispatchResult.NoSelection;
            return;
        }

        if (lastSelectedShipCount == 1)
        {
            ShipDestinationController selectedShip = selectedShips[0];

            if (selectedShip == null || !selectedShip.isActiveAndEnabled)
            {
                lastSelectedShipCount = 0;
                lastDispatchResult = DispatchResult.NoSelection;
                return;
            }

            lastSingleShip = selectedShip;
            lastDispatchResult = DispatchResult.SingleShip;
            dispatchSequence++;
            DispatchSequenceChanged?.Invoke(dispatchSequence);
            ClearPendingGroupCommand();
            CancelPlayerFollow(selectedShip);
            ClearPlayerStop(selectedShip);
            selectedShip.SetDestination(
                worldDestination,
                selectionMode,
                navigationAssistMode
            );
            lastSingleShip = selectedShip;
            lastDispatchResult = DispatchResult.SingleShip;
            return;
        }

        if (!FormationGeometrySnapshot.TryCapture(selectedShips,
            selectionManager.PrimarySelectedShip, out FormationGeometrySnapshot preflight)
            || !IsValidFormationSnapshot(preflight)) return;
        lastDispatchResult = DispatchResult.RequiresFormation;
        dispatchSequence++;
        DispatchSequenceChanged?.Invoke(dispatchSequence);
        foreach (ShipDestinationController ship in selectedShips) CancelPlayerFollow(ship);
        ClearPlayerStops(selectedShips);
        groupCommandPending = true;
        pendingGroupDestination = worldDestination;
        pendingGroupSelectionMode = selectionMode;
        pendingGroupNavigationAssistMode = navigationAssistMode;
        pendingGroupRequestedManeuverStyle = selectionManager != null
            ? selectionManager.RequestedFormationManeuverStyle
            : FormationManeuverStyle.Together;
        pendingGroupHasExplicitFormationHeading = false;
        pendingGroupExplicitFormationHeading = 0f;
        pendingGroupGeometrySnapshot = null;
        pendingGroupShipCount = lastSelectedShipCount;
        pendingGroupDispatchSequence = dispatchSequence;
        lastDispatchResult = DispatchResult.RequiresFormation;
    }


    public void DispatchFormationPlacement(
        Vector3 formationCenter,
        float formationHeading,
        FormationGeometrySnapshot geometrySnapshot,
        ShipDestinationController.TurnSelectionMode selectionMode,
        WindNavigationAssistMode navigationAssistMode
    )
    {
        if (!IsFinite(formationCenter) || !IsFinite(formationHeading)
            || !IsValidFormationSnapshot(geometrySnapshot)) return;
        lastDispatchResult = DispatchResult.RequiresFormation;
        dispatchSequence++;
        DispatchSequenceChanged?.Invoke(dispatchSequence);
        lastWorldDestination = formationCenter;
        lastSelectionMode = selectionMode;
        lastNavigationAssistMode = navigationAssistMode;
        lastSingleShip = null;

        ClearPendingGroupCommand();
        foreach (FormationGeometryMember member in geometrySnapshot.Members)
            CancelPlayerFollow(member.Ship);
        ClearPlayerStops(geometrySnapshot);
        groupCommandPending = true;
        pendingGroupDestination = formationCenter;
        pendingGroupSelectionMode = selectionMode;
        pendingGroupNavigationAssistMode = navigationAssistMode;
        pendingGroupRequestedManeuverStyle = selectionManager != null
            ? selectionManager.RequestedFormationManeuverStyle
            : FormationManeuverStyle.Together;
        pendingGroupHasExplicitFormationHeading = true;
        pendingGroupExplicitFormationHeading = Mathf.Repeat(
            formationHeading,
            360f
        );
        pendingGroupGeometrySnapshot = geometrySnapshot;
        pendingGroupShipCount = geometrySnapshot.Members.Count;
        pendingGroupDispatchSequence = dispatchSequence;
        lastSelectedShipCount = pendingGroupShipCount;
        lastDispatchResult = DispatchResult.RequiresFormation;
    }


    public void DispatchFormationDestination(
        Vector3 worldDestination,
        FormationGeometrySnapshot geometrySnapshot,
        ShipDestinationController.TurnSelectionMode selectionMode,
        WindNavigationAssistMode navigationAssistMode
    )
    {
        if (!IsFinite(worldDestination)
            || !IsValidFormationSnapshot(geometrySnapshot)) return;
        lastDispatchResult = DispatchResult.RequiresFormation;
        dispatchSequence++;
        DispatchSequenceChanged?.Invoke(dispatchSequence);
        lastWorldDestination = worldDestination;
        lastSelectionMode = selectionMode;
        lastNavigationAssistMode = navigationAssistMode;
        lastSingleShip = null;

        ClearPendingGroupCommand();
        foreach (FormationGeometryMember member in geometrySnapshot.Members)
            CancelPlayerFollow(member.Ship);
        ClearPlayerStops(geometrySnapshot);
        groupCommandPending = true;
        pendingGroupDestination = worldDestination;
        pendingGroupSelectionMode = selectionMode;
        pendingGroupNavigationAssistMode = navigationAssistMode;
        pendingGroupRequestedManeuverStyle = selectionManager != null
            ? selectionManager.RequestedFormationManeuverStyle
            : FormationManeuverStyle.Together;
        pendingGroupHasExplicitFormationHeading = false;
        pendingGroupExplicitFormationHeading = 0f;
        pendingGroupGeometrySnapshot = geometrySnapshot;
        pendingGroupShipCount = geometrySnapshot.Members.Count;
        pendingGroupDispatchSequence = dispatchSequence;
        lastSelectedShipCount = pendingGroupShipCount;
        lastDispatchResult = DispatchResult.RequiresFormation;
    }


    public void ClearPendingGroupCommand()
    {
        groupCommandPending = false;
        pendingGroupDestination = Vector3.zero;
        pendingGroupSelectionMode =
            ShipDestinationController.TurnSelectionMode.Auto;
        pendingGroupNavigationAssistMode =
            WindNavigationAssistMode.Assisted;
        pendingGroupRequestedManeuverStyle = FormationManeuverStyle.Together;
        pendingGroupHasExplicitFormationHeading = false;
        pendingGroupExplicitFormationHeading = 0f;
        pendingGroupGeometrySnapshot = null;
        pendingGroupShipCount = 0;
        pendingGroupDispatchSequence = 0;
    }


    public void DispatchStopSelectedShips()
    {
        if (selectionManager == null)
        {
            lastSelectedShipCount = 0;
            lastDispatchResult = DispatchResult.NoSelection;
            return;
        }

        IReadOnlyList<ShipDestinationController> selectedShips =
            selectionManager.SelectedShips;
        lastSelectedShipCount = selectedShips.Count;

        if (lastSelectedShipCount == 0)
        {
            lastDispatchResult = DispatchResult.NoSelection;
            return;
        }

        FormationCommandController formation =
            GetFormationCommandController();
        if (formation != null && ContainsActiveFormationMember(
            formation,
            selectedShips
        ))
        {
            formation.CancelFormation();
        }

        ClearPendingGroupCommand();

        foreach (ShipDestinationController selectedShip in selectedShips)
        {
            CancelPlayerFollow(selectedShip);
            ApplyPlayerStop(selectedShip);
        }

        lastSingleShip = lastSelectedShipCount == 1
            ? selectedShips[0]
            : null;
        lastDispatchResult = DispatchResult.StopSelectedShips;
    }

    public bool TryDispatchDirectedHeading(
        float targetHeading,
        TurnDirection direction,
        out DirectedHeadingCommandResult result
    )
    {
        float normalizedHeading = float.IsNaN(targetHeading)
            || float.IsInfinity(targetHeading)
            ? 0f
            : Mathf.Repeat(targetHeading, 360f);
        ShipDestinationController ship = null;
        ShipManeuverPlanner.ManeuverType preview =
            ShipManeuverPlanner.ManeuverType.None;
        DirectedHeadingFailure failure;

        if (selectionManager == null)
        {
            failure = DirectedHeadingFailure.NoSelection;
        }
        else
        {
            IReadOnlyList<ShipDestinationController> selectedShips =
                selectionManager.SelectedShips;
            if (selectedShips.Count != 1)
            {
                failure = selectedShips.Count == 0
                    ? DirectedHeadingFailure.NoSelection
                    : DirectedHeadingFailure.RequiresSingleSelection;
            }
            else
            {
                ship = selectedShips[0];
                if (ship == null || !ship.isActiveAndEnabled)
                {
                    failure = DirectedHeadingFailure.InvalidSelectedShip;
                }
                else
                {
                    ShipManeuverPlanner planner =
                        ship.GetComponent<ShipManeuverPlanner>();
                    ShipSailingSpeed speed =
                        ship.GetComponent<ShipSailingSpeed>();
                    ShipHeadingController heading =
                        ship.GetComponent<ShipHeadingController>();
                    ShipTurning turning = ship.GetComponent<ShipTurning>();
                    if (planner == null || !planner.isActiveAndEnabled
                        || speed == null || !speed.isActiveAndEnabled
                        || heading == null || !heading.isActiveAndEnabled
                        || turning == null || !turning.isActiveAndEnabled)
                    {
                        failure = DirectedHeadingFailure.MissingMovementComponents;
                    }
                    else if (float.IsNaN(targetHeading)
                        || float.IsInfinity(targetHeading)
                        || (direction != TurnDirection.Clockwise
                            && direction != TurnDirection.CounterClockwise))
                    {
                        failure = DirectedHeadingFailure.InvalidRequest;
                    }
                    else if (!planner.TryPreviewDirectedHeading(
                        targetHeading, direction, out preview))
                    {
                        failure = DirectedHeadingFailure.PreviewUnavailable;
                    }
                    else if (preview == ShipManeuverPlanner.ManeuverType.None
                        || preview == ShipManeuverPlanner.ManeuverType.Complex)
                    {
                        failure = DirectedHeadingFailure.UnsupportedManeuver;
                    }
                    else if (!planner.CanExecuteCoordinatedManeuver(preview))
                    {
                        failure = DirectedHeadingFailure.OwnerUnavailable;
                    }
                    else
                    {
                        FormationCommandController formation =
                            GetFormationCommandController();
                        if (formation != null
                            && formation.ContainsActiveMember(ship))
                        {
                            formation.CancelFormation();
                        }

                        lastDispatchResult = DispatchResult.DirectedHeading;
                        dispatchSequence++;
                        DispatchSequenceChanged?.Invoke(dispatchSequence);
                        ClearPendingGroupCommand();
                        CancelPlayerFollow(ship);
                        ship.ClearDestination();
                        ClearPlayerStop(ship);
                        planner.ExecuteHeadingCommand(normalizedHeading, direction);
                        lastSelectedShipCount = 1;
                        lastSingleShip = ship;
                        lastDispatchResult = DispatchResult.DirectedHeading;
                        result = new DirectedHeadingCommandResult(
                            true, ship, normalizedHeading, direction,
                            preview, planner.CurrentManeuver,
                            DirectedHeadingFailure.None
                        );
                        return true;
                    }
                }
            }
        }

        result = new DirectedHeadingCommandResult(
            false, ship, normalizedHeading, direction, preview,
            ShipManeuverPlanner.ManeuverType.None, failure
        );
        return false;
    }

    public bool TryDecreaseSelectedSpeedOrder()
    {
        return TryStepSelectedSpeedOrder(false);
    }

    public bool TryIncreaseSelectedSpeedOrder()
    {
        return TryStepSelectedSpeedOrder(true);
    }

    private bool TryStepSelectedSpeedOrder(bool increase)
    {
        if (selectionManager == null)
        {
            return false;
        }

        IReadOnlyList<ShipDestinationController> selectedShips =
            selectionManager.SelectedShips;
        if (selectedShips.Count != 1
            || selectedShips[0] == null
            || !selectedShips[0].isActiveAndEnabled)
        {
            return false;
        }

        ShipDestinationController ship = selectedShips[0];
        ShipSailingSpeed sailingSpeed = ship.GetComponent<ShipSailingSpeed>();
        if (sailingSpeed == null || !sailingSpeed.isActiveAndEnabled)
        {
            return false;
        }

        FormationCommandController formation = GetFormationCommandController();
        if (formation != null && formation.ContainsActiveMember(ship))
        {
            return false;
        }

        CancelPlayerFollow(ship);
        if (increase)
        {
            sailingSpeed.IncreasePlayerSpeedOrder();
        }
        else
        {
            sailingSpeed.DecreasePlayerSpeedOrder();
        }

        sailingSpeed.ClearPlayerStopSpeedCap();
        return true;
    }


    private FormationCommandController GetFormationCommandController()
    {
        if (formationCommandController == null)
        {
            formationCommandController = GetComponent<
                FormationCommandController
            >();
        }

        return formationCommandController;
    }


    private static bool ContainsActiveFormationMember(
        FormationCommandController formation,
        IReadOnlyList<ShipDestinationController> selectedShips
    )
    {
        foreach (ShipDestinationController selectedShip in selectedShips)
        {
            if (formation.ContainsActiveMember(selectedShip))
            {
                return true;
            }
        }

        return false;
    }


    private static void ClearPlayerStops(
        IReadOnlyList<ShipDestinationController> ships
    )
    {
        foreach (ShipDestinationController ship in ships)
        {
            ClearPlayerStop(ship);
        }
    }


    private static void ClearPlayerStops(
        FormationGeometrySnapshot geometrySnapshot
    )
    {
        foreach (FormationGeometryMember member in geometrySnapshot.Members)
        {
            ClearPlayerStop(member.Ship);
        }
    }


    private static void ClearPlayerStop(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        ShipSailingSpeed sailingSpeed = ship.GetComponent<ShipSailingSpeed>();
        if (sailingSpeed != null)
        {
            sailingSpeed.ClearPlayerStopSpeedCap();
        }
    }


    private static void CancelPlayerFollow(ShipDestinationController ship)
    {
        if (ship == null) return;
        ShipFollowController follow = ship.GetComponent<ShipFollowController>();
        if (follow != null) follow.CancelFollow();
        ShipSailingSpeed speed = ship.GetComponent<ShipSailingSpeed>();
        if (speed != null) speed.ClearFollowMaximumTargetSpeed();
    }

    private static bool IsValidFormationSnapshot(FormationGeometrySnapshot snapshot)
    {
        if (snapshot == null || snapshot.Members.Count < 2
            || !IsFinite(snapshot.FormationCenter) || !IsFinite(snapshot.FormationHeading)) return false;
        foreach (FormationGeometryMember member in snapshot.Members)
            if (member.Ship == null || !member.Ship.isActiveAndEnabled
                || !IsFinite(member.LocalX) || !IsFinite(member.LocalZ)) return false;
        return true;
    }

    private static bool IsFinite(Vector3 point)
        => IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);


    private static void ApplyPlayerStop(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        ship.ClearDestination();

        ShipManeuverPlanner maneuverPlanner = ship.GetComponent<
            ShipManeuverPlanner
        >();
        if (maneuverPlanner != null)
        {
            maneuverPlanner.CancelCurrentManeuver();
        }

        ShipTacking shipTacking = ship.GetComponent<ShipTacking>();
        if (shipTacking != null)
        {
            shipTacking.CancelTack();
        }

        ShipWearing shipWearing = ship.GetComponent<ShipWearing>();
        if (shipWearing != null)
        {
            shipWearing.CancelWear();
        }

        ShipHeadingController headingController = ship.GetComponent<
            ShipHeadingController
        >();
        if (headingController != null)
        {
            headingController.CancelHeadingCommand();
        }

        ShipSailingSpeed sailingSpeed = ship.GetComponent<ShipSailingSpeed>();
        if (sailingSpeed != null)
        {
            sailingSpeed.SetPlayerStopSpeedCap(0f);
        }
    }
}
