using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatRakingEvaluatorTests
{
    private const string FoundationProfilePath =
        "Assets/Assets/Game/Data/SO_CombatRaking_Foundation.asset";
    private const float AngleTolerance = 0.05f;

    private readonly List<UnityEngine.Object> temporaryObjects = new();
    private readonly Dictionary<CombatHullRegion, CombatHitRegion> regions =
        new();
    private GameObject targetRoot;
    private Transform visualRoot;
    private CombatRakingProfile foundationProfile;


    [SetUp]
    public void SetUp()
    {
        targetRoot = new GameObject("Raking Target Root");
        temporaryObjects.Add(targetRoot);

        visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(targetRoot.transform, false);

        foreach (CombatHullRegion region in Enum.GetValues(
            typeof(CombatHullRegion)
        ))
        {
            GameObject regionObject = new GameObject(region.ToString());
            regionObject.transform.SetParent(targetRoot.transform, false);
            CombatHitRegion hitRegion =
                regionObject.AddComponent<CombatHitRegion>();
            SerializedObject serialized = new SerializedObject(hitRegion);
            serialized.FindProperty("region").enumValueIndex = (int)region;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            regions.Add(region, hitRegion);
        }

        foundationProfile =
            AssetDatabase.LoadAssetAtPath<CombatRakingProfile>(
                FoundationProfilePath
            );
        Assert.That(foundationProfile, Is.Not.Null);
    }


    [TearDown]
    public void TearDown()
    {
        for (int index = temporaryObjects.Count - 1; index >= 0; index--)
        {
            if (temporaryObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    temporaryObjects[index]
                );
            }
        }

        temporaryObjects.Clear();
        regions.Clear();
    }


    [Test]
    public void FoundationProfile_HasTemporaryPhaseEightValues()
    {
        Assert.That(
            foundationProfile.RakingHalfAngleDegrees,
            Is.EqualTo(20f)
        );
        Assert.That(
            foundationProfile.BowDamageMultiplier,
            Is.EqualTo(2f)
        );
        Assert.That(
            foundationProfile.SternDamageMultiplier,
            Is.EqualTo(2f)
        );
        Assert.That(
            Enum.GetValues(typeof(CombatRakingType)),
            Is.EquivalentTo(new[]
            {
                CombatRakingType.None,
                CombatRakingType.Bow,
                CombatRakingType.Stern
            })
        );
    }


    [TestCase(0f, true)]
    [TestCase(19.9f, true)]
    [TestCase(20f, true)]
    [TestCase(20.1f, false)]
    [TestCase(90f, false)]
    public void FoundationBoundary_IsInclusiveAtTwentyDegrees(
        float angleDegrees,
        bool expectedRaking
    )
    {
        CombatHitContext context = CreateContext(
            RotateHorizontal(-targetRoot.transform.forward, angleDegrees)
        );

        Assert.That(
            CombatRakingEvaluator.TryEvaluate(
                context,
                foundationProfile,
                out CombatRakingResult result
            ),
            Is.True
        );
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsRaking, Is.EqualTo(expectedRaking));
        Assert.That(
            result.LongitudinalAngleDegrees,
            Is.EqualTo(angleDegrees).Within(AngleTolerance)
        );
        Assert.That(
            result.Type,
            Is.EqualTo(
                expectedRaking
                    ? CombatRakingType.Bow
                    : CombatRakingType.None
            )
        );
        Assert.That(
            result.DamageMultiplier,
            Is.EqualTo(expectedRaking ? 2f : 1f)
        );
    }


    [Test]
    public void TravelAgainstTargetForward_IsBowRake()
    {
        CombatRakingResult result = Evaluate(
            -targetRoot.transform.forward
        );

        Assert.That(result.Type, Is.EqualTo(CombatRakingType.Bow));
        Assert.That(result.IsRaking, Is.True);
        Assert.That(
            result.LongitudinalAngleDegrees,
            Is.Zero.Within(AngleTolerance)
        );
    }


    [Test]
    public void TravelWithTargetForward_IsSternRake()
    {
        CombatRakingResult result = Evaluate(
            targetRoot.transform.forward
        );

        Assert.That(result.Type, Is.EqualTo(CombatRakingType.Stern));
        Assert.That(result.IsRaking, Is.True);
        Assert.That(
            result.LongitudinalAngleDegrees,
            Is.Zero.Within(AngleTolerance)
        );
    }


    [Test]
    public void TargetRootYaw_RotatesRakingBasis()
    {
        targetRoot.transform.rotation = Quaternion.Euler(0f, 73f, 0f);

        CombatRakingResult bow = Evaluate(-targetRoot.transform.forward);
        CombatRakingResult stern = Evaluate(targetRoot.transform.forward);

        Assert.That(bow.Type, Is.EqualTo(CombatRakingType.Bow));
        Assert.That(stern.Type, Is.EqualTo(CombatRakingType.Stern));
        Assert.That(
            bow.LongitudinalAngleDegrees,
            Is.Zero.Within(AngleTolerance)
        );
        Assert.That(
            stern.LongitudinalAngleDegrees,
            Is.Zero.Within(AngleTolerance)
        );
    }


    [Test]
    public void SameRelativeGeometry_IsInvariantUnderWorldYaw()
    {
        targetRoot.transform.rotation = Quaternion.identity;
        CombatRakingResult first = Evaluate(RotateHorizontal(
            -targetRoot.transform.forward,
            12.5f
        ));

        targetRoot.transform.rotation = Quaternion.Euler(0f, 137f, 0f);
        CombatRakingResult rotated = Evaluate(RotateHorizontal(
            -targetRoot.transform.forward,
            12.5f
        ));

        Assert.That(rotated.Type, Is.EqualTo(first.Type));
        Assert.That(rotated.IsRaking, Is.EqualTo(first.IsRaking));
        Assert.That(
            rotated.LongitudinalAngleDegrees,
            Is.EqualTo(first.LongitudinalAngleDegrees)
                .Within(AngleTolerance)
        );
        Assert.That(
            rotated.DamageMultiplier,
            Is.EqualTo(first.DamageMultiplier)
        );
    }


    [Test]
    public void VisualRootRotation_DoesNotAffectResult()
    {
        Vector3 incomingDirection = RotateHorizontal(
            targetRoot.transform.forward,
            10f
        );
        CombatRakingResult before = Evaluate(incomingDirection);

        visualRoot.localRotation = Quaternion.Euler(31f, 149f, -24f);
        CombatRakingResult after = Evaluate(incomingDirection);

        AssertEquivalent(before, after);
    }


    [Test]
    public void HitRegion_DoesNotDefineRakingType()
    {
        Vector3 incomingDirection = -targetRoot.transform.forward;

        foreach (CombatHullRegion region in Enum.GetValues(
            typeof(CombatHullRegion)
        ))
        {
            CombatRakingResult result = Evaluate(
                incomingDirection,
                region
            );
            Assert.That(
                result.Type,
                Is.EqualTo(CombatRakingType.Bow),
                region.ToString()
            );
        }
    }


    [Test]
    public void ZeroHorizontalIncomingVector_ReturnsNeutralInvalidResult()
    {
        AssertNeutralFailure(CreateContext(Vector3.up));
    }


    [Test]
    public void NonFiniteIncomingVector_ReturnsNeutralWithoutNaN()
    {
        AssertNeutralFailure(CreateContext(new Vector3(
            float.NaN,
            0f,
            1f
        )));
    }


    [TestCase(float.NaN, 2f, 2f)]
    [TestCase(-0.1f, 2f, 2f)]
    [TestCase(90.1f, 2f, 2f)]
    [TestCase(20f, -0.1f, 2f)]
    [TestCase(20f, 2f, float.PositiveInfinity)]
    public void InvalidProfile_ReturnsNeutralWithoutNaN(
        float halfAngle,
        float bowMultiplier,
        float sternMultiplier
    )
    {
        CombatRakingProfile profile = CreateProfile(
            halfAngle,
            bowMultiplier,
            sternMultiplier
        );
        CombatHitContext context = CreateContext(
            -targetRoot.transform.forward
        );

        Assert.That(
            CombatRakingEvaluator.TryEvaluate(
                context,
                profile,
                out CombatRakingResult result
            ),
            Is.False
        );
        AssertNeutral(result);
    }


    [Test]
    public void BowAndSternMultipliers_AreReturnedIndependently()
    {
        CombatRakingProfile profile = CreateProfile(20f, 1.75f, 2.5f);

        CombatRakingResult bow = Evaluate(
            -targetRoot.transform.forward,
            CombatHullRegion.Midship,
            profile
        );
        CombatRakingResult stern = Evaluate(
            targetRoot.transform.forward,
            CombatHullRegion.Midship,
            profile
        );

        Assert.That(bow.Type, Is.EqualTo(CombatRakingType.Bow));
        Assert.That(bow.DamageMultiplier, Is.EqualTo(1.75f));
        Assert.That(stern.Type, Is.EqualTo(CombatRakingType.Stern));
        Assert.That(stern.DamageMultiplier, Is.EqualTo(2.5f));
    }


    [Test]
    public void ZeroAngleAndMultipliers_AreValidConfiguration()
    {
        CombatRakingProfile profile = CreateProfile(0f, 0f, 0f);

        CombatRakingResult result = Evaluate(
            -targetRoot.transform.forward,
            CombatHullRegion.Midship,
            profile
        );

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsRaking, Is.True);
        Assert.That(result.Type, Is.EqualTo(CombatRakingType.Bow));
        Assert.That(result.DamageMultiplier, Is.Zero);
    }


    [Test]
    public void Evaluation_DoesNotMutateAuthoritativeRoot()
    {
        targetRoot.transform.position = new Vector3(31f, 4f, -17f);
        targetRoot.transform.rotation = Quaternion.Euler(0f, 64f, 0f);
        Vector3 position = targetRoot.transform.position;
        Quaternion rotation = targetRoot.transform.rotation;
        Vector3 scale = targetRoot.transform.localScale;

        Evaluate(RotateHorizontal(targetRoot.transform.forward, 15f));

        Assert.That(targetRoot.transform.position, Is.EqualTo(position));
        Assert.That(targetRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(targetRoot.transform.localScale, Is.EqualTo(scale));
    }


    [Test]
    public void Result_IsImmutableValueContract()
    {
        Type resultType = typeof(CombatRakingResult);

        Assert.That(resultType.IsValueType, Is.True);
        Assert.That(
            resultType.GetProperties()
                .All(property => property.SetMethod == null),
            Is.True
        );
        Assert.That(
            resultType.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
    }


    private CombatRakingResult Evaluate(
        Vector3 incomingVelocityWorld,
        CombatHullRegion region = CombatHullRegion.Midship,
        CombatRakingProfile profile = null
    )
    {
        Assert.That(
            CombatRakingEvaluator.TryEvaluate(
                CreateContext(incomingVelocityWorld, region),
                profile == null ? foundationProfile : profile,
                out CombatRakingResult result
            ),
            Is.True
        );
        return result;
    }


    private void AssertNeutralFailure(CombatHitContext context)
    {
        Assert.That(
            CombatRakingEvaluator.TryEvaluate(
                context,
                foundationProfile,
                out CombatRakingResult result
            ),
            Is.False
        );
        AssertNeutral(result);
    }


    private static void AssertNeutral(CombatRakingResult result)
    {
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.IsRaking, Is.False);
        Assert.That(result.Type, Is.EqualTo(CombatRakingType.None));
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
        Assert.That(float.IsNaN(result.LongitudinalAngleDegrees), Is.False);
        Assert.That(float.IsNaN(result.DamageMultiplier), Is.False);
    }


    private static void AssertEquivalent(
        CombatRakingResult expected,
        CombatRakingResult actual
    )
    {
        Assert.That(actual.IsValid, Is.EqualTo(expected.IsValid));
        Assert.That(actual.IsRaking, Is.EqualTo(expected.IsRaking));
        Assert.That(actual.Type, Is.EqualTo(expected.Type));
        Assert.That(
            actual.LongitudinalAngleDegrees,
            Is.EqualTo(expected.LongitudinalAngleDegrees)
                .Within(AngleTolerance)
        );
        Assert.That(
            actual.DamageMultiplier,
            Is.EqualTo(expected.DamageMultiplier)
        );
    }


    private CombatHitContext CreateContext(
        Vector3 incomingVelocityWorld,
        CombatHullRegion region = CombatHullRegion.Midship
    )
    {
        ConstructorInfo constructor = typeof(CombatHitContext)
            .GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            .Single();
        return (CombatHitContext)constructor.Invoke(new object[]
        {
            default(ShotSample),
            targetRoot,
            null,
            regions[region],
            Vector3.zero,
            Vector3.up,
            incomingVelocityWorld
        });
    }


    private CombatRakingProfile CreateProfile(
        float halfAngle,
        float bowMultiplier,
        float sternMultiplier
    )
    {
        CombatRakingProfile profile =
            ScriptableObject.CreateInstance<CombatRakingProfile>();
        temporaryObjects.Add(profile);
        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("rakingHalfAngleDegrees").floatValue =
            halfAngle;
        serialized.FindProperty("bowDamageMultiplier").floatValue =
            bowMultiplier;
        serialized.FindProperty("sternDamageMultiplier").floatValue =
            sternMultiplier;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }


    private static Vector3 RotateHorizontal(
        Vector3 direction,
        float angleDegrees
    )
    {
        return Quaternion.AngleAxis(angleDegrees, Vector3.up) * direction;
    }
}
