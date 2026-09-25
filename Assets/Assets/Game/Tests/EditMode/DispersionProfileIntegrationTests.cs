using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DispersionProfileIntegrationTests
{
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_Dispersion_Foundation.asset";
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";

    private static readonly string[] GenericProxyPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };


    [Test]
    public void FoundationAsset_UsesTemporaryAngularConfiguration()
    {
        DispersionProfile profile =
            AssetDatabase.LoadAssetAtPath<DispersionProfile>(ProfilePath);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.HorizontalHalfAngleDegrees, Is.EqualTo(1.5f));
        Assert.That(profile.VerticalHalfAngleDegrees, Is.EqualTo(0.75f));
        Assert.That(profile.FoundationSpreadScale, Is.EqualTo(1f));
    }


    [Test]
    public void Profile_HasNoRangeExposureGeometryOrProjectileConfiguration()
    {
        FieldInfo[] fields = typeof(DispersionProfile).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
        );

        foreach (FieldInfo field in fields)
        {
            string name = field.Name.ToLowerInvariant();
            Assert.That(name, Does.Not.Contain("range"), field.Name);
            Assert.That(name, Does.Not.Contain("exposure"), field.Name);
            Assert.That(name, Does.Not.Contain("geometry"), field.Name);
            Assert.That(name, Does.Not.Contain("renderer"), field.Name);
            Assert.That(name, Does.Not.Contain("projectile"), field.Name);
        }
    }


    [Test]
    public void ProfileSource_DeclaresTemporaryPlaytestBoundPolicy()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/DispersionProfile.cs"
        );
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("TEMPORARY"));
        Assert.That(source, Does.Contain("PLAYTEST-BOUND"));
        Assert.That(source, Does.Contain("NOT BALANCE-FROZEN"));
    }


    [Test]
    public void GelderlandCombatPrefab_ReferencesFoundationProfileOnRoot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        DispersionProfile expectedProfile =
            AssetDatabase.LoadAssetAtPath<DispersionProfile>(ProfilePath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(expectedProfile, Is.Not.Null);
        ShipDispersionConfiguration[] configurations =
            prefab.GetComponentsInChildren<ShipDispersionConfiguration>(true);
        Assert.That(configurations, Has.Length.EqualTo(1));
        Assert.That(configurations[0].gameObject, Is.SameAs(prefab));
        Assert.That(
            configurations[0].DispersionProfile,
            Is.SameAs(expectedProfile)
        );
    }


    [TestCaseSource(nameof(GenericProxyPaths))]
    public void GenericMovementProxy_HasNoDispersionConfiguration(
        string prefabPath
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponentsInChildren<ShipDispersionConfiguration>(true),
            Is.Empty
        );
    }
}
