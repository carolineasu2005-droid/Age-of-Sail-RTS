using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CombatOutcomeVFXBridgePlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";
    private const float Tolerance = 0.0001f;


    [UnityTest]
    public IEnumerator ResolvedHullHit_DrivesExistingPlaceholderReceiver()
    {
        GameObject source = new GameObject("VFX Bridge Source");
        GameObject receiverObject = new GameObject("VFX Bridge Receiver");
        CombatVFXPlaceholderReceiver receiver = receiverObject
            .AddComponent<CombatVFXPlaceholderReceiver>();
        GameObject target = InstantiateCombatShip();
        GameObject placeholder = null;

        try
        {
            ShotSample shot = CreateShotSample(source);
            ShipCombatGeometry geometry =
                target.GetComponent<ShipCombatGeometry>();
            Vector3 point = new Vector3(14f, 3f, -5f);
            Vector3 normal = new Vector3(-1f, 0.2f, 0.1f).normalized;
            ProjectileTerminalContact contact = CreateContact(
                shot,
                ProjectileTerminalContactKind.CombatGeometryContact,
                geometry.MidshipRegion.QueryCollider,
                point,
                normal
            );
            FoundationShotOutcome outcome = ResolveOutcome(contact);
            HashSet<GameObject> before = CaptureGameObjects();

            Assert.That(
                CombatOutcomeVFXBridge.TryEmitResolvedOutcome(
                    outcome,
                    receiver
                ),
                Is.True
            );
            placeholder = FindAddedPlaceholder(
                before,
                "Combat VFX Placeholder - Hull Impact"
            );

            Assert.That(
                Vector3.Distance(placeholder.transform.position, point),
                Is.LessThan(Tolerance)
            );
            Assert.That(
                Vector3.Dot(placeholder.transform.up, normal),
                Is.GreaterThan(0.9999f)
            );
        }
        finally
        {
            UnityEngine.Object.Destroy(placeholder);
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(receiverObject);
            UnityEngine.Object.Destroy(source);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator ResolvedWaterMiss_DrivesExistingPlaceholderReceiver()
    {
        GameObject source = new GameObject("Water VFX Bridge Source");
        GameObject receiverObject = new GameObject("Water VFX Bridge Receiver");
        CombatVFXPlaceholderReceiver receiver = receiverObject
            .AddComponent<CombatVFXPlaceholderReceiver>();
        GameObject placeholder = null;

        try
        {
            ShotSample shot = CreateShotSample(source);
            Vector3 point = new Vector3(22f, -1.5f, 7f);
            ProjectileTerminalContact contact = CreateContact(
                shot,
                ProjectileTerminalContactKind.WaterContact,
                null,
                point,
                Vector3.up
            );
            FoundationShotOutcome outcome = ResolveOutcome(contact);
            HashSet<GameObject> before = CaptureGameObjects();

            Assert.That(
                CombatOutcomeVFXBridge.TryEmitResolvedOutcome(
                    outcome,
                    receiver
                ),
                Is.True
            );
            placeholder = FindAddedPlaceholder(
                before,
                "Combat VFX Placeholder - Water Impact"
            );

            Assert.That(
                Vector3.Distance(placeholder.transform.position, point),
                Is.LessThan(Tolerance)
            );
            Assert.That(
                Vector3.Dot(placeholder.transform.up, Vector3.up),
                Is.GreaterThan(0.9999f)
            );
        }
        finally
        {
            UnityEngine.Object.Destroy(placeholder);
            UnityEngine.Object.Destroy(receiverObject);
            UnityEngine.Object.Destroy(source);
        }

        yield return null;
    }


    private static FoundationShotOutcome ResolveOutcome(
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


    private static ProjectileTerminalContact CreateContact(
        ShotSample shot,
        ProjectileTerminalContactKind kind,
        Collider collider,
        Vector3 point,
        Vector3 normal
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
            0.75f
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
            51u,
            new Vector3(1f, 4f, 2f),
            new Vector3(30f, 0f, 4f),
            new Vector3(40f, 8f, 2f),
            new Vector3(0f, -9.81f, 0f),
            1.5f,
            -1.5f,
            8f,
            FoundationAmmunitionType.RoundShot
        });
    }


    private static HashSet<GameObject> CaptureGameObjects()
    {
        return UnityEngine.Object
            .FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .ToHashSet();
    }


    private static GameObject FindAddedPlaceholder(
        HashSet<GameObject> before,
        string expectedName
    )
    {
        GameObject[] added = UnityEngine.Object
            .FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .Where(candidate => !before.Contains(candidate))
            .ToArray();
        Assert.That(added, Has.Length.EqualTo(1));
        Assert.That(added[0].name, Is.EqualTo(expectedName));
        return added[0];
    }


    private static GameObject InstantiateCombatShip()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        return UnityEngine.Object.Instantiate(prefab);
#else
        Assert.Ignore("Prefab loading requires the Unity Editor.");
        return null;
#endif
    }
}
