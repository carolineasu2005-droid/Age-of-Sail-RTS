using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatVFXEventContractTests
{
    private static readonly Type[] EventTypes =
    {
        typeof(CombatMuzzleFireEvent),
        typeof(CombatWaterImpactEvent),
        typeof(CombatHullImpactEvent)
    };


    [Test]
    public void MuzzleEvent_PreservesConstructedData()
    {
        GameObject sourceShip = new GameObject("Source Ship");

        try
        {
            Transform muzzle = new GameObject("P07").transform;
            muzzle.SetParent(sourceShip.transform);
            Vector3 position = new Vector3(10f, 2f, -4f);
            Vector3 direction = new Vector3(-1f, 0.2f, 0.5f);
            CombatMuzzleFireEvent eventData = new CombatMuzzleFireEvent(
                position,
                direction,
                sourceShip,
                "P07",
                muzzle
            );

            position = Vector3.zero;
            direction = Vector3.zero;

            Assert.That(eventData.PositionWorld, Is.EqualTo(new Vector3(10f, 2f, -4f)));
            Assert.That(eventData.DirectionWorld, Is.EqualTo(new Vector3(-1f, 0.2f, 0.5f)));
            Assert.That(eventData.SourceShip, Is.SameAs(sourceShip));
            Assert.That(eventData.MuzzleIdentifier, Is.EqualTo("P07"));
            Assert.That(eventData.MuzzleTransform, Is.SameAs(muzzle));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceShip);
        }
    }


    [Test]
    public void WaterImpactEvent_PreservesConstructedData()
    {
        Vector3 position = new Vector3(7f, 0f, 19f);
        Vector3 normal = new Vector3(0.1f, 0.95f, -0.2f);
        CombatWaterImpactEvent eventData = new CombatWaterImpactEvent(
            position,
            normal
        );

        position = Vector3.zero;
        normal = Vector3.zero;

        Assert.That(eventData.PositionWorld, Is.EqualTo(new Vector3(7f, 0f, 19f)));
        Assert.That(eventData.NormalWorld, Is.EqualTo(new Vector3(0.1f, 0.95f, -0.2f)));
    }


    [Test]
    public void HullImpactEvent_PreservesConstructedData()
    {
        GameObject targetShip = new GameObject("Target Ship");

        try
        {
            Vector3 position = new Vector3(-12f, 3f, 8f);
            Vector3 normal = new Vector3(0.8f, 0.1f, -0.4f);
            CombatHullImpactEvent eventData = new CombatHullImpactEvent(
                position,
                normal,
                targetShip,
                CombatHullRegion.Stern
            );

            position = Vector3.zero;
            normal = Vector3.zero;

            Assert.That(eventData.PositionWorld, Is.EqualTo(new Vector3(-12f, 3f, 8f)));
            Assert.That(eventData.NormalWorld, Is.EqualTo(new Vector3(0.8f, 0.1f, -0.4f)));
            Assert.That(eventData.TargetShip, Is.SameAs(targetShip));
            Assert.That(
                eventData.HitRegion,
                Is.EqualTo(CombatHullRegion.Stern)
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetShip);
        }
    }


    [Test]
    public void EventContracts_AreReadOnlyAndOnlyMuzzleEventCarriesTransform()
    {
        foreach (Type eventType in EventTypes)
        {
            Assert.That(eventType.IsValueType, Is.True, eventType.Name);

            foreach (PropertyInfo property in eventType.GetProperties())
            {
                Assert.That(property.GetMethod, Is.Not.Null, property.Name);
                Assert.That(property.SetMethod, Is.Null, property.Name);
                Assert.That(
                    property.PropertyType == typeof(Transform),
                    Is.EqualTo(eventType == typeof(CombatMuzzleFireEvent)
                        && property.Name == nameof(CombatMuzzleFireEvent.MuzzleTransform))
                );
            }

            foreach (FieldInfo field in eventType.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ))
            {
                Assert.That(field.IsInitOnly, Is.True, field.Name);
                Assert.That(
                    field.FieldType == typeof(Transform),
                    Is.EqualTo(eventType == typeof(CombatMuzzleFireEvent)
                        && field.Name == "<MuzzleTransform>k__BackingField")
                );
            }
        }
    }


    [Test]
    public void Construction_RequiresNoGameplayComponents()
    {
        GameObject sourceShip = new GameObject("Source Ship");
        GameObject targetShip = new GameObject("Target Ship");

        try
        {
            Assert.That(sourceShip.GetComponents<Component>(), Has.Length.EqualTo(1));
            Assert.That(targetShip.GetComponents<Component>(), Has.Length.EqualTo(1));

            Assert.DoesNotThrow(() =>
            {
                _ = new CombatMuzzleFireEvent(
                    Vector3.one,
                    Vector3.forward,
                    sourceShip,
                    "S01"
                );
                _ = new CombatWaterImpactEvent(Vector3.one, Vector3.up);
                _ = new CombatHullImpactEvent(
                    Vector3.one,
                    Vector3.right,
                    targetShip,
                    CombatHullRegion.Bow
                );
            });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceShip);
            UnityEngine.Object.DestroyImmediate(targetShip);
        }
    }


    [Test]
    public void MultipleEvents_RemainIndependent()
    {
        CombatWaterImpactEvent first = new CombatWaterImpactEvent(
            new Vector3(1f, 2f, 3f),
            Vector3.up
        );
        CombatWaterImpactEvent second = new CombatWaterImpactEvent(
            new Vector3(20f, 30f, 40f),
            Vector3.forward
        );

        Assert.That(first.PositionWorld, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        Assert.That(first.NormalWorld, Is.EqualTo(Vector3.up));
        Assert.That(second.PositionWorld, Is.EqualTo(new Vector3(20f, 30f, 40f)));
        Assert.That(second.NormalWorld, Is.EqualTo(Vector3.forward));
    }


    [Test]
    public void ReceiverInterface_ContainsOnlyTheThreeVFXNotifications()
    {
        MethodInfo[] methods = typeof(ICombatVFXEventReceiver).GetMethods();

        Assert.That(methods, Has.Length.EqualTo(3));
        AssertReceiverMethod(methods, "OnMuzzleFire", typeof(CombatMuzzleFireEvent));
        AssertReceiverMethod(methods, "OnWaterImpact", typeof(CombatWaterImpactEvent));
        AssertReceiverMethod(methods, "OnHullImpact", typeof(CombatHullImpactEvent));
    }


    private static void AssertReceiverMethod(
        MethodInfo[] methods,
        string methodName,
        Type eventType
    )
    {
        MethodInfo method = methods.Single(candidate => candidate.Name == methodName);
        ParameterInfo[] parameters = method.GetParameters();

        Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
        Assert.That(parameters, Has.Length.EqualTo(1));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(eventType));
    }
}
