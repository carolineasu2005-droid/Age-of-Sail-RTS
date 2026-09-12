using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipArtDefinitionTests
{
    private GameObject root;
    private ShipArtDefinition definition;


    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Ship Root");
        definition = root.AddComponent<ShipArtDefinition>();
    }


    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }


    [Test]
    public void TryGetLength_ReturnsBowToSternDistanceAlongRootLocalZ()
    {
        SetReference("bowReference", CreateReference("Bow", new Vector3(2f, 1f, 18f)));
        SetReference("sternReference", CreateReference("Stern", new Vector3(-3f, -1f, -12f)));

        bool available = definition.TryGetLength(out float length);

        Assert.That(available, Is.True);
        Assert.That(length, Is.EqualTo(30f).Within(0.0001f));
    }


    [Test]
    public void TryGetBeam_ReturnsPortToStarboardDistanceAlongRootLocalX()
    {
        SetReference("portReference", CreateReference("Port", new Vector3(-5f, 0f, 3f)));
        SetReference("starboardReference", CreateReference("Starboard", new Vector3(4f, 2f, -1f)));

        bool available = definition.TryGetBeam(out float beam);

        Assert.That(available, Is.True);
        Assert.That(beam, Is.EqualTo(9f).Within(0.0001f));
    }


    [Test]
    public void DimensionQueries_AreInvariantUnderRootTranslation()
    {
        AssignDimensionReferences();
        AssertDimensions(30f, 10f);

        root.transform.position = new Vector3(125f, -7f, 43f);

        AssertDimensions(30f, 10f);
    }


    [Test]
    public void DimensionQueries_AreInvariantUnderRootYRotation()
    {
        AssignDimensionReferences();
        AssertDimensions(30f, 10f);

        root.transform.rotation = Quaternion.Euler(0f, 137f, 0f);

        AssertDimensions(30f, 10f);
    }


    [Test]
    public void DimensionQueries_AreInvariantUnderCombinedRootTransform()
    {
        AssignDimensionReferences();

        root.transform.SetPositionAndRotation(
            new Vector3(-81f, 12f, 209f),
            Quaternion.Euler(0f, 53f, 0f)
        );

        AssertDimensions(30f, 10f);
    }


    [Test]
    public void DimensionQueries_SupportReferencesUnderNestedTransforms()
    {
        Transform group = CreateReference(
            "Nested Art References",
            new Vector3(3f, 4f, -6f)
        );
        group.localRotation = Quaternion.Euler(12f, 31f, -8f);

        Transform bow = CreateNestedReference(group, "Bow", new Vector3(1f, 2f, 17f));
        Transform stern = CreateNestedReference(group, "Stern", new Vector3(-2f, -1f, -13f));
        Transform port = CreateNestedReference(group, "Port", new Vector3(-6f, 1f, 4f));
        Transform starboard = CreateNestedReference(group, "Starboard", new Vector3(4f, -2f, -3f));
        SetReference("bowReference", bow);
        SetReference("sternReference", stern);
        SetReference("portReference", port);
        SetReference("starboardReference", starboard);

        AssertDimensions(30f, 10f);
    }


    [Test]
    public void TryGetLength_ReturnsUnavailableAndZeroWhenEitherReferenceIsMissing()
    {
        SetReference("bowReference", CreateReference("Bow", Vector3.forward * 10f));

        bool missingStern = definition.TryGetLength(out float missingSternLength);

        Assert.That(missingStern, Is.False);
        Assert.That(missingSternLength, Is.Zero);

        SetReference("bowReference", null);
        SetReference("sternReference", CreateReference("Stern", Vector3.back * 10f));

        bool missingBow = definition.TryGetLength(out float missingBowLength);

        Assert.That(missingBow, Is.False);
        Assert.That(missingBowLength, Is.Zero);
    }


    [Test]
    public void TryGetBeam_ReturnsUnavailableAndZeroWhenEitherReferenceIsMissing()
    {
        SetReference("portReference", CreateReference("Port", Vector3.left * 5f));

        bool missingStarboard = definition.TryGetBeam(out float missingStarboardBeam);

        Assert.That(missingStarboard, Is.False);
        Assert.That(missingStarboardBeam, Is.Zero);

        SetReference("portReference", null);
        SetReference("starboardReference", CreateReference("Starboard", Vector3.right * 5f));

        bool missingPort = definition.TryGetBeam(out float missingPortBeam);

        Assert.That(missingPort, Is.False);
        Assert.That(missingPortBeam, Is.Zero);
    }


    [Test]
    public void ReferenceContract_UsesPrivateSerializedFieldsAndGetterOnlyProperties()
    {
        string[] referenceNames =
        {
            "VisualRoot",
            "WaterlineReference",
            "CenterReference",
            "BowReference",
            "SternReference",
            "PortReference",
            "StarboardReference",
            "DeckReference"
        };

        foreach (string propertyName in referenceNames)
        {
            PropertyInfo property = typeof(ShipArtDefinition).GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing property {propertyName}.");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(Transform)));
            Assert.That(property.GetMethod, Is.Not.Null);
            Assert.That(property.GetMethod.IsPublic, Is.True);
            Assert.That(property.SetMethod, Is.Null, $"{propertyName} must be read-only.");

            string fieldName = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
            FieldInfo field = typeof(ShipArtDefinition).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.That(field, Is.Not.Null, $"Missing serialized field {fieldName}.");
            Assert.That(field.IsPrivate, Is.True);
            Assert.That(field.FieldType, Is.EqualTo(typeof(Transform)));
            Assert.That(
                field.IsDefined(typeof(SerializeField), false),
                Is.True,
                $"{fieldName} must be serialized."
            );
        }
    }


    [Test]
    public void Component_IsPassiveAndRequiresNoMovementComponents()
    {
        Component[] components = root.GetComponents<Component>();

        Assert.That(components, Has.Length.EqualTo(2));
        Assert.That(components[0], Is.TypeOf<Transform>());
        Assert.That(components[1], Is.SameAs(definition));
        Assert.That(root.transform.childCount, Is.Zero);
        Assert.That(definition.TryGetLength(out float length), Is.False);
        Assert.That(definition.TryGetBeam(out float beam), Is.False);
        Assert.That(length, Is.Zero);
        Assert.That(beam, Is.Zero);
    }


    private void AssignDimensionReferences()
    {
        SetReference("bowReference", CreateReference("Bow", new Vector3(0f, 0f, 18f)));
        SetReference("sternReference", CreateReference("Stern", new Vector3(0f, 0f, -12f)));
        SetReference("portReference", CreateReference("Port", new Vector3(-6f, 0f, 0f)));
        SetReference("starboardReference", CreateReference("Starboard", new Vector3(4f, 0f, 0f)));
    }


    private void AssertDimensions(float expectedLength, float expectedBeam)
    {
        Assert.That(definition.TryGetLength(out float length), Is.True);
        Assert.That(definition.TryGetBeam(out float beam), Is.True);
        Assert.That(length, Is.EqualTo(expectedLength).Within(0.0001f));
        Assert.That(beam, Is.EqualTo(expectedBeam).Within(0.0001f));
    }


    private Transform CreateReference(string name, Vector3 localPosition)
    {
        GameObject referenceObject = new GameObject(name);
        referenceObject.transform.SetParent(root.transform, false);
        referenceObject.transform.localPosition = localPosition;
        return referenceObject.transform;
    }


    private Transform CreateNestedReference(
        Transform parent,
        string name,
        Vector3 rootLocalPosition
    )
    {
        GameObject referenceObject = new GameObject(name);
        referenceObject.transform.SetParent(parent, false);
        referenceObject.transform.position = root.transform.TransformPoint(rootLocalPosition);
        return referenceObject.transform;
    }


    private void SetReference(string fieldName, Transform value)
    {
        FieldInfo field = typeof(ShipArtDefinition).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(definition, value);
    }
}
