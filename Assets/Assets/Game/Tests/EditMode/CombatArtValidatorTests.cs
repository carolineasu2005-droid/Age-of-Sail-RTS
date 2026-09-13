using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class CombatArtValidatorTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private static readonly HashSet<string> ActiveContractIds =
        new HashSet<string>
        {
            "ART-PFB-003",
            "ART-PFB-004",
            "ART-API-001",
            "ART-API-004",
            "ART-API-005",
            "ART-API-006",
            "ART-API-007",
            "ART-API-011",
            "ART-API-012",
            "ART-API-015",
            "ART-REF-002",
            "ART-REF-003",
            "ART-REF-004",
            "ART-REF-005",
            "ART-REF-006",
            "ART-REF-007",
            "ART-REF-008",
            "ART-REF-009",
            "ART-REF-013",
            "ART-REF-014",
            "ART-REF-015",
            "ART-REF-016",
            "ART-REF-017",
            "ART-REF-018",
            "ART-REF-019",
            "ART-SCK-001",
            "ART-SCK-002",
            "ART-LYR-001",
            "ART-CONV-009",
            "ART-CONV-010"
        };


    [Test]
    public void GelderlandCombatPrefab_PassesFoundationGate()
    {
        GameObject prefab = LoadCombatPrefab();
        IReadOnlyList<CombatArtValidationResult> results =
            CombatArtValidator.Validate(prefab);

        Assert.That(results, Is.Not.Empty);
        Assert.That(
            results.All(
                result => result.Severity
                    == CombatArtValidationSeverity.PASS
            ),
            Is.True
        );
    }


    [Test]
    public void MissingRequiredComponent_GeneratesSpecificError()
    {
        GameObject incompleteShip = new GameObject("Incomplete Ship");

        try
        {
            CombatArtValidationResult result = CombatArtValidator
                .Validate(incompleteShip)
                .Single(candidate => candidate.ContractId == "ART-API-001");

            AssertError(result, "ART-API-001", incompleteShip);
        }
        finally
        {
            Object.DestroyImmediate(incompleteShip);
        }
    }


    [Test]
    public void MissingArtReference_GeneratesSpecificError()
    {
        GameObject ship = CreateValidCombatShip();

        try
        {
            ShipArtDefinition definition =
                ship.GetComponent<ShipArtDefinition>();
            SerializedObject serializedDefinition =
                new SerializedObject(definition);
            serializedDefinition.FindProperty("deckReference")
                .objectReferenceValue = null;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            CombatArtValidationResult result = CombatArtValidator
                .Validate(ship)
                .Single(candidate => candidate.ContractId == "ART-REF-009");

            AssertError(result, "ART-REF-009", definition);
            Assert.That(definition.DeckReference, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [Test]
    public void InvalidCombatGeometryLayer_GeneratesSpecificError()
    {
        GameObject ship = CreateValidCombatShip();

        try
        {
            Collider collider = ship.GetComponent<ShipCombatGeometry>()
                .BowRegion.QueryCollider;
            collider.gameObject.layer = 0;

            CombatArtValidationResult result = CombatArtValidator
                .Validate(ship)
                .Single(
                    candidate => candidate.ContractId == "ART-LYR-001"
                        && candidate.Severity
                            == CombatArtValidationSeverity.ERROR
                );

            AssertError(result, "ART-LYR-001", collider);
            Assert.That(collider.gameObject.layer, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [Test]
    public void InvalidRootScale_GeneratesSpecificErrorWithoutRepair()
    {
        GameObject ship = CreateValidCombatShip();
        Vector3 authoredScale = new Vector3(2f, 0.5f, 1.25f);
        ship.transform.localScale = authoredScale;

        try
        {
            CombatArtValidationResult result = CombatArtValidator
                .Validate(ship)
                .Single(candidate => candidate.ContractId == "ART-PFB-004");

            AssertError(result, "ART-PFB-004", ship);
            Assert.That(ship.transform.localScale, Is.EqualTo(authoredScale));
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [Test]
    public void InvalidPortMuzzleSide_GeneratesSpecificErrorWithoutRepair()
    {
        GameObject ship = CreateValidCombatShip();

        try
        {
            Transform socket = ship.GetComponent<ShipMuzzleSockets>()
                .PortMuzzles[0];
            Vector3 invalidLocalPosition = ship.transform.InverseTransformPoint(
                socket.position
            );
            invalidLocalPosition.x = Mathf.Abs(invalidLocalPosition.x) + 1f;
            socket.position = ship.transform.TransformPoint(
                invalidLocalPosition
            );

            CombatArtValidationResult result = CombatArtValidator
                .Validate(ship)
                .Single(
                    candidate => candidate.ContractId == "ART-SCK-001"
                        && candidate.Severity
                            == CombatArtValidationSeverity.ERROR
                );

            AssertError(result, "ART-SCK-001", socket);
            Assert.That(
                ship.transform.InverseTransformPoint(socket.position).x,
                Is.GreaterThan(0f)
            );
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [Test]
    public void MissingVFXReceiver_GeneratesSpecificError()
    {
        GameObject ship = CreateValidCombatShip();

        try
        {
            CombatVFXPlaceholderReceiver receiver =
                ship.GetComponentInChildren<CombatVFXPlaceholderReceiver>(
                    true
                );
            Object.DestroyImmediate(receiver);

            CombatArtValidationResult result = CombatArtValidator
                .Validate(ship)
                .Single(
                    candidate => candidate.ContractId == "ART-API-015"
                );

            AssertError(result, "ART-API-015", ship);
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [TestCase(
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab"
    )]
    [TestCase(
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab"
    )]
    [TestCase(
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    )]
    public void GenericMovementProxy_RemainsFreeOfCombatArtContracts(
        string prefabPath
    )
    {
        GameObject prefab = LoadPrefab(prefabPath);

        Assert.That(
            prefab.GetComponentsInChildren<ShipArtDefinition>(true),
            Is.Empty
        );
        Assert.That(
            prefab.GetComponentsInChildren<ShipMuzzleSockets>(true),
            Is.Empty
        );
        Assert.That(
            prefab.GetComponentsInChildren<ShipCombatGeometry>(true),
            Is.Empty
        );
        Assert.That(
            prefab.GetComponentsInChildren<ShipExposureReference>(true),
            Is.Empty
        );
        Assert.That(
            prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(behaviour => behaviour is ICombatVFXEventReceiver),
            Is.False
        );
        Assert.That(prefab.transform.Find("ArtReferences"), Is.Null);
        Assert.That(prefab.transform.Find("CombatSockets"), Is.Null);
        Assert.That(prefab.transform.Find("CombatGeometry"), Is.Null);
    }


    [Test]
    public void PartiallyContaminatedGenericProxy_IsRejected()
    {
        GameObject genericProxy = Object.Instantiate(
            LoadPrefab(
                "Assets/Assets/Game/Ship/Proxy/"
                    + "PF_Proxy_Medium_v01.prefab"
            )
        );

        try
        {
            genericProxy.AddComponent<ShipArtDefinition>();
            IReadOnlyList<CombatArtValidationResult> results =
                CombatArtValidator.Validate(genericProxy);

            Assert.That(
                results.Any(
                    result => result.Severity
                        == CombatArtValidationSeverity.ERROR
                ),
                Is.True
            );
            AssertOnlyConfiguredCombatPrefabCanPass(results);
        }
        finally
        {
            Object.DestroyImmediate(genericProxy);
        }
    }


    [Test]
    public void ReportGeneration_FormatsFoundationGateSummary()
    {
        GameObject prefab = LoadCombatPrefab();
        IReadOnlyList<CombatArtValidationResult> results =
            CombatArtValidator.Validate(prefab);

        string report = CombatArtValidator.FormatReport(prefab, results);
        string expected = string.Join(
            Environment.NewLine,
            "Combat Art Validation Report",
            string.Empty,
            "Ship:",
            "PF_Ship_Gelderland_Combat_v01",
            string.Empty,
            "Status:",
            "PASS",
            string.Empty,
            "Checks:",
            "\u2713 Root Contract",
            "\u2713 ShipArtDefinition",
            "\u2713 Muzzle Socket",
            "\u2713 Combat Geometry",
            "\u2713 Exposure",
            "\u2713 VFX"
        );

        Assert.That(report, Is.EqualTo(expected));
    }


    [Test]
    public void ReportGeneration_PropagatesContractErrors()
    {
        GameObject ship = CreateValidCombatShip();
        ship.transform.localScale = Vector3.one * 2f;

        try
        {
            IReadOnlyList<CombatArtValidationResult> results =
                CombatArtValidator.Validate(ship);
            string report = CombatArtValidator.FormatReport(ship, results);

            Assert.That(
                report,
                Does.Contain("Status:" + Environment.NewLine + "ERROR")
            );
            Assert.That(report, Does.Contain("\u2717 Root Contract"));
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [Test]
    public void ReportGeneration_PropagatesContractWarnings()
    {
        GameObject prefab = LoadCombatPrefab();
        List<CombatArtValidationResult> results = CombatArtValidator
            .Validate(prefab)
            .ToList();
        int vfxResultIndex = results.FindIndex(
            result => result.ContractId == "ART-API-015"
        );
        Assert.That(vfxResultIndex, Is.GreaterThanOrEqualTo(0));
        CombatArtValidationResult original = results[vfxResultIndex];
        results[vfxResultIndex] = new CombatArtValidationResult(
            original.ContractId,
            CombatArtValidationSeverity.WARNING,
            "VFX warning for report aggregation coverage.",
            original.TargetObject
        );

        string report = CombatArtValidator.FormatReport(prefab, results);

        Assert.That(
            report,
            Does.Contain("Status:" + Environment.NewLine + "WARNING")
        );
        Assert.That(report, Does.Contain("\u26A0 VFX"));
    }


    [Test]
    public void AlternateVFXReceiverImplementation_GeneratesPass()
    {
        GameObject ship = new GameObject("Ship With Alternate VFX Receiver");
        GameObject vfxRoot = new GameObject("VFX");
        vfxRoot.transform.SetParent(ship.transform, false);
        ValidatorTestVFXReceiver receiver =
            vfxRoot.AddComponent<ValidatorTestVFXReceiver>();

        try
        {
            CombatArtValidationResult result = CombatArtValidator
                .Validate(ship)
                .Single(candidate => candidate.ContractId == "ART-API-015");

            Assert.That(
                result.Severity,
                Is.EqualTo(CombatArtValidationSeverity.PASS)
            );
            Assert.That(result.TargetObject, Is.SameAs(receiver));
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    [Test]
    public void Validation_DoesNotModifyPrefabObjectState()
    {
        GameObject prefab = LoadCombatPrefab();
        Transform[] transforms = prefab.GetComponentsInChildren<Transform>(
            true
        );
        Component[] rootComponents = prefab.GetComponents<Component>();
        Vector3[] positions = transforms
            .Select(item => item.localPosition)
            .ToArray();
        Quaternion[] rotations = transforms
            .Select(item => item.localRotation)
            .ToArray();
        Vector3[] scales = transforms
            .Select(item => item.localScale)
            .ToArray();
        bool wasDirty = EditorUtility.IsDirty(prefab);

        CombatArtValidator.Validate(prefab);

        Assert.That(
            prefab.GetComponentsInChildren<Transform>(true),
            Is.EqualTo(transforms)
        );
        Assert.That(
            prefab.GetComponents<Component>(),
            Is.EqualTo(rootComponents)
        );
        Assert.That(
            transforms.Select(item => item.localPosition),
            Is.EqualTo(positions)
        );
        Assert.That(
            transforms.Select(item => item.localRotation),
            Is.EqualTo(rotations)
        );
        Assert.That(
            transforms.Select(item => item.localScale),
            Is.EqualTo(scales)
        );
        Assert.That(EditorUtility.IsDirty(prefab), Is.EqualTo(wasDirty));
    }


    [Test]
    public void Results_ContainOnlyStableActiveContractIds()
    {
        GameObject ship = CreateValidCombatShip();

        try
        {
            IReadOnlyList<CombatArtValidationResult> results =
                CombatArtValidator.Validate(ship);

            Assert.That(
                results.All(
                    result => ActiveContractIds.Contains(result.ContractId)
                ),
                Is.True
            );
            Assert.That(
                results.All(
                    result => !string.IsNullOrWhiteSpace(result.Message)
                ),
                Is.True
            );
            Assert.That(
                results.All(result => result.TargetObject != null),
                Is.True
            );
        }
        finally
        {
            Object.DestroyImmediate(ship);
        }
    }


    private static GameObject CreateValidCombatShip()
    {
        GameObject ship = Object.Instantiate(LoadCombatPrefab());
        ship.name = "Valid Combat Ship";
        return ship;
    }


    private static GameObject LoadCombatPrefab()
    {
        return LoadPrefab(CombatPrefabPath);
    }


    private static GameObject LoadPrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }


    private static void AssertOnlyConfiguredCombatPrefabCanPass(
        IReadOnlyList<CombatArtValidationResult> results
    )
    {
        Assert.That(
            results.All(
                result => result.Severity
                    == CombatArtValidationSeverity.PASS
            ),
            Is.False
        );
    }


    private static void AssertError(
        CombatArtValidationResult result,
        string contractId,
        UnityEngine.Object target
    )
    {
        Assert.That(result.ContractId, Is.EqualTo(contractId));
        Assert.That(
            result.Severity,
            Is.EqualTo(CombatArtValidationSeverity.ERROR)
        );
        Assert.That(result.Message, Is.Not.Empty);
        Assert.That(result.TargetObject, Is.SameAs(target));
    }
}

public sealed class ValidatorTestVFXReceiver
    : MonoBehaviour,
        ICombatVFXEventReceiver
{
    public void OnMuzzleFire(CombatMuzzleFireEvent eventData)
    {
    }


    public void OnWaterImpact(CombatWaterImpactEvent eventData)
    {
    }


    public void OnHullImpact(CombatHullImpactEvent eventData)
    {
    }
}
