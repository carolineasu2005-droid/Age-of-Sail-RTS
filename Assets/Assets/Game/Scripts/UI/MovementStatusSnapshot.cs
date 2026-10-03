using System.Text;
using UnityEngine;

// A presentation copy. Movement owners and the preview session remain authoritative.
public readonly struct MovementStatusSnapshot
{
    internal MovementStatusSnapshot(
        int selectionCount, ShipDestinationController ship,
        float playerSpeedOrder, float currentSpeed, float availableTargetSpeed,
        float effectiveTargetSpeed, bool playerStopped, float physicalHeading,
        float courseHeading, float relativeWindAngleSigned,
        ShipManeuverPlanner.ManeuverType plannerManeuver, bool plannerActive,
        bool tackActive, bool wearActive, bool previewActive,
        float previewDelta, float previewTargetHeading,
        TurnDirection previewDirection,
        ShipManeuverPlanner.ManeuverType previewManeuver,
        bool previewClassificationAvailable)
    {
        SelectionCount = selectionCount;
        Ship = ship;
        PlayerSpeedOrder = playerSpeedOrder;
        CurrentSpeed = currentSpeed;
        AvailableTargetSpeed = availableTargetSpeed;
        EffectiveTargetSpeed = effectiveTargetSpeed;
        PlayerStopped = playerStopped;
        PhysicalHeading = physicalHeading;
        CourseHeading = courseHeading;
        RelativeWindAngleSigned = relativeWindAngleSigned;
        PlannerManeuver = plannerManeuver;
        PlannerActive = plannerActive;
        TackActive = tackActive;
        WearActive = wearActive;
        PreviewActive = previewActive;
        PreviewDelta = previewDelta;
        PreviewTargetHeading = previewTargetHeading;
        PreviewDirection = previewDirection;
        PreviewManeuver = previewManeuver;
        PreviewClassificationAvailable = previewClassificationAvailable;
    }

    public int SelectionCount { get; }
    public ShipDestinationController Ship { get; }
    public bool HasControllableShip => SelectionCount == 1 && Ship != null;
    public float PlayerSpeedOrder { get; }
    public float CurrentSpeed { get; }
    public float AvailableTargetSpeed { get; }
    public float EffectiveTargetSpeed { get; }
    public bool PlayerStopped { get; }
    public float PhysicalHeading { get; }
    public float CourseHeading { get; }
    public float RelativeWindAngleSigned { get; }
    public ShipManeuverPlanner.ManeuverType PlannerManeuver { get; }
    public bool PlannerActive { get; }
    public bool TackActive { get; }
    public bool WearActive { get; }
    public bool PreviewActive { get; }
    public float PreviewDelta { get; }
    public float PreviewTargetHeading { get; }
    public TurnDirection PreviewDirection { get; }
    public ShipManeuverPlanner.ManeuverType PreviewManeuver { get; }
    public bool PreviewClassificationAvailable { get; }
}

public static class MovementStatusReadModel
{
    public static MovementStatusSnapshot Capture(
        ShipSelectionManager selection,
        ShipDirectedHeadingPreviewController preview)
    {
        int count = selection != null ? selection.SelectedCount : 0;
        if (count != 1)
        {
            return new MovementStatusSnapshot(count, null, 0f, 0f, 0f, 0f,
                false, 0f, 0f, 0f, default, false, false, false,
                false, 0f, 0f, default, default, false);
        }

        ShipDestinationController ship = selection.PrimarySelectedShip;
        if (ship == null || !ship.isActiveAndEnabled
            || !ship.gameObject.activeInHierarchy)
        {
            return new MovementStatusSnapshot(count, null, 0f, 0f, 0f, 0f,
                false, 0f, 0f, 0f, default, false, false, false,
                false, 0f, 0f, default, default, false);
        }

        ShipSailingSpeed speed = ship.GetComponent<ShipSailingSpeed>();
        ShipManeuverPlanner planner = ship.GetComponent<ShipManeuverPlanner>();
        if (speed == null || !speed.isActiveAndEnabled
            || planner == null || !planner.isActiveAndEnabled)
        {
            return new MovementStatusSnapshot(count, null, 0f, 0f, 0f, 0f,
                false, 0f, 0f, 0f, default, false, false, false,
                false, 0f, 0f, default, default, false);
        }

        ShipTacking tack = ship.GetComponent<ShipTacking>();
        ShipWearing wear = ship.GetComponent<ShipWearing>();
        bool hasPreview = preview != null && preview.IsPreviewActive
            && preview.PreviewShip == ship;
        return new MovementStatusSnapshot(
            count, ship, speed.PlayerSpeedOrderNormalized,
            speed.CurrentSpeed, speed.AvailableTargetSpeed,
            speed.EffectiveTargetSpeed, speed.IsPlayerStopped,
            Mathf.Repeat(ship.transform.eulerAngles.y, 360f),
            speed.CourseHeading, speed.RelativeWindAngleSigned,
            planner.CurrentManeuver, planner.IsActive,
            tack != null && tack.IsActive, wear != null && wear.IsActive,
            hasPreview, hasPreview ? preview.AngularDelta : 0f,
            hasPreview ? preview.TargetHeading : 0f,
            hasPreview ? preview.Direction : default,
            hasPreview ? preview.PreviewManeuver : default,
            hasPreview && preview.IsClassificationAvailable);
    }
}

public static class MovementStatusFormatter
{
    public static string Format(MovementStatusSnapshot status)
    {
        if (status.SelectionCount == 0)
        {
            return "Movement Status\nNo ship selected";
        }

        if (status.SelectionCount > 1)
        {
            return "Movement Status\nSelect exactly one ship";
        }

        if (!status.HasControllableShip)
        {
            return "Movement Status\nSelected ship unavailable";
        }

        StringBuilder text = new(320);
        text.Append("Movement: ").AppendLine(status.Ship.name);
        text.Append("Speed Order: ")
            .AppendLine($"{status.PlayerSpeedOrder * 100f:0}%");
        text.Append("Current Speed: ").AppendLine($"{status.CurrentSpeed:0.0} m/s");
        text.Append("Available Target: ")
            .AppendLine($"{status.AvailableTargetSpeed:0.0} m/s");
        text.Append("Effective Target: ")
            .AppendLine($"{status.EffectiveTargetSpeed:0.0} m/s");
        text.Append("Player Stop: ").AppendLine(status.PlayerStopped ? "ON" : "OFF");
        text.Append("Heading: ").AppendLine($"{status.PhysicalHeading:0}°");
        text.Append("Course: ").AppendLine($"{status.CourseHeading:0}°");
        text.Append("Relative Wind: ")
            .AppendLine($"{status.RelativeWindAngleSigned:+0;-0;0}°");
        text.Append("Planner: ").Append(status.PlannerManeuver)
            .AppendLine(status.PlannerActive ? " (Active)" : " (Inactive)");
        text.Append("Tack: ").Append(status.TackActive ? "Active" : "Inactive")
            .Append("  Wear: ").Append(status.WearActive ? "Active" : "Inactive");
        if (status.PreviewActive)
        {
            text.AppendLine().Append("Turn Preview: ")
                .Append(status.PreviewDirection == TurnDirection.Clockwise
                    ? "CW " : "CCW ")
                .AppendLine($"{status.PreviewDelta:0}°");
            text.Append("Target Heading: ")
                .AppendLine($"{status.PreviewTargetHeading:0}°");
            text.Append("Predicted: ")
                .Append(status.PreviewClassificationAvailable
                    ? status.PreviewManeuver.ToString()
                    : "Unavailable");
        }

        return text.ToString();
    }
}
