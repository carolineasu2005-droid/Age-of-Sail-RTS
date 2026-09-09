using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerStopPlayModeTests
{
    private readonly List<Object> createdObjects = new();


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.Destroy(createdObject);
            }
        }

        yield return null;
        createdObjects.Clear();
    }


    [UnityTest]
    public IEnumerator PlayerStop_UsesNaturalDragWithoutSnappingCurrentSpeed()
    {
        GameObject windObject = CreateObject("Test Wind");
        GlobalWind wind = windObject.AddComponent<GlobalWind>();
        wind.windFromDegrees = 90f;

        GameObject shipObject = CreateObject("Stopped Ship");
        ShipSailingSpeed speed = shipObject.AddComponent<ShipSailingSpeed>();
        SailPolarProfile polarProfile = ScriptableObject.CreateInstance<
            SailPolarProfile
        >();
        createdObjects.Add(polarProfile);
        SetPrivateField(speed, "globalWind", wind);
        SetPrivateField(speed, "sailPolarProfile", polarProfile);
        SetPrivateField(speed, "currentSpeed", 3f);

        speed.SetPlayerStopSpeedCap(0f);
        yield return null;

        Assert.That(speed.IsPlayerStopped, Is.True);
        Assert.That(speed.CurrentSpeed, Is.GreaterThan(0f));
        Assert.That(speed.CurrentSpeed, Is.LessThan(3f));
        Assert.That(speed.EffectiveTargetSpeed, Is.EqualTo(0f));
    }


    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
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
        Assert.That(field, Is.Not.Null,
            $"Required runtime field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }
}
