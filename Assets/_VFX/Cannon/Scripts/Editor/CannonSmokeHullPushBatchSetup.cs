using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CannonSmokeHullPushBatchSetup
{
    private const string MenuPath =
        "Tools/VFX/Cannon Smoke/Setup Hull Push On Selected Ship";

    [MenuItem(MenuPath)]
    private static void SetupOnSelectedShip()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection.Length != 1 || !IsSceneSelection(selection[0]))
            return;

        Transform shipRoot = selection[0].transform;
        LingeringSmokeShapeExpansion[] effects =
            shipRoot.GetComponentsInChildren<LingeringSmokeShapeExpansion>(true);

        int configured = 0;
        int alreadyHadHullPush = 0;
        int skipped = 0;
        int warnings = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Cannon Smoke Hull Push");

        if (effects.Length == 0)
        {
            warnings++;
            Debug.LogWarning(
                $"Cannon smoke hull push: no LingeringSmokeShapeExpansion component found under {shipRoot.name}.",
                shipRoot);
        }

        foreach (LingeringSmokeShapeExpansion effect in effects)
        {
            CannonSmokeHullPush[] existingPushes =
                effect.GetComponents<CannonSmokeHullPush>();
            if (existingPushes.Length > 0)
                alreadyHadHullPush++;

            if (existingPushes.Length > 1)
            {
                skipped++;
                warnings++;
                Warn(effect, "multiple CannonSmokeHullPush components; cannot choose one");
                continue;
            }

            if (!TryFindParticleSystem(effect, out ParticleSystem particles,
                    out string particleFailure))
            {
                skipped++;
                warnings++;
                Warn(effect, particleFailure);
                continue;
            }

            Transform muzzleSocket = FindMuzzleSocket(effect.transform, shipRoot);
            if (muzzleSocket == null)
            {
                skipped++;
                warnings++;
                Warn(effect, "no MuzzleSocket ancestor within the selected ship");
                continue;
            }

            if (particles.main.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                warnings++;
                Warn(effect, "ParticleSystem simulation space is not World; hull push will remain inactive");
            }

            SerializedObject effectObject = new SerializedObject(effect);
            SerializedProperty hullPushProperty = effectObject.FindProperty("hullPush");
            if (hullPushProperty == null)
            {
                skipped++;
                warnings++;
                Warn(effect, "LingeringSmokeShapeExpansion.hullPush is missing");
                continue;
            }

            CannonSmokeHullPush hullPush = existingPushes.Length == 1
                ? existingPushes[0]
                : Undo.AddComponent<CannonSmokeHullPush>(effect.gameObject);

            SerializedObject pushObject = new SerializedObject(hullPush);
            SerializedProperty particleProperty =
                pushObject.FindProperty("smokeParticleSystem");
            SerializedProperty rootProperty = pushObject.FindProperty("shipRoot");
            SerializedProperty muzzleProperty = pushObject.FindProperty("muzzleSocket");

            if (particleProperty == null || rootProperty == null
                || muzzleProperty == null)
            {
                if (existingPushes.Length == 0)
                    Undo.DestroyObjectImmediate(hullPush);

                skipped++;
                warnings++;
                Warn(effect, "a CannonSmokeHullPush serialized reference is missing");
                continue;
            }

            bool pushChanged = particleProperty.objectReferenceValue != particles
                || rootProperty.objectReferenceValue != shipRoot
                || muzzleProperty.objectReferenceValue != muzzleSocket;
            if (pushChanged)
            {
                Undo.RecordObject(hullPush, "Wire Cannon Smoke Hull Push");
                particleProperty.objectReferenceValue = particles;
                rootProperty.objectReferenceValue = shipRoot;
                muzzleProperty.objectReferenceValue = muzzleSocket;
                pushObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(hullPush);
            }

            bool effectChanged = hullPushProperty.objectReferenceValue != hullPush;
            if (effectChanged)
            {
                Undo.RecordObject(effect, "Wire Lingering Smoke Hull Push");
                hullPushProperty.objectReferenceValue = hullPush;
                effectObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(effect);
            }

            bool changed = existingPushes.Length == 0 || pushChanged || effectChanged;
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(effect.gameObject.scene);
            }

            configured++;
            Debug.Log(changed
                ? $"Cannon smoke hull push configured: {effect.name}"
                : $"Cannon smoke hull push already configured: {effect.name}", effect);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.DisplayDialog(
            "Cannon Smoke Hull Push Setup",
            $"Found: {effects.Length}\n" +
            $"Successfully configured: {configured}\n" +
            $"Already had CannonSmokeHullPush: {alreadyHadHullPush}\n" +
            $"Skipped: {skipped}\n" +
            $"Warnings: {warnings}",
            "OK");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateSetupOnSelectedShip()
    {
        GameObject[] selection = Selection.gameObjects;
        return selection.Length == 1 && IsSceneSelection(selection[0]);
    }

    private static bool IsSceneSelection(GameObject selected)
    {
        return selected != null
            && selected.scene.IsValid()
            && selected.scene.isLoaded
            && !EditorUtility.IsPersistent(selected)
            && PrefabStageUtility.GetCurrentPrefabStage() == null;
    }

    private static bool TryFindParticleSystem(
        LingeringSmokeShapeExpansion effect,
        out ParticleSystem particles,
        out string failure)
    {
        ParticleSystem[] sameObject = effect.GetComponents<ParticleSystem>();
        if (sameObject.Length == 1)
        {
            particles = sameObject[0];
            failure = null;
            return true;
        }

        if (sameObject.Length > 1)
        {
            particles = null;
            failure = "multiple ParticleSystems on the smoke GameObject";
            return false;
        }

        ParticleSystem[] children =
            effect.GetComponentsInChildren<ParticleSystem>(true);
        if (children.Length == 1)
        {
            particles = children[0];
            failure = null;
            return true;
        }

        particles = null;
        failure = children.Length == 0
            ? "missing ParticleSystem on the smoke GameObject or its children"
            : "multiple child ParticleSystems; cannot choose one";
        return false;
    }

    private static Transform FindMuzzleSocket(Transform current, Transform shipRoot)
    {
        while (current != null)
        {
            if (current.name == "MuzzleSocket")
                return current;

            if (current == shipRoot)
                break;

            current = current.parent;
        }

        return null;
    }

    private static void Warn(LingeringSmokeShapeExpansion effect, string reason)
    {
        Debug.LogWarning($"Cannon smoke hull push skipped/warning: {effect.name}: {reason}.",
            effect);
    }
}
