using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RebuildMovementProxyPrefabs
{
    private const string MediumPrefabName = "PF_Proxy_Medium_v01";
    private const string LightPrefabName = "PF_Proxy_Light_v01";
    private const string HeavyPrefabName = "PF_Proxy_Heavy_v01";

    private static readonly Type[] RequiredComponentTypes =
    {
        typeof(ShipMovementProfileController),
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipLeeway),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController)
    };

    [MenuItem("Tools/Sailing/Rebuild Light Heavy Movement Proxies")]
    private static void RebuildMovementProxies()
    {
        string mediumPrefabPath = FindExactPrefabPath(MediumPrefabName);

        if (string.IsNullOrEmpty(mediumPrefabPath)
            || !ValidateMediumPrefab(mediumPrefabPath))
        {
            return;
        }

        ShipMovementProfile lightProfile = FindExactProfile("MovementProfile_Light");
        ShipMovementProfile mediumProfile = FindExactProfile("MovementProfile_Medium");
        ShipMovementProfile heavyProfile = FindExactProfile("MovementProfile_Heavy");

        if (lightProfile == null || mediumProfile == null || heavyProfile == null)
        {
            return;
        }

        bool shouldRebuild = EditorUtility.DisplayDialog(
            "Rebuild Movement Proxy Prefabs",
            "PF_Proxy_Light_v01 and PF_Proxy_Heavy_v01 will be rebuilt from Medium. "
            + "Their existing prefab contents will be replaced. Movement Profile assets will not be overwritten.",
            "Rebuild Prefabs",
            "Cancel"
        );

        if (!shouldRebuild)
        {
            return;
        }

        string prefabDirectory = Path.GetDirectoryName(mediumPrefabPath)
            ?.Replace('\\', '/');
        string lightPrefabPath = prefabDirectory + "/" + LightPrefabName + ".prefab";
        string heavyPrefabPath = prefabDirectory + "/" + HeavyPrefabName + ".prefab";

        RebuildPrefab(mediumPrefabPath, lightPrefabPath, LightPrefabName, lightProfile);
        RebuildPrefab(mediumPrefabPath, heavyPrefabPath, HeavyPrefabName, heavyProfile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool lightValid = ValidateGeneratedPrefab(lightPrefabPath, lightProfile);
        bool heavyValid = ValidateGeneratedPrefab(heavyPrefabPath, heavyProfile);

        UnityEngine.Object lightPrefab = AssetDatabase.LoadMainAssetAtPath(
            lightPrefabPath
        );
        UnityEngine.Object heavyPrefab = AssetDatabase.LoadMainAssetAtPath(
            heavyPrefabPath
        );
        Selection.objects = new[] { lightPrefab, heavyPrefab };
        EditorGUIUtility.PingObject(lightPrefab);

        Debug.Log(
            "Rebuilt Light and Heavy movement proxy prefabs. "
            + "Validation: Light=" + lightValid + ", Heavy=" + heavyValid + "."
        );
    }


    private static string FindExactPrefabPath(string prefabName)
    {
        string[] matchingPaths = AssetDatabase.FindAssets(prefabName + " t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path) == prefabName)
            .ToArray();

        if (matchingPaths.Length == 1)
        {
            return matchingPaths[0];
        }

        ShowError(matchingPaths.Length == 0
            ? "No prefab named " + prefabName + " was found."
            : "Multiple prefabs named " + prefabName + " were found.");
        return string.Empty;
    }


    private static ShipMovementProfile FindExactProfile(string profileName)
    {
        List<ShipMovementProfile> matchingProfiles = AssetDatabase.FindAssets(
            profileName + " t:ShipMovementProfile"
        )
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path) == profileName)
            .Select(AssetDatabase.LoadAssetAtPath<ShipMovementProfile>)
            .Where(profile => profile != null)
            .ToList();

        if (matchingProfiles.Count == 1)
        {
            return matchingProfiles[0];
        }

        ShowError(matchingProfiles.Count == 0
            ? "No Movement Profile asset named " + profileName + " was found."
            : "Multiple Movement Profile assets named " + profileName + " were found.");
        return null;
    }


    private static bool ValidateMediumPrefab(string prefabPath)
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            List<string> missingComponents = GetMissingComponents(prefabContents);

            if (missingComponents.Count == 0)
            {
                return true;
            }

            ShowError(
                "PF_Proxy_Medium_v01 is missing required components: "
                + string.Join(", ", missingComponents)
            );
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }


    private static void RebuildPrefab(
        string sourcePrefabPath,
        string targetPrefabPath,
        string targetPrefabName,
        ShipMovementProfile movementProfile
    )
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(
            sourcePrefabPath
        );

        try
        {
            prefabContents.name = targetPrefabName;
            AssignMovementProfile(prefabContents, movementProfile);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, targetPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }


    private static void AssignMovementProfile(
        GameObject prefabContents,
        ShipMovementProfile movementProfile
    )
    {
        ShipMovementProfileController controller =
            prefabContents.GetComponent<ShipMovementProfileController>();
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty profileProperty =
            serializedController.FindProperty("movementProfile");
        profileProperty.objectReferenceValue = movementProfile;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }


    private static bool ValidateGeneratedPrefab(
        string prefabPath,
        ShipMovementProfile expectedProfile
    )
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            List<string> missingComponents = GetMissingComponents(prefabContents);

            if (missingComponents.Count > 0)
            {
                Debug.LogError(
                    "Generated prefab is missing required components: "
                    + string.Join(", ", missingComponents)
                );
                return false;
            }

            ShipMovementProfileController controller =
                prefabContents.GetComponent<ShipMovementProfileController>();
            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty profileProperty =
                serializedController.FindProperty("movementProfile");

            if (profileProperty.objectReferenceValue != expectedProfile)
            {
                Debug.LogError(
                    "Generated prefab has an unexpected Movement Profile assignment."
                );
                return false;
            }

            return ValidateInternalReferences(prefabContents);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }


    private static List<string> GetMissingComponents(GameObject prefabContents)
    {
        List<string> missingComponents = new List<string>();

        foreach (Type componentType in RequiredComponentTypes)
        {
            if (prefabContents.GetComponent(componentType) == null)
            {
                missingComponents.Add(componentType.Name);
            }
        }

        return missingComponents;
    }


    private static bool ValidateInternalReferences(GameObject prefabContents)
    {
        return ValidateReference<ShipSailingSpeed>(
                   prefabContents, "shipTurning", typeof(ShipTurning)
               )
               && ValidateReference<ShipSailingSpeed>(
                   prefabContents, "shipLeeway", typeof(ShipLeeway)
               )
               && ValidateReference<ShipMovementProfileController>(
                   prefabContents, "shipSailingSpeed", typeof(ShipSailingSpeed)
               )
               && ValidateReference<ShipMovementProfileController>(
                   prefabContents, "shipTurning", typeof(ShipTurning)
               )
               && ValidateReference<ShipMovementProfileController>(
                   prefabContents, "shipTacking", typeof(ShipTacking)
               )
               && ValidateReference<ShipManeuverPlanner>(
                   prefabContents, "headingController", typeof(ShipHeadingController)
               )
               && ValidateReference<ShipManeuverPlanner>(
                   prefabContents, "shipTacking", typeof(ShipTacking)
               )
               && ValidateReference<ShipManeuverPlanner>(
                   prefabContents, "shipWearing", typeof(ShipWearing)
               )
               && ValidateReference<ShipDestinationController>(
                   prefabContents, "maneuverPlanner", typeof(ShipManeuverPlanner)
               );
    }


    private static bool ValidateReference<T>(
        GameObject prefabContents,
        string propertyName,
        Type expectedType
    ) where T : Component
    {
        T component = prefabContents.GetComponent<T>();
        Component expectedComponent = prefabContents.GetComponent(expectedType);
        SerializedObject serializedComponent = new SerializedObject(component);
        SerializedProperty property = serializedComponent.FindProperty(propertyName);

        bool isValid = property != null
            && property.objectReferenceValue == expectedComponent;

        if (!isValid)
        {
            Debug.LogError(
                "Generated prefab internal reference is invalid: "
                + typeof(T).Name + "." + propertyName
            );
        }

        return isValid;
    }


    private static void ShowError(string message)
    {
        EditorUtility.DisplayDialog("Rebuild Movement Proxy Prefabs", message, "OK");
        Debug.LogError(message);
    }
}
