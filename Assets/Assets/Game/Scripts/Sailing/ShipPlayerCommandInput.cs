using UnityEngine;
using UnityEngine.InputSystem;

public class ShipPlayerCommandInput : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Camera commandCamera;

    [SerializeField]
    private ShipDestinationController destinationController;


    [Header("Runtime Debug")]

    [SerializeField]
    private bool lastClickValid;

    [SerializeField]
    private bool destinationControllerAvailable;

    [SerializeField]
    private bool rightMouseDetectedThisFrame;

    [SerializeField]
    private Vector2 lastMouseScreenPosition;

    [SerializeField]
    private Vector3 lastWorldDestination;

    [SerializeField]
    private ShipDestinationController.TurnSelectionMode lastSelectionMode;

    [SerializeField]
    private bool cameraAvailable;


    private void Awake()
    {
        if (commandCamera == null)
        {
            commandCamera = Camera.main;
        }
    }


    private void Update()
    {
        if (commandCamera == null)
        {
            commandCamera = Camera.main;
        }

        cameraAvailable = commandCamera != null;
        destinationControllerAvailable = destinationController != null;
        rightMouseDetectedThisFrame = Mouse.current != null
            && Mouse.current.rightButton.wasPressedThisFrame;

        if (!rightMouseDetectedThisFrame)
        {
            return;
        }

        lastClickValid = false;
        lastMouseScreenPosition = Mouse.current.position.ReadValue();
        lastSelectionMode = GetSelectionMode();

        if (commandCamera == null || destinationController == null)
        {
            return;
        }

        Ray clickRay = commandCamera.ScreenPointToRay(
            lastMouseScreenPosition
        );
        Plane navigationPlane = new Plane(
            Vector3.up,
            new Vector3(0f, destinationController.transform.position.y, 0f)
        );

        if (!navigationPlane.Raycast(clickRay, out float enter))
        {
            return;
        }

        lastClickValid = true;
        lastWorldDestination = clickRay.GetPoint(enter);
        destinationController.SetDestination(
            lastWorldDestination,
            lastSelectionMode
        );
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
