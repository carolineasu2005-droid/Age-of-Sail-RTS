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
        StopSelectedShips
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
        dispatchSequence++;
        DispatchSequenceChanged?.Invoke(dispatchSequence);
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

            if (selectedShip == null)
            {
                lastSelectedShipCount = 0;
                lastDispatchResult = DispatchResult.NoSelection;
                return;
            }

            ClearPendingGroupCommand();
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
        dispatchSequence++;
        DispatchSequenceChanged?.Invoke(dispatchSequence);
        lastWorldDestination = formationCenter;
        lastSelectionMode = selectionMode;
        lastNavigationAssistMode = navigationAssistMode;
        lastSingleShip = null;

        if (geometrySnapshot == null || geometrySnapshot.Members.Count < 2)
        {
            lastSelectedShipCount = 0;
            lastDispatchResult = DispatchResult.NoSelection;
            return;
        }

        ClearPendingGroupCommand();
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
        dispatchSequence++;
        DispatchSequenceChanged?.Invoke(dispatchSequence);
        lastWorldDestination = worldDestination;
        lastSelectionMode = selectionMode;
        lastNavigationAssistMode = navigationAssistMode;
        lastSingleShip = null;

        if (geometrySnapshot == null || geometrySnapshot.Members.Count < 2)
        {
            lastSelectedShipCount = 0;
            lastDispatchResult = DispatchResult.NoSelection;
            return;
        }

        ClearPendingGroupCommand();
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
            ApplyPlayerStop(selectedShip);
        }

        lastSingleShip = lastSelectedShipCount == 1
            ? selectedShips[0]
            : null;
        lastDispatchResult = DispatchResult.StopSelectedShips;
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
