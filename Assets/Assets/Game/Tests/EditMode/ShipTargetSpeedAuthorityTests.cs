using NUnit.Framework;

public class ShipTargetSpeedAuthorityTests
{
    [Test]
    public void ComposeEffectiveTargetSpeed_WithoutExternalCapsUsesAvailableSpeed()
    {
        float effective = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, false, 0f, false, 0f);

        Assert.That(effective, Is.EqualTo(4.2f));
    }


    [Test]
    public void ComposeEffectiveTargetSpeed_FormationCapBelowAvailableLimitsSpeed()
    {
        float effective = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, true, 2.5f, false, 0f);

        Assert.That(effective, Is.EqualTo(2.5f));
    }


    [Test]
    public void ComposeEffectiveTargetSpeed_FormationCapAboveAvailableDoesNotIncreaseSpeed()
    {
        float effective = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, true, 8f, false, 0f);

        Assert.That(effective, Is.EqualTo(4.2f));
    }


    [Test]
    public void ComposeEffectiveTargetSpeed_PlayerStopCapZeroStopsTargetWithoutManeuverFloor()
    {
        float effective = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, false, 0f, true, 0f);

        Assert.That(effective, Is.EqualTo(0f));
    }


    [Test]
    public void ComposeEffectiveTargetSpeed_ClearingOneAuthorityLeavesOtherActive()
    {
        float formationOnly = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, true, 3f, false, 0f);
        float stopOnly = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4.2f, false, 0f, false, 0f, true, 0f);

        Assert.That(formationOnly, Is.EqualTo(3f));
        Assert.That(stopOnly, Is.EqualTo(0f));
    }


    [Test]
    public void ComposeEffectiveTargetSpeed_ClampsNegativeInputsWithoutReverseSpeed()
    {
        float effective = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            -2f, false, 0f, true, -1f, true, -3f);

        Assert.That(effective, Is.EqualTo(0f));
    }


    [Test]
    public void ComposeEffectiveTargetSpeed_PreservesManeuverFloorBeforeExternalCaps()
    {
        float effective = ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            0.4f, true, 1f, true, 0.8f, false, 0f);

        Assert.That(effective, Is.EqualTo(0.8f));
    }

    [Test]
    public void PlayerOrder_CapsNormalAvailableSpeed()
    {
        Assert.That(ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4f, 0.5f, false, 0f, false, 0f, false, 0f
        ), Is.EqualTo(2f));
    }

    [Test]
    public void ManeuverMinimum_CanExceedLowPlayerOrder()
    {
        Assert.That(ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4f, 0.25f, true, 1.6f, false, 0f, false, 0f
        ), Is.EqualTo(1.6f));
    }

    [Test]
    public void FormationMaximum_AppliesAfterManeuverMinimum()
    {
        Assert.That(ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4f, 0.25f, true, 2f, true, 1.4f, false, 0f
        ), Is.EqualTo(1.4f));
    }

    [Test]
    public void PlayerStopZero_WinsOverPlayerOrderManeuverAndFormation()
    {
        Assert.That(ShipSailingSpeed.ComposeEffectiveTargetSpeed(
            4f, 0.25f, true, 2f, true, 1.4f, true, 0f
        ), Is.EqualTo(0f));
    }
}
