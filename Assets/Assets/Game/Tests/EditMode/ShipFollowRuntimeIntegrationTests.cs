using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ShipFollowRuntimeIntegrationTests
{
    private const string ProxyFolder = "Assets/Assets/Game/Ship/Proxy/";
    private const string ScenePath = "Assets/Scenes/Prototype/SailingPrototype_01_Speed.unity";
    private static readonly Type[] FollowTypes =
    {
        typeof(ShipFollowController), typeof(ShipFollowTrailRecorder),
        typeof(ShipFollowNavigationController)
    };

    [TestCase("PF_Proxy_Light_v01")]
    [TestCase("PF_Proxy_Medium_v01")]
    [TestCase("PF_Proxy_Heavy_v01")]
    [TestCase("PF_Ship_Gelderland_Combat_v01")]
    [TestCase("PF_Ship_Gelderland_CombatAI_v01")]
    [TestCase("Team1.1")]
    public void ParticipatingPrefabs_HaveExactlyOneFollowStackOnMovementRoot(string name)
    {
        string path = name == "Team1.1" ? "Assets/Assets/Game/Ship/Prefab/Team1.1.prefab"
            : ProxyFolder + name + ".prefab";
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(root, Is.Not.Null, path);
        AssertFollowRoot(root);
        Assert.That(root.GetComponent<ShipFollowController>().HasFollowIntent, Is.False);
        Assert.That(root.GetComponent<ShipFollowTrailRecorder>().SubscriberCount, Is.Zero);
        Assert.That(root.GetComponent<ShipFollowNavigationController>().State,
            Is.EqualTo(ShipFollowNavigationController.ReplayState.Inactive));
        Assert.That(root.GetComponent<ShipFollowNavigationController>().DesiredFollowGap, Is.EqualTo(60f));
        if (name.StartsWith("PF_Proxy_", StringComparison.Ordinal))
        {
            Assert.That(root.GetComponentsInChildren<ShipCombatState>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<ShipCombatAIController>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<ShipCombatGeometry>(true), Is.Empty);
        }
    }

    [Test]
    public void MovementRebuildDefinition_RequiresGenericFollowStack()
    {
        FieldInfo field = typeof(RebuildMovementProxyPrefabs).GetField("RequiredComponentTypes",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Type[] required = (Type[])field.GetValue(null);
        foreach (Type type in FollowTypes) Assert.That(required, Does.Contain(type));
        Assert.That(required, Has.No.Member(typeof(ShipCombatAIController)));
        Assert.That(required, Has.No.Member(typeof(ShipCombatGeometry)));
    }

    [Test]
    public void Prototype_UsesInheritedFollowStacksAndFourInactivePlayerGateShips()
    {
        // Preview loading preserves the developer's current scene and never saves an asset.
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            var destinations = roots.SelectMany(root =>
                root.GetComponentsInChildren<ShipDestinationController>(true)).ToArray();
            Assert.That(destinations.Length, Is.GreaterThanOrEqualTo(10));
            foreach (ShipDestinationController ship in destinations) AssertFollowRoot(ship.gameObject);
            foreach (ShipFollowController follow in roots.SelectMany(root =>
                root.GetComponentsInChildren<ShipFollowController>(true)))
                Assert.That(follow.GetComponent<ShipDestinationController>(), Is.Not.Null,
                    "Follow must never be attached to presentation, camera or preview objects.");

            GameObject group = roots.Single(root => root.name == "FollowPlaytest_EnableForManualGate");
            Assert.That(group.activeSelf, Is.False);
            var playerShips = group.GetComponentsInChildren<ShipDestinationController>(true);
            Assert.That(playerShips, Has.Length.EqualTo(4));
            Assert.That(playerShips.Select(ship => ship.name), Is.EquivalentTo(
                new[] { "Follow_A", "Follow_B", "Follow_C", "Follow_D" }));
            foreach (ShipDestinationController ship in playerShips)
            {
                Assert.That(ship.GetComponent<ShipCombatState>(), Is.Not.Null);
                Assert.That(ship.GetComponent<ShipCombatAffiliation>().TeamId, Is.EqualTo(1));
                Assert.That(ship.GetComponentsInChildren<ShipCombatAIController>(true), Is.Empty);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(ship.gameObject), Is.Not.Null);
            }
            Assert.That(CombatRelationshipResolver.Resolve(
                playerShips[0].GetComponent<ShipCombatAffiliation>(),
                playerShips[1].GetComponent<ShipCombatAffiliation>()), Is.EqualTo(CombatRelationship.Friendly));

            MovementStatusPanel panel = roots.SelectMany(root =>
                root.GetComponentsInChildren<MovementStatusPanel>(true)).Single();
            SerializedObject serialized = new SerializedObject(panel);
            Assert.That(serialized.FindProperty("selectionManager").objectReferenceValue,
                Is.SameAs(panel.GetComponent<ShipSelectionManager>()));
            Assert.That(serialized.FindProperty("playerInput").objectReferenceValue,
                Is.SameAs(panel.GetComponent<ShipPlayerCommandInput>()));
            Assert.That(serialized.FindProperty("headingPreview").objectReferenceValue,
                Is.SameAs(panel.GetComponent<ShipDirectedHeadingPreviewController>()));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void AssertFollowRoot(GameObject root)
    {
        foreach (Type type in FollowTypes)
        {
            Assert.That(root.GetComponents(type), Has.Length.EqualTo(1), root.name + ": " + type.Name);
            Assert.That(root.GetComponentsInChildren(type, true), Has.Length.EqualTo(1),
                "No duplicate Follow component on a child of " + root.name);
            Assert.That(((Behaviour)root.GetComponent(type)).enabled, Is.True);
        }
        foreach (Type type in new[] { typeof(ShipDestinationController), typeof(ShipSailingSpeed),
            typeof(ShipTurning), typeof(ShipHeadingController), typeof(ShipManeuverPlanner) })
            Assert.That(root.GetComponent(type), Is.Not.Null, root.name + ": " + type.Name);
    }
}
