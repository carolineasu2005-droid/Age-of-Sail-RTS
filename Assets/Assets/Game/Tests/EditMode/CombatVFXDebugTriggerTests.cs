using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatVFXDebugTriggerTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new List<GameObject>();


    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }


    [Test]
    public void MuzzleCommands_ForwardRegisteredSocketWorldPosesOnly()
    {
        GameObject ship = CreateObject("Ship Root");
        ship.transform.SetPositionAndRotation(
            new Vector3(31f, 2f, -18f),
            Quaternion.Euler(0f, 43f, 0f)
        );
        Transform portContainer = CreateChild(ship.transform, "Port");
        Transform starboardContainer = CreateChild(
            ship.transform,
            "Starboard"
        );
        Transform portSocket = CreateChild(portContainer, "P01");
        portSocket.localPosition = new Vector3(-5.5f, 0.75f, 3f);
        portSocket.localRotation = Quaternion.Euler(0f, -90f, 0f);
        Transform starboardSocket = CreateChild(starboardContainer, "S01");
        starboardSocket.localPosition = new Vector3(5.5f, 0.75f, -3f);
        starboardSocket.localRotation = Quaternion.Euler(0f, 90f, 0f);

        ShipMuzzleSockets sockets = ship.AddComponent<ShipMuzzleSockets>();
        SetField(sockets, "portMuzzlesContainer", portContainer);
        SetField(sockets, "starboardMuzzlesContainer", starboardContainer);
        RecordingCombatVFXEventReceiver receiver =
            ship.AddComponent<RecordingCombatVFXEventReceiver>();
        CombatVFXDebugTrigger trigger =
            ship.AddComponent<CombatVFXDebugTrigger>();
        SetField(trigger, "receiverBehaviour", receiver);
        SetField(trigger, "muzzleSockets", sockets);
        Vector3 shipPosition = ship.transform.position;
        Quaternion shipRotation = ship.transform.rotation;

        InvokeContextCommand(trigger, "TriggerPortMuzzleFire");
        InvokeContextCommand(trigger, "TriggerStarboardMuzzleFire");

        Assert.That(receiver.MuzzleEvents, Has.Count.EqualTo(2));
        AssertMuzzleEvent(receiver.MuzzleEvents[0], portSocket, ship);
        AssertMuzzleEvent(receiver.MuzzleEvents[1], starboardSocket, ship);
        Assert.That(ship.transform.position, Is.EqualTo(shipPosition));
        Assert.That(ship.transform.rotation, Is.EqualTo(shipRotation));
    }


    [Test]
    public void ImpactCommands_ForwardConfiguredWorldPosesOnly()
    {
        GameObject triggerObject = CreateObject("Debug Trigger");
        GameObject targetShip = CreateObject("Target Ship");
        RecordingCombatVFXEventReceiver receiver =
            triggerObject.AddComponent<RecordingCombatVFXEventReceiver>();
        CombatVFXDebugTrigger trigger =
            triggerObject.AddComponent<CombatVFXDebugTrigger>();
        Transform waterReference = CreateObject("Water Impact Reference")
            .transform;
        waterReference.SetPositionAndRotation(
            new Vector3(14f, 0f, -27f),
            Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(0.1f, 0.98f, 0.15f).normalized
            )
        );
        Transform hullReference = CreateObject("Hull Impact Reference")
            .transform;
        hullReference.SetPositionAndRotation(
            new Vector3(-9f, 2.2f, 6f),
            Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(-0.9f, 0.1f, 0.3f).normalized
            )
        );
        SetField(trigger, "receiverBehaviour", receiver);
        SetField(trigger, "waterImpactReference", waterReference);
        SetField(trigger, "hullImpactReference", hullReference);
        SetField(trigger, "hullTargetShip", targetShip);
        Vector3 targetPosition = targetShip.transform.position;
        Quaternion targetRotation = targetShip.transform.rotation;

        InvokeContextCommand(trigger, "TriggerWaterImpact");
        InvokeContextCommand(trigger, "TriggerHullImpact");

        Assert.That(receiver.WaterEvents, Has.Count.EqualTo(1));
        Assert.That(
            receiver.WaterEvents[0].PositionWorld,
            Is.EqualTo(waterReference.position)
        );
        Assert.That(
            receiver.WaterEvents[0].NormalWorld,
            Is.EqualTo(waterReference.up)
        );
        Assert.That(receiver.HullEvents, Has.Count.EqualTo(1));
        Assert.That(
            receiver.HullEvents[0].PositionWorld,
            Is.EqualTo(hullReference.position)
        );
        Assert.That(
            receiver.HullEvents[0].NormalWorld,
            Is.EqualTo(hullReference.up)
        );
        Assert.That(
            receiver.HullEvents[0].TargetShip,
            Is.SameAs(targetShip)
        );
        Assert.That(
            receiver.HullEvents[0].HitRegion,
            Is.EqualTo(CombatHullRegion.Midship)
        );
        Assert.That(targetShip.transform.position, Is.EqualTo(targetPosition));
        Assert.That(targetShip.transform.rotation, Is.EqualTo(targetRotation));
    }


    [TestCase("TriggerPortMuzzleFire")]
    [TestCase("TriggerStarboardMuzzleFire")]
    [TestCase("TriggerWaterImpact")]
    [TestCase("TriggerHullImpact")]
    public void ManualCommand_HasInspectorContextMenu(string methodName)
    {
        MethodInfo method = typeof(CombatVFXDebugTrigger).GetMethod(
            methodName,
            PrivateInstance
        );

        Assert.That(method, Is.Not.Null);
        Assert.That(
            method.GetCustomAttribute<ContextMenu>(),
            Is.Not.Null
        );
    }


    private GameObject CreateObject(string name)
    {
        GameObject createdObject = new GameObject(name);
        createdObjects.Add(createdObject);
        return createdObject;
    }


    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }


    private static void SetField(
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


    private static void InvokeContextCommand(
        CombatVFXDebugTrigger trigger,
        string methodName
    )
    {
        MethodInfo method = typeof(CombatVFXDebugTrigger).GetMethod(
            methodName,
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null, $"Missing command {methodName}.");
        method.Invoke(trigger, null);
    }


    private static void AssertMuzzleEvent(
        CombatMuzzleFireEvent eventData,
        Transform expectedSocket,
        GameObject expectedShip
    )
    {
        Assert.That(eventData.PositionWorld, Is.EqualTo(expectedSocket.position));
        Assert.That(
            eventData.DirectionWorld,
            Is.EqualTo(expectedSocket.forward)
        );
        Assert.That(eventData.SourceShip, Is.SameAs(expectedShip));
        Assert.That(eventData.MuzzleIdentifier, Is.EqualTo(expectedSocket.name));
    }
}

public sealed class RecordingCombatVFXEventReceiver
    : MonoBehaviour,
        ICombatVFXEventReceiver
{
    public List<CombatMuzzleFireEvent> MuzzleEvents { get; } =
        new List<CombatMuzzleFireEvent>();

    public List<CombatWaterImpactEvent> WaterEvents { get; } =
        new List<CombatWaterImpactEvent>();

    public List<CombatHullImpactEvent> HullEvents { get; } =
        new List<CombatHullImpactEvent>();


    public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
    {
        MuzzleEvents.Add(eventData);
    }


    public void OnWaterImpact(CombatWaterImpactEvent eventData)
    {
        WaterEvents.Add(eventData);
    }


    public void OnHullImpact(CombatHullImpactEvent eventData)
    {
        HullEvents.Add(eventData);
    }
}
