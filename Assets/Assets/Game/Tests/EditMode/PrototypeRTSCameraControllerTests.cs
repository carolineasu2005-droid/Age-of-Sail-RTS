using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public class PrototypeRTSCameraControllerTests
{
    [Test]
    public void CameraFocusHotkey_UsesHomeAndExcludesFormationLeadAndStopKeys()
    {
        Assert.That(PrototypeRTSCameraController.IsCameraFocusKey(Key.Home),
            Is.True);
        Assert.That(PrototypeRTSCameraController.IsCameraFocusKey(Key.F),
            Is.False);
        Assert.That(PrototypeRTSCameraController.IsCameraFocusKey(Key.S),
            Is.False);
    }

    [Test]
    public void CalculateEdgeScrollInput_ReturnsExpectedCardinalAndCornerDirections()
    {
        Assert.That(PrototypeRTSCameraController.CalculateEdgeScrollInput(
            new Vector2(500f, 1075f), 1000, 1080, 12f),
            Is.EqualTo(Vector2.up));
        Assert.That(PrototypeRTSCameraController.CalculateEdgeScrollInput(
            new Vector2(5f, 5f), 1000, 1080, 12f),
            Is.EqualTo(new Vector2(-1f, -1f)));
        Assert.That(PrototypeRTSCameraController.CalculateEdgeScrollInput(
            new Vector2(500f, 500f), 1000, 1080, 12f),
            Is.EqualTo(Vector2.zero));
    }


    [Test]
    public void CalculateEdgeScrollInput_RejectsPointerOutsideScreenBounds()
    {
        Assert.That(PrototypeRTSCameraController.CalculateEdgeScrollInput(
            new Vector2(-1f, 500f), 1000, 1080, 12f),
            Is.EqualTo(Vector2.zero));
        Assert.That(PrototypeRTSCameraController.CalculateEdgeScrollInput(
            new Vector2(1001f, 500f), 1000, 1080, 12f),
            Is.EqualTo(Vector2.zero));
    }


    [Test]
    public void TryGetHorizontalCameraBasis_ProjectsAndNormalizesCameraAxes()
    {
        bool valid = PrototypeRTSCameraController.TryGetHorizontalCameraBasis(
            new Vector3(0f, -1f, 1f),
            Vector3.right,
            out Vector3 forward,
            out Vector3 right
        );

        Assert.That(valid, Is.True);
        Assert.That(forward, Is.EqualTo(Vector3.forward));
        Assert.That(right, Is.EqualTo(Vector3.right));
    }


    [Test]
    public void TryGetHorizontalCameraBasis_RejectsVerticalCameraAxes()
    {
        bool valid = PrototypeRTSCameraController.TryGetHorizontalCameraBasis(
            Vector3.up,
            Vector3.right,
            out _,
            out _
        );

        Assert.That(valid, Is.False);
    }
}
