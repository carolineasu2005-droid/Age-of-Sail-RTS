using UnityEngine;
using UnityEngine.InputSystem;

public class PrototypeRTSCameraController : MonoBehaviour
{
    public static bool IsCameraFocusKey(Key key)
    {
        return key == Key.Home;
    }


    [Header("Movement Settings")]

    [SerializeField]
    private float panSpeed = 30f;

    [SerializeField]
    private float zoomSpeed = 80f;

    [SerializeField]
    private float minimumHeight = 15f;

    [SerializeField]
    private float maximumHeight = 120f;

    [SerializeField]
    [Min(1f)]
    private float edgeScrollThresholdPixels = 12f;

    [SerializeField]
    private float navigationPlaneY;


    [Header("Focus")]

    [SerializeField]
    private Transform focusTarget;

    private Camera controlledCamera;
    private bool middleMouseDragging;
    private Vector3 middleMouseGrabWorldPoint;


    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
    }


    private void Update()
    {
        HandlePan();
        HandleZoom();
        HandleFocus();
    }


    private void HandlePan()
    {
        if (Mouse.current == null || !Application.isFocused)
        {
            return;
        }

        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            middleMouseDragging = TryGetWorldPointOnNavigationPlane(
                Mouse.current.position.ReadValue(),
                out middleMouseGrabWorldPoint
            );
        }

        if (middleMouseDragging)
        {
            if (!Mouse.current.middleButton.isPressed)
            {
                middleMouseDragging = false;
                return;
            }

            if (Mouse.current.middleButton.isPressed
                && TryGetWorldPointOnNavigationPlane(
                    Mouse.current.position.ReadValue(),
                    out Vector3 currentWorldPoint
                ))
            {
                transform.position += middleMouseGrabWorldPoint
                    - currentWorldPoint;
            }

            return;
        }

        Vector2 edgeInput = CalculateEdgeScrollInput(
            Mouse.current.position.ReadValue(),
            Screen.width,
            Screen.height,
            edgeScrollThresholdPixels
        );

        if (edgeInput.sqrMagnitude <= 0f
            || !TryGetHorizontalCameraBasis(
                transform.forward,
                transform.right,
                out Vector3 horizontalForward,
                out Vector3 horizontalRight
            ))
        {
            return;
        }

        Vector3 movement = horizontalRight * edgeInput.x
            + horizontalForward * edgeInput.y;

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        transform.position += movement * panSpeed * Time.deltaTime;
    }


    public static Vector2 CalculateEdgeScrollInput(
        Vector2 mousePosition,
        int screenWidth,
        int screenHeight,
        float thresholdPixels
    )
    {
        if (screenWidth <= 0 || screenHeight <= 0 || thresholdPixels <= 0f
            || mousePosition.x < 0f || mousePosition.y < 0f
            || mousePosition.x > screenWidth || mousePosition.y > screenHeight)
        {
            return Vector2.zero;
        }

        float horizontal = mousePosition.x <= thresholdPixels
            ? -1f
            : mousePosition.x >= screenWidth - thresholdPixels ? 1f : 0f;
        float vertical = mousePosition.y <= thresholdPixels
            ? -1f
            : mousePosition.y >= screenHeight - thresholdPixels ? 1f : 0f;
        return new Vector2(horizontal, vertical);
    }


    public static bool TryGetHorizontalCameraBasis(
        Vector3 cameraForward,
        Vector3 cameraRight,
        out Vector3 horizontalForward,
        out Vector3 horizontalRight
    )
    {
        horizontalForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
        horizontalRight = Vector3.ProjectOnPlane(cameraRight, Vector3.up);

        if (horizontalForward.sqrMagnitude <= 0.0001f
            || horizontalRight.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        horizontalForward.Normalize();
        horizontalRight.Normalize();
        return true;
    }


    private bool TryGetWorldPointOnNavigationPlane(
        Vector2 screenPosition,
        out Vector3 worldPoint
    )
    {
        worldPoint = Vector3.zero;

        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        if (controlledCamera == null)
        {
            return false;
        }

        Ray ray = controlledCamera.ScreenPointToRay(screenPosition);
        Plane navigationPlane = new Plane(
            Vector3.up,
            new Vector3(0f, navigationPlaneY, 0f)
        );

        if (!navigationPlane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldPoint = ray.GetPoint(enter);
        return true;
    }


    private void HandleZoom()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scrollInput = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Approximately(scrollInput, 0f))
        {
            return;
        }

        Vector3 position = transform.position;
        position.y = Mathf.Clamp(
            position.y - scrollInput * zoomSpeed * Time.deltaTime,
            minimumHeight,
            maximumHeight
        );
        transform.position = position;
    }


    private void HandleFocus()
    {
        if (focusTarget == null
            || Keyboard.current == null
            || !Keyboard.current.homeKey.wasPressedThisFrame)
        {
            return;
        }

        Ray centerRay = new Ray(transform.position, transform.forward);
        Plane targetPlane = new Plane(Vector3.up, focusTarget.position);
        Vector3 position = transform.position;

        if (targetPlane.Raycast(centerRay, out float enter))
        {
            Vector3 centerPoint = centerRay.GetPoint(enter);
            Vector3 centerOffset = centerPoint - transform.position;
            position.x = focusTarget.position.x - centerOffset.x;
            position.z = focusTarget.position.z - centerOffset.z;
        }
        else
        {
            position.x = focusTarget.position.x;
            position.z = focusTarget.position.z;
        }

        transform.position = position;
    }
}
