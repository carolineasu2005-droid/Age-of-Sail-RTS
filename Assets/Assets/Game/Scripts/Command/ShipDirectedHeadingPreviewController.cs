using System;
using UnityEngine;

// Transient control presentation. Movement ownership stays with the Change 2 command path.
public sealed class ShipDirectedHeadingPreviewController : MonoBehaviour
{
    public const double StepIntervalSeconds = 0.1d;
    public const float StepAngleDegrees = 10f;
    public const int MaximumSteps = 35;

    [SerializeField]
    private ShipSelectionManager selectionManager;

    [SerializeField]
    private ShipPlayerCommandInput playerCommandInput;

    private readonly ShipDirectedHeadingGhostRenderer ghost = new();
    private ShipSelectionManager subscribedSelectionManager;
    private ShipDestinationController selectedShip;
    private ShipManeuverPlanner planner;
    private double beginUnscaledTime;
    private float initialHeading;
    private TurnDirection direction;
    private int completedSteps;
    private float targetHeading;
    private ShipManeuverPlanner.ManeuverType previewManeuver;
    private bool classificationAvailable;
    private bool previewActive;

    public bool IsPreviewActive => previewActive;
    public ShipDestinationController PreviewShip => selectedShip;
    public float InitialHeading => initialHeading;
    public TurnDirection Direction => direction;
    public int CompletedSteps => completedSteps;
    public float AngularDelta => completedSteps * StepAngleDegrees;
    public float TargetHeading => targetHeading;
    public ShipManeuverPlanner.ManeuverType PreviewManeuver => previewManeuver;
    public bool IsClassificationAvailable => classificationAvailable;
    public bool IsGhostVisible => ghost.IsVisible;
    public Vector3 GhostPosition => ghost.Position;
    public Quaternion GhostRotation => ghost.Rotation;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (previewActive)
        {
            UpdatePreview(Time.unscaledTimeAsDouble);
        }
    }

    private void LateUpdate()
    {
        if (!previewActive)
        {
            return;
        }

        if (!IsSessionValid())
        {
            CancelPreview();
            return;
        }

        ghost.SetPose(selectedShip.transform.position, targetHeading);
        ghost.Draw();
    }

    private void OnDisable()
    {
        CancelPreview();
        UnsubscribeSelection();
    }

    private void OnDestroy()
    {
        CancelPreview();
        UnsubscribeSelection();
    }

    public bool BeginPreview(
        ShipDestinationController ship,
        TurnDirection turnDirection,
        double startUnscaledTime)
    {
        CancelPreview();
        ResolveReferences();

        if (!isActiveAndEnabled
            || ship == null
            || !ship.isActiveAndEnabled
            || !ship.gameObject.activeInHierarchy
            || selectionManager == null
            || selectionManager.SelectedCount != 1
            || selectionManager.PrimarySelectedShip != ship
            || playerCommandInput == null
            || !playerCommandInput.isActiveAndEnabled
            || (turnDirection != TurnDirection.Clockwise
                && turnDirection != TurnDirection.CounterClockwise)
            || double.IsNaN(startUnscaledTime)
            || double.IsInfinity(startUnscaledTime))
        {
            return false;
        }

        ShipManeuverPlanner shipPlanner =
            ship.GetComponent<ShipManeuverPlanner>();
        if (shipPlanner == null || !shipPlanner.isActiveAndEnabled)
        {
            return false;
        }

        selectedShip = ship;
        planner = shipPlanner;
        direction = turnDirection;
        beginUnscaledTime = startUnscaledTime;
        initialHeading = Mathf.Repeat(ship.transform.eulerAngles.y, 360f);
        targetHeading = initialHeading;
        previewActive = true;
        ghost.Show(ship);
        RefreshPreview();
        return true;
    }

    public bool UpdatePreview(double currentUnscaledTime)
    {
        if (!previewActive)
        {
            return false;
        }

        if (!IsSessionValid()
            || double.IsNaN(currentUnscaledTime)
            || double.IsInfinity(currentUnscaledTime))
        {
            CancelPreview();
            return false;
        }

        double elapsed = Math.Max(0d, currentUnscaledTime - beginUnscaledTime);
        int steps = (int)Math.Min(
            MaximumSteps,
            Math.Floor(elapsed / StepIntervalSeconds + 1e-9d));
        completedSteps = Math.Max(completedSteps, steps);
        float signedDelta = completedSteps * StepAngleDegrees
            * (direction == TurnDirection.Clockwise ? 1f : -1f);
        targetHeading = Mathf.Repeat(initialHeading + signedDelta, 360f);
        RefreshPreview();
        return true;
    }

    public bool CommitPreview(
        double releaseUnscaledTime,
        out ShipCommandDispatcher.DirectedHeadingCommandResult result)
    {
        result = default;
        if (!UpdatePreview(releaseUnscaledTime))
        {
            return false;
        }

        int finalSteps = completedSteps;
        float finalHeading = targetHeading;
        TurnDirection finalDirection = direction;
        ShipPlayerCommandInput input = playerCommandInput;
        CancelPreview();
        return finalSteps > 0
            && input != null
            && input.TrySubmitDirectedHeading(
                finalHeading, finalDirection, out result);
    }

    public void CancelPreview()
    {
        previewActive = false;
        ghost.Hide();
        selectedShip = null;
        planner = null;
        beginUnscaledTime = 0d;
        initialHeading = 0f;
        direction = default;
        completedSteps = 0;
        targetHeading = 0f;
        previewManeuver = ShipManeuverPlanner.ManeuverType.None;
        classificationAvailable = false;
    }

    private void RefreshPreview()
    {
        classificationAvailable = planner.TryPreviewDirectedHeading(
            targetHeading, direction, out previewManeuver);
        ghost.SetPose(selectedShip.transform.position, targetHeading);
    }

    private bool IsSessionValid()
    {
        return isActiveAndEnabled
            && selectedShip != null
            && selectedShip.isActiveAndEnabled
            && selectedShip.gameObject.activeInHierarchy
            && planner != null
            && planner.isActiveAndEnabled
            && playerCommandInput != null
            && playerCommandInput.isActiveAndEnabled
            && selectionManager != null
            && selectionManager.SelectedCount == 1
            && selectionManager.PrimarySelectedShip == selectedShip;
    }

    private void ResolveReferences()
    {
        if (selectionManager == null)
        {
            selectionManager = GetComponent<ShipSelectionManager>();
        }

        if (playerCommandInput == null)
        {
            playerCommandInput = GetComponent<ShipPlayerCommandInput>();
        }

        if (isActiveAndEnabled && subscribedSelectionManager != selectionManager)
        {
            UnsubscribeSelection();
            subscribedSelectionManager = selectionManager;
            if (subscribedSelectionManager != null)
            {
                subscribedSelectionManager.SelectionMembershipChanged +=
                    HandleSelectionMembershipChanged;
            }
        }
    }

    private void UnsubscribeSelection()
    {
        if (subscribedSelectionManager != null)
        {
            subscribedSelectionManager.SelectionMembershipChanged -=
                HandleSelectionMembershipChanged;
            subscribedSelectionManager = null;
        }
    }

    private void HandleSelectionMembershipChanged()
    {
        CancelPreview();
    }
}
