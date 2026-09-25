using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatSemanticHitQueryTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";

    private GameObject sourceShip;
    private GameObject targetShip;
    private ShotSample shot;


    [SetUp]
    public void SetUp()
    {
        sourceShip = new GameObject("Semantic Query Source Ship");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        targetShip = UnityEngine.Object.Instantiate(prefab);
        shot = CreateShotSample(sourceShip);
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(sourceShip);
        UnityEngine.Object.DestroyImmediate(targetShip);
    }


    [TestCase(CombatHullRegion.Bow)]
    [TestCase(CombatHullRegion.Midship)]
    [TestCase(CombatHullRegion.Stern)]
    public void RegisteredCollider_ResolvesSemanticRegionAndTargetRoot(
        CombatHullRegion expectedRegion
    )
    {
        ShipCombatGeometry geometry =
            targetShip.GetComponent<ShipCombatGeometry>();
        CombatHitRegion region = GetRegion(geometry, expectedRegion);
        Vector3 point = new Vector3(12f, 3f, -8f);
        Vector3 normal = new Vector3(-0.75f, 0.2f, 0.4f);
        float elapsedTime = 0.75f;
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.CombatGeometryContact,
            region.QueryCollider,
            point,
            normal,
            elapsedTime
        );

        bool resolved = CombatSemanticHitQuery.TryResolve(
            contact,
            out CombatHitContext context,
            out CombatSemanticHitQueryFailure failure
        );

        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(CombatSemanticHitQueryFailure.None));
        Assert.That(context.SourceShot.SourceShipRootIdentity, Is.SameAs(sourceShip));
        Assert.That(context.TargetShipRoot, Is.SameAs(targetShip));
        Assert.That(context.TargetCombatGeometry, Is.SameAs(geometry));
        Assert.That(context.HitRegion, Is.SameAs(region));
        Assert.That(context.Region, Is.EqualTo(expectedRegion));
        Assert.That(context.WorldHitPoint, Is.EqualTo(point));
        Assert.That(context.WorldHitNormal, Is.EqualTo(normal));
        Assert.That(
            context.IncomingVelocityWorld,
            Is.EqualTo(
                shot.InitialVelocityWorld
                    + shot.GravityWorld * elapsedTime
            )
        );
        Assert.That(
            context.IncomingDirectionWorld,
            Is.EqualTo(context.IncomingVelocityWorld.normalized)
        );
        Assert.That(
            context.AmmunitionType,
            Is.EqualTo(FoundationAmmunitionType.RoundShot)
        );
    }


    [Test]
    public void UnregisteredGenericAndRenderObjects_CannotFabricateHitContext()
    {
        GameObject unregistered = CreateCandidate("Unregistered Collider", false);
        GameObject generic = CreateCandidate("ShipCollider", false);
        GameObject rendered = CreateCandidate("Rendered Hull", true);

        try
        {
            foreach (GameObject candidate in new[]
            {
                unregistered,
                generic,
                rendered
            })
            {
                ProjectileTerminalContact contact = CreateContact(
                    ProjectileTerminalContactKind.CombatGeometryContact,
                    candidate.GetComponent<Collider>(),
                    Vector3.one,
                    Vector3.up,
                    0.5f
                );

                Assert.That(
                    CombatSemanticHitQuery.TryResolve(
                        contact,
                        out _,
                        out CombatSemanticHitQueryFailure failure
                    ),
                    Is.False,
                    candidate.name
                );
                Assert.That(
                    failure,
                    Is.EqualTo(
                        CombatSemanticHitQueryFailure
                            .UnregisteredCombatRegion
                    ),
                    candidate.name
                );
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unregistered);
            UnityEngine.Object.DestroyImmediate(generic);
            UnityEngine.Object.DestroyImmediate(rendered);
        }
    }


    [Test]
    public void FormalGenericShipCollider_CannotBecomeSemanticHit()
    {
        Transform genericColliderObject = targetShip.transform.Find(
            "CollisionRoot/ShipCollider"
        );
        Assert.That(genericColliderObject, Is.Not.Null);
        Collider genericCollider =
            genericColliderObject.GetComponent<Collider>();
        Assert.That(genericCollider, Is.Not.Null);
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.CombatGeometryContact,
            genericCollider,
            Vector3.one,
            Vector3.up,
            0.5f
        );

        Assert.That(
            CombatSemanticHitQuery.TryResolve(
                contact,
                out _,
                out CombatSemanticHitQueryFailure failure
            ),
            Is.False
        );
        Assert.That(
            failure,
            Is.EqualTo(CombatSemanticHitQueryFailure.WrongPhysicsLayer)
        );
    }


    [Test]
    public void RegionWithoutRegisteredOwnerChain_FailsExplicitly()
    {
        GameObject candidate = CreateCandidate(
            "Unowned Combat Region",
            false
        );

        try
        {
            candidate.AddComponent<CombatHitRegion>();
            ProjectileTerminalContact contact = CreateContact(
                ProjectileTerminalContactKind.CombatGeometryContact,
                candidate.GetComponent<Collider>(),
                Vector3.one,
                Vector3.up,
                0.5f
            );

            Assert.That(
                CombatSemanticHitQuery.TryResolve(
                    contact,
                    out _,
                    out CombatSemanticHitQueryFailure failure
                ),
                Is.False
            );
            Assert.That(
                failure,
                Is.EqualTo(
                    CombatSemanticHitQueryFailure.InvalidOwnershipChain
                )
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(candidate);
        }
    }


    [Test]
    public void WaterContact_BecomesTargetlessGeometricMiss()
    {
        Vector3 point = new Vector3(40f, -1.5f, 25f);
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.WaterContact,
            null,
            point,
            Vector3.up,
            1.25f
        );

        bool resolved = FoundationShotOutcomeResolver.TryResolve(
            contact,
            out FoundationShotOutcome outcome,
            out FoundationShotOutcomeFailure failure
        );

        Assert.That(resolved, Is.True);
        Assert.That(failure, Is.EqualTo(FoundationShotOutcomeFailure.None));
        Assert.That(outcome.Kind, Is.EqualTo(FoundationShotOutcomeKind.WaterMiss));
        Assert.That(outcome.IsHit, Is.False);
        Assert.That(outcome.HasHitContext, Is.False);
        Assert.That(outcome.HitContext.TargetShipRoot, Is.Null);
        Assert.That(outcome.WorldPoint, Is.EqualTo(point));
        Assert.That(outcome.WorldNormal, Is.EqualTo(Vector3.up));
        Assert.That(outcome.SourceShot.SourceShipRootIdentity, Is.SameAs(sourceShip));
    }


    [Test]
    public void ExpiredFallback_IsExplicitNonHitAndEmitsNoImpactEvent()
    {
        ProjectileTerminalContact contact = CreateContact(
            ProjectileTerminalContactKind.ExpiredSafetyFallback,
            null,
            new Vector3(100f, 20f, 30f),
            Vector3.zero,
            shot.MaxLifetimeSeconds
        );
        RecordingReceiver receiver = new RecordingReceiver();

        Assert.That(
            FoundationShotOutcomeResolver.TryResolve(
                contact,
                out FoundationShotOutcome outcome,
                out _
            ),
            Is.True
        );
        Assert.That(
            outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.ExpiredNonHit)
        );
        Assert.That(outcome.IsHit, Is.False);
        Assert.That(outcome.HasHitContext, Is.False);
        Assert.That(
            CombatOutcomeVFXBridge.TryEmitResolvedOutcome(
                outcome,
                receiver
            ),
            Is.False
        );
        Assert.That(receiver.HullImpactCount, Is.Zero);
        Assert.That(receiver.WaterImpactCount, Is.Zero);
    }


    [Test]
    public void VFXBridge_EmitsOnlyTheEventMatchingResolvedOutcome()
    {
        ShipCombatGeometry geometry =
            targetShip.GetComponent<ShipCombatGeometry>();
        Vector3 hullPoint = new Vector3(8f, 2f, -4f);
        Vector3 hullNormal = Vector3.left;
        FoundationShotOutcome hullOutcome = ResolveOutcome(CreateContact(
            ProjectileTerminalContactKind.CombatGeometryContact,
            geometry.MidshipRegion.QueryCollider,
            hullPoint,
            hullNormal,
            0.5f
        ));
        Vector3 waterPoint = new Vector3(20f, -1.5f, 12f);
        FoundationShotOutcome waterOutcome = ResolveOutcome(CreateContact(
            ProjectileTerminalContactKind.WaterContact,
            null,
            waterPoint,
            Vector3.up,
            1f
        ));
        RecordingReceiver receiver = new RecordingReceiver();
        FoundationShotOutcome preservedHullOutcome = hullOutcome;

        Assert.That(
            CombatOutcomeVFXBridge.TryEmitResolvedOutcome(
                hullOutcome,
                receiver
            ),
            Is.True
        );
        Assert.That(receiver.HullImpactCount, Is.EqualTo(1));
        Assert.That(receiver.WaterImpactCount, Is.Zero);
        Assert.That(receiver.LastHullImpact.PositionWorld, Is.EqualTo(hullPoint));
        Assert.That(receiver.LastHullImpact.NormalWorld, Is.EqualTo(hullNormal));
        Assert.That(receiver.LastHullImpact.TargetShip, Is.SameAs(targetShip));
        Assert.That(hullOutcome.Kind, Is.EqualTo(preservedHullOutcome.Kind));
        Assert.That(hullOutcome.WorldPoint, Is.EqualTo(preservedHullOutcome.WorldPoint));
        Assert.That(hullOutcome.WorldNormal, Is.EqualTo(preservedHullOutcome.WorldNormal));
        Assert.That(
            hullOutcome.HitContext.TargetShipRoot,
            Is.SameAs(preservedHullOutcome.HitContext.TargetShipRoot)
        );

        Assert.That(
            CombatOutcomeVFXBridge.TryEmitResolvedOutcome(
                waterOutcome,
                receiver
            ),
            Is.True
        );
        Assert.That(receiver.HullImpactCount, Is.EqualTo(1));
        Assert.That(receiver.WaterImpactCount, Is.EqualTo(1));
        Assert.That(receiver.LastWaterImpact.PositionWorld, Is.EqualTo(waterPoint));
        Assert.That(receiver.LastWaterImpact.NormalWorld, Is.EqualTo(Vector3.up));
    }


    [Test]
    public void InvalidSemanticContact_EmitsNoHullImpact()
    {
        GameObject generic = CreateCandidate("ShipCollider", false);

        try
        {
            RecordingReceiver receiver = new RecordingReceiver();
            ProjectileTerminalContact contact = CreateContact(
                ProjectileTerminalContactKind.CombatGeometryContact,
                generic.GetComponent<Collider>(),
                Vector3.one,
                Vector3.up,
                0.5f
            );

            Assert.That(
                FoundationShotOutcomeResolver.TryResolve(
                    contact,
                    out _,
                    out FoundationShotOutcomeFailure failure
                ),
                Is.False
            );
            Assert.That(
                failure,
                Is.EqualTo(
                    FoundationShotOutcomeFailure.SemanticHitQueryFailed
                )
            );
            Assert.That(receiver.HullImpactCount, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(generic);
        }
    }


    [Test]
    public void MuzzleBridge_PreparesShotOriginAndDirectionWithoutEmittingOutcome()
    {
        RecordingReceiver receiver = new RecordingReceiver();

        Assert.That(
            CombatOutcomeVFXBridge.TryEmitMuzzleFire(
                shot,
                "Port-00",
                receiver
            ),
            Is.True
        );
        Assert.That(receiver.MuzzleFireCount, Is.EqualTo(1));
        Assert.That(receiver.HullImpactCount, Is.Zero);
        Assert.That(receiver.WaterImpactCount, Is.Zero);
        Assert.That(receiver.LastMuzzleFire.PositionWorld, Is.EqualTo(shot.OriginWorld));
        Assert.That(
            receiver.LastMuzzleFire.DirectionWorld,
            Is.EqualTo(shot.InitialVelocityWorld.normalized)
        );
        Assert.That(receiver.LastMuzzleFire.SourceShip, Is.SameAs(sourceShip));
        Assert.That(receiver.LastMuzzleFire.MuzzleIdentifier, Is.EqualTo("Port-00"));
    }


    [Test]
    public void QueryAndVFX_DoNotMutateShipPoseOrReloadState()
    {
        ShipCombatState combatState = targetShip.GetComponent<ShipCombatState>();
        ShipCombatGeometry geometry =
            targetShip.GetComponent<ShipCombatGeometry>();
        Vector3 sourcePosition = sourceShip.transform.position;
        Quaternion sourceRotation = sourceShip.transform.rotation;
        Vector3 targetPosition = targetShip.transform.position;
        Quaternion targetRotation = targetShip.transform.rotation;
        BroadsideReloadState portState = combatState.PortBroadsideState;
        BroadsideReloadState starboardState = combatState.StarboardBroadsideState;
        float portRemaining = combatState.PortReloadRemainingSeconds;
        float starboardRemaining = combatState.StarboardReloadRemainingSeconds;
        RecordingReceiver receiver = new RecordingReceiver();
        FoundationShotOutcome outcome = ResolveOutcome(CreateContact(
            ProjectileTerminalContactKind.CombatGeometryContact,
            geometry.BowRegion.QueryCollider,
            Vector3.one,
            Vector3.back,
            0.5f
        ));

        CombatOutcomeVFXBridge.TryEmitResolvedOutcome(outcome, receiver);

        Assert.That(sourceShip.transform.position, Is.EqualTo(sourcePosition));
        Assert.That(sourceShip.transform.rotation, Is.EqualTo(sourceRotation));
        Assert.That(targetShip.transform.position, Is.EqualTo(targetPosition));
        Assert.That(targetShip.transform.rotation, Is.EqualTo(targetRotation));
        Assert.That(combatState.PortBroadsideState, Is.EqualTo(portState));
        Assert.That(combatState.StarboardBroadsideState, Is.EqualTo(starboardState));
        Assert.That(combatState.PortReloadRemainingSeconds, Is.EqualTo(portRemaining));
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.EqualTo(starboardRemaining));
    }


    [Test]
    public void OutcomeContractsAreImmutable_AndProjectileOwnsNoResolution()
    {
        foreach (Type type in new[]
        {
            typeof(CombatHitContext),
            typeof(FoundationShotOutcome)
        })
        {
            Assert.That(type.IsValueType, Is.True);
            Assert.That(
                type.GetProperties().All(property => property.SetMethod == null),
                Is.True,
                type.Name
            );
            Assert.That(
                type.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                ).All(field => field.IsInitOnly),
                Is.True,
                type.Name
            );
        }

        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string projectileSource = File.ReadAllText(
            Path.Combine(combatRoot, "CombatProjectile.cs")
        );
        string phase6DSource = string.Join(
            "\n",
            new[]
            {
                "CombatHitContext.cs",
                "CombatSemanticHitQuery.cs",
                "FoundationShotOutcome.cs",
                "FoundationShotOutcomeResolver.cs",
                "CombatOutcomeVFXBridge.cs"
            }.Select(fileName => File.ReadAllText(
                Path.Combine(combatRoot, fileName)
            ))
        );

        Assert.That(projectileSource, Does.Not.Contain("CombatSemanticHitQuery"));
        Assert.That(projectileSource, Does.Not.Contain("FoundationShotOutcome"));
        Assert.That(projectileSource, Does.Not.Contain("CombatOutcomeVFXBridge"));
        Assert.That(projectileSource, Does.Not.Contain("Damage"));
        Assert.That(projectileSource, Does.Not.Contain("Integrity"));
        Assert.That(projectileSource, Does.Not.Contain("HitChance"));
        Assert.That(phase6DSource, Does.Not.Contain("HitChance"));
        Assert.That(phase6DSource, Does.Not.Contain("UnityEngine.Random"));
        Assert.That(phase6DSource, Does.Not.Contain("Damage"));
        Assert.That(phase6DSource, Does.Not.Contain("Integrity"));
        Assert.That(phase6DSource, Does.Not.Contain("Disabled"));
        Assert.That(phase6DSource, Does.Not.Contain("Sinking"));
        Assert.That(phase6DSource, Does.Not.Contain("Raking"));
        Assert.That(phase6DSource, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(phase6DSource, Does.Not.Contain("SetAutoFireEnabled"));
        Assert.That(phase6DSource, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(phase6DSource, Does.Not.Contain("ShipTurning"));
        Assert.That(phase6DSource, Does.Not.Contain("ShipDestinationController"));
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
            8f,
            FoundationAmmunitionType.RoundShot
        });
    }


    private static CombatHitRegion GetRegion(
        ShipCombatGeometry geometry,
        CombatHullRegion region
    )
    {
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


    private static GameObject CreateCandidate(
        string name,
        bool addRenderer
    )
    {
        GameObject candidate = new GameObject(name);
        candidate.layer = 8;
        candidate.AddComponent<BoxCollider>().isTrigger = true;

        if (addRenderer)
        {
            candidate.AddComponent<MeshFilter>();
            candidate.AddComponent<MeshRenderer>();
        }

        return candidate;
    }


    private sealed class RecordingReceiver : ICombatVFXEventReceiver
    {
        public int MuzzleFireCount { get; private set; }
        public int WaterImpactCount { get; private set; }
        public int HullImpactCount { get; private set; }
        public CombatMuzzleFireEvent LastMuzzleFire { get; private set; }
        public CombatWaterImpactEvent LastWaterImpact { get; private set; }
        public CombatHullImpactEvent LastHullImpact { get; private set; }


        public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
        {
            MuzzleFireCount++;
            LastMuzzleFire = eventData;
        }


        public void OnWaterImpact(CombatWaterImpactEvent eventData)
        {
            WaterImpactCount++;
            LastWaterImpact = eventData;
        }


        public void OnHullImpact(CombatHullImpactEvent eventData)
        {
            HullImpactCount++;
            LastHullImpact = eventData;
        }
    }
}
