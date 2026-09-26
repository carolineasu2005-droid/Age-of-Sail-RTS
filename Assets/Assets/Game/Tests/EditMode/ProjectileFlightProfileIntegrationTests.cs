using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ProjectileFlightProfileIntegrationTests
{
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_ProjectileFlight_Foundation.asset";
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
    public void FoundationProfile_HasValidTemporaryBallisticValues()
    {
        ProjectileFlightProfile profile = LoadProfile();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        float maximumRangeMeters =
            prefab.GetComponent<ShipFireEligibility>().MaximumRangeMeters;

        Assert.That(
            profile.NominalHorizontalSpeedMetersPerSecond,
            Is.EqualTo(120f)
        );
        Assert.That(
            profile.GravityMagnitudeMetersPerSecondSquared,
            Is.EqualTo(9.81f)
        );
        Assert.That(profile.MaxLifetimeSeconds, Is.EqualTo(10f));
        Assert.That(
            profile.MaxLifetimeSeconds,
            Is.GreaterThan(
                maximumRangeMeters
                    / profile.NominalHorizontalSpeedMetersPerSecond
            )
        );
    }


    [Test]
    public void Profile_DoesNotDuplicateDispersionRangeOrDamageData()
    {
        FieldInfo[] fields = typeof(ProjectileFlightProfile).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
        );

        foreach (FieldInfo field in fields)
        {
            string name = field.Name.ToLowerInvariant();
            Assert.That(name, Does.Not.Contain("dispersion"), field.Name);
            Assert.That(name, Does.Not.Contain("range"), field.Name);
            Assert.That(name, Does.Not.Contain("damage"), field.Name);
            Assert.That(name, Does.Not.Contain("exposure"), field.Name);
        }
    }


    [Test]
    public void ProfileSource_DeclaresTemporaryPlaytestBoundPolicy()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ProjectileFlightProfile.cs"
        );
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("TEMPORARY"));
        Assert.That(source, Does.Contain("PLAYTEST-BOUND"));
        Assert.That(source, Does.Contain("NOT BALANCE-FROZEN"));
        Assert.That(source, Does.Not.Contain("Rigidbody"));
    }


    [Test]
    public void GelderlandCombatPrefab_ReferencesFoundationProfileOnRoot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        ProjectileFlightProfile profile = LoadProfile();

        Assert.That(prefab, Is.Not.Null);
        ShipProjectileFlightConfiguration[] configurations =
            prefab.GetComponentsInChildren<
                ShipProjectileFlightConfiguration
            >(true);
        Assert.That(configurations, Has.Length.EqualTo(1));
        Assert.That(configurations[0].gameObject, Is.SameAs(prefab));
        Assert.That(
            configurations[0].ProjectileFlightProfile,
            Is.SameAs(profile)
        );
    }


    [TestCaseSource(nameof(GenericProxyPaths))]
    public void GenericMovementProxy_HasNoProjectileFlightConfiguration(
        string prefabPath
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponentsInChildren<
                ShipProjectileFlightConfiguration
            >(true),
            Is.Empty
        );
    }


    [Test]
    public void InvalidSpeed_IsRejectedBeforeAnySampleIsReturned()
    {
        GameObject instance = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(CombatPrefabPath)
        );
        ProjectileFlightProfile invalidProfile =
            ScriptableObject.CreateInstance<ProjectileFlightProfile>();

        try
        {
            SetPrivateField(
                invalidProfile,
                "nominalHorizontalSpeedMetersPerSecond",
                0f
            );
            SetPrivateField(
                instance.GetComponent<ShipProjectileFlightConfiguration>(),
                "projectileFlightProfile",
                invalidProfile
            );
            FireAimBasis basis = BuildBlindFireBasis(instance);

            bool sampled = ShipBroadsideShotSampler.TrySample(
                instance,
                basis,
                9u,
                out BroadsideShotSamplingResult result,
                out BroadsideShotSamplingFailure failure
            );

            Assert.That(sampled, Is.False);
            Assert.That(result.Count, Is.Zero);
            Assert.That(
                failure,
                Is.EqualTo(
                    BroadsideShotSamplingFailure.InvalidFlightProfile
                )
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(invalidProfile);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }


    private static ProjectileFlightProfile LoadProfile()
    {
        ProjectileFlightProfile profile =
            AssetDatabase.LoadAssetAtPath<ProjectileFlightProfile>(
                ProfilePath
            );
        Assert.That(profile, Is.Not.Null);
        return profile;
    }


    private static FireAimBasis BuildBlindFireBasis(GameObject sourceRoot)
    {
        CombatLifecycleTestUtility.EnsureOperational(sourceRoot);
        Vector3 aimPoint = sourceRoot.transform.position
            + sourceRoot.transform.right * 100f;
        ShipFireEligibility eligibility =
            sourceRoot.GetComponent<ShipFireEligibility>();
        Assert.That(
            eligibility.TryEvaluateBlindFireAtPoint(
                aimPoint,
                out BlindFireEligibilityResult eligibilityResult
            ),
            Is.True
        );
        Assert.That(
            ShipFireAimBasisBuilder.TryBuildBlindFirePoint(
                eligibilityResult,
                out FireAimBasis basis,
                out _
            ),
            Is.True
        );
        return basis;
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
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
