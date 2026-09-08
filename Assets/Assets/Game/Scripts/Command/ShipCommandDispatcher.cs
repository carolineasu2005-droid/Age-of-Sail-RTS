using System.Collections.Generic;
using UnityEngine;

public class ShipCommandDispatcher : MonoBehaviour
{
    public enum DispatchResult
    {
        None,
        NoSelection,
        SingleShip,
        RequiresFormation
    }


    [Header("References")]

    [SerializeField]
    private ShipSelectionManager selectionManager;


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
            selectedShip.SetDestination(
                worldDestination,
                selectionMode,
                navigationAssistMode
            );
            lastSingleShip = selectedShip;
            lastDispatchResult = DispatchResult.SingleShip;
            return;
        }

        groupCommandPending = true;
        pendingGroupDestination = worldDestination;
        pendingGroupSelectionMode = selectionMode;
        pendingGroupNavigationAssistMode = navigationAssistMode;
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
        groupCommandPending = true;
        pendingGroupDestination = formationCenter;
        pendingGroupSelectionMode = selectionMode;
        pendingGroupNavigationAssistMode = navigationAssistMode;
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
        groupCommandPending = true;
        pendingGroupDestination = worldDestination;
        pendingGroupSelectionMode = selectionMode;
        pendingGroupNavigationAssistMode = navigationAssistMode;
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
        pendingGroupHasExplicitFormationHeading = false;
        pendingGroupExplicitFormationHeading = 0f;
        pendingGroupGeometrySnapshot = null;
        pendingGroupShipCount = 0;
        pendingGroupDispatchSequence = 0;
    }
}
