using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipIntegrityTests
{
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_ShipIntegrity_Foundation.asset";
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";

    private static readonly string[] GenericProxyPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };

    private readonly List<GameObject> createdObjects =
        new List<GameObject>();

    private readonly List<ShipIntegrityProfile> createdProfiles =
        new List<ShipIntegrityProfile>();

    private ShipIntegrityProfile profile;


    [SetUp]
    public void SetUp()
    {
        profile = CreateProfile(100f, 0.25f, 0.1f);
    }


    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            UnityEngine.Object.DestroyImmediate(createdObject);
        }

        foreach (ShipIntegrityProfile createdProfile in createdProfiles)
        {
            UnityEngine.Object.DestroyImmediate(createdProfile);
        }

        createdObjects.Clear();
        createdProfiles.Clear();
    }


    [Test]
    public void ValidShip_InitializesAtMaximumIntegrity()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        Assert.That(integrity.IsInitialized, Is.True);
        Assert.That(integrity.MaximumIntegrity, Is.EqualTo(100f));
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(100f));
    }


    [Test]
    public void ExplicitInitialization_IsIdempotentAndDoesNotRestoreIntegrity()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);
        Assert.That(
            integrity.TryApplyIntegrityLoss(20f, out _),
            Is.True
        );
        float currentIntegrity = integrity.CurrentIntegrity;

        Assert.That(integrity.TryInitialize(), Is.True);
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(currentIntegrity));
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
    }


    [Test]
    public void ValidShip_InitialLifecycleIsOperational()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
        Assert.That(integrity.IsCombatDisabled, Is.False);
        Assert.That(integrity.IsSinking, Is.False);
    }


    [Test]
    public void LifecycleEnum_HasExactlyFoundationStates()
    {
        Assert.That(
            Enum.GetValues(typeof(ShipCombatLifecycleState)),
            Is.EquivalentTo(new[]
            {
                ShipCombatLifecycleState.Operational,
                ShipCombatLifecycleState.CombatDisabled,
                ShipCombatLifecycleState.Sinking
            })
        );
    }


    [Test]
    public void CurrentIntegrity_IsExternallyReadOnly()
    {
        PropertyInfo property = typeof(ShipIntegrity).GetProperty(
            nameof(ShipIntegrity.CurrentIntegrity)
        );

        Assert.That(property, Is.Not.Null);
        Assert.That(property.SetMethod, Is.Null);
        Assert.That(
            typeof(ShipIntegrity).GetFields(
                BindingFlags.Instance | BindingFlags.Public
            ),
            Is.Empty
        );
    }


    [Test]
    public void InvalidOrNonFiniteLoss_IsRejectedWithoutMutation()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);
        float[] invalidLosses =
        {
            0f,
            -1f,
            float.NaN,
            float.PositiveInfinity,
            float.NegativeInfinity
        };

        foreach (float invalidLoss in invalidLosses)
        {
            bool applied = integrity.TryApplyIntegrityLoss(
                invalidLoss,
                out ShipIntegrityTransition transition
            );

            Assert.That(applied, Is.False, invalidLoss.ToString());
            Assert.That(transition, Is.EqualTo(default(ShipIntegrityTransition)));
            Assert.That(integrity.CurrentIntegrity, Is.EqualTo(100f));
            Assert.That(
                integrity.LifecycleState,
                Is.EqualTo(ShipCombatLifecycleState.Operational)
            );
        }
    }


    [Test]
    public void IntegrityLoss_ClampsCurrentIntegrityAtZero()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        Assert.That(
            integrity.TryApplyIntegrityLoss(1000f, out _),
            Is.True
        );
        Assert.That(integrity.CurrentIntegrity, Is.Zero);
    }


    [Test]
    public void LossApi_CannotIncreaseIntegrityAboveMaximum()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        Assert.That(
            integrity.TryApplyIntegrityLoss(-10f, out _),
            Is.False
        );
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(100f));

        integrity.TryApplyIntegrityLoss(10f, out _);
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(90f));
        Assert.That(
            integrity.CurrentIntegrity,
            Is.LessThanOrEqualTo(integrity.MaximumIntegrity)
        );
    }


    [Test]
    public void IntegrityAboveDisabledThreshold_RemainsOperational()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(74f, out _);

        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(26f));
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
    }


    [Test]
    public void ExactDisabledThreshold_IsCombatDisabledInclusively()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(75f, out _);

        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(25f));
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.CombatDisabled)
        );
    }


    [Test]
    public void BelowDisabledThreshold_IsCombatDisabled()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(76f, out _);

        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.CombatDisabled)
        );
        Assert.That(integrity.IsSinking, Is.False);
    }


    [Test]
    public void ExactSinkingThreshold_IsSinkingInclusively()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(90f, out _);

        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(10f));
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
    }


    [Test]
    public void BelowSinkingThreshold_IsSinking()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(91f, out _);

        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
    }


    [Test]
    public void Sinking_ImpliesCombatDisabled()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(100f, out _);

        Assert.That(integrity.IsSinking, Is.True);
        Assert.That(integrity.IsCombatDisabled, Is.True);
    }


    [Test]
    public void CombatDisabled_DoesNotImplySinking()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        integrity.TryApplyIntegrityLoss(80f, out _);

        Assert.That(integrity.IsCombatDisabled, Is.True);
        Assert.That(integrity.IsSinking, Is.False);
    }


    [Test]
    public void OneLargeLoss_CanTransitionOperationalDirectlyToSinking()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);

        bool applied = integrity.TryApplyIntegrityLoss(
            95f,
            out ShipIntegrityTransition transition
        );

        Assert.That(applied, Is.True);
        Assert.That(
            transition.PreviousLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
        Assert.That(
            transition.CurrentLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
        Assert.That(transition.LifecycleChanged, Is.True);
    }


    [Test]
    public void IdenticalLossSequence_ProducesDeterministicTransitions()
    {
        ShipIntegrity first = CreateIntegrity(profile);
        ShipIntegrity second = CreateIntegrity(profile);
        float[] losses = { 20f, 55f, 15f };

        foreach (float loss in losses)
        {
            first.TryApplyIntegrityLoss(loss, out ShipIntegrityTransition a);
            second.TryApplyIntegrityLoss(loss, out ShipIntegrityTransition b);

            Assert.That(b.PreviousIntegrity, Is.EqualTo(a.PreviousIntegrity));
            Assert.That(b.CurrentIntegrity, Is.EqualTo(a.CurrentIntegrity));
            Assert.That(
                b.PreviousLifecycleState,
                Is.EqualTo(a.PreviousLifecycleState)
            );
            Assert.That(
                b.CurrentLifecycleState,
                Is.EqualTo(a.CurrentLifecycleState)
            );
        }
    }


    [Test]
    public void RepeatedReads_DoNotMutateState()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);
        integrity.TryApplyIntegrityLoss(80f, out _);

        for (int index = 0; index < 20; index++)
        {
            Assert.That(integrity.CurrentIntegrity, Is.EqualTo(20f));
            Assert.That(
                integrity.LifecycleState,
                Is.EqualTo(ShipCombatLifecycleState.CombatDisabled)
            );
            Assert.That(integrity.IsCombatDisabled, Is.True);
            Assert.That(integrity.IsSinking, Is.False);
        }
    }


    [Test]
    public void Lifecycle_IsNotDerivedFromEnabledOrActiveState()
    {
        ShipIntegrity integrity = CreateIntegrity(profile);
        integrity.TryApplyIntegrityLoss(80f, out _);

        integrity.enabled = false;
        integrity.gameObject.SetActive(false);

        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(20f));
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.CombatDisabled)
        );
    }


    [Test]
    public void InvalidThresholdOrdering_DoesNotInitialize()
    {
        ShipIntegrityProfile invalidProfile = CreateProfile(
            100f,
            0.1f,
            0.25f
        );

        ShipIntegrity integrity = CreateIntegrity(invalidProfile, false);

        Assert.That(integrity.IsInitialized, Is.False);
        Assert.That(
            integrity.TryApplyIntegrityLoss(1f, out _),
            Is.False
        );
    }


    [Test]
    public void IntegrityContracts_HaveNoRegionOrDeferredSubsystemState()
    {
        string memberNames = string.Join(
            " ",
            typeof(ShipIntegrity).GetMembers(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).Select(member => member.Name)
        ).ToLowerInvariant();

        foreach (string forbidden in new[]
        {
            "bowintegrity",
            "midshipintegrity",
            "sternintegrity",
            "bowhp",
            "midshiphp",
            "sternhp",
            "masthp",
            "cannonhp",
            "rigging",
            "crew",
            "firestate",
            "flooding"
        })
        {
            Assert.That(memberNames, Does.Not.Contain(forbidden));
        }
    }


    [Test]
    public void IntegrityOwner_HasNoProjectileVfxOrMovementDependency()
    {
        Type[] forbiddenTypes =
        {
            typeof(CombatProjectile),
            typeof(ShotSample),
            typeof(FoundationShotOutcome),
            typeof(ICombatVFXEventReceiver),
            typeof(CombatVFXPlaceholderReceiver),
            typeof(ShipSailingSpeed),
            typeof(ShipTurning),
            typeof(ShipDestinationController)
        };
        IEnumerable<Type> dependencyTypes = typeof(ShipIntegrity)
            .GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).Select(field => field.FieldType)
            .Concat(typeof(ShipIntegrity).GetMethods(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).SelectMany(method =>
                method.GetParameters().Select(parameter =>
                    parameter.ParameterType.IsByRef
                        ? parameter.ParameterType.GetElementType()
                        : parameter.ParameterType
                ).Append(method.ReturnType)
            ));

        Assert.That(
            dependencyTypes.Any(type => forbiddenTypes.Contains(type)),
            Is.False
        );
    }


    [Test]
    public void TransitionContract_IsImmutable()
    {
        Assert.That(typeof(ShipIntegrityTransition).IsValueType, Is.True);
        Assert.That(
            typeof(ShipIntegrityTransition).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
        Assert.That(
            typeof(ShipIntegrityTransition).GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly
            ).All(property => property.SetMethod == null),
            Is.True
        );
    }


    [Test]
    public void FoundationProfile_IsTemporaryPlaytestBoundConfiguration()
    {
        ShipIntegrityProfile asset =
            AssetDatabase.LoadAssetAtPath<ShipIntegrityProfile>(ProfilePath);
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipIntegrityProfile.cs"
        ));

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.MaximumIntegrity, Is.EqualTo(1000f));
        Assert.That(
            asset.SinkingThresholdNormalized,
            Is.LessThanOrEqualTo(asset.CombatDisabledThresholdNormalized)
        );
        Assert.That(
            asset.CombatDisabledThresholdNormalized,
            Is.LessThanOrEqualTo(1f)
        );
        Assert.That(source, Does.Contain("TEMPORARY"));
        Assert.That(source, Does.Contain("PLAYTEST-BOUND"));
        Assert.That(source, Does.Contain("NOT BALANCE-FROZEN"));
    }


    [Test]
    public void GelderlandCombatPrefab_HasOneConfiguredIntegrityOwner()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        ShipIntegrityProfile expectedProfile =
            AssetDatabase.LoadAssetAtPath<ShipIntegrityProfile>(ProfilePath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(expectedProfile, Is.Not.Null);
        ShipIntegrity[] owners =
            prefab.GetComponentsInChildren<ShipIntegrity>(true);
        Assert.That(owners, Has.Length.EqualTo(1));
        Assert.That(owners[0].gameObject, Is.SameAs(prefab));
        Assert.That(owners[0].IntegrityProfile, Is.SameAs(expectedProfile));

        Assert.That(
            expectedProfile.MaximumIntegrity,
            Is.GreaterThan(0f)
        );
        Assert.That(
            expectedProfile.SinkingThresholdNormalized,
            Is.GreaterThanOrEqualTo(0f)
        );
        Assert.That(
            expectedProfile.SinkingThresholdNormalized,
            Is.LessThanOrEqualTo(
                expectedProfile.CombatDisabledThresholdNormalized
            )
        );
        Assert.That(
            expectedProfile.CombatDisabledThresholdNormalized,
            Is.LessThanOrEqualTo(1f)
        );
    }


    [TestCaseSource(nameof(GenericProxyPaths))]
    public void GenericMovementProxy_HasNoIntegrityOwner(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponentsInChildren<ShipIntegrity>(true),
            Is.Empty
        );
    }


    private ShipIntegrityProfile CreateProfile(
        float maximumIntegrity,
        float combatDisabledThresholdNormalized,
        float sinkingThresholdNormalized
    )
    {
        ShipIntegrityProfile createdProfile =
            ScriptableObject.CreateInstance<ShipIntegrityProfile>();
        SetPrivateField(
            createdProfile,
            "maximumIntegrity",
            maximumIntegrity
        );
        SetPrivateField(
            createdProfile,
            "combatDisabledThresholdNormalized",
            combatDisabledThresholdNormalized
        );
        SetPrivateField(
            createdProfile,
            "sinkingThresholdNormalized",
            sinkingThresholdNormalized
        );
        createdProfiles.Add(createdProfile);
        return createdProfile;
    }


    private ShipIntegrity CreateIntegrity(
        ShipIntegrityProfile integrityProfile,
        bool expectInitialized = true
    )
    {
        GameObject root = new GameObject("Integrity Test Ship");
        root.SetActive(false);
        ShipIntegrity integrity = root.AddComponent<ShipIntegrity>();
        SetPrivateField(integrity, "integrityProfile", integrityProfile);
        createdObjects.Add(root);
        root.SetActive(true);
        bool initialized = integrity.TryInitialize();
        Assert.That(initialized, Is.EqualTo(expectInitialized));
        Assert.That(integrity.IsInitialized, Is.EqualTo(expectInitialized));
        return integrity;
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
