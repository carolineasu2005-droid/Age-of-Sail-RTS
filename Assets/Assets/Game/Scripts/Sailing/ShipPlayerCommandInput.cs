using UnityEngine;
using UnityEngine.InputSystem;

public class ShipPlayerCommandInput : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Camera commandCamera;

    [SerializeField]
    private ShipSelectionManager selectionManager;

    [SerializeField]
    private ShipCommandDispatcher commandDispatcher;


    [Header("Navigation Settings")]

    [SerializeField]
    private float defaultNavigationPlaneY;

    [SerializeField]
    private WindNavigationAssistMode windNavigationAssistMode =
        WindNavigationAssistMode.Assisted;


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
    private Vector2 lastSelectionClickPosition;

    [SerializeField]
    private bool lastSelectionPickValid;

    [SerializeField]
    private ShipDestinationController lastSelectedShip;

    [SerializeField]
    private bool rightMouseDetectedThisFrame;

    [SerializeField]
    private bool lastClickValid;

    [SerializeField]
    private Vector2 lastMouseScreenPosition;

    [SerializeField]
    private Vector3 lastWorldDestination;

    [SerializeField]
    private ShipDestinationController.TurnSelectionMode lastSelectionMode;


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
        rightMouseDetectedThisFrame = Mouse.current != null
            && Mouse.current.rightButton.wasPressedThisFrame;

        if (leftMouseDetectedThisFrame)
        {
            HandleSelectionClick();
        }

        if (rightMouseDetectedThisFrame)
        {
            HandleDestinationClick();
        }
    }


    private void HandleSelectionClick()
    {
        lastSelectionPickValid = false;
        lastSelectedShip = null;
        lastSelectionClickPosition = Mouse.current.position.ReadValue();

        if (commandCamera == null || selectionManager == null)
        {
            return;
        }

        bool shiftHeld = IsShiftHeld();

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


    private void HandleDestinationClick()
    {
        lastClickValid = false;
        lastMouseScreenPosition = Mouse.current.position.ReadValue();
        lastSelectionMode = GetSelectionMode();

        if (commandCamera == null || commandDispatcher == null)
        {
            return;
        }

        float navigationPlaneY = defaultNavigationPlaneY;

        if (selectionManager != null
            && selectionManager.TryGetSelectionNavigationY(
                out float selectionNavigationY
            ))
        {
            navigationPlaneY = selectionNavigationY;
        }

        Ray clickRay = commandCamera.ScreenPointToRay(
            lastMouseScreenPosition
        );
        Plane navigationPlane = new Plane(
            Vector3.up,
            new Vector3(0f, navigationPlaneY, 0f)
        );

        if (!navigationPlane.Raycast(clickRay, out float enter))
        {
            return;
        }

        lastClickValid = true;
        lastWorldDestination = clickRay.GetPoint(enter);
        commandDispatcher.DispatchDestination(
            lastWorldDestination,
            lastSelectionMode,
            windNavigationAssistMode
        );
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
