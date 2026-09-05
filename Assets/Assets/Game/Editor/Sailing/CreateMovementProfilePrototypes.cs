using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CreateMovementProfilePrototypes
{
    private const string ProfileDirectory =
        "Assets/Assets/Game/Data/MovementProfiles";
    private const string MediumPrefabName = "PF_Proxy_Medium_v01";

    [MenuItem("Tools/Sailing/Create Movement Profile Prototypes")]
    private static void CreatePrototypes()
    {
        string mediumPrefabPath = FindMediumPrefabPath();

        if (string.IsNullOrEmpty(mediumPrefabPath))
        {
            return;
        }

        EnsureFolder(ProfileDirectory);

        List<string> createdAssets = new List<string>();
        List<string> skippedAssets = new List<string>();

        ShipMovementProfile lightProfile = GetOrCreateProfile(
            "MovementProfile_Light",
            new ProfileValues(5.5f, 5.5f, 8f, 16f, 32f, 60f, 2.5f, 8f, 9f),
            createdAssets,
            skippedAssets
        );
        ShipMovementProfile mediumProfile = GetOrCreateProfile(
            "MovementProfile_Medium",
            new ProfileValues(5f, 8f, 12f, 20f, 30f, 45f, 3f, 6f, 7f),
            createdAssets,
            skippedAssets
        );
        ShipMovementProfile heavyProfile = GetOrCreateProfile(
            "MovementProfile_Heavy",
            new ProfileValues(4.5f, 11f, 17f, 26f, 28f, 30f, 3.5f, 4.5f, 5.5f),
            createdAssets,
            skippedAssets
        );

        if (lightProfile == null || mediumProfile == null || heavyProfile == null)
        {
            EditorUtility.DisplayDialog(
                "Movement Profile Prototypes",
                "Profile creation could not complete because an existing asset has an unexpected type.",
                "OK"
            );
            return;
        }

        ConfigurePrefabProfile(mediumPrefabPath, mediumProfile);

        string prefabDirectory = Path.GetDirectoryName(mediumPrefabPath)
            ?.Replace('\\', '/');
        CreateProxyPrefab(
            mediumPrefabPath,
            prefabDirectory,
            "PF_Proxy_Light_v01",
            lightProfile,
            createdAssets,
            skippedAssets
        );
        CreateProxyPrefab(
            mediumPrefabPath,
            prefabDirectory,
            "PF_Proxy_Heavy_v01",
            heavyProfile,
            createdAssets,
            skippedAssets
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object profileDirectoryAsset = AssetDatabase.LoadAssetAtPath<Object>(
            ProfileDirectory
        );
        Selection.activeObject = profileDirectoryAsset;
        EditorGUIUtility.PingObject(profileDirectoryAsset);

        Debug.Log(
            "Movement Profile Prototypes complete. Created: "
            + string.Join(", ", createdAssets)
            + ". Skipped existing: "
            + string.Join(", ", skippedAssets)
            + "."
        );
    }


    private static string FindMediumPrefabPath()
    {
        string[] prefabPaths = AssetDatabase.FindAssets(
            MediumPrefabName + " t:Prefab"
        )
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path)
                == MediumPrefabName)
            .ToArray();

        if (prefabPaths.Length == 1)
        {
            return prefabPaths[0];
        }

        string message = prefabPaths.Length == 0
            ? "No prefab named PF_Proxy_Medium_v01 was found."
            : "Multiple prefabs named PF_Proxy_Medium_v01 were found.";
        EditorUtility.DisplayDialog(
            "Movement Profile Prototypes",
            message,
            "OK"
        );
        Debug.LogError(message);
        return string.Empty;
    }


    private static void EnsureFolder(string folderPath)
    {
        string[] folders = folderPath.Split('/');
        string currentPath = folders[0];

        for (int index = 1; index < folders.Length; index++)
        {
            string nextPath = currentPath + "/" + folders[index];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, folders[index]);
            }

            currentPath = nextPath;
        }
    }


    private static ShipMovementProfile GetOrCreateProfile(
        string profileName,
        ProfileValues values,
        List<string> createdAssets,
        List<string> skippedAssets
    )
    {
        string profilePath = ProfileDirectory + "/" + profileName + ".asset";
        Object existingAsset = AssetDatabase.LoadMainAssetAtPath(profilePath);

        if (existingAsset != null)
        {
            ShipMovementProfile existingProfile =
                existingAsset as ShipMovementProfile;

            if (existingProfile == null)
            {
                Debug.LogError(
                    "Existing asset has an unexpected type: " + profilePath
                );
            }
            else
            {
                skippedAssets.Add(profilePath);
            }

            return existingProfile;
        }

        ShipMovementProfile profile =
            ScriptableObject.CreateInstance<ShipMovementProfile>();
        profile.baseMaxSpeed = values.baseMaxSpeed;
        profile.accelerationTimeConstant = values.accelerationTimeConstant;
        profile.naturalDragTimeConstant = values.naturalDragTimeConstant;
        profile.fullTurnDragTimeConstant = values.fullTurnDragTimeConstant;
        profile.maxRudderAngle = values.maxRudderAngle;
        profile.rudderResponse = values.rudderResponse;
        profile.rudderReferenceSpeed = values.rudderReferenceSpeed;
        profile.maxTurnRate = values.maxTurnRate;
        profile.tackYawAssistRate = values.tackYawAssistRate;

        AssetDatabase.CreateAsset(profile, profilePath);
        createdAssets.Add(profilePath);
        return profile;
    }


    private static void ConfigurePrefabProfile(
        string prefabPath,
        ShipMovementProfile movementProfile
    )
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            ShipMovementProfileController controller =
                prefabContents.GetComponent<ShipMovementProfileController>();

            if (controller == null)
            {
                controller = prefabContents.AddComponent<ShipMovementProfileController>();
            }

            AssignMovementProfile(controller, movementProfile);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }


    private static void CreateProxyPrefab(
        string sourcePrefabPath,
        string prefabDirectory,
        string prefabName,
        ShipMovementProfile movementProfile,
        List<string> createdAssets,
        List<string> skippedAssets
    )
    {
        string prefabPath = prefabDirectory + "/" + prefabName + ".prefab";

        if (AssetDatabase.LoadMainAssetAtPath(prefabPath) != null)
        {
            skippedAssets.Add(prefabPath);
            return;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(
            sourcePrefabPath
        );

        try
        {
            prefabContents.name = prefabName;
            ShipMovementProfileController controller =
                prefabContents.GetComponent<ShipMovementProfileController>();

            if (controller == null)
            {
                controller = prefabContents.AddComponent<ShipMovementProfileController>();
            }

            AssignMovementProfile(controller, movementProfile);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            createdAssets.Add(prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }


    private static void AssignMovementProfile(
        ShipMovementProfileController controller,
        ShipMovementProfile movementProfile
    )
    {
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty profileProperty =
            serializedController.FindProperty("movementProfile");
        profileProperty.objectReferenceValue = movementProfile;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }


    private readonly struct ProfileValues
    {
        public readonly float baseMaxSpeed;
        public readonly float accelerationTimeConstant;
        public readonly float naturalDragTimeConstant;
        public readonly float fullTurnDragTimeConstant;
        public readonly float maxRudderAngle;
        public readonly float rudderResponse;
        public readonly float rudderReferenceSpeed;
        public readonly float maxTurnRate;
        public readonly float tackYawAssistRate;

        public ProfileValues(
            float baseMaxSpeed,
            float accelerationTimeConstant,
            float naturalDragTimeConstant,
            float fullTurnDragTimeConstant,
            float maxRudderAngle,
            float rudderResponse,
            float rudderReferenceSpeed,
            float maxTurnRate,
            float tackYawAssistRate
        )
        {
            this.baseMaxSpeed = baseMaxSpeed;
            this.accelerationTimeConstant = accelerationTimeConstant;
            this.naturalDragTimeConstant = naturalDragTimeConstant;
            this.fullTurnDragTimeConstant = fullTurnDragTimeConstant;
            this.maxRudderAngle = maxRudderAngle;
            this.rudderResponse = rudderResponse;
            this.rudderReferenceSpeed = rudderReferenceSpeed;
            this.maxTurnRate = maxTurnRate;
            this.tackYawAssistRate = tackYawAssistRate;
        }
    }
}
