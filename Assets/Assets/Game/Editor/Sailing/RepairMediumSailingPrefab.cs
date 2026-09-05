using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RepairMediumSailingPrefab
{
    private const string MediumPrefabName = "PF_Proxy_Medium_v01";
    private const string MediumProfileName = "MovementProfile_Medium";

    private static readonly Type[] RequiredComponentTypes =
    {
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipLeeway),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController),
        typeof(ShipMovementProfileController)
    };

    private static readonly Dictionary<Type, string[]> ConfigurationFields =
        new Dictionary<Type, string[]>
        {
            {
                typeof(ShipSailingSpeed), new[]
                {
                    "globalWind", "sailPolarProfile", "shipTurning", "shipLeeway",
                    "baseMaxSpeed", "accelerationTimeConstant",
                    "naturalDragTimeConstant", "stopThreshold",
                    "fullTurnDragTimeConstant", "headingArrowLength",
                    "velocityArrowScale"
                }
            },
            {
                typeof(ShipTurning), new[]
                {
                    "shipSailingSpeed", "maxRudderAngle", "rudderResponse",
                    "rudderReferenceSpeed", "maxTurnRate"
                }
            },
            {
                typeof(ShipHeadingController), new[]
                {
                    "shipTurning", "rudderEaseAngle", "headingTolerance"
                }
            },
            {
                typeof(ShipTacking), new[]
                {
                    "shipSailingSpeed", "shipTurning", "headingController",
                    "tackYawAssistRate", "tackMinimumTargetSpeed",
                    "tackAbortSpeed", "tackAbortDelay", "tackExitAngle",
                    "windCrossDeadZone"
                }
            },
            {
                typeof(ShipWearing), new[]
                {
                    "shipSailingSpeed", "headingController",
                    "downwindCrossThreshold"
                }
            },
            {
                typeof(ShipLeeway), new[] { "leewayCurve" }
            },
            {
                typeof(ShipManeuverPlanner), new[]
                {
                    "globalWind", "headingController", "shipTacking", "shipWearing"
                }
            },
            {
                typeof(ShipDestinationController), new[]
                {
                    "maneuverPlanner", "globalWind", "arrivalRadius",
                    "replanHeadingThreshold", "directSailingThreshold",
                    "directResumeThreshold", "closeHauledHeadingAngle",
                    "maximumTackCorridorHalfWidth",
                    "minimumTackCorridorHalfWidth", "corridorDistanceRatio",
                    "corridorSwitchFactor"
                }
            },
            {
                typeof(ShipMovementProfileController), new[]
                {
                    "movementProfile", "shipSailingSpeed", "shipTurning", "shipTacking"
                }
            }
        };

    private static readonly string[] RuntimeBooleanFields =
    {
        "isActive", "hasDestination", "reachedDestination", "navigationBlocked",
        "profileApplied", "debugStartTack", "debugCancelTack", "debugStartWear",
        "debugCancelWear", "debugExecuteCommand", "debugCancelCommand"
    };

    private static readonly string[] RuntimeEnumFields =
    {
        "state", "currentManeuver", "navigationMode"
    };

    [MenuItem("Tools/Sailing/Repair Medium Sailing Prefab From Scene")]
    private static void RepairMediumPrefab()
    {
        bool shouldRepair = EditorUtility.DisplayDialog(
            "Repair Medium Sailing Prefab",
            "The active Scene Medium instance will be used as the configuration source. "
            + "The Medium prefab asset will be updated. Light and Heavy prefabs will not be changed.",
            "Repair Prefab",
            "Cancel"
        );

        if (!shouldRepair)
        {
            return;
        }

        GameObject sourceRoot = FindValidatedSceneMedium();

        if (sourceRoot == null)
        {
            return;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
            sourceRoot
        );
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );

        if (prefabAsset == null || prefabAsset.name != MediumPrefabName)
        {
            ShowError("The validated Scene instance does not resolve to PF_Proxy_Medium_v01.");
            return;
        }

        if (!HasRequiredComponentStack(sourceRoot))
        {
            return;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            foreach (Type componentType in RequiredComponentTypes)
            {
                Component sourceComponent = sourceRoot.GetComponent(componentType);
                Component targetComponent = prefabContents.GetComponent(componentType);

                if (targetComponent == null)
                {
                    targetComponent = prefabContents.AddComponent(componentType);
                }

                CopyConfiguration(
                    sourceComponent,
                    targetComponent,
                    sourceRoot,
                    prefabContents
                );
                ClearRuntimeState(targetComponent);
            }

            AssignMediumProfileIfAvailable(prefabContents);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
        }
        catch (Exception exception)
        {
            ShowError("The Medium prefab repair failed: " + exception.Message);
            return;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateRepairedPrefab(prefabPath);

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
        Debug.Log("Repaired Medium sailing prefab from the active Scene instance.");
    }


    private static GameObject FindValidatedSceneMedium()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        List<GameObject> candidates = new List<GameObject>();

        foreach (GameObject rootObject in activeScene.GetRootGameObjects())
        {
            if (rootObject.name == MediumPrefabName
                && !EditorUtility.IsPersistent(rootObject)
                && PrefabUtility.IsPartOfPrefabInstance(rootObject))
            {
                candidates.Add(rootObject);
            }
        }

        if (candidates.Count != 1)
        {
            ShowError(candidates.Count == 0
                ? "No valid Scene instance named PF_Proxy_Medium_v01 was found."
                : "Multiple Scene instances named PF_Proxy_Medium_v01 were found.");
            return null;
        }

        return candidates[0];
    }


    private static bool HasRequiredComponentStack(GameObject sourceRoot)
    {
        List<string> missingComponents = new List<string>();

        foreach (Type componentType in RequiredComponentTypes)
        {
            if (sourceRoot.GetComponent(componentType) == null)
            {
                missingComponents.Add(componentType.Name);
            }
        }

        if (missingComponents.Count == 0)
        {
            return true;
        }

        ShowError(
            "The Scene Medium instance is missing required components: "
            + string.Join(", ", missingComponents)
        );
        return false;
    }


    private static void CopyConfiguration(
        Component sourceComponent,
        Component targetComponent,
        GameObject sourceRoot,
        GameObject targetRoot
    )
    {
        SerializedObject sourceSerialized = new SerializedObject(sourceComponent);
        SerializedObject targetSerialized = new SerializedObject(targetComponent);
        sourceSerialized.Update();
        targetSerialized.Update();

        foreach (string fieldName in ConfigurationFields[sourceComponent.GetType()])
        {
            SerializedProperty sourceProperty = sourceSerialized.FindProperty(fieldName);
            SerializedProperty targetProperty = targetSerialized.FindProperty(fieldName);

            if (sourceProperty == null || targetProperty == null)
            {
                Debug.LogWarning(
                    "Skipped missing configuration field "
                    + sourceComponent.GetType().Name + "." + fieldName
                );
                continue;
            }

            if (sourceProperty.propertyType == SerializedPropertyType.ObjectReference)
            {
                targetProperty.objectReferenceValue = RemapReference(
                    sourceProperty.objectReferenceValue,
                    sourceRoot,
                    targetRoot,
                    sourceComponent.GetType().Name + "." + fieldName
                );
            }
            else
            {
                targetSerialized.CopyFromSerializedProperty(sourceProperty);
            }
        }

        targetSerialized.ApplyModifiedPropertiesWithoutUndo();
    }


    private static UnityEngine.Object RemapReference(
        UnityEngine.Object sourceReference,
        GameObject sourceRoot,
        GameObject targetRoot,
        string fieldPath
    )
    {
        if (sourceReference == null || EditorUtility.IsPersistent(sourceReference))
        {
            return sourceReference;
        }

        GameObject sourceObject = GetReferenceGameObject(sourceReference);

        if (sourceObject != null
            && (sourceObject == sourceRoot
                || sourceObject.transform.IsChildOf(sourceRoot.transform)))
        {
            Transform targetTransform = FindEquivalentTransform(
                sourceObject.transform,
                sourceRoot.transform,
                targetRoot.transform
            );

            if (targetTransform == null)
            {
                Debug.LogWarning(
                    "Cleared unmapped same-ship reference " + fieldPath
                );
                return null;
            }

            if (sourceReference is GameObject)
            {
                return targetTransform.gameObject;
            }

            Component sourceComponent = sourceReference as Component;
            return sourceComponent != null
                ? targetTransform.GetComponent(sourceComponent.GetType())
                : null;
        }

        Debug.LogWarning("Cleared external Scene reference " + fieldPath);
        return null;
    }


    private static GameObject GetReferenceGameObject(
        UnityEngine.Object reference
    )
    {
        GameObject gameObject = reference as GameObject;

        if (gameObject != null)
        {
            return gameObject;
        }

        Component component = reference as Component;
        return component != null ? component.gameObject : null;
    }


    private static Transform FindEquivalentTransform(
        Transform sourceTransform,
        Transform sourceRoot,
        Transform targetRoot
    )
    {
        List<int> siblingIndices = new List<int>();
        Transform current = sourceTransform;

        while (current != sourceRoot)
        {
            siblingIndices.Add(current.GetSiblingIndex());
            current = current.parent;

            if (current == null)
            {
                return null;
            }
        }

        Transform target = targetRoot;

        for (int index = siblingIndices.Count - 1; index >= 0; index--)
        {
            int siblingIndex = siblingIndices[index];

            if (siblingIndex >= target.childCount)
            {
                return null;
            }

            target = target.GetChild(siblingIndex);
        }

        return target;
    }


    private static void ClearRuntimeState(Component component)
    {
        SerializedObject serializedComponent = new SerializedObject(component);
        serializedComponent.Update();

        foreach (string fieldName in RuntimeBooleanFields)
        {
            SerializedProperty property = serializedComponent.FindProperty(fieldName);

            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = false;
            }
        }

        foreach (string fieldName in RuntimeEnumFields)
        {
            SerializedProperty property = serializedComponent.FindProperty(fieldName);

            if (property != null && property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = 0;
            }
        }

        SerializedProperty profileName = serializedComponent.FindProperty(
            "appliedProfileName"
        );

        if (profileName != null
            && profileName.propertyType == SerializedPropertyType.String)
        {
            profileName.stringValue = string.Empty;
        }

        serializedComponent.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void AssignMediumProfileIfAvailable(GameObject prefabContents)
    {
        string[] profilePaths = AssetDatabase.FindAssets(
            MediumProfileName + " t:ShipMovementProfile"
        );
        List<ShipMovementProfile> profiles = new List<ShipMovementProfile>();

        foreach (string profileGuid in profilePaths)
        {
            string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);

            if (Path.GetFileNameWithoutExtension(profilePath) == MediumProfileName)
            {
                ShipMovementProfile profile =
                    AssetDatabase.LoadAssetAtPath<ShipMovementProfile>(profilePath);

                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
        }

        if (profiles.Count != 1)
        {
            Debug.LogWarning(profiles.Count == 0
                ? "MovementProfile_Medium was not found."
                : "Multiple MovementProfile_Medium assets were found.");
            return;
        }

        ShipMovementProfileController controller =
            prefabContents.GetComponent<ShipMovementProfileController>();
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty profileProperty =
            serializedController.FindProperty("movementProfile");
        profileProperty.objectReferenceValue = profiles[0];
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void ValidateRepairedPrefab(string prefabPath)
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            foreach (Type componentType in RequiredComponentTypes)
            {
                if (prefabContents.GetComponent(componentType) == null)
                {
                    Debug.LogError(
                        "Repaired prefab is missing " + componentType.Name
                    );
                }
            }

            ValidateReference<ShipSailingSpeed>(
                prefabContents, "shipTurning", typeof(ShipTurning)
            );
            ValidateReference<ShipSailingSpeed>(
                prefabContents, "shipLeeway", typeof(ShipLeeway)
            );
            ValidateReference<ShipMovementProfileController>(
                prefabContents, "shipSailingSpeed", typeof(ShipSailingSpeed)
            );
            ValidateReference<ShipMovementProfileController>(
                prefabContents, "shipTurning", typeof(ShipTurning)
            );
            ValidateReference<ShipMovementProfileController>(
                prefabContents, "shipTacking", typeof(ShipTacking)
            );
            ValidateReference<ShipManeuverPlanner>(
                prefabContents, "headingController", typeof(ShipHeadingController)
            );
            ValidateReference<ShipManeuverPlanner>(
                prefabContents, "shipTacking", typeof(ShipTacking)
            );
            ValidateReference<ShipManeuverPlanner>(
                prefabContents, "shipWearing", typeof(ShipWearing)
            );
            ValidateReference<ShipDestinationController>(
                prefabContents, "maneuverPlanner", typeof(ShipManeuverPlanner)
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }


    private static void ValidateReference<T>(
        GameObject prefabContents,
        string propertyName,
        Type expectedType
    ) where T : Component
    {
        T component = prefabContents.GetComponent<T>();

        if (component == null)
        {
            return;
        }

        SerializedObject serializedComponent = new SerializedObject(component);
        SerializedProperty property = serializedComponent.FindProperty(propertyName);
        Component expectedComponent = prefabContents.GetComponent(expectedType);

        if (property == null
            || property.objectReferenceValue != expectedComponent)
        {
            Debug.LogWarning(
                "Prefab internal reference is not valid: "
                + typeof(T).Name + "." + propertyName
            );
        }
    }


    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog("Repair Medium Sailing Prefab", message, "OK");
        Debug.LogError(message);
    }
}
