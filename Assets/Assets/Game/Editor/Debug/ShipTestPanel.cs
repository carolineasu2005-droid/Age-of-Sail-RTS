using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class ShipTestPanel : EditorWindow
{
    private const string MenuPath =
        "Tools/RTS Debug/Ship Test Panel";
    private const float ArcLineWidth = 3f;
    private const float TargetLineWidth = 4f;

    private static readonly Color PortArcColor =
        new Color(0.25f, 0.65f, 1f);
    private static readonly Color StarboardArcColor =
        new Color(1f, 0.55f, 0.2f);
    private static readonly Color EffectiveRangeColor =
        new Color(0.3f, 0.9f, 1f);
    private static readonly Color MaximumRangeColor =
        new Color(1f, 0.85f, 0.2f);
    private static readonly Color ClearLineColor =
        new Color(0.25f, 1f, 0.35f);
    private static readonly Color BlockedLineColor =
        new Color(1f, 0.2f, 0.2f);

    [SerializeField]
    private GameObject targetShipRoot;

    [SerializeField]
    private GameObject eligibilityTargetShipRoot;

    [SerializeField]
    private bool targetRelationshipAllowsFire = true;

    private Vector2 scrollPosition;

    private string lastCommandResult;


    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        ShipTestPanel window = GetWindow<ShipTestPanel>();
        window.titleContent = new GUIContent("Ship Test Panel");
        window.Show();
    }


    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawSceneDebug;
    }


    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneDebug;
    }


    private void OnInspectorUpdate()
    {
        Repaint();
        SceneView.RepaintAll();
    }


    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Ship Test Panel",
            EditorStyles.boldLabel
        );

        GameObject selectedShooter =
            (GameObject)EditorGUILayout.ObjectField(
                "Shooter Ship Root",
                targetShipRoot,
                typeof(GameObject),
                true
            );

        if (selectedShooter != targetShipRoot)
        {
            targetShipRoot = selectedShooter;
            lastCommandResult = string.Empty;
            SceneView.RepaintAll();
        }

        GameObject selectedEligibilityTarget =
            (GameObject)EditorGUILayout.ObjectField(
                "Eligibility Target Root",
                eligibilityTargetShipRoot,
                typeof(GameObject),
                true
            );

        if (selectedEligibilityTarget != eligibilityTargetShipRoot)
        {
            eligibilityTargetShipRoot = selectedEligibilityTarget;
            SceneView.RepaintAll();
        }

        bool relationshipAllowsFire = EditorGUILayout.Toggle(
            "Relationship Allows Fire",
            targetRelationshipAllowsFire
        );

        if (relationshipAllowsFire != targetRelationshipAllowsFire)
        {
            targetRelationshipAllowsFire = relationshipAllowsFire;
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();

        using (EditorGUILayout.ScrollViewScope scrollView =
            new EditorGUILayout.ScrollViewScope(scrollPosition))
        {
            scrollPosition = scrollView.scrollPosition;

            if (TryGetCombatState(
                targetShipRoot,
                out ShipCombatState combatState,
                out string stateMessage
            ))
            {
                DrawCombatState(combatState);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    stateMessage,
                    MessageType.Info
                );
            }

            EditorGUILayout.Space();
            DrawFireEligibilitySection();
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


    private void DrawFireEligibilitySection()
    {
        EditorGUILayout.LabelField(
            "Fire Eligibility",
            EditorStyles.boldLabel
        );

        if (!TryEvaluateForDebug(
            targetShipRoot,
            eligibilityTargetShipRoot,
            targetRelationshipAllowsFire,
            out FireEligibilityResult result,
            out string message
        ))
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            BuildEligibilitySummary(eligibilityTargetShipRoot, result),
            result.CanFire ? MessageType.Info : MessageType.Warning
        );
    }


    private void DrawSceneDebug(SceneView sceneView)
    {
        if (sceneView == null
            || Event.current == null
            || Event.current.type != EventType.Repaint
            || targetShipRoot == null)
        {
            return;
        }

        ShipFireEligibility evaluator =
            targetShipRoot.GetComponent<ShipFireEligibility>();

        if (evaluator == null)
        {
            return;
        }

        DrawArcAndRangeVisualization(targetShipRoot.transform, evaluator);

        if (!TryEvaluateForDebug(
            targetShipRoot,
            eligibilityTargetShipRoot,
            targetRelationshipAllowsFire,
            out FireEligibilityResult result,
            out string _
        ))
        {
            return;
        }

        Color previousColor = Handles.color;
        Handles.color = result.Blocked
            ? BlockedLineColor
            : ClearLineColor;
        Vector3 labelPosition = eligibilityTargetShipRoot.transform.position;

        if (result.HasObstructionPath)
        {
            Handles.DrawAAPolyLine(
                TargetLineWidth,
                result.ObstructionOriginWorld,
                result.ObstructionDestinationWorld
            );
            labelPosition = result.ObstructionDestinationWorld;
        }

        Handles.Label(
            labelPosition + Vector3.up * 2f,
            BuildEligibilitySummary(eligibilityTargetShipRoot, result)
        );
        Handles.color = previousColor;
    }


    private static void DrawArcAndRangeVisualization(
        Transform shooterRoot,
        ShipFireEligibility evaluator
    )
    {
        Color previousColor = Handles.color;
        Vector3 center = shooterRoot.position;
        float effectiveRange = evaluator.EffectiveRangeMeters;
        float maximumRange = evaluator.MaximumRangeMeters;

        if (effectiveRange > 0f)
        {
            Handles.color = EffectiveRangeColor;
            Handles.DrawWireDisc(center, Vector3.up, effectiveRange);
        }

        if (maximumRange > 0f)
        {
            Handles.color = MaximumRangeColor;
            Handles.DrawWireDisc(center, Vector3.up, maximumRange);
            DrawBroadsideArc(
                shooterRoot,
                center,
                maximumRange,
                evaluator.ForwardArcLimitDegrees,
                evaluator.AftArcLimitDegrees,
                CombatSide.Port
            );
            DrawBroadsideArc(
                shooterRoot,
                center,
                maximumRange,
                evaluator.ForwardArcLimitDegrees,
                evaluator.AftArcLimitDegrees,
                CombatSide.Starboard
            );
        }

        Handles.color = previousColor;
    }


    private static void DrawBroadsideArc(
        Transform shooterRoot,
        Vector3 center,
        float radius,
        float forwardLimitDegrees,
        float aftLimitDegrees,
        CombatSide side
    )
    {
        bool starboard = side == CombatSide.Starboard;
        Vector3 broadside = starboard
            ? shooterRoot.right
            : -shooterRoot.right;
        float forwardRotation = starboard
            ? -forwardLimitDegrees
            : forwardLimitDegrees;
        float aftRotation = starboard
            ? aftLimitDegrees
            : -aftLimitDegrees;
        Vector3 forwardBoundary = Quaternion.AngleAxis(
            forwardRotation,
            Vector3.up
        ) * broadside;
        Vector3 aftBoundary = Quaternion.AngleAxis(
            aftRotation,
            Vector3.up
        ) * broadside;
        float sweep = forwardLimitDegrees + aftLimitDegrees;

        if (!starboard)
        {
            sweep = -sweep;
        }

        Handles.color = starboard
            ? StarboardArcColor
            : PortArcColor;
        Handles.DrawWireArc(
            center,
            Vector3.up,
            forwardBoundary,
            sweep,
            radius,
            ArcLineWidth
        );
        Handles.DrawAAPolyLine(
            ArcLineWidth,
            center,
            center + forwardBoundary * radius
        );
        Handles.DrawAAPolyLine(
            ArcLineWidth,
            center,
            center + aftBoundary * radius
        );
        Handles.Label(
            center + broadside * radius,
            side.ToString().ToUpperInvariant()
        );
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
            message = "Assign one explicit Shooter Ship Root.";
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


    private static bool TryEvaluateForDebug(
        GameObject shooterRoot,
        GameObject targetRoot,
        bool relationshipAllowsFire,
        out FireEligibilityResult result,
        out string message
    )
    {
        result = default;

        if (shooterRoot == null)
        {
            message = "Assign one explicit Shooter Ship Root.";
            return false;
        }

        if (targetRoot == null)
        {
            message = "Assign one explicit Eligibility Target Root.";
            return false;
        }

        ShipFireEligibility evaluator =
            shooterRoot.GetComponent<ShipFireEligibility>();

        if (evaluator == null)
        {
            message = $"{shooterRoot.name} does not have a "
                + "ShipFireEligibility on the assigned Ship Root.";
            return false;
        }

        Physics.SyncTransforms();

        if (!evaluator.TryEvaluate(
            targetRoot,
            relationshipAllowsFire,
            out result
        ))
        {
            message = "Fire Eligibility could not evaluate the assigned "
                + "shooter/target configuration.";
            return false;
        }

        message = string.Empty;
        return true;
    }


    private static string BuildEligibilitySummary(
        GameObject targetRoot,
        FireEligibilityResult result
    )
    {
        StringBuilder summary = new StringBuilder();
        summary.AppendLine("Fire Eligibility");
        summary.Append("Target: ").AppendLine(targetRoot.name);
        summary.Append("Side: ").AppendLine(
            result.Side.HasValue
                ? result.Side.Value.ToString().ToUpperInvariant()
                : "NONE"
        );
        summary.Append("Arc: ").AppendLine(
            FormatPassFail(result.InBroadsideArc)
        );
        summary.Append("Distance: ")
            .Append(result.DistanceMeters.ToString("F1"))
            .AppendLine(" m");
        summary.Append("Effective: ").AppendLine(
            result.WithinEffectiveRange ? "PASS" : "OUTSIDE (INFO)"
        );
        summary.Append("Maximum: ").AppendLine(
            FormatPassFail(result.WithinMaximumRange)
        );
        summary.Append("Reload: ").AppendLine(
            result.ReloadReady ? "READY" : "NOT READY"
        );
        summary.Append("Target Legal: ").AppendLine(
            FormatYesNo(result.TargetLegal)
        );
        summary.Append("Blocked: ").AppendLine(FormatBlocked(result));
        summary.Append("Lifecycle: ").AppendLine(
            result.LifecycleAllowsFire
                ? "AVAILABLE (BRIDGE)"
                : "BLOCKED"
        );
        summary.Append("CAN FIRE: ").AppendLine(
            FormatYesNo(result.CanFire)
        );

        if (!result.CanFire)
        {
            summary.Append("Reasons: ").Append(
                FormatFailureReasons(result.FailureReasons)
            );
        }

        return summary.ToString();
    }


    private static string FormatFailureReasons(
        FireEligibilityFailure failures
    )
    {
        if (failures == FireEligibilityFailure.None)
        {
            return "NONE";
        }

        StringBuilder reasons = new StringBuilder();
        AppendFailure(
            reasons,
            failures,
            FireEligibilityFailure.TargetIllegal,
            "TARGET_ILLEGAL"
        );
        AppendFailure(
            reasons,
            failures,
            FireEligibilityFailure.NoBroadsideArc,
            "OUT_OF_ARC"
        );
        AppendFailure(
            reasons,
            failures,
            FireEligibilityFailure.BeyondMaximumRange,
            "BEYOND_MAXIMUM_RANGE"
        );
        AppendFailure(
            reasons,
            failures,
            FireEligibilityFailure.BroadsideReloading,
            "RELOADING"
        );
        AppendFailure(
            reasons,
            failures,
            FireEligibilityFailure.LifecycleDisallowsFire,
            "LIFECYCLE_BLOCKED"
        );
        AppendFailure(
            reasons,
            failures,
            FireEligibilityFailure.Obstructed,
            "BLOCKED"
        );
        return reasons.ToString();
    }


    private static void AppendFailure(
        StringBuilder reasons,
        FireEligibilityFailure failures,
        FireEligibilityFailure candidate,
        string label
    )
    {
        if ((failures & candidate) == 0)
        {
            return;
        }

        if (reasons.Length > 0)
        {
            reasons.Append('\n');
        }

        reasons.Append(label);
    }


    private static string FormatBlocked(FireEligibilityResult result)
    {
        if (!result.Blocked)
        {
            return "NO (CLEAR)";
        }

        return result.BlockingObject != null
            ? $"YES ({result.BlockingObject.name})"
            : "YES";
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


    private static string FormatPassFail(bool passed)
    {
        return passed ? "PASS" : "FAIL";
    }


    private static string FormatYesNo(bool value)
    {
        return value ? "YES" : "NO";
    }


    private static string FormatOnOff(bool enabled)
    {
        return enabled ? "ON" : "OFF";
    }
}
