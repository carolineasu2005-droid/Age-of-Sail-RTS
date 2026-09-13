using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipMuzzleSocketsTests
{
    private GameObject root;
    private ShipMuzzleSockets muzzleSockets;
    private Transform portContainer;
    private Transform starboardContainer;


    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Ship Root");
        muzzleSockets = root.AddComponent<ShipMuzzleSockets>();
        portContainer = CreateContainer("Port");
        starboardContainer = CreateContainer("Starboard");
        SetContainer("portMuzzlesContainer", portContainer);
        SetContainer("starboardMuzzlesContainer", starboardContainer);
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
    }


    [Test]
    public void PortMuzzles_ReturnsPortContainerChildren()
    {
        Transform first = CreateSocket(portContainer, "Port A", 8f);
        Transform second = CreateSocket(portContainer, "Port B", -3f);

        Assert.That(muzzleSockets.PortMuzzles, Is.EqualTo(new[] { first, second }));
    }


    [Test]
    public void StarboardMuzzles_ReturnsStarboardContainerChildren()
    {
        Transform first = CreateSocket(starboardContainer, "Starboard A", 9f);
        Transform second = CreateSocket(starboardContainer, "Starboard B", -4f);

        Assert.That(
            muzzleSockets.StarboardMuzzles,
            Is.EqualTo(new[] { first, second })
        );
    }


    [Test]
    public void Muzzles_AreOrderedByDescendingRootLocalZ()
    {
        Transform middle = CreateSocket(portContainer, "Middle", 2f);
        Transform aft = CreateSocket(portContainer, "Aft", -12f);
        Transform forward = CreateSocket(portContainer, "Forward", 17f);

        Assert.That(
            muzzleSockets.PortMuzzles,
            Is.EqualTo(new[] { forward, middle, aft })
        );
    }


    [Test]
    public void CanonicalOrder_DoesNotDependOnSiblingOrder()
    {
        Transform aft = CreateSocket(portContainer, "Aft", -10f);
        Transform forward = CreateSocket(portContainer, "Forward", 10f);
        Transform middle = CreateSocket(portContainer, "Middle", 0f);

        Assert.That(
            muzzleSockets.PortMuzzles,
            Is.EqualTo(new[] { forward, middle, aft })
        );
    }


    [Test]
    public void EffectivelyEqualRootLocalZ_UsesSiblingIndexAsTieBreaker()
    {
        Transform firstSibling = CreateSocket(portContainer, "First", 5f);
        Transform secondSibling = CreateSocket(
            portContainer,
            "Second",
            5f + 0.00001f
        );

        Assert.That(
            muzzleSockets.PortMuzzles,
            Is.EqualTo(new[] { firstSibling, secondSibling })
        );
    }


    [Test]
    public void CanonicalOrder_IsInvariantUnderRootTranslation()
    {
        Transform aft = CreateSocket(portContainer, "Aft", -7f);
        Transform forward = CreateSocket(portContainer, "Forward", 11f);
        root.transform.position = new Vector3(125f, -8f, 410f);

        Assert.That(
            muzzleSockets.PortMuzzles,
            Is.EqualTo(new[] { forward, aft })
        );
    }


    [Test]
    public void CanonicalOrder_IsInvariantUnderRootRotation()
    {
        Transform aft = CreateSocket(portContainer, "Aft", -7f);
        Transform forward = CreateSocket(portContainer, "Forward", 11f);
        root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        Assert.That(
            muzzleSockets.PortMuzzles,
            Is.EqualTo(new[] { forward, aft })
        );
    }


    [Test]
    public void CanonicalOrder_UsesRootSpaceThroughTransformedContainer()
    {
        portContainer.localPosition = new Vector3(4f, 2f, -6f);
        portContainer.localRotation = Quaternion.Euler(15f, 47f, -8f);
        Transform aft = CreateSocketAtRootLocal(
            portContainer,
            "Aft",
            new Vector3(-5f, 1f, -14f)
        );
        Transform forward = CreateSocketAtRootLocal(
            portContainer,
            "Forward",
            new Vector3(-5f, -2f, 19f)
        );

        Assert.That(
            muzzleSockets.PortMuzzles,
            Is.EqualTo(new[] { forward, aft })
        );
    }


    [Test]
    public void MissingPortContainer_ReturnsEmptyCollection()
    {
        SetContainer("portMuzzlesContainer", null);

        Assert.That(muzzleSockets.PortMuzzles, Is.Empty);
    }


    [Test]
    public void MissingStarboardContainer_ReturnsEmptyCollection()
    {
        SetContainer("starboardMuzzlesContainer", null);

        Assert.That(muzzleSockets.StarboardMuzzles, Is.Empty);
    }


    [Test]
    public void ReturnedCollection_CannotMutateCanonicalOrdering()
    {
        Transform forward = CreateSocket(portContainer, "Forward", 10f);
        Transform aft = CreateSocket(portContainer, "Aft", -10f);
        IReadOnlyList<Transform> readOnly = muzzleSockets.PortMuzzles;
        IList<Transform> listView = readOnly as IList<Transform>;

        Assert.That(listView, Is.Not.Null);
        Assert.That(listView.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => listView[0] = aft);
        Assert.That(readOnly, Is.EqualTo(new[] { forward, aft }));
    }


    [Test]
    public void Component_HasNoMovementComponentDependency()
    {
        Component[] components = root.GetComponents<Component>();

        Assert.That(components, Has.Length.EqualTo(2));
        Assert.That(components[0], Is.TypeOf<Transform>());
        Assert.That(components[1], Is.SameAs(muzzleSockets));
        Assert.That(muzzleSockets.PortMuzzles, Is.Empty);
        Assert.That(muzzleSockets.StarboardMuzzles, Is.Empty);
    }


    [Test]
    public void SocketForward_RotatesWithRootAsNominalDirection()
    {
        Transform socket = CreateSocket(portContainer, "Port Muzzle", 0f);
        socket.localRotation = Quaternion.Euler(0f, -90f, 0f);

        Assert.That(
            Vector3.Distance(socket.forward, root.transform.TransformDirection(Vector3.left)),
            Is.LessThan(0.0001f)
        );

        root.transform.rotation = Quaternion.Euler(0f, 63f, 0f);

        Assert.That(
            Vector3.Distance(socket.forward, root.transform.TransformDirection(Vector3.left)),
            Is.LessThan(0.0001f)
        );
    }


    private Transform CreateContainer(string name)
    {
        GameObject containerObject = new GameObject(name);
        containerObject.transform.SetParent(root.transform, false);
        return containerObject.transform;
    }


    private Transform CreateSocket(
        Transform container,
        string name,
        float rootLocalZ
    )
    {
        return CreateSocketAtRootLocal(
            container,
            name,
            new Vector3(0f, 0f, rootLocalZ)
        );
    }


    private Transform CreateSocketAtRootLocal(
        Transform container,
        string name,
        Vector3 rootLocalPosition
    )
    {
        GameObject socketObject = new GameObject(name);
        socketObject.transform.SetParent(container, false);
        socketObject.transform.position = root.transform.TransformPoint(
            rootLocalPosition
        );
        return socketObject.transform;
    }


    private void SetContainer(string fieldName, Transform value)
    {
        FieldInfo field = typeof(ShipMuzzleSockets).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(muzzleSockets, value);
    }
}
