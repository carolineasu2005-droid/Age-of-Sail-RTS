using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CannonLingeringSmokeIntegrationTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const string SmokePrefabPath =
        "Assets/Assets/Game/VFX/Cannon/Prefabs/"
        + "VFX_Cannon_LingeringSmoke_v01.prefab";

    private static readonly string[] GenericMovementPrefabPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };


    [Test]
    public void ApprovedSmokePrefab_PreservesAuthoredPlaybackAndCleanupContract()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            SmokePrefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(
            prefab.transform.localRotation,
            Is.EqualTo(Quaternion.identity)
        );
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        ParticleSystem[] particleSystems = prefab
            .GetComponentsInChildren<ParticleSystem>(true);
        Assert.That(particleSystems, Has.Length.EqualTo(1));

        ParticleSystem.MainModule main = particleSystems[0].main;
        Assert.That(main.loop, Is.False);
        Assert.That(main.playOnAwake, Is.False);
        Assert.That(
            main.stopAction,
            Is.EqualTo(ParticleSystemStopAction.Destroy)
        );
    }


    [Test]
    public void GelderlandReceiver_ReferencesApprovedSmokePrefab()
    {
        GameObject combatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        GameObject smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            SmokePrefabPath
        );

        Assert.That(combatPrefab, Is.Not.Null);
        Assert.That(smokePrefab, Is.Not.Null);
        CombatVFXPlaceholderReceiver[] receivers = combatPrefab
            .GetComponentsInChildren<CombatVFXPlaceholderReceiver>(true);
        Assert.That(receivers, Has.Length.EqualTo(1));
        Assert.That(
            receivers[0].CannonLingeringSmokePrefab,
            Is.SameAs(smokePrefab)
        );
        Assert.That(
            combatPrefab.GetComponentsInChildren<ParticleSystem>(true)
                .Any(candidate => candidate.name.Contains(
                    "Cannon_LingeringSmoke"
                )),
            Is.False,
            "Smoke must remain a referenced transient prefab, not a child."
        );
    }


    [Test]
    public void GenericMovementProxies_RemainFreeOfCombatVFXIntegration()
    {
        foreach (string prefabPath in GenericMovementPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath
            );

            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(
                prefab.GetComponentsInChildren<CombatVFXPlaceholderReceiver>(
                    true
                ),
                Is.Empty,
                prefabPath
            );
            Assert.That(
                prefab.GetComponentsInChildren<ParticleSystem>(true)
                    .Any(candidate => candidate.name.Contains(
                        "Cannon_LingeringSmoke"
                    )),
                Is.False,
                prefabPath
            );
        }
    }


    [Test]
    public void ReceiverContract_UsesOneSerializedSmokeReference()
    {
        FieldInfo field = typeof(CombatVFXPlaceholderReceiver).GetField(
            "cannonLingeringSmokePrefab",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(field, Is.Not.Null);
        Assert.That(field.FieldType, Is.EqualTo(typeof(GameObject)));
        Assert.That(
            field.GetCustomAttribute<SerializeField>(),
            Is.Not.Null
        );
        Assert.That(
            typeof(CombatVFXPlaceholderReceiver)
                .GetMethod(nameof(CombatVFXPlaceholderReceiver.OnMuzzleFire))
                .GetParameters()
                .Single()
                .ParameterType,
            Is.EqualTo(typeof(CombatMuzzleFireEvent))
        );
    }


    [Test]
    public void ReceiverSource_HasNoGameplayOrMovementOwnershipDependencies()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/CombatArt/CombatVFXPlaceholderReceiver.cs"
        ));

        Assert.That(source, Does.Contain("particleSystem.Play(true)"));
        Assert.That(source, Does.Not.Contain("ShotSample"));
        Assert.That(source, Does.Not.Contain("CombatProjectile"));
        Assert.That(source, Does.Not.Contain("CombatDamage"));
        Assert.That(source, Does.Not.Contain("ShipIntegrity"));
        Assert.That(source, Does.Not.Contain("Reload"));
        Assert.That(source, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("ShipDestinationController"));
        Assert.That(source, Does.Not.Contain("GlobalWind"));
        Assert.That(source, Does.Not.Contain("CombatSide.Port"));
        Assert.That(source, Does.Not.Contain("CombatSide.Starboard"));
    }
}
