using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipAutoTargetScorerTests
{
    private const float Tolerance = 0.0001f;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipCombatState shooterState;
    private ShipFireEligibility eligibility;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateShip("Shooter Root", true);
        targetRoot = CreateShip("Target Root", true);
        targetRoot.transform.position = Vector3.right * 50f;
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetRanges(100f, 200f);
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(targetRoot);
        Object.DestroyImmediate(shooterRoot);
    }


    [Test]
    public void IllegalCandidate_IsRejectedWithNonSelectableDefaultResult()
    {
        targetRoot.transform.position = Vector3.forward * 50f;
        FireEligibilityResult fireResult = EvaluateEligibility();

        bool scored = ShipAutoTargetScorer.TryEvaluate(
            shooterRoot,
            targetRoot,
            fireResult,
            out AutoTargetScoreResult score
        );

        Assert.That(fireResult.CanFire, Is.False);
        Assert.That(scored, Is.False);
        Assert.That(score.Selectable, Is.False);
        Assert.That(score.FinalScore, Is.Zero);
    }


    [Test]
    public void BroadsideExposure_ScoresHigherThanBowOnAtSameDistance()
    {
        targetRoot.transform.rotation = Quaternion.identity;
        AutoTargetScoreResult broadside = EvaluateScore();

        targetRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        AutoTargetScoreResult bowOn = EvaluateScore();

        Assert.That(broadside.ExposureNormalized, Is.EqualTo(1f)
            .Within(Tolerance));
        Assert.That(bowOn.ExposureNormalized, Is.EqualTo(1f / 3f)
            .Within(Tolerance));
        Assert.That(broadside.FinalScore, Is.GreaterThan(bowOn.FinalScore));
    }


    [Test]
    public void SameExposure_NearTargetScoresAtLeastFartherLegalTarget()
    {
        targetRoot.transform.position = Vector3.right * 50f;
        AutoTargetScoreResult near = EvaluateScore();

        targetRoot.transform.position = Vector3.right * 150f;
        AutoTargetScoreResult far = EvaluateScore();

        Assert.That(
            far.ExposureNormalized,
            Is.EqualTo(near.ExposureNormalized).Within(Tolerance)
        );
        Assert.That(near.FinalScore, Is.GreaterThanOrEqualTo(far.FinalScore));
    }


    [Test]
    public void InsideEffectiveRange_HasFullRangeQuality()
    {
        targetRoot.transform.position = Vector3.right * 75f;

        AutoTargetScoreResult score = EvaluateScore();

        Assert.That(
            score.RangeQualityNormalized,
            Is.EqualTo(1f).Within(Tolerance)
        );
    }


    [Test]
    public void BetweenEffectiveAndMaximum_HasLinearPartialRangeQuality()
    {
        targetRoot.transform.position = Vector3.right * 150f;

        AutoTargetScoreResult score = EvaluateScore();

        Assert.That(score.RangeQualityNormalized, Is.GreaterThan(0f));
        Assert.That(score.RangeQualityNormalized, Is.LessThan(1f));
        Assert.That(
            score.RangeQualityNormalized,
            Is.EqualTo(0.5f).Within(Tolerance)
        );
    }


    [Test]
    public void ExactMaximumRange_IsSelectableWithZeroRangeQuality()
    {
        targetRoot.transform.position = Vector3.right * 200f;
        FireEligibilityResult fireResult = EvaluateEligibility();

        AutoTargetScoreResult score = EvaluateScore(fireResult);

        Assert.That(fireResult.CanFire, Is.True);
        Assert.That(score.Selectable, Is.True);
        Assert.That(score.RangeQualityNormalized, Is.Zero);
        Assert.That(score.FinalScore, Is.Zero);
    }


    [Test]
    public void EqualEffectiveAndMaximumRange_IsSafeAndDeterministic()
    {
        SetRanges(100f, 100f);
        targetRoot.transform.position = Vector3.right * 100f;

        AutoTargetScoreResult first = EvaluateScore();
        AutoTargetScoreResult second = EvaluateScore();

        Assert.That(first.RangeQualityNormalized, Is.EqualTo(1f));
        Assert.That(second.RangeQualityNormalized, Is.EqualTo(1f));
        Assert.That(second.FinalScore, Is.EqualTo(first.FinalScore));
    }


    [Test]
    public void ScoreAndFactors_StayWithinNormalizedBounds()
    {
        Vector3[] positions =
        {
            Vector3.right * 25f,
            Vector3.right * 100f,
            Vector3.right * 150f,
            Vector3.right * 200f
        };
        float[] targetYawDegrees = { 0f, 35f, 90f };

        foreach (Vector3 position in positions)
        {
            foreach (float targetYaw in targetYawDegrees)
            {
                targetRoot.transform.position = position;
                targetRoot.transform.rotation = Quaternion.Euler(
                    0f,
                    targetYaw,
                    0f
                );
                AutoTargetScoreResult score = EvaluateScore();

                AssertNormalized(score.ExposureNormalized);
                AssertNormalized(score.RangeQualityNormalized);
                AssertNormalized(score.FinalScore);
            }
        }
    }


    [Test]
    public void UnrelatedCombatState_DoesNotChangeScore()
    {
        targetRoot.transform.position = Vector3.right * 150f;
        AutoTargetScoreResult before = EvaluateScore();

        shooterState.SetBlindFireEnabled(true);
        shooterState.SetAutoFireEnabled(true);
        shooterState.TryCommitBroadsideFire(CombatSide.Port);
        AutoTargetScoreResult after = EvaluateScore();

        Assert.That(
            after.ExposureNormalized,
            Is.EqualTo(before.ExposureNormalized).Within(Tolerance)
        );
        Assert.That(
            after.RangeQualityNormalized,
            Is.EqualTo(before.RangeQualityNormalized).Within(Tolerance)
        );
        Assert.That(
            after.FinalScore,
            Is.EqualTo(before.FinalScore).Within(Tolerance)
        );
    }


    [Test]
    public void RootTranslationAndRotation_AreRespectedThroughExposureApi()
    {
        AutoTargetScoreResult before = EvaluateScore();

        shooterRoot.transform.SetPositionAndRotation(
            new Vector3(120f, 3f, -75f),
            Quaternion.Euler(0f, 90f, 0f)
        );
        targetRoot.transform.SetPositionAndRotation(
            shooterRoot.transform.position
                + shooterRoot.transform.right * 50f,
            shooterRoot.transform.rotation
        );
        AutoTargetScoreResult after = EvaluateScore();

        Assert.That(
            after.ExposureNormalized,
            Is.EqualTo(before.ExposureNormalized).Within(Tolerance)
        );
        Assert.That(
            after.RangeQualityNormalized,
            Is.EqualTo(before.RangeQualityNormalized).Within(Tolerance)
        );
        Assert.That(
            after.FinalScore,
            Is.EqualTo(before.FinalScore).Within(Tolerance)
        );
    }


    [Test]
    public void Scoring_WorksWithoutRendererOrMesh()
    {
        Assert.That(targetRoot.GetComponentInChildren<Renderer>(), Is.Null);
        Assert.That(targetRoot.GetComponentInChildren<MeshFilter>(), Is.Null);

        AutoTargetScoreResult score = EvaluateScore();

        Assert.That(score.Selectable, Is.True);
        Assert.That(score.FinalScore, Is.Positive);
    }


    [Test]
    public void Scoring_DoesNotWriteRootPoseOrExposureReferences()
    {
        ShipExposureReference targetExposure =
            targetRoot.GetComponent<ShipExposureReference>();
        ShipArtDefinition artDefinition = targetExposure.ArtDefinition;
        Vector3 shooterPosition = shooterRoot.transform.position;
        Quaternion shooterRotation = shooterRoot.transform.rotation;
        Vector3 targetPosition = targetRoot.transform.position;
        Quaternion targetRotation = targetRoot.transform.rotation;
        Transform artReference = artDefinition.BowReference;
        Vector3 artLocalPosition = artReference.localPosition;

        EvaluateScore();

        Assert.That(shooterRoot.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(shooterRotation));
        Assert.That(targetRoot.transform.position, Is.EqualTo(targetPosition));
        Assert.That(targetRoot.transform.rotation, Is.EqualTo(targetRotation));
        Assert.That(artDefinition.BowReference, Is.SameAs(artReference));
        Assert.That(artReference.localPosition, Is.EqualTo(artLocalPosition));
    }


    [Test]
    public void ScoringSource_HasNoForbiddenGameplayOrPresentationDependency()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipAutoTargetScorer.cs"
        );
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Not.Contain("Renderer"));
        Assert.That(source, Does.Not.Contain("Mesh"));
        Assert.That(source, Does.Not.Contain("VisualRoot"));
        Assert.That(source, Does.Not.Contain("ShipCombatGeometry"));
        Assert.That(source, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("Integrity"));
        Assert.That(source, Does.Not.Contain("Raking"));
        Assert.That(source, Does.Not.Contain("Projectile"));
        Assert.That(source, Does.Not.Contain("transform.position ="));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
    }


    [Test]
    public void ResultSurface_IsImmutableAndFormulaIsExact()
    {
        targetRoot.transform.position = Vector3.right * 150f;
        targetRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        AutoTargetScoreResult score = EvaluateScore();

        Assert.That(
            score.FinalScore,
            Is.EqualTo(
                score.ExposureNormalized * score.RangeQualityNormalized
            ).Within(Tolerance)
        );

        foreach (PropertyInfo property
                 in typeof(AutoTargetScoreResult).GetProperties())
        {
            Assert.That(property.GetMethod, Is.Not.Null, property.Name);
            Assert.That(property.SetMethod, Is.Null, property.Name);
        }
    }


    private AutoTargetScoreResult EvaluateScore()
    {
        return EvaluateScore(EvaluateEligibility());
    }


    private AutoTargetScoreResult EvaluateScore(
        FireEligibilityResult fireResult
    )
    {
        bool evaluated = ShipAutoTargetScorer.TryEvaluate(
            shooterRoot,
            targetRoot,
            fireResult,
            out AutoTargetScoreResult score
        );

        Assert.That(evaluated, Is.True);
        Assert.That(score.Selectable, Is.True);
        return score;
    }


    private FireEligibilityResult EvaluateEligibility()
    {
        Physics.SyncTransforms();
        bool evaluated = eligibility.TryEvaluate(
            targetRoot,
            true,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private GameObject CreateShip(string name, bool includeExposure)
    {
        GameObject root = new GameObject(name);
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        SetArtReferences(root, artDefinition);
        root.AddComponent<ShipCombatGeometry>();

        if (includeExposure)
        {
            ShipExposureReference exposure =
                root.AddComponent<ShipExposureReference>();
            SetPrivateField(
                exposure,
                "shipArtDefinition",
                artDefinition
            );
        }

        return root;
    }


    private static void SetArtReferences(
        GameObject root,
        ShipArtDefinition artDefinition
    )
    {
        SetArtReference(root, artDefinition, "centerReference", Vector3.zero);
        SetArtReference(root, artDefinition, "waterlineReference", Vector3.zero);
        SetArtReference(root, artDefinition, "deckReference", Vector3.up * 4f);
        SetArtReference(root, artDefinition, "bowReference", Vector3.forward * 15f);
        SetArtReference(root, artDefinition, "sternReference", Vector3.back * 15f);
        SetArtReference(root, artDefinition, "portReference", Vector3.left * 5f);
        SetArtReference(root, artDefinition, "starboardReference", Vector3.right * 5f);
    }


    private static void SetArtReference(
        GameObject root,
        ShipArtDefinition artDefinition,
        string fieldName,
        Vector3 localPosition
    )
    {
        GameObject referenceObject = new GameObject(fieldName);
        referenceObject.transform.SetParent(root.transform, false);
        referenceObject.transform.localPosition = localPosition;
        SetPrivateField(
            artDefinition,
            fieldName,
            referenceObject.transform
        );
    }


    private void SetRanges(float effectiveMeters, float maximumMeters)
    {
        SetPrivateField(
            eligibility,
            "effectiveRangeMeters",
            effectiveMeters
        );
        SetPrivateField(
            eligibility,
            "maximumRangeMeters",
            maximumMeters
        );
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            PrivateInstance
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }


    private static void AssertNormalized(float value)
    {
        Assert.That(value, Is.GreaterThanOrEqualTo(0f));
        Assert.That(value, Is.LessThanOrEqualTo(1f));
    }
}
