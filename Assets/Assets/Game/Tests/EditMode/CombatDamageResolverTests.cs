using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatDamageResolverTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";
    private const string DamageProfilePath =
        "Assets/Assets/Game/Data/SO_CombatDamage_Foundation.asset";

    private static readonly string[] GenericProxyPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };

    private readonly List<UnityEngine.Object> temporaryObjects = new();
    private GameObject sourceShip;
    private GameObject targetShip;
    private CombatDamageProfile foundationProfile;
    private ShotSample shot;


    [SetUp]
    public void SetUp()
    {
        sourceShip = new GameObject("Damage Test Source Ship");
        temporaryObjects.Add(sourceShip);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        targetShip = UnityEngine.Object.Instantiate(prefab);
        CombatLifecycleTestUtility.EnsureOperational(targetShip);
        temporaryObjects.Add(targetShip);

        foundationProfile =
            AssetDatabase.LoadAssetAtPath<CombatDamageProfile>(
                DamageProfilePath
            );
        Assert.That(foundationProfile, Is.Not.Null);
        shot = CreateShotSample(sourceShip);
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
    }


    [Test]
    public void ValidRoundShotHullHit_AppliesBaseDamage()
    {
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            outcome,
            foundationProfile,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatDamageResolutionFailure.None));
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Applied, Is.True);
        Assert.That(
            result.RequestedDamage,
            Is.EqualTo(foundationProfile.RoundShotBaseDamage)
        );
        Assert.That(
            result.AppliedDamage,
            Is.EqualTo(foundationProfile.RoundShotBaseDamage)
        );
        Assert.That(
            integrity.CurrentIntegrity,
            Is.EqualTo(
                integrity.MaximumIntegrity
                    - foundationProfile.RoundShotBaseDamage
            )
        );
    }


    [TestCase(CombatHullRegion.Bow)]
    [TestCase(CombatHullRegion.Midship)]
    [TestCase(CombatHullRegion.Stern)]
    public void FoundationRegionMultipliers_AreNeutralAndPreserveRegion(
        CombatHullRegion region
    )
    {
        FoundationShotOutcome outcome = ResolveHullOutcome(region);

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                foundationProfile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(
            foundationProfile.GetRegionMultiplier(region),
            Is.EqualTo(1f)
        );
        Assert.That(result.HasHitRegion, Is.True);
        Assert.That(result.HitRegion, Is.EqualTo(region));
        Assert.That(
            result.AppliedDamage,
            Is.EqualTo(foundationProfile.RoundShotBaseDamage)
        );
    }


    [TestCase(CombatHullRegion.Bow)]
    [TestCase(CombatHullRegion.Midship)]
    [TestCase(CombatHullRegion.Stern)]
    public void DamageAndVfxConsumeTheSameResolvedHitRegion(
        CombatHullRegion region
    )
    {
        FoundationShotOutcome outcome = ResolveHullOutcome(region);
        RecordingCombatVFXEventReceiver receiver =
            sourceShip.AddComponent<RecordingCombatVFXEventReceiver>();

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                foundationProfile,
                out CombatDamageResult damage,
                out _
            ),
            Is.True
        );
        Assert.That(
            CombatOutcomeVFXBridge.TryEmitResolvedOutcome(
                outcome,
                receiver
            ),
            Is.True
        );

        Assert.That(receiver.HullEvents, Has.Count.EqualTo(1));
        Assert.That(damage.HitRegion, Is.EqualTo(region));
        Assert.That(
            receiver.HullEvents[0].HitRegion,
            Is.EqualTo(damage.HitRegion)
        );
        Assert.That(
            receiver.HullEvents[0].TargetShip,
            Is.SameAs(damage.TargetShipRoot)
        );
    }


    [Test]
    public void Formula_MultipliesBaseDamageByRegionMultiplier()
    {
        CombatDamageProfile profile = CreateProfile(120f, 1.5f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Bow
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.RequestedDamage, Is.EqualTo(180f));
        Assert.That(result.AppliedDamage, Is.EqualTo(180f));
    }


    [TestCase(FoundationShotOutcomeKind.WaterMiss)]
    [TestCase(FoundationShotOutcomeKind.ExpiredNonHit)]
    public void NonHitOutcome_AppliesZeroDamageAndDoesNotMutateIntegrity(
        FoundationShotOutcomeKind kind
    )
    {
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        float previousIntegrity = integrity.CurrentIntegrity;
        FoundationShotOutcome outcome = ResolveNonHitOutcome(kind);

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            outcome,
            null,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatDamageResolutionFailure.None));
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Applied, Is.False);
        Assert.That(result.RequestedDamage, Is.Zero);
        Assert.That(result.AppliedDamage, Is.Zero);
        Assert.That(result.TargetShipRoot, Is.Null);
        Assert.That(result.HasHitRegion, Is.False);
        Assert.That(result.HasIntegrityTransition, Is.False);
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(previousIntegrity));
    }


    [Test]
    public void InvalidSemanticOutcome_AppliesNoDamage()
    {
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        float previousIntegrity = integrity.CurrentIntegrity;

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            default,
            foundationProfile,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(resolved, Is.False);
        Assert.That(result.Accepted, Is.False);
        Assert.That(
            failure,
            Is.EqualTo(CombatDamageResolutionFailure.InvalidOutcome)
        );
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(previousIntegrity));
    }


    [Test]
    public void RepeatedValidHits_AccumulateDeterministically()
    {
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        FoundationShotOutcome first = ResolveHullOutcome(
            CombatHullRegion.Bow
        );
        FoundationShotOutcome second = ResolveHullOutcome(
            CombatHullRegion.Stern
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                first,
                foundationProfile,
                out _,
                out _
            ),
            Is.True
        );
        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                second,
                foundationProfile,
                out _,
                out _
            ),
            Is.True
        );
        Assert.That(
            integrity.CurrentIntegrity,
            Is.EqualTo(
                integrity.MaximumIntegrity
                    - 2f * foundationProfile.RoundShotBaseDamage
            )
        );
    }


    [Test]
    public void OversizedHit_ClampsAtZeroAndReportsActualAppliedDamage()
    {
        CombatDamageProfile profile = CreateProfile(1500f, 1f, 1f, 1f);
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.RequestedDamage, Is.EqualTo(1500f));
        Assert.That(result.AppliedDamage, Is.EqualTo(1000f));
        Assert.That(result.PreviousIntegrity, Is.EqualTo(1000f));
        Assert.That(result.CurrentIntegrity, Is.Zero);
        Assert.That(integrity.CurrentIntegrity, Is.Zero);
        Assert.That(
            result.CurrentLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
    }


    [Test]
    public void DamageResult_RecordsIntegrityAndLifecycleTransition()
    {
        CombatDamageProfile profile = CreateProfile(750f, 1f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.HasIntegrityTransition, Is.True);
        Assert.That(result.PreviousIntegrity, Is.EqualTo(1000f));
        Assert.That(result.CurrentIntegrity, Is.EqualTo(250f));
        Assert.That(
            result.PreviousLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
        Assert.That(
            result.CurrentLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.CombatDisabled)
        );
        Assert.That(result.LifecycleChanged, Is.True);
    }


    [Test]
    public void LargeHit_CanTransitionDirectlyFromOperationalToSinking()
    {
        CombatDamageProfile profile = CreateProfile(950f, 1f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Stern
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(
            result.PreviousLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
        Assert.That(
            result.CurrentLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
        Assert.That(result.LifecycleChanged, Is.True);
    }


    [Test]
    public void AlreadySinkingTarget_ClampsRemainingDamageThenReportsZero()
    {
        CombatDamageProfile sinking = CreateProfile(950f, 1f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );
        CombatDamageResolver.TryResolveAndApply(
            outcome,
            sinking,
            out _,
            out _
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                foundationProfile,
                out CombatDamageResult remainingResult,
                out _
            ),
            Is.True
        );
        Assert.That(remainingResult.Accepted, Is.True);
        Assert.That(remainingResult.Applied, Is.True);
        Assert.That(remainingResult.AppliedDamage, Is.EqualTo(50f));
        Assert.That(remainingResult.CurrentIntegrity, Is.Zero);
        Assert.That(
            remainingResult.CurrentLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                foundationProfile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Applied, Is.False);
        Assert.That(result.AppliedDamage, Is.Zero);
        Assert.That(result.PreviousIntegrity, Is.Zero);
        Assert.That(result.CurrentIntegrity, Is.Zero);
        Assert.That(result.LifecycleChanged, Is.False);
        Assert.That(
            result.CurrentLifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
    }


    [Test]
    public void FormalTerminalCoordinator_AppliesResolvedDamageBeforePresentation()
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        GameObject formalSource = UnityEngine.Object.Instantiate(sourcePrefab);
        temporaryObjects.Add(formalSource);
        shot = CreateShotSample(formalSource);
        ShipBroadsideFireExecutor executor =
            formalSource.GetComponent<ShipBroadsideFireExecutor>();
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.CombatGeometryContact,
            GetRegion(CombatHullRegion.Midship).QueryCollider,
            Vector3.one,
            Vector3.back,
            0.5f
        );
        MethodInfo handler = typeof(ShipBroadsideFireExecutor).GetMethod(
            "HandleTerminalContact",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(executor.CombatDamageProfile, Is.SameAs(foundationProfile));
        Assert.That(handler, Is.Not.Null);
        handler.Invoke(executor, new object[] { contact, 0u, null });

        Assert.That(
            integrity.CurrentIntegrity,
            Is.EqualTo(
                integrity.MaximumIntegrity
                    - foundationProfile.RoundShotBaseDamage
            )
        );
        Assert.That(executor.HasLastTerminalResolution, Is.True);
        Assert.That(
            executor.LastTerminalResolution.Outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.HullHit)
        );
        Assert.That(
            executor.LastTerminalResolution.Outcome.HitContext.Region,
            Is.EqualTo(CombatHullRegion.Midship)
        );
        Assert.That(
            executor.LastTerminalResolution.DamageResolutionSucceeded,
            Is.True
        );
        Assert.That(
            executor.LastTerminalResolution.DamageResult.HitRegion,
            Is.EqualTo(CombatHullRegion.Midship)
        );
    }


    [Test]
    public void DamageContracts_AreImmutableAndResolverOwnsNoLifecycleThresholds()
    {
        Type resultType = typeof(CombatDamageResult);
        Type diagnosticsType =
            typeof(CombatTerminalResolutionDiagnostics);

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
        Assert.That(diagnosticsType.IsValueType, Is.True);
        Assert.That(
            diagnosticsType.GetProperties()
                .All(property => property.SetMethod == null),
            Is.True
        );
        Assert.That(
            diagnosticsType.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
        Assert.That(
            typeof(CombatDamageResolver).GetFields(
                BindingFlags.Static
                    | BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
            ),
            Is.Empty
        );
        bool ownsLifecycleThreshold =
            typeof(CombatDamageProfile).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).Any(field => field.Name.IndexOf(
                "threshold",
                StringComparison.OrdinalIgnoreCase
            ) >= 0);
        Assert.That(ownsLifecycleThreshold, Is.False);
    }


    [Test]
    public void DamagePipeline_HasNoForbiddenGameplayDependencies()
    {
        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string damageSource = string.Join(
            "\n",
            new[]
            {
                "CombatDamageProfile.cs",
                "CombatDamageResult.cs",
                "CombatDamageResolver.cs"
            }.Select(fileName => File.ReadAllText(
                Path.Combine(combatRoot, fileName)
            ))
        );
        string projectileSource = File.ReadAllText(
            Path.Combine(combatRoot, "CombatProjectile.cs")
        );
        string outcomeSource = File.ReadAllText(
            Path.Combine(combatRoot, "FoundationShotOutcome.cs")
        );
        string vfxSource = File.ReadAllText(
            Path.Combine(combatRoot, "CombatOutcomeVFXBridge.cs")
        );

        foreach (string forbidden in new[]
        {
            "Physics.",
            "Renderer",
            "Mesh",
            "Distance",
            "IncomingVelocity",
            "Exposure",
            "Dispersion",
            "HitChance",
            "Critical",
            "Raking",
            "Crew",
            "Rigging",
            "Flooding",
            "Penetration",
            "Armor",
            "UnityEngine.Random",
            "ShipSailingSpeed",
            "ShipTurning",
            "ShipDestinationController"
        })
        {
            Assert.That(damageSource, Does.Not.Contain(forbidden), forbidden);
        }

        Assert.That(projectileSource, Does.Not.Contain("ShipIntegrity"));
        Assert.That(projectileSource, Does.Not.Contain("CombatDamageResolver"));
        Assert.That(outcomeSource, Does.Not.Contain("ShipIntegrity"));
        Assert.That(outcomeSource, Does.Not.Contain("CombatDamageResolver"));
        Assert.That(vfxSource, Does.Not.Contain("ShipIntegrity"));
        Assert.That(vfxSource, Does.Not.Contain("CombatDamageResolver"));
    }


    [Test]
    public void FoundationDamageSupportsRoundShotOnlyAndNoRegionHp()
    {
        Assert.That(
            Enum.GetValues(typeof(FoundationAmmunitionType)),
            Is.EquivalentTo(new[] { FoundationAmmunitionType.RoundShot })
        );

        string integritySource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipIntegrity.cs"
        )).ToLowerInvariant();
        Assert.That(integritySource, Does.Not.Contain("bowhp"));
        Assert.That(integritySource, Does.Not.Contain("midshiphp"));
        Assert.That(integritySource, Does.Not.Contain("sternhp"));
        Assert.That(integritySource, Does.Not.Contain("regionintegrity"));
    }


    [Test]
    public void FormalPrefabHasDamageProfile_GenericMovementProxiesRemainClean()
    {
        GameObject combatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        ShipBroadsideFireExecutor executor =
            combatPrefab.GetComponent<ShipBroadsideFireExecutor>();

        Assert.That(executor, Is.Not.Null);
        Assert.That(executor.CombatDamageProfile, Is.SameAs(foundationProfile));
        Assert.That(foundationProfile.RoundShotBaseDamage, Is.EqualTo(100f));
        Assert.That(foundationProfile.BowMultiplier, Is.EqualTo(1f));
        Assert.That(foundationProfile.MidshipMultiplier, Is.EqualTo(1f));
        Assert.That(foundationProfile.SternMultiplier, Is.EqualTo(1f));

        foreach (string path in GenericProxyPaths)
        {
            GameObject generic = AssetDatabase.LoadAssetAtPath<GameObject>(
                path
            );
            Assert.That(generic, Is.Not.Null, path);
            Assert.That(
                generic.GetComponentsInChildren<ShipBroadsideFireExecutor>(
                    true
                ),
                Is.Empty,
                path
            );
            Assert.That(
                generic.GetComponentsInChildren<ShipIntegrity>(true),
                Is.Empty,
                path
            );
        }
    }


    private FoundationShotOutcome ResolveHullOutcome(
        CombatHullRegion region
    )
    {
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.CombatGeometryContact,
            GetRegion(region).QueryCollider,
            Vector3.one,
            Vector3.back,
            0.5f
        );
        return ResolveOutcome(contact);
    }


    private FoundationShotOutcome ResolveNonHitOutcome(
        FoundationShotOutcomeKind kind
    )
    {
        ProjectileTerminalContactKind contactKind =
            kind == FoundationShotOutcomeKind.WaterMiss
                ? ProjectileTerminalContactKind.WaterContact
                : ProjectileTerminalContactKind.ExpiredSafetyFallback;
        return ResolveOutcome(CreateContact(
            contactKind,
            null,
            new Vector3(10f, 0f, 20f),
            Vector3.up,
            1f
        ));
    }


    private FoundationShotOutcome ResolveOutcome(
        ProjectileTerminalContact contact
    )
    {
        Assert.That(
            FoundationShotOutcomeResolver.TryResolve(
                contact,
                out FoundationShotOutcome outcome,
                out FoundationShotOutcomeFailure failure
            ),
            Is.True,
            failure.ToString()
        );
        return outcome;
    }


    private CombatHitRegion GetRegion(CombatHullRegion region)
    {
        ShipCombatGeometry geometry =
            targetShip.GetComponent<ShipCombatGeometry>();

        switch (region)
        {
            case CombatHullRegion.Bow:
                return geometry.BowRegion;
            case CombatHullRegion.Midship:
                return geometry.MidshipRegion;
            case CombatHullRegion.Stern:
                return geometry.SternRegion;
            default:
                throw new ArgumentOutOfRangeException(nameof(region));
        }
    }


    private ProjectileTerminalContact CreateContact(
        ProjectileTerminalContactKind kind,
        Collider collider,
        Vector3 point,
        Vector3 normal,
        float elapsedTime
    )
    {
        ConstructorInfo constructor = typeof(ProjectileTerminalContact)
            .GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            .Single();
        return (ProjectileTerminalContact)constructor.Invoke(new object[]
        {
            kind,
            shot,
            point,
            normal,
            collider,
            0.5f,
            elapsedTime
        });
    }


    private CombatDamageProfile CreateProfile(
        float baseDamage,
        float bowMultiplier,
        float midshipMultiplier,
        float sternMultiplier
    )
    {
        CombatDamageProfile profile =
            ScriptableObject.CreateInstance<CombatDamageProfile>();
        temporaryObjects.Add(profile);
        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("roundShotBaseDamage").floatValue = baseDamage;
        serialized.FindProperty("bowMultiplier").floatValue = bowMultiplier;
        serialized.FindProperty("midshipMultiplier").floatValue =
            midshipMultiplier;
        serialized.FindProperty("sternMultiplier").floatValue =
            sternMultiplier;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }


    private static ShotSample CreateShotSample(GameObject source)
    {
        ConstructorInfo constructor = typeof(ShotSample).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic
        ).Single();
        return (ShotSample)constructor.Invoke(new object[]
        {
            source,
            CombatSide.Port,
            0,
            123u,
            new Vector3(3f, 4f, 5f),
            new Vector3(30f, 2f, 10f),
            new Vector3(40f, 8f, 2f),
            new Vector3(0f, -9.81f, 0f),
            1.5f,
            -1.5f,
            10f,
            FoundationAmmunitionType.RoundShot
        });
    }
}
