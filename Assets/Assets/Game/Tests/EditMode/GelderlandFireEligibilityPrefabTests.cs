using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GelderlandFireEligibilityPrefabTests
{
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
    public void GelderlandCombatPrefab_HasOneRootEligibilityEvaluator()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        ShipFireEligibility[] evaluators =
            prefab.GetComponentsInChildren<ShipFireEligibility>(true);
        Assert.That(evaluators, Has.Length.EqualTo(1));
        Assert.That(evaluators[0].gameObject, Is.SameAs(prefab));
        Assert.That(prefab.GetComponent<ShipCombatState>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<ShipArtDefinition>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<ShipCombatGeometry>(), Is.Not.Null);
    }


    [Test]
    public void GelderlandCombatPrefab_UsesTemporaryFoundationConfiguration()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        ShipFireEligibility evaluator =
            prefab.GetComponent<ShipFireEligibility>();

        Assert.That(evaluator, Is.Not.Null);
        Assert.That(evaluator.ForwardArcLimitDegrees, Is.EqualTo(80f));
        Assert.That(evaluator.AftArcLimitDegrees, Is.EqualTo(70f));
        Assert.That(evaluator.EffectiveRangeMeters, Is.EqualTo(300f));
        Assert.That(evaluator.MaximumRangeMeters, Is.EqualTo(500f));
    }


    [TestCaseSource(nameof(GenericProxyPaths))]
    public void GenericMovementProxy_HasNoFireEligibilityComponent(
        string prefabPath
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponentsInChildren<ShipFireEligibility>(true),
            Is.Empty
        );
    }
}
