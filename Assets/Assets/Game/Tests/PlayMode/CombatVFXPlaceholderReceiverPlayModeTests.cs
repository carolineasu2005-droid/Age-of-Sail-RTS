using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CombatVFXPlaceholderReceiverPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const float PositionTolerance = 0.0001f;
    private const float DirectionDotTolerance = 0.9999f;

    private static readonly Type[] MovementBehaviourTypes =
    {
        typeof(ShipMovementProfileController),
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipLeeway),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController)
    };

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private GameObject receiverObject;
    private CombatVFXPlaceholderReceiver receiver;


    [UnitySetUp]
    public IEnumerator SetUp()
    {
        receiverObject = new GameObject("Placeholder VFX Receiver");
        receiver = receiverObject.AddComponent<CombatVFXPlaceholderReceiver>();
        yield return null;
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject spawnedObject in spawnedObjects)
        {
            if (spawnedObject != null)
            {
                UnityEngine.Object.Destroy(spawnedObject);
            }
        }

        if (receiverObject != null)
        {
            UnityEngine.Object.Destroy(receiverObject);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator GelderlandPortAndStarboardMuzzles_SpawnAtSocketWorldPose()
    {
        GameObject ship = InstantiateCombatShip();

        try
        {
            ShipMuzzleSockets sockets = ship.GetComponent<ShipMuzzleSockets>();
            ShipArtDefinition artDefinition =
                ship.GetComponent<ShipArtDefinition>();
            Assert.That(sockets, Is.Not.Null);
            Assert.That(artDefinition, Is.Not.Null);
            Assert.That(sockets.PortMuzzles, Is.Not.Empty);
            Assert.That(sockets.StarboardMuzzles, Is.Not.Empty);

            Transform portSocket = sockets.PortMuzzles[0];
            Transform starboardSocket = sockets.StarboardMuzzles[0];
            float portSocketLocalX =
                ship.transform.InverseTransformPoint(portSocket.position).x;
            float starboardSocketLocalX = ship.transform
                .InverseTransformPoint(starboardSocket.position)
                .x;
            float portSideLocalX = ship.transform
                .InverseTransformPoint(artDefinition.PortReference.position)
                .x;
            float starboardSideLocalX = ship.transform
                .InverseTransformPoint(artDefinition.StarboardReference.position)
                .x;
            Assert.That(
                portSocketLocalX,
                Is.LessThanOrEqualTo(portSideLocalX + PositionTolerance)
            );
            Assert.That(
                starboardSocketLocalX,
                Is.GreaterThanOrEqualTo(
                    starboardSideLocalX - PositionTolerance
                )
            );
            Assert.That(
                Vector3.Dot(portSocket.forward, ship.transform.right),
                Is.LessThan(-DirectionDotTolerance)
            );
            Assert.That(
                Vector3.Dot(starboardSocket.forward, ship.transform.right),
                Is.GreaterThan(DirectionDotTolerance)
            );

            GameObject portPlaceholder = SpawnMuzzlePlaceholder(
                ship,
                portSocket
            );
            GameObject starboardPlaceholder = SpawnMuzzlePlaceholder(
                ship,
                starboardSocket
            );

            AssertWorldPose(
                portPlaceholder.transform,
                portSocket.position,
                portSocket.forward,
                false
            );
            AssertWorldPose(
                starboardPlaceholder.transform,
                starboardSocket.position,
                starboardSocket.forward,
                false
            );
        }
        finally
        {
            UnityEngine.Object.Destroy(ship);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator MuzzleSpawn_UsesSocketPoseAfterShipTranslationAndRotation()
    {
        GameObject ship = InstantiateCombatShip();

        try
        {
            ShipMuzzleSockets sockets = ship.GetComponent<ShipMuzzleSockets>();
            Transform socket = sockets.StarboardMuzzles[6];
            Vector3 originalPosition = socket.position;
            Vector3 originalDirection = socket.forward;

            ship.transform.SetPositionAndRotation(
                new Vector3(80f, 3f, -45f),
                Quaternion.Euler(0f, 127f, 0f)
            );

            Assert.That(
                Vector3.Distance(socket.position, originalPosition),
                Is.GreaterThan(1f)
            );
            Assert.That(
                Vector3.Dot(socket.forward, originalDirection),
                Is.LessThan(0.99f)
            );

            GameObject placeholder = SpawnMuzzlePlaceholder(ship, socket);

            AssertWorldPose(
                placeholder.transform,
                socket.position,
                socket.forward,
                false
            );
        }
        finally
        {
            UnityEngine.Object.Destroy(ship);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator WaterImpact_SpawnsAtPositionWithUpAlignedToNormal()
    {
        Vector3 position = new Vector3(17f, 0.25f, -31f);
        Vector3 normal = new Vector3(0.2f, 0.95f, -0.1f).normalized;
        Vector3 receiverPosition = receiverObject.transform.position;
        Quaternion receiverRotation = receiverObject.transform.rotation;
        HashSet<GameObject> before = CaptureGameObjects();

        receiver.OnWaterImpact(new CombatWaterImpactEvent(position, normal));
        GameObject placeholder = CaptureSpawnedObject(before);

        AssertWorldPose(placeholder.transform, position, normal, true);
        AssertTransformUnchanged(
            receiverObject.transform,
            receiverPosition,
            receiverRotation
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator HullImpact_SpawnsAtPositionWithUpAlignedToNormal()
    {
        GameObject targetShip = InstantiateCombatShip();

        try
        {
            targetShip.transform.SetPositionAndRotation(
                new Vector3(-12f, 4f, 9f),
                Quaternion.Euler(0f, 38f, 0f)
            );
            ShipCombatGeometry geometry =
                targetShip.GetComponent<ShipCombatGeometry>();
            BoxCollider hullCollider =
                geometry.MidshipRegion.QueryCollider as BoxCollider;
            Assert.That(hullCollider, Is.Not.Null);

            Vector3 position = hullCollider.transform.TransformPoint(
                hullCollider.center
                    + Vector3.right * (hullCollider.size.x * 0.5f)
            );
            Vector3 normal = hullCollider.transform.right.normalized;
            Vector3 receiverPosition = receiverObject.transform.position;
            Quaternion receiverRotation = receiverObject.transform.rotation;
            Vector3 targetPosition = targetShip.transform.position;
            Quaternion targetRotation = targetShip.transform.rotation;
            HashSet<GameObject> before = CaptureGameObjects();

            receiver.OnHullImpact(
                new CombatHullImpactEvent(position, normal, targetShip)
            );
            GameObject placeholder = CaptureSpawnedObject(before);

            AssertWorldPose(placeholder.transform, position, normal, true);
            AssertTransformUnchanged(
                receiverObject.transform,
                receiverPosition,
                receiverRotation
            );
            AssertTransformUnchanged(
                targetShip.transform,
                targetPosition,
                targetRotation
            );
        }
        finally
        {
            UnityEngine.Object.Destroy(targetShip);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator DisabledReceiver_CreatesNothingAndChangesNoOwnedInput()
    {
        GameObject sourceShip = InstantiateCombatShip();
        GameObject targetShip = new GameObject("Target Ship");

        try
        {
            receiver.VisualSpawningEnabled = false;
            ShipArtDefinition artDefinition =
                sourceShip.GetComponent<ShipArtDefinition>();
            ShipMuzzleSockets sockets =
                sourceShip.GetComponent<ShipMuzzleSockets>();
            ShipCombatGeometry geometry =
                sourceShip.GetComponent<ShipCombatGeometry>();
            ShipExposureReference exposure =
                sourceShip.GetComponent<ShipExposureReference>();
            Transform visualRoot = artDefinition.VisualRoot;
            Transform portMuzzle = sockets.PortMuzzles[0];
            Collider midshipCollider = geometry.MidshipRegion.QueryCollider;
            CombatMuzzleFireEvent muzzleEvent = new CombatMuzzleFireEvent(
                portMuzzle.position,
                portMuzzle.forward,
                sourceShip,
                portMuzzle.name
            );
            CombatWaterImpactEvent waterEvent = new CombatWaterImpactEvent(
                new Vector3(4f, 5f, 6f),
                Vector3.up
            );
            CombatHullImpactEvent hullEvent = new CombatHullImpactEvent(
                new Vector3(7f, 8f, 9f),
                Vector3.forward,
                targetShip
            );
            Vector3 receiverPosition = receiverObject.transform.position;
            Quaternion receiverRotation = receiverObject.transform.rotation;
            Vector3 sourcePosition = sourceShip.transform.position;
            Quaternion sourceRotation = sourceShip.transform.rotation;
            Vector3 targetPosition = targetShip.transform.position;
            Quaternion targetRotation = targetShip.transform.rotation;
            HashSet<GameObject> before = CaptureGameObjects();

            receiver.OnMuzzleFire(muzzleEvent);
            receiver.OnWaterImpact(waterEvent);
            receiver.OnHullImpact(hullEvent);

            Assert.That(CaptureGameObjects(), Is.EquivalentTo(before));
            Assert.That(muzzleEvent.PositionWorld, Is.EqualTo(portMuzzle.position));
            Assert.That(muzzleEvent.DirectionWorld, Is.EqualTo(portMuzzle.forward));
            Assert.That(waterEvent.PositionWorld, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(waterEvent.NormalWorld, Is.EqualTo(Vector3.up));
            Assert.That(hullEvent.PositionWorld, Is.EqualTo(new Vector3(7f, 8f, 9f)));
            Assert.That(hullEvent.NormalWorld, Is.EqualTo(Vector3.forward));
            AssertTransformUnchanged(
                receiverObject.transform,
                receiverPosition,
                receiverRotation
            );
            AssertTransformUnchanged(
                sourceShip.transform,
                sourcePosition,
                sourceRotation
            );
            AssertTransformUnchanged(
                targetShip.transform,
                targetPosition,
                targetRotation
            );
            Assert.That(artDefinition.VisualRoot, Is.SameAs(visualRoot));
            Assert.That(sockets.PortMuzzles[0], Is.SameAs(portMuzzle));
            Assert.That(
                geometry.MidshipRegion.QueryCollider,
                Is.SameAs(midshipCollider)
            );
            Assert.That(exposure, Is.Not.Null);
        }
        finally
        {
            UnityEngine.Object.Destroy(sourceShip);
            UnityEngine.Object.Destroy(targetShip);
        }

        yield return null;
    }


    private GameObject SpawnMuzzlePlaceholder(
        GameObject sourceShip,
        Transform socket
    )
    {
        Vector3 receiverPosition = receiverObject.transform.position;
        Quaternion receiverRotation = receiverObject.transform.rotation;
        Vector3 shipPosition = sourceShip.transform.position;
        Quaternion shipRotation = sourceShip.transform.rotation;
        HashSet<GameObject> before = CaptureGameObjects();

        receiver.OnMuzzleFire(
            new CombatMuzzleFireEvent(
                socket.position,
                socket.forward,
                sourceShip,
                socket.name
            )
        );
        GameObject placeholder = CaptureSpawnedObject(before);

        AssertTransformUnchanged(
            receiverObject.transform,
            receiverPosition,
            receiverRotation
        );
        AssertTransformUnchanged(
            sourceShip.transform,
            shipPosition,
            shipRotation
        );
        return placeholder;
    }


    private GameObject CaptureSpawnedObject(HashSet<GameObject> before)
    {
        GameObject[] addedObjects = UnityEngine.Object
            .FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .Where(candidate => !before.Contains(candidate))
            .ToArray();

        Assert.That(addedObjects, Has.Length.EqualTo(1));
        GameObject spawnedObject = addedObjects[0];
        Collider collider = spawnedObject.GetComponent<Collider>();

        Assert.That(collider == null || !collider.enabled, Is.True);
        spawnedObjects.Add(spawnedObject);
        return spawnedObject;
    }


    private static HashSet<GameObject> CaptureGameObjects()
    {
        return UnityEngine.Object
            .FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .ToHashSet();
    }


    private static void AssertWorldPose(
        Transform placeholder,
        Vector3 expectedPosition,
        Vector3 expectedDirection,
        bool directionUsesUp
    )
    {
        Assert.That(
            Vector3.Distance(placeholder.position, expectedPosition),
            Is.LessThan(PositionTolerance)
        );
        Vector3 actualDirection = directionUsesUp
            ? placeholder.up
            : placeholder.forward;
        Assert.That(
            Vector3.Dot(actualDirection, expectedDirection.normalized),
            Is.GreaterThan(DirectionDotTolerance)
        );
    }


    private static void AssertTransformUnchanged(
        Transform target,
        Vector3 expectedPosition,
        Quaternion expectedRotation
    )
    {
        Assert.That(target.position, Is.EqualTo(expectedPosition));
        Assert.That(target.rotation, Is.EqualTo(expectedRotation));
    }


    private static GameObject InstantiateCombatShip()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);

        foreach (Type componentType in MovementBehaviourTypes)
        {
            Behaviour behaviour = instance.GetComponent(componentType) as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        return instance;
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        return null;
#endif
    }
}
