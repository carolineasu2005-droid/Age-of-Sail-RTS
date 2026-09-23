using UnityEditor;
using UnityEngine;

public sealed class ShipTestPanel : EditorWindow
{
    private const string MenuPath =
        "Tools/RTS Debug/Ship Test Panel";

    [SerializeField]
    private GameObject targetShipRoot;

    private Vector2 scrollPosition;

    private string lastCommandResult;


    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        ShipTestPanel window = GetWindow<ShipTestPanel>();
        window.titleContent = new GUIContent("Ship Test Panel");
        window.Show();
    }


    private void OnInspectorUpdate()
    {
        Repaint();
    }


    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Ship Test Panel",
            EditorStyles.boldLabel
        );

        GameObject selectedTarget = (GameObject)EditorGUILayout.ObjectField(
            "Target Ship Root",
            targetShipRoot,
            typeof(GameObject),
            true
        );

        if (selectedTarget != targetShipRoot)
        {
            targetShipRoot = selectedTarget;
            lastCommandResult = string.Empty;
        }

        EditorGUILayout.Space();

        if (!TryGetCombatState(
            targetShipRoot,
            out ShipCombatState combatState,
            out string message
        ))
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
            return;
        }

        using (EditorGUILayout.ScrollViewScope scrollView =
            new EditorGUILayout.ScrollViewScope(scrollPosition))
        {
            scrollPosition = scrollView.scrollPosition;
            DrawCombatState(combatState);
        }
    }


    private void DrawCombatState(ShipCombatState combatState)
    {
        EditorGUILayout.LabelField(
            "Combat State",
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "Auto Fire",
            FormatOnOff(combatState.AutoFireEnabled)
        );

        if (GUILayout.Button("Toggle Auto Fire"))
        {
            ToggleAutoFire(combatState);
            lastCommandResult = $"Auto Fire is now "
                + $"{FormatOnOff(combatState.AutoFireEnabled)}.";
        }

        string manualTargetName = combatState.ManualTarget != null
            ? combatState.ManualTarget.name
            : "None";
        EditorGUILayout.LabelField("Manual Target", manualTargetName);

        EditorGUILayout.LabelField(
            "Blind Fire",
            FormatOnOff(combatState.BlindFireEnabled)
        );

        if (GUILayout.Button("Toggle Blind Fire"))
        {
            ToggleBlindFire(combatState);
            lastCommandResult = $"Blind Fire is now "
                + $"{FormatOnOff(combatState.BlindFireEnabled)}.";
        }

        EditorGUILayout.Space();
        DrawBroadside(
            combatState,
            CombatSide.Port,
            combatState.PortBroadsideState,
            combatState.PortReloadRemainingSeconds
        );

        EditorGUILayout.Space();
        DrawBroadside(
            combatState,
            CombatSide.Starboard,
            combatState.StarboardBroadsideState,
            combatState.StarboardReloadRemainingSeconds
        );

        if (!string.IsNullOrEmpty(lastCommandResult))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                lastCommandResult,
                MessageType.None
            );
        }
    }


    private void DrawBroadside(
        ShipCombatState combatState,
        CombatSide side,
        BroadsideReloadState state,
        float reloadRemainingSeconds
    )
    {
        string sideName = side.ToString();

        EditorGUILayout.LabelField(
            $"{sideName} Broadside",
            EditorStyles.boldLabel
        );
        EditorGUILayout.LabelField(
            "State",
            state.ToString().ToUpperInvariant()
        );

        if (state == BroadsideReloadState.Reloading)
        {
            EditorGUILayout.LabelField(
                "Remaining",
                $"{reloadRemainingSeconds:F2} s"
            );
        }

        if (GUILayout.Button($"Debug Fire {sideName}"))
        {
            bool committed = TryDebugFire(combatState, side);
            lastCommandResult = committed
                ? $"{sideName} readiness consumed; reload started."
                : $"{sideName} was not consumed; it may already be "
                    + "reloading.";
        }
    }


    private static bool TryGetCombatState(
        GameObject target,
        out ShipCombatState combatState,
        out string message
    )
    {
        combatState = null;

        if (target == null)
        {
            message = "Assign one explicit Target Ship Root.";
            return false;
        }

        combatState = target.GetComponent<ShipCombatState>();

        if (combatState == null)
        {
            message = $"{target.name} does not have a ShipCombatState on "
                + "the assigned Ship Root.";
            return false;
        }

        message = string.Empty;
        return true;
    }


    private static void ToggleAutoFire(ShipCombatState combatState)
    {
        combatState.SetAutoFireEnabled(!combatState.AutoFireEnabled);
    }


    private static void ToggleBlindFire(ShipCombatState combatState)
    {
        combatState.SetBlindFireEnabled(!combatState.BlindFireEnabled);
    }


    private static bool TryDebugFire(
        ShipCombatState combatState,
        CombatSide side
    )
    {
        return combatState.TryCommitBroadsideFire(side);
    }


    private static string FormatOnOff(bool enabled)
    {
        return enabled ? "ON" : "OFF";
    }
}
