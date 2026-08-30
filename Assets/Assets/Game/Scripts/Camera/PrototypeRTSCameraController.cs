using UnityEngine;
using UnityEngine.InputSystem;

public class PrototypeRTSCameraController : MonoBehaviour
{
    [Header("Movement Settings")]

    [SerializeField]
    private float panSpeed = 30f;

    [SerializeField]
    private float zoomSpeed = 80f;

    [SerializeField]
    private float minimumHeight = 15f;

    [SerializeField]
    private float maximumHeight = 120f;


    [Header("Focus")]

    [SerializeField]
    private Transform focusTarget;


    private void Update()
    {
        HandlePan();
        HandleZoom();
        HandleFocus();
    }


    private void HandlePan()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        float horizontalInput = 0f;
        float verticalInput = 0f;

        if (Keyboard.current.aKey.isPressed
            || Keyboard.current.leftArrowKey.isPressed)
        {
            horizontalInput -= 1f;
        }

        if (Keyboard.current.dKey.isPressed
            || Keyboard.current.rightArrowKey.isPressed)
        {
            horizontalInput += 1f;
        }

        if (Keyboard.current.sKey.isPressed
            || Keyboard.current.downArrowKey.isPressed)
        {
            verticalInput -= 1f;
        }

        if (Keyboard.current.wKey.isPressed
            || Keyboard.current.upArrowKey.isPressed)
        {
            verticalInput += 1f;
        }

        Vector3 horizontalForward = Vector3.ProjectOnPlane(
            transform.forward,
            Vector3.up
        );
        Vector3 horizontalRight = Vector3.ProjectOnPlane(
            transform.right,
            Vector3.up
        );

        if (horizontalForward.sqrMagnitude <= 0.0001f
            || horizontalRight.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        horizontalForward.Normalize();
        horizontalRight.Normalize();

        Vector3 movement = horizontalRight * horizontalInput
            + horizontalForward * verticalInput;

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        transform.position += movement * panSpeed * Time.deltaTime;
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
            || !Keyboard.current.fKey.wasPressedThisFrame)
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
