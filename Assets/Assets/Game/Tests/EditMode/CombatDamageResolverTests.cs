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
    private const string RakingProfilePath =
        "Assets/Assets/Game/Data/SO_CombatRaking_Foundation.asset";

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
    private CombatRakingProfile foundationRakingProfile;
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
        SetTeamId(sourceShip, 1);
        SetTeamId(targetShip, 2);

        foundationProfile =
            AssetDatabase.LoadAssetAtPath<CombatDamageProfile>(
                DamageProfilePath
            );
        Assert.That(foundationProfile, Is.Not.Null);
        foundationRakingProfile =
            AssetDatabase.LoadAssetAtPath<CombatRakingProfile>(
                RakingProfilePath
            );
        Assert.That(foundationRakingProfile, Is.Not.Null);
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
            foundationRakingProfile,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatDamageResolutionFailure.None));
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Applied, Is.True);
        Assert.That(result.HasRakingResult, Is.True);
        Assert.That(result.IsRaking, Is.False);
        Assert.That(result.RakingType, Is.EqualTo(CombatRakingType.None));
        Assert.That(
            result.LongitudinalAngleDegrees,
            Is.InRange(80f, 90f)
        );
        Assert.That(result.RakingMultiplier, Is.EqualTo(1f));
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


    [Test]
    public void FriendlyShipContact_TerminatesAsExplicitZeroDamageOutcome()
    {
        SetTeamId(targetShip, 1);
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        float currentIntegrity = integrity.CurrentIntegrity;
        ShipCombatLifecycleState lifecycle = integrity.LifecycleState;
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            outcome,
            foundationProfile,
            foundationRakingProfile,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(
            outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.FriendlyShipBlocked)
        );
        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatDamageResolutionFailure.None));
        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Applied, Is.False);
        Assert.That(result.TargetShipRoot, Is.SameAs(targetShip));
        Assert.That(result.HasHitRegion, Is.True);
        Assert.That(result.HasRakingResult, Is.False);
        Assert.That(result.RequestedDamage, Is.Zero);
        Assert.That(result.AppliedDamage, Is.Zero);
        Assert.That(result.HasIntegrityTransition, Is.False);
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(currentIntegrity));
        Assert.That(integrity.LifecycleState, Is.EqualTo(lifecycle));
    }


    [Test]
    public void UnknownShipContact_TerminatesAsExplicitZeroDamageOutcome()
    {
        SetTeamId(
            targetShip,
            ShipCombatAffiliation.UnconfiguredTeamId
        );
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        float currentIntegrity = integrity.CurrentIntegrity;
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            outcome,
            foundationProfile,
            foundationRakingProfile,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(
            outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.UnknownShipBlocked)
        );
        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatDamageResolutionFailure.None));
        Assert.That(result.Applied, Is.False);
        Assert.That(result.HasRakingResult, Is.False);
        Assert.That(result.RequestedDamage, Is.Zero);
        Assert.That(result.AppliedDamage, Is.Zero);
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(currentIntegrity));
    }


    [Test]
    public void WorldObstructionOutcome_ProducesNoShipDamageOrRaking()
    {
        GameObject obstructionObject = new GameObject(
            "Damage Test World Obstruction"
        );
        temporaryObjects.Add(obstructionObject);
        obstructionObject.layer = CombatObstructionVolume.LayerIndex;
        BoxCollider collider = obstructionObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        CombatObstructionVolume obstruction = obstructionObject
            .AddComponent<CombatObstructionVolume>();
        SetPrivateField(obstruction, "queryCollider", collider);
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.WorldObstructionContact,
            collider,
            Vector3.one,
            Vector3.left,
            0.5f
        );
        FoundationShotOutcome outcome = ResolveOutcome(contact);

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            outcome,
            foundationProfile,
            foundationRakingProfile,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(
            outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.WorldObstructionBlocked)
        );
        Assert.That(outcome.HasHitContext, Is.False);
        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatDamageResolutionFailure.None));
        Assert.That(result.TargetShipRoot, Is.Null);
        Assert.That(result.Applied, Is.False);
        Assert.That(result.HasRakingResult, Is.False);
        Assert.That(result.RequestedDamage, Is.Zero);
        Assert.That(result.AppliedDamage, Is.Zero);
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
                foundationRakingProfile,
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
                foundationRakingProfile,
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
                foundationRakingProfile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.RequestedDamage, Is.EqualTo(180f));
        Assert.That(result.AppliedDamage, Is.EqualTo(180f));
    }


    [Test]
    public void BowRakeOnMidship_AppliesFoundationRakingMultiplier()
    {
        CombatDamageResult result = ResolveRakingHit(
            -targetShip.transform.forward,
            CombatHullRegion.Midship,
            foundationRakingProfile
        );

        Assert.That(result.HitRegion, Is.EqualTo(CombatHullRegion.Midship));
        Assert.That(result.IsRaking, Is.True);
        Assert.That(result.RakingType, Is.EqualTo(CombatRakingType.Bow));
        Assert.That(
            result.LongitudinalAngleDegrees,
            Is.LessThanOrEqualTo(
                foundationRakingProfile.RakingHalfAngleDegrees
            )
        );
        Assert.That(result.RakingMultiplier, Is.EqualTo(2f));
        Assert.That(result.RequestedDamage, Is.EqualTo(200f));
        Assert.That(result.AppliedDamage, Is.EqualTo(200f));
    }


    [Test]
    public void SternRakeOnMidship_AppliesFoundationRakingMultiplier()
    {
        CombatDamageResult result = ResolveRakingHit(
            targetShip.transform.forward,
            CombatHullRegion.Midship,
            foundationRakingProfile
        );

        Assert.That(result.HitRegion, Is.EqualTo(CombatHullRegion.Midship));
        Assert.That(result.IsRaking, Is.True);
        Assert.That(result.RakingType, Is.EqualTo(CombatRakingType.Stern));
        Assert.That(
            result.LongitudinalAngleDegrees,
            Is.LessThanOrEqualTo(
                foundationRakingProfile.RakingHalfAngleDegrees
            )
        );
        Assert.That(result.RakingMultiplier, Is.EqualTo(2f));
        Assert.That(result.RequestedDamage, Is.EqualTo(200f));
        Assert.That(result.AppliedDamage, Is.EqualTo(200f));
    }


    [Test]
    public void IndependentRakingMultipliers_RequireNoDamageCodeChange()
    {
        CombatRakingProfile customRaking = CreateRakingProfile(
            20f,
            1.5f,
            2.5f
        );
        float previousIntegrity = targetShip
            .GetComponent<ShipIntegrity>()
            .CurrentIntegrity;

        CombatDamageResult bow = ResolveRakingHit(
            -targetShip.transform.forward,
            CombatHullRegion.Midship,
            customRaking
        );
        CombatDamageResult stern = ResolveRakingHit(
            targetShip.transform.forward,
            CombatHullRegion.Midship,
            customRaking
        );

        Assert.That(bow.RakingType, Is.EqualTo(CombatRakingType.Bow));
        Assert.That(bow.RequestedDamage, Is.EqualTo(150f));
        Assert.That(stern.RakingType, Is.EqualTo(CombatRakingType.Stern));
        Assert.That(stern.RequestedDamage, Is.EqualTo(250f));
        Assert.That(
            targetShip.GetComponent<ShipIntegrity>().CurrentIntegrity,
            Is.EqualTo(previousIntegrity - 400f)
        );
    }


    [Test]
    public void RakingDamage_DoesNotModifyRootPoseOrReloadState()
    {
        Vector3 sourcePosition = sourceShip.transform.position;
        Quaternion sourceRotation = sourceShip.transform.rotation;
        Vector3 targetPosition = targetShip.transform.position;
        Quaternion targetRotation = targetShip.transform.rotation;
        ShipCombatState combatState =
            targetShip.GetComponent<ShipCombatState>();
        BroadsideReloadState portState = combatState.PortBroadsideState;
        BroadsideReloadState starboardState =
            combatState.StarboardBroadsideState;
        float portRemaining = combatState.PortReloadRemainingSeconds;
        float starboardRemaining =
            combatState.StarboardReloadRemainingSeconds;

        CombatDamageResult result = ResolveRakingHit(
            targetShip.transform.forward,
            CombatHullRegion.Midship,
            foundationRakingProfile
        );

        Assert.That(result.IsRaking, Is.True);
        Assert.That(sourceShip.transform.position, Is.EqualTo(sourcePosition));
        Assert.That(sourceShip.transform.rotation, Is.EqualTo(sourceRotation));
        Assert.That(targetShip.transform.position, Is.EqualTo(targetPosition));
        Assert.That(targetShip.transform.rotation, Is.EqualTo(targetRotation));
        Assert.That(combatState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(starboardState)
        );
        Assert.That(
            combatState.PortReloadRemainingSeconds,
            Is.EqualTo(portRemaining)
        );
        Assert.That(
            combatState.StarboardReloadRemainingSeconds,
            Is.EqualTo(starboardRemaining)
        );
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
        Assert.That(result.HasRakingResult, Is.False);
        Assert.That(result.IsRaking, Is.False);
        Assert.That(result.RakingMultiplier, Is.Zero);
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
            foundationRakingProfile,
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
    public void HullHitWithoutRakingProfile_IsRejectedWithoutMutation()
    {
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        float previousIntegrity = integrity.CurrentIntegrity;
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        bool resolved = CombatDamageResolver.TryResolveAndApply(
            outcome,
            foundationProfile,
            null,
            out CombatDamageResult result,
            out CombatDamageResolutionFailure failure
        );

        Assert.That(resolved, Is.False);
        Assert.That(result.Accepted, Is.False);
        Assert.That(
            failure,
            Is.EqualTo(CombatDamageResolutionFailure.MissingRakingProfile)
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
                foundationRakingProfile,
                out _,
                out _
            ),
            Is.True
        );
        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                second,
                foundationProfile,
                foundationRakingProfile,
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
        CombatDamageProfile profile = CreateProfile(15000f, 1f, 1f, 1f);
        ShipIntegrity integrity = targetShip.GetComponent<ShipIntegrity>();
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                foundationRakingProfile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.RequestedDamage, Is.EqualTo(15000f));
        Assert.That(result.AppliedDamage, Is.EqualTo(10000f));
        Assert.That(result.PreviousIntegrity, Is.EqualTo(10000f));
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
        CombatDamageProfile profile = CreateProfile(7500f, 1f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                foundationRakingProfile,
                out CombatDamageResult result,
                out _
            ),
            Is.True
        );
        Assert.That(result.HasIntegrityTransition, Is.True);
        Assert.That(result.PreviousIntegrity, Is.EqualTo(10000f));
        Assert.That(result.CurrentIntegrity, Is.EqualTo(2500f));
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
        CombatDamageProfile profile = CreateProfile(9500f, 1f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Stern
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                profile,
                foundationRakingProfile,
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
        CombatDamageProfile sinking = CreateProfile(9950f, 1f, 1f, 1f);
        FoundationShotOutcome outcome = ResolveHullOutcome(
            CombatHullRegion.Midship
        );
        CombatDamageResolver.TryResolveAndApply(
            outcome,
            sinking,
            foundationRakingProfile,
            out _,
            out _
        );

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                foundationProfile,
                foundationRakingProfile,
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
                foundationRakingProfile,
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
        SetTeamId(formalSource, 1);
        shot = CreateShotSample(
            formalSource,
            targetShip.transform.forward * 100f,
            Vector3.zero
        );
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
        Assert.That(
            executor.CombatRakingProfile,
            Is.SameAs(foundationRakingProfile)
        );
        Assert.That(handler, Is.Not.Null);
        handler.Invoke(executor, new object[] { contact, 0u, null });

        Assert.That(
            integrity.CurrentIntegrity,
            Is.EqualTo(
                integrity.MaximumIntegrity
                    - foundationProfile.RoundShotBaseDamage
                    * foundationRakingProfile.SternDamageMultiplier
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
        Assert.That(
            executor.LastTerminalResolution.DamageResult.RakingType,
            Is.EqualTo(CombatRakingType.Stern)
        );
        Assert.That(
            executor.LastTerminalResolution.DamageResult.AppliedDamage,
            Is.EqualTo(
                foundationProfile.RoundShotBaseDamage
                    * foundationRakingProfile.SternDamageMultiplier
            )
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
        string[] rakingIndependentSources =
        {
            Path.Combine(combatRoot, "ShotSample.cs"),
            Path.Combine(combatRoot, "CombatProjectile.cs"),
            Path.Combine(combatRoot, "CombatProjectileTrajectory.cs"),
            Path.Combine(combatRoot, "ShipBroadsideShotSampler.cs"),
            Path.Combine(combatRoot, "DispersionGeometry.cs"),
            Path.Combine(combatRoot, "ShipFireEligibility.cs"),
            Path.Combine(combatRoot, "ShipAutoTargetScorer.cs"),
            Path.Combine(combatRoot, "ShipCombatState.cs"),
            Path.Combine(combatRoot, "CombatOutcomeVFXBridge.cs"),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/CombatArt/ShipExposureReference.cs"
            ),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/CombatArt/"
                    + "CombatVFXPlaceholderReceiver.cs"
            ),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/VFX/Cannon/"
                    + "LingeringSmokeShapeExpansion.cs"
            ),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/Sailing/ShipSailingSpeed.cs"
            ),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/Sailing/ShipTurning.cs"
            ),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/Command/FormationGeometrySnapshot.cs"
            ),
            Path.Combine(
                Application.dataPath,
                "Assets/Game/Scripts/Command/FormationCommandController.cs"
            )
        };

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

        Assert.That(damageSource, Does.Contain("CombatRakingResult"));
        Assert.That(
            damageSource,
            Does.Contain("CombatRakingEvaluator.TryEvaluate")
        );

        Assert.That(projectileSource, Does.Not.Contain("ShipIntegrity"));
        Assert.That(projectileSource, Does.Not.Contain("CombatRaking"));
        Assert.That(projectileSource, Does.Not.Contain("CombatDamageResolver"));
        Assert.That(outcomeSource, Does.Not.Contain("ShipIntegrity"));
        Assert.That(outcomeSource, Does.Not.Contain("CombatDamageResolver"));
        Assert.That(outcomeSource, Does.Not.Contain("CombatRaking"));
        Assert.That(vfxSource, Does.Not.Contain("ShipIntegrity"));
        Assert.That(vfxSource, Does.Not.Contain("CombatDamageResolver"));
        Assert.That(vfxSource, Does.Not.Contain("CombatRaking"));

        foreach (string sourcePath in rakingIndependentSources)
        {
            Assert.That(
                File.ReadAllText(sourcePath),
                Does.Not.Contain("CombatRaking"),
                sourcePath
            );
        }
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
    public void FormalPrefabHasDamageAndRakingProfiles_GenericProxiesRemainClean()
    {
        GameObject combatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        ShipBroadsideFireExecutor executor =
            combatPrefab.GetComponent<ShipBroadsideFireExecutor>();
        ShipCombatState combatState =
            combatPrefab.GetComponent<ShipCombatState>();
        ShipIntegrity integrity = combatPrefab.GetComponent<ShipIntegrity>();
        ShipMuzzleSockets sockets =
            combatPrefab.GetComponent<ShipMuzzleSockets>();
        CombatVFXPlaceholderReceiver receiver = combatPrefab
            .GetComponentsInChildren<CombatVFXPlaceholderReceiver>(true)
            .Single();

        Assert.That(executor, Is.Not.Null);
        Assert.That(executor.CombatDamageProfile, Is.SameAs(foundationProfile));
        Assert.That(
            executor.CombatRakingProfile,
            Is.SameAs(foundationRakingProfile)
        );
        Assert.That(
            foundationRakingProfile.RakingHalfAngleDegrees,
            Is.EqualTo(20f)
        );
        Assert.That(
            foundationRakingProfile.BowDamageMultiplier,
            Is.EqualTo(2f)
        );
        Assert.That(
            foundationRakingProfile.SternDamageMultiplier,
            Is.EqualTo(2f)
        );
        Assert.That(foundationProfile.RoundShotBaseDamage, Is.EqualTo(100f));
        Assert.That(foundationProfile.BowMultiplier, Is.EqualTo(1f));
        Assert.That(foundationProfile.MidshipMultiplier, Is.EqualTo(1f));
        Assert.That(foundationProfile.SternMultiplier, Is.EqualTo(1f));
        Assert.That(combatState, Is.Not.Null);
        Assert.That(
            combatState.BroadsideReloadDurationSeconds,
            Is.EqualTo(10f)
        );
        Assert.That(integrity, Is.Not.Null);
        Assert.That(integrity.IntegrityProfile, Is.Not.Null);
        Assert.That(
            integrity.IntegrityProfile.MaximumIntegrity,
            Is.EqualTo(10000f)
        );
        Assert.That(
            integrity.IntegrityProfile.CombatDisabledThresholdNormalized,
            Is.EqualTo(0.25f)
        );
        Assert.That(
            integrity.IntegrityProfile.SinkingThresholdNormalized,
            Is.EqualTo(0.05f)
        );
        Assert.That(sockets, Is.Not.Null);
        Assert.That(sockets.PortMuzzles, Has.Count.EqualTo(13));
        Assert.That(sockets.StarboardMuzzles, Has.Count.EqualTo(13));
        Assert.That(receiver, Is.Not.Null);
        Assert.That(receiver.CannonLingeringSmokePrefab, Is.Not.Null);

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
            Assert.That(
                AssetDatabase.GetDependencies(path, true),
                Does.Not.Contain(RakingProfilePath),
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


    private static void SetTeamId(GameObject shipRoot, int teamId)
    {
        ShipCombatAffiliation affiliation =
            shipRoot.GetComponent<ShipCombatAffiliation>();

        if (affiliation == null)
        {
            affiliation = shipRoot.AddComponent<ShipCombatAffiliation>();
        }

        FieldInfo field = typeof(ShipCombatAffiliation).GetField(
            "teamId",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null);
        field.SetValue(affiliation, teamId);
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
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }


    private CombatDamageResult ResolveRakingHit(
        Vector3 travelDirectionWorld,
        CombatHullRegion region,
        CombatRakingProfile rakingProfile
    )
    {
        shot = CreateShotSample(
            sourceShip,
            travelDirectionWorld.normalized * 100f,
            Vector3.zero
        );
        FoundationShotOutcome outcome = ResolveHullOutcome(region);

        Assert.That(
            CombatDamageResolver.TryResolveAndApply(
                outcome,
                foundationProfile,
                rakingProfile,
                out CombatDamageResult result,
                out CombatDamageResolutionFailure failure
            ),
            Is.True,
            failure.ToString()
        );
        return result;
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


    private CombatRakingProfile CreateRakingProfile(
        float halfAngleDegrees,
        float bowMultiplier,
        float sternMultiplier
    )
    {
        CombatRakingProfile profile =
            ScriptableObject.CreateInstance<CombatRakingProfile>();
        temporaryObjects.Add(profile);
        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("rakingHalfAngleDegrees").floatValue =
            halfAngleDegrees;
        serialized.FindProperty("bowDamageMultiplier").floatValue =
            bowMultiplier;
        serialized.FindProperty("sternDamageMultiplier").floatValue =
            sternMultiplier;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }


    private static ShotSample CreateShotSample(GameObject source)
    {
        return CreateShotSample(
            source,
            new Vector3(40f, 8f, 2f),
            new Vector3(0f, -9.81f, 0f)
        );
    }


    private static ShotSample CreateShotSample(
        GameObject source,
        Vector3 initialVelocityWorld,
        Vector3 gravityWorld
    )
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
            initialVelocityWorld,
            gravityWorld,
            1.5f,
            -1.5f,
            10f,
            FoundationAmmunitionType.RoundShot
        });
    }
}
