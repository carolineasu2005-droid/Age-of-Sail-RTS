using System;
using UnityEditor;
using UnityEngine;

internal static class CombatLifecycleTestUtility
{
    private const string IntegrityProfilePath =
        "Assets/Assets/Game/Data/SO_ShipIntegrity_Foundation.asset";


    public static ShipIntegrity EnsureOperational(GameObject root)
    {
        ShipIntegrity integrity = EnsureInitialized(root);

        if (integrity.LifecycleState
            != ShipCombatLifecycleState.Operational)
        {
            throw new InvalidOperationException(
                "Expected an Operational Ship Integrity fixture."
            );
        }

        return integrity;
    }


    private static ShipIntegrity EnsureInitialized(GameObject root)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        ShipIntegrity integrity = root.GetComponent<ShipIntegrity>();

        if (integrity == null)
        {
            integrity = root.AddComponent<ShipIntegrity>();
        }

        if (integrity.IntegrityProfile == null)
        {
            ShipIntegrityProfile profile =
                AssetDatabase.LoadAssetAtPath<ShipIntegrityProfile>(
                    IntegrityProfilePath
                );

            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Foundation Ship Integrity profile is missing."
                );
            }

            SerializedObject serialized = new SerializedObject(integrity);
            serialized.FindProperty("integrityProfile").objectReferenceValue =
                profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!integrity.TryInitialize())
        {
            throw new InvalidOperationException(
                "Ship Integrity failed to initialize from its profile."
            );
        }

        return integrity;
    }


    public static ShipIntegrity AddOperationalIntegrity(GameObject root)
    {
        return EnsureOperational(root);
    }


    public static ShipIntegrity SetLifecycleThroughIntegrityLoss(
        GameObject root,
        ShipCombatLifecycleState desiredLifecycle
    )
    {
        ShipIntegrity integrity = EnsureInitialized(root);

        if (integrity.LifecycleState == desiredLifecycle)
        {
            return integrity;
        }

        if (desiredLifecycle == ShipCombatLifecycleState.Operational
            || integrity.LifecycleState
                == ShipCombatLifecycleState.Sinking)
        {
            throw new InvalidOperationException(
                "Lifecycle fixtures cannot restore lost Integrity."
            );
        }

        ShipIntegrityProfile profile = integrity.IntegrityProfile;
        float targetNormalized;

        if (desiredLifecycle == ShipCombatLifecycleState.CombatDisabled)
        {
            if (profile.CombatDisabledThresholdNormalized
                <= profile.SinkingThresholdNormalized)
            {
                throw new InvalidOperationException(
                    "Profile has no distinct CombatDisabled interval."
                );
            }

            targetNormalized =
                profile.CombatDisabledThresholdNormalized;
        }
        else if (desiredLifecycle == ShipCombatLifecycleState.Sinking)
        {
            targetNormalized = profile.SinkingThresholdNormalized;
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(desiredLifecycle),
                desiredLifecycle,
                "Unknown Combat lifecycle state."
            );
        }

        float targetIntegrity = integrity.MaximumIntegrity
            * targetNormalized;
        float loss = integrity.CurrentIntegrity - targetIntegrity;

        if (loss <= 0f
            || !integrity.TryApplyIntegrityLoss(loss, out _))
        {
            throw new InvalidOperationException(
                "Authoritative Integrity loss did not reach the requested state."
            );
        }

        if (integrity.LifecycleState != desiredLifecycle)
        {
            throw new InvalidOperationException(
                $"Expected {desiredLifecycle}, reached "
                + $"{integrity.LifecycleState}."
            );
        }

        return integrity;
    }
}
