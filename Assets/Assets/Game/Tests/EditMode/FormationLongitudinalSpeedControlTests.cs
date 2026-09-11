using NUnit.Framework;
using UnityEngine;

public class FormationLongitudinalSpeedControlTests
{
    private const float ReferenceSpeed = 3.6f;
    private const float AvailableSpeed = 4.2f;
    private const float Deadband = 4f;
    private const float FullDistance = 40f;


    [Test]
    public void FormationForwardError_UsesSlotMinusShipProjectedOnFormationForward()
    {
        float lagging = FormationCommandController.CalculateFormationForwardError(
            new Vector3(0f, 0f, 8f),
            Vector3.zero,
            Vector3.forward
        );
        float aligned = FormationCommandController.CalculateFormationForwardError(
            Vector3.zero,
            Vector3.zero,
            Vector3.forward
        );
        float ahead = FormationCommandController.CalculateFormationForwardError(
            new Vector3(0f, 0f, -8f),
            Vector3.zero,
            Vector3.forward
        );

        Assert.That(lagging, Is.EqualTo(8f));
        Assert.That(aligned, Is.EqualTo(0f));
        Assert.That(ahead, Is.EqualTo(-8f));
    }


    [Test]
    public void ReformingHeading_AheadMemberUsesBoundedLateralCorrection()
    {
        const float formationHeading = 310f;
        float desiredHeading = FormationCommandController
            .CalculateReformingDesiredHeading(
                formationHeading,
                -25f,
                18f,
                3f,
                30f
            );
        float longitudinalOnlyHeading = FormationCommandController
            .CalculateReformingDesiredHeading(
                formationHeading,
                -25f,
                0f,
                3f,
                30f
            );

        Assert.That(Mathf.DeltaAngle(formationHeading, desiredHeading),
            Is.EqualTo(30f).Within(0.001f));
        Assert.That(longitudinalOnlyHeading,
            Is.EqualTo(formationHeading).Within(0.001f));
        Assert.That(ShipManeuverPlanner.ClassifyDirectedArc(
            formationHeading,
            desiredHeading,
            TurnDirection.Clockwise,
            Vector3.forward
        ), Is.EqualTo(ShipManeuverPlanner.ManeuverType.NormalTurn));
    }


    [Test]
    public void DesiredFormationSpeed_AlignedMemberUsesReferenceSpeed()
    {
        float desiredSpeed = Calculate(0f);

        Assert.That(desiredSpeed, Is.EqualTo(ReferenceSpeed).Within(0.001f));
    }


    [Test]
    public void DesiredFormationSpeed_InsideDeadbandRetainsReferenceSpeed()
    {
        float desiredSpeed = Calculate(Deadband - 0.01f);

        Assert.That(desiredSpeed, Is.EqualTo(ReferenceSpeed).Within(0.001f));
    }


    [Test]
    public void DesiredFormationSpeed_LaggingMemberUsesAvailableCatchUpHeadroom()
    {
        float desiredSpeed = Calculate(20f);

        Assert.That(desiredSpeed, Is.GreaterThan(ReferenceSpeed));
        Assert.That(desiredSpeed, Is.LessThanOrEqualTo(AvailableSpeed));
    }


    [Test]
    public void DesiredFormationSpeed_LaggingAtFullDistanceUsesAvailableSpeed()
    {
        float desiredSpeed = Calculate(FullDistance);

        Assert.That(desiredSpeed, Is.EqualTo(AvailableSpeed).Within(0.001f));
    }


    [Test]
    public void DesiredFormationSpeed_LaggingWithoutHeadroomDoesNotExceedAvailableSpeed()
    {
        float desiredSpeed = Calculate(FullDistance, ReferenceSpeed);

        Assert.That(desiredSpeed, Is.EqualTo(ReferenceSpeed).Within(0.001f));
    }


    [Test]
    public void DesiredFormationSpeed_AheadMemberSlowsBelowReferenceSpeed()
    {
        float desiredSpeed = Calculate(-20f);

        Assert.That(desiredSpeed, Is.LessThan(ReferenceSpeed));
        Assert.That(desiredSpeed, Is.GreaterThan(0f));
    }


    [Test]
    public void DesiredFormationSpeed_FarAheadMemberStopsWithoutReverseSpeed()
    {
        float desiredSpeed = Calculate(-FullDistance);

        Assert.That(desiredSpeed, Is.EqualTo(0f).Within(0.001f));
    }


    [Test]
    public void DesiredFormationSpeed_ReferenceAboveAvailabilityClampsToAvailableSpeed()
    {
        float desiredSpeed = FormationCommandController
            .CalculateDesiredFormationSpeed(
                0f,
                4f,
                3f,
                Deadband,
                FullDistance,
                FullDistance
            );

        Assert.That(desiredSpeed, Is.EqualTo(3f).Within(0.001f));
    }


    [TestCase("Assisted")]
    [TestCase("Direct")]
    [TestCase("Manual")]
    public void StationKeeping_AllNavigationControlModesUseSameSpeedRule(string mode)
    {
        float desiredSpeed = Calculate(20f);

        Assert.That(desiredSpeed, Is.GreaterThan(ReferenceSpeed),
            $"{mode} Formation navigation must retain longitudinal station keeping.");
    }


    [Test]
    public void StationKeeping_ExecutingManeuverSuspendsFormationSpeedCaps()
    {
        bool applies = FormationCommandController
            .ShouldApplyLongitudinalStationKeeping(
                FormationCommandController.FormationManeuverState.Executing
            );

        Assert.That(applies, Is.False);
    }


    [Test]
    public void StationKeeping_ReformingRestoresFormationSpeedCaps()
    {
        bool applies = FormationCommandController
            .ShouldApplyLongitudinalStationKeeping(
                FormationCommandController.FormationManeuverState.Reforming
            );

        Assert.That(applies, Is.True);
    }


    [Test]
    public void AnchorMoveSpeed_ReformingIgnoresSlowedMemberWhileNormalMovementRetainsMinimum()
    {
        const float formationReferenceSpeed = 3.6f;
        const float stationKeepingSlowedMemberSpeed = 1.4f;

        float reformingSpeed = FormationCommandController
            .CalculateFormationAnchorMoveSpeed(
                FormationCommandController.FormationManeuverState.Reforming,
                formationReferenceSpeed,
                stationKeepingSlowedMemberSpeed
            );
        float normalSpeed = FormationCommandController
            .CalculateFormationAnchorMoveSpeed(
                FormationCommandController.FormationManeuverState.None,
                formationReferenceSpeed,
                stationKeepingSlowedMemberSpeed
            );

        Assert.That(reformingSpeed,
            Is.EqualTo(formationReferenceSpeed).Within(0.001f));
        Assert.That(normalSpeed,
            Is.EqualTo(stationKeepingSlowedMemberSpeed).Within(0.001f));
    }


    private static float Calculate(
        float forwardError,
        float availableSpeed = AvailableSpeed
    )
    {
        return FormationCommandController.CalculateDesiredFormationSpeed(
            forwardError,
            ReferenceSpeed,
            availableSpeed,
            Deadband,
            FullDistance,
            FullDistance
        );
    }
}
