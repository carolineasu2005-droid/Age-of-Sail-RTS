using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AutoTargetScoringProfileTests
{
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_AutoTargetScoring_Foundation.asset";
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
    public void FoundationProfile_UsesExactNeutralVisibility()
    {
        AutoTargetScoringProfile profile =
            AssetDatabase.LoadAssetAtPath<AutoTargetScoringProfile>(
                ProfilePath
            );

        Assert.That(profile, Is.Not.Null);
        Assert.That(
            AutoTargetScoringProfile
                .FoundationVisibilityQualityNormalized,
            Is.EqualTo(1f)
        );
        Assert.That(profile.VisibilityQualityNormalized, Is.EqualTo(1f));
    }


    [Test]
    public void Profile_HasNoDuplicateRangeOrAccuracyConfiguration()
    {
        FieldInfo[] fields = typeof(AutoTargetScoringProfile).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
        );

        foreach (FieldInfo field in fields)
        {
            string name = field.Name.ToLowerInvariant();
            Assert.That(name, Does.Not.Contain("range"), field.Name);
            Assert.That(name, Does.Not.Contain("accuracy"), field.Name);
            Assert.That(name, Does.Not.Contain("dispersion"), field.Name);
            Assert.That(name, Does.Not.Contain("projectile"), field.Name);
        }
    }


    [Test]
    public void ProfileSource_DeclaresTemporaryPlaytestBoundPolicy()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/AutoTargetScoringProfile.cs"
        );
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("TEMPORARY"));
        Assert.That(source, Does.Contain("PLAYTEST-BOUND"));
        Assert.That(source, Does.Contain("NOT BALANCE-FROZEN"));
        Assert.That(source, Does.Not.Contain("Smoke"));
        Assert.That(source, Does.Not.Contain("Raycast"));
    }


    [Test]
    public void RuntimeScripts_HaveNoSmokeOrVisibilityProviderImplementation()
    {
        string runtimeScriptsRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts"
        );
        string[] scriptPaths = Directory.GetFiles(
            runtimeScriptsRoot,
            "*.cs",
            SearchOption.AllDirectories
        );

        foreach (string scriptPath in scriptPaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(scriptPath);
            Assert.That(fileName, Does.Not.Contain("Smoke"), scriptPath);
            Assert.That(
                fileName,
                Does.Not.Contain("VisibilityProvider"),
                scriptPath
            );
        }
    }


    [Test]
    public void GelderlandCombatPrefab_ReferencesFoundationProfileOnRoot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        AutoTargetScoringProfile expectedProfile =
            AssetDatabase.LoadAssetAtPath<AutoTargetScoringProfile>(
                ProfilePath
            );

        Assert.That(prefab, Is.Not.Null);
        ShipAutoTargetScoringConfiguration[] configurations =
            prefab.GetComponentsInChildren<
                ShipAutoTargetScoringConfiguration
            >(true);
        Assert.That(configurations, Has.Length.EqualTo(1));
        Assert.That(configurations[0].gameObject, Is.SameAs(prefab));
        Assert.That(
            configurations[0].ScoringProfile,
            Is.SameAs(expectedProfile)
        );
    }


    [TestCaseSource(nameof(GenericProxyPaths))]
    public void GenericMovementProxy_HasNoScoringConfiguration(
        string prefabPath
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponentsInChildren<
                ShipAutoTargetScoringConfiguration
            >(true),
            Is.Empty
        );
    }
}
