using UnityEngine;
using UnityEngine.InputSystem;

public class ShipPlayerCommandInput : MonoBehaviour
{
    public enum PendingFormationTemplate
    {
        None,
        LineAhead
    }

    [Header("References")]

    [SerializeField]
    private Camera commandCamera;

    [SerializeField]
    private ShipSelectionManager selectionManager;

    [SerializeField]
    private ShipCommandDispatcher commandDispatcher;

    [SerializeField]
    private FormationCommandController formationCommandController;


    [Header("Navigation Settings")]

    [SerializeField]
    private float defaultNavigationPlaneY;

    [SerializeField]
    private WindNavigationAssistMode windNavigationAssistMode =
        WindNavigationAssistMode.Assisted;


    [Header("Box Selection Settings")]

    [SerializeField]
    [Range(2f, 30f)]
    private float boxSelectionDragThreshold = 8f;


    [Header("Formation Placement Settings")]

    [SerializeField]
    [Range(2f, 30f)]
    private float rightDragThreshold = 10f;


    [Header("Runtime Debug")]

    [SerializeField]
    private bool cameraAvailable;

    [SerializeField]
    private bool selectionManagerAvailable;

    [SerializeField]
    private bool commandDispatcherAvailable;

    [SerializeField]
    private bool leftMouseDetectedThisFrame;

    [SerializeField]
    private bool leftMouseReleasedThisFrame;

    [SerializeField]
    private bool selectionGestureActive;

    [SerializeField]
    private bool boxSelectionActive;

    [SerializeField]
    private bool selectionShiftHeldAtMouseDown;

    [SerializeField]
    private Vector2 selectionMouseDownScreenPosition;

    [SerializeField]
    private Vector2 selectionCurrentScreenPosition;

    [SerializeField]
    private Rect selectionScreenRect;

    [SerializeField]
    private int lastBoxSelectionCount;

    [SerializeField]
    private Vector2 lastSelectionClickPosition;

    [SerializeField]
    private bool lastSelectionPickValid;

    [SerializeField]
    private ShipDestinationController lastSelectedShip;

    [SerializeField]
    private bool rightMouseDetectedThisFrame;

    [SerializeField]
    private bool rightMouseReleasedThisFrame;

    [SerializeField]
    private bool rightPlacementGestureActive;

    [SerializeField]
    private bool formationPlacementPreviewActive;

    [SerializeField]
    private bool rightMouseDownFormationSnapshotCaptured;

    [SerializeField]
    private Vector2 rightMouseDownScreenPosition;

    [SerializeField]
    private Vector2 rightMouseCurrentScreenPosition;

    [SerializeField]
    private Vector3 rightMouseDownWorldPosition;

    [SerializeField]
    private Vector3 rightMouseCurrentWorldPosition;

    [SerializeField]
    private float previewFormationHeading;

    [SerializeField]
    private float lastValidPreviewHeading;

    [SerializeField]
    private bool hasLastValidPreviewHeading;

    [SerializeField]
    private int previewGhostMeshCount;

    [SerializeField]
    private bool lastClickValid;

    [SerializeField]
    private Vector2 lastMouseScreenPosition;

    [SerializeField]
    private Vector3 lastWorldDestination;

    [SerializeField]
    private ShipDestinationController.TurnSelectionMode lastSelectionMode;

    [SerializeField]
    private PendingFormationTemplate pendingFormationTemplate;

    private FormationGeometrySnapshot rightMouseDownGeometrySnapshot;

    private FormationPlacementPreviewRenderer formationPlacementPreviewRenderer;

    public PendingFormationTemplate PendingTemplate => pendingFormationTemplate;


    private void Awake()
    {
        if (commandCamera == null)
        {
            commandCamera = Camera.main;
        }

        if (selectionManager == null)
        {
            selectionManager = GetComponent<ShipSelectionManager>();
        }

        if (commandDispatcher == null)
        {
            commandDispatcher = GetComponent<ShipCommandDispatcher>();
        }

        if (formationCommandController == null)
        {
            formationCommandController = GetComponent<
                FormationCommandController
            >();
        }

    }


    private void OnEnable()
    {
        if (selectionManager != null)
        {
            selectionManager.SelectionMembershipChanged +=
                ClearPendingFormationTemplate;
        }
    }


    private void Update()
    {
        if (commandCamera == null)
        {
            commandCamera = Camera.main;
        }

        cameraAvailable = commandCamera != null;
        selectionManagerAvailable = selectionManager != null;
        commandDispatcherAvailable = commandDispatcher != null;
        leftMouseDetectedThisFrame = Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;
        leftMouseReleasedThisFrame = Mouse.current != null
            && Mouse.current.leftButton.wasReleasedThisFrame;
        rightMouseDetectedThisFrame = Mouse.current != null
            && Mouse.current.rightButton.wasPressedThisFrame;
        rightMouseReleasedThisFrame = Mouse.current != null
            && Mouse.current.rightButton.wasReleasedThisFrame;

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            ToggleLineAheadTemplate();
        }

        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            StopSelectedShips();
        }

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            ToggleHoveredFormationLead();
        }

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            ToggleRequestedFormationManeuverStyle();
        }

        if (leftMouseDetectedThisFrame)
        {
            BeginSelectionGesture();
        }

        if (selectionGestureActive
            && Mouse.current != null
            && (Mouse.current.leftButton.isPressed
                || leftMouseReleasedThisFrame))
        {
            UpdateSelectionGesture(Mouse.current.position.ReadValue());
        }

        if (leftMouseReleasedThisFrame)
        {
            CompleteSelectionGesture();
        }

        if (rightMouseDetectedThisFrame)
        {
            BeginDestinationGesture();
        }

        if (rightPlacementGestureActive
            && Mouse.current != null
            && (Mouse.current.rightButton.isPressed
                || rightMouseReleasedThisFrame))
        {
            UpdateDestinationGesture(Mouse.current.position.ReadValue());
        }

        if (rightMouseReleasedThisFrame)
        {
            CompleteDestinationGesture();
        }
    }


    private void BeginSelectionGesture()
    {
        lastSelectionPickValid = false;
        lastSelectedShip = null;
        selectionGestureActive = true;
        boxSelectionActive = false;
        selectionShiftHeldAtMouseDown = IsShiftHeld();
        selectionMouseDownScreenPosition = Mouse.current.position.ReadValue();
        selectionCurrentScreenPosition = selectionMouseDownScreenPosition;
        selectionScreenRect = new Rect(
            selectionMouseDownScreenPosition,
            Vector2.zero
        );
        lastSelectionClickPosition = selectionMouseDownScreenPosition;
        lastBoxSelectionCount = 0;
    }


    private void UpdateSelectionGesture(Vector2 screenPosition)
    {
        selectionCurrentScreenPosition = screenPosition;
        selectionScreenRect = GetNormalizedScreenRect(
            selectionMouseDownScreenPosition,
            selectionCurrentScreenPosition
        );

        if (boxSelectionActive)
        {
            return;
        }

        float dragDistanceSquared = (
            selectionCurrentScreenPosition
            - selectionMouseDownScreenPosition
        ).sqrMagnitude;
        float dragThresholdSquared = boxSelectionDragThreshold
            * boxSelectionDragThreshold;

        if (dragDistanceSquared > dragThresholdSquared)
        {
            boxSelectionActive = true;
        }
    }


    private void CompleteSelectionGesture()
    {
        if (!selectionGestureActive)
        {
            return;
        }

        if (boxSelectionActive)
        {
            HandleBoxSelection();
        }
        else
        {
            HandleSelectionClick(
                selectionMouseDownScreenPosition,
                selectionShiftHeldAtMouseDown
            );
        }

        selectionGestureActive = false;
        boxSelectionActive = false;
    }


    private void HandleSelectionClick(
        Vector2 selectionClickPosition,
        bool shiftHeld
    )
    {
        lastSelectionClickPosition = selectionClickPosition;

        if (commandCamera == null || selectionManager == null)
        {
            return;
        }

        if (selectionManager.TryPickShip(
            commandCamera,
            lastSelectionClickPosition,
            out ShipDestinationController selectedShip
        ))
        {
            if (shiftHeld)
            {
                selectionManager.ToggleSelection(selectedShip);
            }
            else
            {
                selectionManager.SelectSingle(selectedShip);
            }

            lastSelectionPickValid = true;
            lastSelectedShip = selectedShip;
            return;
        }

        if (!shiftHeld)
        {
            selectionManager.ClearSelection();
        }
    }


    private void HandleBoxSelection()
    {
        lastBoxSelectionCount = 0;

        if (commandCamera == null || selectionManager == null)
        {
            return;
        }

        lastBoxSelectionCount = selectionManager.SelectShipsInScreenRect(
            commandCamera,
            selectionScreenRect,
            selectionShiftHeldAtMouseDown
        );
    }


    private void OnGUI()
    {
        if (!selectionGestureActive
            || !boxSelectionActive
            || Event.current.type != EventType.Repaint)
        {
            return;
        }

        Rect guiSelectionRect = new Rect(
            selectionScreenRect.xMin,
            Screen.height - selectionScreenRect.yMax,
            selectionScreenRect.width,
            selectionScreenRect.height
        );
        Color previousColor = GUI.color;

        GUI.color = new Color(0.2f, 0.7f, 1f, 0.2f);
        GUI.DrawTexture(guiSelectionRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        DrawRectangleBorder(guiSelectionRect, 1f);

        GUI.color = previousColor;
    }


    private static Rect GetNormalizedScreenRect(
        Vector2 firstPoint,
        Vector2 secondPoint
    )
    {
        return Rect.MinMaxRect(
            Mathf.Min(firstPoint.x, secondPoint.x),
            Mathf.Min(firstPoint.y, secondPoint.y),
            Mathf.Max(firstPoint.x, secondPoint.x),
            Mathf.Max(firstPoint.y, secondPoint.y)
        );
    }


    private static void DrawRectangleBorder(
        Rect rectangle,
        float borderWidth
    )
    {
        GUI.DrawTexture(new Rect(
            rectangle.xMin,
            rectangle.yMin,
            rectangle.width,
            borderWidth
        ), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(
            rectangle.xMin,
            rectangle.yMax - borderWidth,
            rectangle.width,
            borderWidth
        ), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(
            rectangle.xMin,
            rectangle.yMin,
            borderWidth,
            rectangle.height
        ), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(
            rectangle.xMax - borderWidth,
            rectangle.yMin,
            borderWidth,
            rectangle.height
        ), Texture2D.whiteTexture);
    }


    private void BeginDestinationGesture()
    {
        lastClickValid = false;
        lastMouseScreenPosition = Mouse.current.position.ReadValue();
        rightPlacementGestureActive = false;
        formationPlacementPreviewActive = false;
        rightMouseDownFormationSnapshotCaptured = false;
        rightMouseDownGeometrySnapshot = null;
        hasLastValidPreviewHeading = false;
        previewGhostMeshCount = 0;
        HideFormationPlacementPreview();

        if (commandCamera == null || commandDispatcher == null)
        {
            return;
        }

        if (!TryGetWorldDestination(
            lastMouseScreenPosition,
            out Vector3 mouseDownWorldPosition
        ))
        {
            return;
        }

        rightPlacementGestureActive = true;
        rightMouseDownScreenPosition = lastMouseScreenPosition;
        rightMouseCurrentScreenPosition = rightMouseDownScreenPosition;
        rightMouseDownWorldPosition = mouseDownWorldPosition;
        rightMouseCurrentWorldPosition = mouseDownWorldPosition;

        if (formationCommandController != null
            && formationCommandController
                .TryCaptureSelectedFormationGeometry(
                    out FormationGeometrySnapshot geometrySnapshot
                )
            && geometrySnapshot.Members.Count >= 2)
        {
            if (pendingFormationTemplate == PendingFormationTemplate.LineAhead)
            {
                FormationMemberOrder memberOrder =
                    FormationMemberOrder.CreateWithDesignatedLead(
                        geometrySnapshot,
                        selectionManager != null
                            ? selectionManager.DesignatedFormationLead
                            : null
                    );
                geometrySnapshot =
                    FormationLayoutGenerator.CreateStandardLineAhead(
                        memberOrder,
                        geometrySnapshot.FormationCenter,
                        geometrySnapshot.FormationHeading
                    );
            }

            rightMouseDownGeometrySnapshot = geometrySnapshot;
            rightMouseDownFormationSnapshotCaptured = true;
        }
    }


    private void UpdateDestinationGesture(Vector2 screenPosition)
    {
        rightMouseCurrentScreenPosition = screenPosition;

        if (formationPlacementPreviewActive)
        {
            if (!CanUseFormationPlacementSnapshot())
            {
                CancelDestinationGesture();
                return;
            }

            UpdateFormationPlacementPreview(screenPosition);
            return;
        }

        if (!rightMouseDownFormationSnapshotCaptured)
        {
            return;
        }

        float dragDistanceSquared = (
            rightMouseCurrentScreenPosition
            - rightMouseDownScreenPosition
        ).sqrMagnitude;
        float dragThresholdSquared = rightDragThreshold * rightDragThreshold;

        if (dragDistanceSquared <= dragThresholdSquared)
        {
            return;
        }

        formationPlacementPreviewActive = true;
        UpdateFormationPlacementPreview(screenPosition);
    }


    private void CompleteDestinationGesture()
    {
        if (!rightPlacementGestureActive)
        {
            return;
        }

        if (formationPlacementPreviewActive)
        {
            CommitFormationPlacement();
        }
        else
        {
            CommitNormalDestinationClick();
        }

        rightPlacementGestureActive = false;
        rightMouseDownFormationSnapshotCaptured = false;
        rightMouseDownGeometrySnapshot = null;
        formationPlacementPreviewActive = false;
        HideFormationPlacementPreview();
    }


    private void UpdateFormationPlacementPreview(Vector2 screenPosition)
    {
        if (!TryGetWorldPositionOnNavigationPlane(
            screenPosition,
            rightMouseDownWorldPosition.y,
            out Vector3 currentWorldPosition
        ))
        {
            return;
        }

        rightMouseCurrentWorldPosition = currentWorldPosition;

        if (!TryGetHeadingBetween(
            rightMouseDownWorldPosition,
            rightMouseCurrentWorldPosition,
            out float heading
        ))
        {
            return;
        }

        previewFormationHeading = heading;
        lastValidPreviewHeading = heading;
        hasLastValidPreviewHeading = true;

        FormationPlacementPreviewRenderer previewRenderer =
            GetOrCreateFormationPlacementPreviewRenderer();

        if (previewRenderer == null)
        {
            return;
        }

        previewRenderer.ShowPreview(
            rightMouseDownGeometrySnapshot,
            rightMouseDownWorldPosition,
            previewFormationHeading,
            commandCamera
        );
        previewGhostMeshCount = previewRenderer.GhostMeshCount;
    }


    private void CommitFormationPlacement()
    {
        if (!CanUseFormationPlacementSnapshot())
        {
            return;
        }

        if (TryGetWorldPositionOnNavigationPlane(
            rightMouseCurrentScreenPosition,
            rightMouseDownWorldPosition.y,
            out Vector3 releaseWorldPosition
        ) && TryGetHeadingBetween(
            rightMouseDownWorldPosition,
            releaseWorldPosition,
            out float releaseHeading
        ))
        {
            rightMouseCurrentWorldPosition = releaseWorldPosition;
            previewFormationHeading = releaseHeading;
            lastValidPreviewHeading = releaseHeading;
            hasLastValidPreviewHeading = true;
        }

        if (!hasLastValidPreviewHeading)
        {
            return;
        }

        lastClickValid = true;
        lastWorldDestination = rightMouseDownWorldPosition;
        lastSelectionMode = GetSelectionMode();
        commandDispatcher.DispatchFormationPlacement(
            rightMouseDownWorldPosition,
            lastValidPreviewHeading,
            rightMouseDownGeometrySnapshot,
            lastSelectionMode,
            windNavigationAssistMode
        );
        ConsumePendingFormationTemplateIfGroupCommandCommitted();
    }


    private void CommitNormalDestinationClick()
    {
        lastClickValid = true;
        lastWorldDestination = rightMouseDownWorldPosition;
        lastSelectionMode = GetSelectionMode();
        if (pendingFormationTemplate == PendingFormationTemplate.LineAhead
            && rightMouseDownGeometrySnapshot != null
            && rightMouseDownGeometrySnapshot.Members.Count >= 2)
        {
            commandDispatcher.DispatchFormationDestination(
                lastWorldDestination,
                rightMouseDownGeometrySnapshot,
                lastSelectionMode,
                windNavigationAssistMode
            );
            ConsumePendingFormationTemplateIfGroupCommandCommitted();
            return;
        }

        commandDispatcher.DispatchDestination(
            lastWorldDestination,
            lastSelectionMode,
            windNavigationAssistMode
        );
    }


    private bool TryGetWorldDestination(
        Vector2 screenPosition,
        out Vector3 worldDestination
    )
    {
        float navigationPlaneY = defaultNavigationPlaneY;

        if (selectionManager != null
            && selectionManager.TryGetSelectionNavigationY(
                out float selectionNavigationY
            ))
        {
            navigationPlaneY = selectionNavigationY;
        }

        return TryGetWorldPositionOnNavigationPlane(
            screenPosition,
            navigationPlaneY,
            out worldDestination
        );
    }


    private bool TryGetWorldPositionOnNavigationPlane(
        Vector2 screenPosition,
        float navigationPlaneY,
        out Vector3 worldPosition
    )
    {
        worldPosition = Vector3.zero;

        if (commandCamera == null)
        {
            return false;
        }

        Ray clickRay = commandCamera.ScreenPointToRay(screenPosition);
        Plane navigationPlane = new Plane(
            Vector3.up,
            new Vector3(0f, navigationPlaneY, 0f)
        );

        if (!navigationPlane.Raycast(clickRay, out float enter))
        {
            return false;
        }

        worldPosition = clickRay.GetPoint(enter);
        return true;
    }


    private static bool TryGetHeadingBetween(
        Vector3 fromPosition,
        Vector3 toPosition,
        out float heading
    )
    {
        Vector3 horizontalOffset = toPosition - fromPosition;
        horizontalOffset.y = 0f;

        if (horizontalOffset.sqrMagnitude <= 0.0001f)
        {
            heading = 0f;
            return false;
        }

        heading = Mathf.Repeat(
            Mathf.Atan2(horizontalOffset.x, horizontalOffset.z)
                * Mathf.Rad2Deg,
            360f
        );
        return true;
    }


    private bool CanUseFormationPlacementSnapshot()
    {
        if (commandCamera == null
            || commandDispatcher == null
            || formationCommandController == null
            || rightMouseDownGeometrySnapshot == null
            || rightMouseDownGeometrySnapshot.Members.Count < 2)
        {
            return false;
        }

        foreach (FormationGeometryMember member
                 in rightMouseDownGeometrySnapshot.Members)
        {
            if (member.Ship == null || !member.Ship.isActiveAndEnabled)
            {
                return false;
            }
        }

        return true;
    }


    private FormationPlacementPreviewRenderer
        GetOrCreateFormationPlacementPreviewRenderer()
    {
        if (formationPlacementPreviewRenderer == null)
        {
            formationPlacementPreviewRenderer = GetComponent<
                FormationPlacementPreviewRenderer
            >();
        }

        if (formationPlacementPreviewRenderer == null)
        {
            formationPlacementPreviewRenderer = gameObject.AddComponent<
                FormationPlacementPreviewRenderer
            >();
        }

        return formationPlacementPreviewRenderer;
    }


    private void CancelDestinationGesture()
    {
        rightPlacementGestureActive = false;
        rightMouseDownFormationSnapshotCaptured = false;
        rightMouseDownGeometrySnapshot = null;
        formationPlacementPreviewActive = false;
        HideFormationPlacementPreview();
    }


    private void HideFormationPlacementPreview()
    {
        if (formationPlacementPreviewRenderer != null)
        {
            formationPlacementPreviewRenderer.HidePreview();
        }

        previewGhostMeshCount = 0;
    }


    private void OnDisable()
    {
        CancelDestinationGesture();

        if (selectionManager != null)
        {
            selectionManager.SelectionMembershipChanged -=
                ClearPendingFormationTemplate;
        }
    }


    public void ToggleLineAheadTemplate()
    {
        if (pendingFormationTemplate == PendingFormationTemplate.LineAhead)
        {
            ClearPendingFormationTemplate();
            return;
        }

        if (selectionManager != null && selectionManager.SelectedCount >= 2)
        {
            pendingFormationTemplate = PendingFormationTemplate.LineAhead;
        }
    }


    public void StopSelectedShips()
    {
        ClearPendingFormationTemplate();

        if (commandDispatcher != null)
        {
            commandDispatcher.DispatchStopSelectedShips();
        }
    }


    public void ToggleRequestedFormationManeuverStyle()
    {
        if (selectionManager != null)
        {
            selectionManager.ToggleRequestedFormationManeuverStyle();
        }
    }


    private void ConsumePendingFormationTemplateIfGroupCommandCommitted()
    {
        if (commandDispatcher != null
            && commandDispatcher.LastDispatchResult
                == ShipCommandDispatcher.DispatchResult.RequiresFormation)
        {
            ClearPendingFormationTemplate();
        }
    }


    private void ClearPendingFormationTemplate()
    {
        pendingFormationTemplate = PendingFormationTemplate.None;
    }


    private void ToggleHoveredFormationLead()
    {
        if (selectionManager == null
            || commandCamera == null
            || Mouse.current == null
            || !selectionManager.TryPickShip(
                commandCamera,
                Mouse.current.position.ReadValue(),
                out ShipDestinationController hoveredShip
            ))
        {
            return;
        }

        if (selectionManager.DesignatedFormationLead == hoveredShip)
        {
            selectionManager.ClearDesignatedFormationLead();
            return;
        }

        selectionManager.TrySetDesignatedFormationLead(hoveredShip);
    }


    private static bool IsShiftHeld()
    {
        return Keyboard.current != null
            && (Keyboard.current.leftShiftKey.isPressed
                || Keyboard.current.rightShiftKey.isPressed);
    }


    private static ShipDestinationController.TurnSelectionMode GetSelectionMode()
    {
        if (Keyboard.current == null)
        {
            return ShipDestinationController.TurnSelectionMode.Auto;
        }

        bool counterClockwiseHeld = Keyboard.current.cKey.isPressed;
        bool clockwiseHeld = Keyboard.current.vKey.isPressed;

        if (counterClockwiseHeld == clockwiseHeld)
        {
            return ShipDestinationController.TurnSelectionMode.Auto;
        }

        return counterClockwiseHeld
            ? ShipDestinationController.TurnSelectionMode.ForceCounterClockwise
            : ShipDestinationController.TurnSelectionMode.ForceClockwise;
    }
}
