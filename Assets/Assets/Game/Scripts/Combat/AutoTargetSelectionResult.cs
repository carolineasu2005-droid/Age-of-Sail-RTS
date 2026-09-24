public readonly struct AutoTargetSelectionResult
{
    internal AutoTargetSelectionResult(
        AutoTargetSideSelectionResult portSelection,
        AutoTargetSideSelectionResult starboardSelection
    )
    {
        PortSelection = portSelection;
        StarboardSelection = starboardSelection;
    }


    public AutoTargetSideSelectionResult PortSelection { get; }

    public AutoTargetSideSelectionResult StarboardSelection { get; }

    public bool HasAnyTarget =>
        PortSelection.HasTarget || StarboardSelection.HasTarget;
}
