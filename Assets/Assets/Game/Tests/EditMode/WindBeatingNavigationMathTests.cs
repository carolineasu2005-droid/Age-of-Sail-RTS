using NUnit.Framework;
using UnityEngine;

public class WindBeatingNavigationMathTests
{
    [Test]
    public void GetHorizontalBearing_UsesNorthEastHeadingConvention()
    {
        Assert.That(WindBeatingNavigationMath.GetHorizontalBearing(
            Vector3.zero,
            Vector3.forward,
            123f
        ), Is.EqualTo(0f).Within(0.001f));
        Assert.That(WindBeatingNavigationMath.GetHorizontalBearing(
            Vector3.zero,
            Vector3.right,
            123f
        ), Is.EqualTo(90f).Within(0.001f));
    }


    [Test]
    public void GetAbsoluteBearingRelativeToWind_ReturnsSmallestAbsoluteAngle()
    {
        Assert.That(WindBeatingNavigationMath.GetAbsoluteBearingRelativeToWind(
            350f,
            10f
        ), Is.EqualTo(20f).Within(0.001f));
    }


    [TestCase(44.9f, true)]
    [TestCase(45f, false)]
    [TestCase(45.1f, false)]
    public void ShouldEnterBeating_UsesStrictThreshold(
        float targetWindAngle,
        bool expected
    )
    {
        Assert.That(WindBeatingNavigationMath.ShouldEnterBeating(
            targetWindAngle,
            45f
        ), Is.EqualTo(expected));
    }


    [TestCase(49.9f, false)]
    [TestCase(50f, true)]
    [TestCase(50.1f, true)]
    public void ShouldResumeDirect_UsesInclusiveThreshold(
        float targetWindAngle,
        bool expected
    )
    {
        Assert.That(WindBeatingNavigationMath.ShouldResumeDirect(
            targetWindAngle,
            50f
        ), Is.EqualTo(expected));
    }


    [Test]
    public void GetCloseHauledCandidates_ReturnsExpectedHeadings()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            WindBeatingNavigationMath.GetCloseHauledCandidates(0f, 50f);

        Assert.That(candidates.PositiveHeading, Is.EqualTo(50f));
        Assert.That(candidates.NegativeHeading, Is.EqualTo(310f));
    }


    [Test]
    public void GetCloseHauledCandidates_NormalizesAcrossZeroDegrees()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            WindBeatingNavigationMath.GetCloseHauledCandidates(10f, 50f);

        Assert.That(candidates.PositiveHeading, Is.EqualTo(60f));
        Assert.That(candidates.NegativeHeading, Is.EqualTo(320f));
    }


    [Test]
    public void SelectInitialAutoCloseHauledHeading_UsesClosestDestinationLeg()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            new WindBeatingNavigationMath.CloseHauledCandidates(50f, 310f);

        Assert.That(WindBeatingNavigationMath
            .SelectInitialAutoCloseHauledHeading(40f, 180f, candidates),
            Is.EqualTo(50f));
        Assert.That(WindBeatingNavigationMath
            .SelectInitialAutoCloseHauledHeading(320f, 180f, candidates),
            Is.EqualTo(310f));
    }


    [Test]
    public void SelectInitialAutoCloseHauledHeading_UsesCurrentHeadingOnTie()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            new WindBeatingNavigationMath.CloseHauledCandidates(50f, 310f);

        Assert.That(WindBeatingNavigationMath
            .SelectInitialAutoCloseHauledHeading(0f, 45f, candidates),
            Is.EqualTo(50f));
        Assert.That(WindBeatingNavigationMath
            .SelectInitialAutoCloseHauledHeading(0f, 315f, candidates),
            Is.EqualTo(310f));
    }


    [Test]
    public void SelectCloseHauledHeadingForDirectedArc_RespectsTurnDirection()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            new WindBeatingNavigationMath.CloseHauledCandidates(50f, 310f);

        Assert.That(WindBeatingNavigationMath
            .SelectCloseHauledHeadingForDirectedArc(
                0f,
                candidates,
                TurnDirection.Clockwise
            ), Is.EqualTo(50f));
        Assert.That(WindBeatingNavigationMath
            .SelectCloseHauledHeadingForDirectedArc(
                0f,
                candidates,
                TurnDirection.CounterClockwise
            ), Is.EqualTo(310f));
    }


    [TestCase(10f, 6f)]
    [TestCase(100f, 8f)]
    [TestCase(500f, 15f)]
    public void CalculateDynamicCorridorHalfWidth_ClampsPrototypeValues(
        float distance,
        float expectedWidth
    )
    {
        Assert.That(WindBeatingNavigationMath
            .CalculateDynamicCorridorHalfWidth(distance, 0.08f, 6f, 15f),
            Is.EqualTo(expectedWidth).Within(0.001f));
    }


    [Test]
    public void ShouldSwitchCloseHauledLeg_PositiveLegUsesInclusivePositiveBoundary()
    {
        const float halfWidth = 6f;
        const float switchFactor = 0.85f;
        const float epsilon = 0.01f;
        float threshold = halfWidth * switchFactor;

        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            threshold - epsilon, 1f, halfWidth, switchFactor), Is.False);
        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            threshold, 1f, halfWidth, switchFactor), Is.True);
        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            threshold + epsilon, 1f, halfWidth, switchFactor), Is.True);
    }


    [Test]
    public void ShouldSwitchCloseHauledLeg_PositiveLegRejectsNegativeBoundary()
    {
        const float halfWidth = 6f;
        const float switchFactor = 0.85f;
        float threshold = halfWidth * switchFactor;

        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            -threshold, 1f, halfWidth, switchFactor), Is.False);
    }


    [Test]
    public void ShouldSwitchCloseHauledLeg_NegativeLegUsesInclusiveNegativeBoundary()
    {
        const float halfWidth = 6f;
        const float switchFactor = 0.85f;
        const float epsilon = 0.01f;
        float threshold = halfWidth * switchFactor;

        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            -threshold + epsilon, -1f, halfWidth, switchFactor), Is.False);
        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            -threshold, -1f, halfWidth, switchFactor), Is.True);
        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            -threshold - epsilon, -1f, halfWidth, switchFactor), Is.True);
    }


    [Test]
    public void ShouldSwitchCloseHauledLeg_NegativeLegRejectsPositiveBoundary()
    {
        const float halfWidth = 6f;
        const float switchFactor = 0.85f;
        float threshold = halfWidth * switchFactor;

        Assert.That(WindBeatingNavigationMath.ShouldSwitchCloseHauledLeg(
            threshold, -1f, halfWidth, switchFactor), Is.False);
    }


    [Test]
    public void GetOppositeCloseHauledHeading_ReturnsOtherCandidate()
    {
        WindBeatingNavigationMath.CloseHauledCandidates candidates =
            new WindBeatingNavigationMath.CloseHauledCandidates(50f, 310f);

        Assert.That(WindBeatingNavigationMath.GetOppositeCloseHauledHeading(
            50f,
            candidates
        ), Is.EqualTo(310f));
        Assert.That(WindBeatingNavigationMath.GetOppositeCloseHauledHeading(
            310f,
            candidates
        ), Is.EqualTo(50f));
    }
}
