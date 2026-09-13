using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CombatArtValidator
{
    private const string MenuPath =
        "Tools/Combat Art/Validate Selected Ship";
    private const float RootScaleTolerance = 0.0001f;
    private const float SideToleranceMeters = 0.0001f;
    private const int CombatGeometryLayer = 8;

    private static readonly string[] RootContractIds =
    {
        "ART-PFB-004",
        "ART-CONV-010"
    };

    private static readonly string[] ArtDefinitionContractIds =
    {
        "ART-API-001",
        "ART-REF-002",
        "ART-REF-003",
        "ART-REF-004",
        "ART-REF-005",
        "ART-REF-006",
        "ART-REF-007",
        "ART-REF-008",
        "ART-REF-009"
    };

    private static readonly string[] MuzzleSocketContractIds =
    {
        "ART-API-004",
        "ART-API-005",
        "ART-API-006",
        "ART-SCK-001",
        "ART-SCK-002"
    };

    private static readonly string[] CombatGeometryContractIds =
    {
        "ART-PFB-003",
        "ART-API-007",
        "ART-REF-013",
        "ART-REF-014",
        "ART-REF-015",
        "ART-REF-016",
        "ART-REF-017",
        "ART-REF-018",
        "ART-LYR-001",
        "ART-CONV-009"
    };

    private static readonly string[] ExposureContractIds =
    {
        "ART-API-011",
        "ART-API-012",
        "ART-REF-019"
    };

    private static readonly string[] VFXContractIds =
    {
        "ART-API-015"
    };


    [MenuItem(MenuPath)]
    private static void ValidateSelectedShip()
    {
        IReadOnlyList<CombatArtValidationResult> results = Validate(
            Selection.activeGameObject
        );

        LogReport(
            FormatReport(Selection.activeGameObject, results),
            GetWorstSeverity(results),
            Selection.activeGameObject
        );

        foreach (CombatArtValidationResult result in results)
        {
            LogResult(result);
        }
    }


    [MenuItem(MenuPath, true)]
    private static bool CanValidateSelectedShip()
    {
        return Selection.activeGameObject != null;
    }


    public static IReadOnlyList<CombatArtValidationResult> Validate(
        GameObject shipRoot
    )
    {
        if (shipRoot == null)
        {
            throw new ArgumentNullException(nameof(shipRoot));
        }

        List<CombatArtValidationResult> results =
            new List<CombatArtValidationResult>();

        ValidateRootScale(shipRoot, results);

        ShipArtDefinition artDefinition =
            ValidateRequiredComponent<ShipArtDefinition>(
                shipRoot,
                "ART-API-001",
                results
            );

        if (artDefinition != null)
        {
            ValidateArtDefinition(artDefinition, results);
        }

        ValidateBowConvention(shipRoot, artDefinition, results);

        ShipMuzzleSockets muzzleSockets =
            ValidateRequiredComponent<ShipMuzzleSockets>(
                shipRoot,
                "ART-API-004",
                results
            );

        if (muzzleSockets != null)
        {
            ValidateMuzzleSockets(shipRoot, muzzleSockets, results);
        }

        ShipCombatGeometry combatGeometry =
            ValidateRequiredComponent<ShipCombatGeometry>(
                shipRoot,
                "ART-API-007",
                results
            );

        if (combatGeometry != null)
        {
            ValidateCombatGeometry(shipRoot, combatGeometry, results);
        }

        ShipExposureReference exposureReference =
            ValidateRequiredComponent<ShipExposureReference>(
                shipRoot,
                "ART-API-011",
                results
            );

        if (exposureReference != null)
        {
            ValidateExposure(exposureReference, results);
        }

        ValidateVFXReceiver(shipRoot, results);
        return results.AsReadOnly();
    }


    public static string FormatReport(
        GameObject shipRoot,
        IReadOnlyList<CombatArtValidationResult> results
    )
    {
        if (shipRoot == null)
        {
            throw new ArgumentNullException(nameof(shipRoot));
        }

        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        CombatArtValidationSeverity status = GetWorstSeverity(results);
        StringBuilder report = new StringBuilder();

        report.AppendLine("Combat Art Validation Report");
        report.AppendLine();
        report.AppendLine("Ship:");
        report.AppendLine(shipRoot.name);
        report.AppendLine();
        report.AppendLine("Status:");
        report.AppendLine(status.ToString());
        report.AppendLine();
        report.AppendLine("Checks:");
        AppendReportSection(
            report,
            "Root Contract",
            results,
            RootContractIds
        );
        AppendReportSection(
            report,
            "ShipArtDefinition",
            results,
            ArtDefinitionContractIds
        );
        AppendReportSection(
            report,
            "Muzzle Socket",
            results,
            MuzzleSocketContractIds
        );
        AppendReportSection(
            report,
            "Combat Geometry",
            results,
            CombatGeometryContractIds
        );
        AppendReportSection(
            report,
            "Exposure",
            results,
            ExposureContractIds
        );
        AppendReportSection(
            report,
            "VFX",
            results,
            VFXContractIds
        );

        return report.ToString().TrimEnd();
    }


    private static void ValidateRootScale(
        GameObject shipRoot,
        ICollection<CombatArtValidationResult> results
    )
    {
        Vector3 scale = shipRoot.transform.localScale;
        bool valid = Mathf.Abs(scale.x - 1f) <= RootScaleTolerance
            && Mathf.Abs(scale.y - 1f) <= RootScaleTolerance
            && Mathf.Abs(scale.z - 1f) <= RootScaleTolerance;

        AddResult(
            results,
            "ART-PFB-004",
            valid,
            "Ship Root local scale is (1, 1, 1).",
            $"Ship Root local scale is {scale}; expected (1, 1, 1).",
            shipRoot
        );
    }


    private static T ValidateRequiredComponent<T>(
        GameObject shipRoot,
        string contractId,
        ICollection<CombatArtValidationResult> results
    ) where T : Component
    {
        T component = shipRoot.GetComponent<T>();
        bool exists = component != null;

        AddResult(
            results,
            contractId,
            exists,
            $"{typeof(T).Name} exists on the ship Root.",
            $"{typeof(T).Name} is missing from the ship Root.",
            exists ? component : shipRoot
        );
        return component;
    }


    private static void ValidateArtDefinition(
        ShipArtDefinition definition,
        ICollection<CombatArtValidationResult> results
    )
    {
        ValidateReference(
            definition.VisualRoot,
            "ART-REF-002",
            "VisualRoot",
            definition,
            results
        );
        ValidateReference(
            definition.WaterlineReference,
            "ART-REF-003",
            "WaterlineReference",
            definition,
            results
        );
        ValidateReference(
            definition.CenterReference,
            "ART-REF-004",
            "CenterReference",
            definition,
            results
        );
        ValidateReference(
            definition.BowReference,
            "ART-REF-005",
            "BowReference",
            definition,
            results
        );
        ValidateReference(
            definition.SternReference,
            "ART-REF-006",
            "SternReference",
            definition,
            results
        );
        ValidateReference(
            definition.PortReference,
            "ART-REF-007",
            "PortReference",
            definition,
            results
        );
        ValidateReference(
            definition.StarboardReference,
            "ART-REF-008",
            "StarboardReference",
            definition,
            results
        );
        ValidateReference(
            definition.DeckReference,
            "ART-REF-009",
            "DeckReference",
            definition,
            results
        );
    }


    private static void ValidateBowConvention(
        GameObject shipRoot,
        ShipArtDefinition definition,
        ICollection<CombatArtValidationResult> results
    )
    {
        bool hasAxisReferences = definition != null
            && definition.BowReference != null
            && definition.SternReference != null;
        bool bowUsesPositiveZ = false;

        if (hasAxisReferences)
        {
            float bowZ = shipRoot.transform.InverseTransformPoint(
                definition.BowReference.position
            ).z;
            float sternZ = shipRoot.transform.InverseTransformPoint(
                definition.SternReference.position
            ).z;
            bowUsesPositiveZ = IsFinite(bowZ)
                && IsFinite(sternZ)
                && bowZ > sternZ;
        }

        AddResult(
            results,
            "ART-CONV-010",
            bowUsesPositiveZ,
            "BowReference is forward of SternReference on Root-local +Z.",
            hasAxisReferences
                ? "BowReference must be forward of SternReference on "
                    + "Root-local +Z."
                : "Root +Z Bow validation requires BowReference and "
                    + "SternReference.",
            definition != null ? definition : shipRoot
        );
    }


    private static void ValidateReference(
        UnityEngine.Object reference,
        string contractId,
        string referenceName,
        Component owner,
        ICollection<CombatArtValidationResult> results
    )
    {
        bool exists = reference != null;

        AddResult(
            results,
            contractId,
            exists,
            $"{referenceName} is assigned.",
            $"{referenceName} is not assigned.",
            exists ? reference : owner
        );
    }


    private static void ValidateMuzzleSockets(
        GameObject shipRoot,
        ShipMuzzleSockets muzzleSockets,
        ICollection<CombatArtValidationResult> results
    )
    {
        ValidateMuzzleCollection(
            shipRoot,
            muzzleSockets.PortMuzzles,
            true,
            "ART-API-005",
            "ART-SCK-001",
            results
        );
        ValidateMuzzleCollection(
            shipRoot,
            muzzleSockets.StarboardMuzzles,
            false,
            "ART-API-006",
            "ART-SCK-002",
            results
        );
    }


    private static void ValidateMuzzleCollection(
        GameObject shipRoot,
        IReadOnlyList<Transform> sockets,
        bool isPort,
        string collectionContractId,
        string socketContractId,
        ICollection<CombatArtValidationResult> results
    )
    {
        string sideName = isPort ? "Port" : "Starboard";
        bool collectionExists = sockets != null && sockets.Count > 0;

        AddResult(
            results,
            collectionContractId,
            collectionExists,
            $"{sideName} muzzle collection contains authored sockets.",
            $"{sideName} muzzle collection is missing or empty.",
            shipRoot
        );

        if (!collectionExists)
        {
            return;
        }

        bool allSocketsValid = true;

        for (int index = 0; index < sockets.Count; index++)
        {
            Transform socket = sockets[index];

            if (socket == null)
            {
                allSocketsValid = false;
                AddError(
                    results,
                    socketContractId,
                    $"{sideName} muzzle collection contains a null entry "
                        + $"at index {index}.",
                    shipRoot
                );
                continue;
            }

            Vector3 localPosition = shipRoot.transform.InverseTransformPoint(
                socket.position
            );
            bool transformValid = socket.IsChildOf(shipRoot.transform)
                && IsFinite(localPosition)
                && IsFinite(socket.forward);

            if (!transformValid)
            {
                allSocketsValid = false;
                AddError(
                    results,
                    socketContractId,
                    $"{sideName} muzzle socket {socket.name} has an invalid "
                        + "Transform or is outside the ship Root hierarchy.",
                    socket
                );
                continue;
            }

            bool sideValid = isPort
                ? localPosition.x < -SideToleranceMeters
                : localPosition.x > SideToleranceMeters;

            if (!sideValid)
            {
                allSocketsValid = false;
                string expectedSide = isPort
                    ? "Port (-X)"
                    : "Starboard (+X)";
                AddError(
                    results,
                    socketContractId,
                    $"{sideName} muzzle socket {socket.name} at Root-local "
                        + $"X={localPosition.x:F3} is not on the "
                        + $"{expectedSide} side.",
                    socket
                );
            }
        }

        if (allSocketsValid)
        {
            AddPass(
                results,
                socketContractId,
                $"All {sideName} muzzle socket Transforms are valid and "
                    + "side-consistent.",
                shipRoot
            );
        }
    }


    private static void ValidateCombatGeometry(
        GameObject shipRoot,
        ShipCombatGeometry geometry,
        ICollection<CombatArtValidationResult> results
    )
    {
        Transform combatGeometryRoot = shipRoot.transform.Find(
            "CombatGeometry"
        );
        Transform expectedMainHull = combatGeometryRoot != null
            ? combatGeometryRoot.Find("MainHull")
            : null;
        bool hierarchyValid = combatGeometryRoot != null
            && expectedMainHull != null
            && geometry.MainHullRoot == expectedMainHull;

        AddResult(
            results,
            "ART-PFB-003",
            hierarchyValid,
            "CombatGeometry/MainHull hierarchy exists and is assigned.",
            "CombatGeometry/MainHull hierarchy is missing or does not match "
                + "ShipCombatGeometry.MainHullRoot.",
            geometry
        );
        AddResult(
            results,
            "ART-REF-013",
            geometry.MainHullRoot != null,
            "ShipCombatGeometry.MainHullRoot is assigned.",
            "ShipCombatGeometry.MainHullRoot is not assigned.",
            geometry.MainHullRoot != null ? geometry.MainHullRoot : geometry
        );

        ValidateCombatRegion(
            geometry,
            geometry.BowRegion,
            CombatHullRegion.Bow,
            "Bow",
            "ART-REF-014",
            results
        );
        ValidateCombatRegion(
            geometry,
            geometry.MidshipRegion,
            CombatHullRegion.Midship,
            "Midship",
            "ART-REF-015",
            results
        );
        ValidateCombatRegion(
            geometry,
            geometry.SternRegion,
            CombatHullRegion.Stern,
            "Stern",
            "ART-REF-016",
            results
        );
    }


    private static void ValidateCombatRegion(
        ShipCombatGeometry geometry,
        CombatHitRegion region,
        CombatHullRegion expectedRegion,
        string expectedName,
        string referenceContractId,
        ICollection<CombatArtValidationResult> results
    )
    {
        if (region == null)
        {
            AddError(
                results,
                referenceContractId,
                $"{expectedName} Combat Geometry region is not assigned.",
                geometry
            );
            return;
        }

        bool semanticRegionValid = region.Region == expectedRegion
            && region.name == expectedName
            && geometry.MainHullRoot != null
            && region.transform.parent == geometry.MainHullRoot;
        AddResult(
            results,
            referenceContractId,
            semanticRegionValid,
            $"{expectedName} semantic region is correctly assigned.",
            $"{expectedName} semantic region identity or hierarchy is invalid.",
            region
        );

        AddResult(
            results,
            "ART-REF-017",
            region.Owner == geometry,
            $"{expectedName} region references its owning geometry.",
            $"{expectedName} region has an invalid Owner reference.",
            region
        );

        Collider collider = region.QueryCollider;

        if (collider == null)
        {
            AddError(
                results,
                "ART-REF-018",
                $"{expectedName} region QueryCollider is not assigned.",
                region
            );
            return;
        }

        AddResult(
            results,
            "ART-REF-018",
            collider is BoxCollider
                && collider.gameObject == region.gameObject,
            $"{expectedName} region uses its authored BoxCollider.",
            $"{expectedName} region QueryCollider must be a BoxCollider on "
                + "the semantic region object.",
            collider
        );
        AddResult(
            results,
            "ART-CONV-009",
            collider.isTrigger,
            $"{expectedName} Combat Geometry collider is a Trigger.",
            $"{expectedName} Combat Geometry collider must be a Trigger.",
            collider
        );

        bool layerValid = LayerMask.NameToLayer("CombatGeometry")
                == CombatGeometryLayer
            && collider.gameObject.layer == CombatGeometryLayer;
        AddResult(
            results,
            "ART-LYR-001",
            layerValid,
            $"{expectedName} collider uses CombatGeometry layer 8.",
            $"{expectedName} collider must use CombatGeometry layer 8.",
            collider
        );
    }


    private static void ValidateExposure(
        ShipExposureReference exposure,
        ICollection<CombatArtValidationResult> results
    )
    {
        bool hasArtDefinition = exposure.ArtDefinition != null;
        AddResult(
            results,
            "ART-REF-019",
            hasArtDefinition,
            "ShipExposureReference.ArtDefinition is assigned.",
            "ShipExposureReference.ArtDefinition is not assigned.",
            hasArtDefinition ? exposure.ArtDefinition : exposure
        );

        bool dimensionsValid = exposure.TryGetReferenceDimensions(
            out Vector3 dimensionsMeters
        ) && IsFinite(dimensionsMeters)
            && dimensionsMeters.x > 0f
            && dimensionsMeters.y > 0f
            && dimensionsMeters.z > 0f;
        AddResult(
            results,
            "ART-API-012",
            dimensionsValid,
            $"Exposure dimensions are positive: {dimensionsMeters} m.",
            "Exposure dimensions are unavailable, non-finite, or not positive.",
            exposure
        );
    }


    private static void ValidateVFXReceiver(
        GameObject shipRoot,
        ICollection<CombatArtValidationResult> results
    )
    {
        MonoBehaviour receiverComponent = null;
        MonoBehaviour[] behaviours =
            shipRoot.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ICombatVFXEventReceiver)
            {
                receiverComponent = behaviour;
                break;
            }
        }

        bool exists = receiverComponent != null;
        AddResult(
            results,
            "ART-API-015",
            exists,
            "An ICombatVFXEventReceiver exists in the ship hierarchy.",
            "No placeholder or production ICombatVFXEventReceiver exists "
                + "in the ship hierarchy.",
            exists ? receiverComponent : shipRoot
        );
    }


    private static void AddResult(
        ICollection<CombatArtValidationResult> results,
        string contractId,
        bool passed,
        string passMessage,
        string errorMessage,
        UnityEngine.Object target
    )
    {
        results.Add(
            new CombatArtValidationResult(
                contractId,
                passed
                    ? CombatArtValidationSeverity.PASS
                    : CombatArtValidationSeverity.ERROR,
                passed ? passMessage : errorMessage,
                target
            )
        );
    }


    private static void AddPass(
        ICollection<CombatArtValidationResult> results,
        string contractId,
        string message,
        UnityEngine.Object target
    )
    {
        results.Add(
            new CombatArtValidationResult(
                contractId,
                CombatArtValidationSeverity.PASS,
                message,
                target
            )
        );
    }


    private static void AddError(
        ICollection<CombatArtValidationResult> results,
        string contractId,
        string message,
        UnityEngine.Object target
    )
    {
        results.Add(
            new CombatArtValidationResult(
                contractId,
                CombatArtValidationSeverity.ERROR,
                message,
                target
            )
        );
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }


    private static void AppendReportSection(
        StringBuilder report,
        string sectionName,
        IReadOnlyList<CombatArtValidationResult> results,
        IReadOnlyList<string> contractIds
    )
    {
        CombatArtValidationSeverity severity = GetSectionSeverity(
            results,
            contractIds
        );
        report.Append(GetSeverityMarker(severity));
        report.Append(' ');
        report.AppendLine(sectionName);
    }


    private static CombatArtValidationSeverity GetSectionSeverity(
        IReadOnlyList<CombatArtValidationResult> results,
        IReadOnlyList<string> contractIds
    )
    {
        CombatArtValidationSeverity severity =
            CombatArtValidationSeverity.PASS;
        bool foundResult = false;

        foreach (CombatArtValidationResult result in results)
        {
            if (!ContainsContract(contractIds, result.ContractId))
            {
                continue;
            }

            foundResult = true;
            severity = GetWorseSeverity(severity, result.Severity);
        }

        return foundResult
            ? severity
            : CombatArtValidationSeverity.ERROR;
    }


    private static bool ContainsContract(
        IReadOnlyList<string> contractIds,
        string contractId
    )
    {
        foreach (string candidate in contractIds)
        {
            if (candidate == contractId)
            {
                return true;
            }
        }

        return false;
    }


    private static CombatArtValidationSeverity GetWorstSeverity(
        IReadOnlyList<CombatArtValidationResult> results
    )
    {
        CombatArtValidationSeverity severity =
            CombatArtValidationSeverity.PASS;

        foreach (CombatArtValidationResult result in results)
        {
            severity = GetWorseSeverity(severity, result.Severity);
        }

        return results.Count > 0
            ? severity
            : CombatArtValidationSeverity.ERROR;
    }


    private static CombatArtValidationSeverity GetWorseSeverity(
        CombatArtValidationSeverity first,
        CombatArtValidationSeverity second
    )
    {
        if (first == CombatArtValidationSeverity.ERROR
            || second == CombatArtValidationSeverity.ERROR)
        {
            return CombatArtValidationSeverity.ERROR;
        }

        if (first == CombatArtValidationSeverity.WARNING
            || second == CombatArtValidationSeverity.WARNING)
        {
            return CombatArtValidationSeverity.WARNING;
        }

        return CombatArtValidationSeverity.PASS;
    }


    private static string GetSeverityMarker(
        CombatArtValidationSeverity severity
    )
    {
        switch (severity)
        {
            case CombatArtValidationSeverity.PASS:
                return "\u2713";
            case CombatArtValidationSeverity.WARNING:
                return "\u26A0";
            default:
                return "\u2717";
        }
    }


    private static void LogReport(
        string report,
        CombatArtValidationSeverity severity,
        UnityEngine.Object target
    )
    {
        switch (severity)
        {
            case CombatArtValidationSeverity.ERROR:
                Debug.LogError(report, target);
                break;
            case CombatArtValidationSeverity.WARNING:
                Debug.LogWarning(report, target);
                break;
            default:
                Debug.Log(report, target);
                break;
        }
    }


    private static void LogResult(CombatArtValidationResult result)
    {
        string message = $"[{result.ContractId}] {result.Severity}: "
            + result.Message;

        switch (result.Severity)
        {
            case CombatArtValidationSeverity.ERROR:
                Debug.LogError(message, result.TargetObject);
                break;
            case CombatArtValidationSeverity.WARNING:
                Debug.LogWarning(message, result.TargetObject);
                break;
            default:
                Debug.Log(message, result.TargetObject);
                break;
        }
    }
}
