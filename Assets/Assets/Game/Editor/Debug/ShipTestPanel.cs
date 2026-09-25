using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class ShipTestPanel : EditorWindow
{
    private const string MenuPath =
        "Tools/RTS Debug/Ship Test Panel";
    private const float ArcLineWidth = 3f;
    private const float TargetLineWidth = 4f;
    private const float BlindFireLineWidth = 5f;

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
    private static readonly Color ManualTargetLineColor =
        new Color(1f, 0.25f, 0.85f);
    private static readonly Color AcceptedBlindFireColor =
        new Color(0.2f, 1f, 0.45f);
    private static readonly Color RejectedBlindFireColor =
        new Color(1f, 0.25f, 0.2f);
    [System.Serializable]
    private sealed class AutoTargetDebugCandidate
    {
        public GameObject targetShipRoot;
        public bool relationshipAllowsFire = true;
    }

    [SerializeField]
    private GameObject targetShipRoot;

    [SerializeField]
    private ShipPlayerCommandInput playerCommandInput;

    [SerializeField]
    private GameObject eligibilityTargetShipRoot;

    [SerializeField]
    private bool targetRelationshipAllowsFire = true;

    [SerializeField]
    private bool useFixedTargetedFireSeed = true;

    [SerializeField]
    private uint targetedFireSeed = 12345u;

    [SerializeField]
    private List<AutoTargetDebugCandidate> autoTargetCandidates =
        new List<AutoTargetDebugCandidate>();

    private Vector2 scrollPosition;

    private string lastCommandResult;

    private bool hasTargetedFireResult;
    private TargetedFireExecutionResult lastTargetedFireResult;


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

        playerCommandInput =
            (ShipPlayerCommandInput)EditorGUILayout.ObjectField(
                "Player Command Input",
                playerCommandInput,
                typeof(ShipPlayerCommandInput),
                true
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
            DrawBlindFireSection();

            EditorGUILayout.Space();
            DrawAutoTargetSection();

            EditorGUILayout.Space();
            DrawFireEligibilitySection();

            EditorGUILayout.Space();
            DrawTargetedFireSection();
        }
    }


    private void DrawBlindFireSection()
    {
        EditorGUILayout.LabelField(
            "BLIND FIRE",
            EditorStyles.boldLabel
        );

        if (playerCommandInput == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the scene's ShipPlayerCommandInput. No temporary "
                    + "keyboard binding is registered for Phase 4.",
                MessageType.Info
            );
            return;
        }

        GameObject selectedShooter =
            playerCommandInput.SelectedBlindFireShooterRoot;
        EditorGUILayout.LabelField(
            "Selected Shooter",
            selectedShooter != null ? selectedShooter.name : "None"
        );
        EditorGUILayout.LabelField(
            "Debug Shooter",
            targetShipRoot != null ? targetShipRoot.name : "None"
        );
        EditorGUILayout.LabelField(
            "Armed",
            FormatYesNo(playerCommandInput.BlindFireArmed)
        );

        if (playerCommandInput.BlindFireArmed)
        {
            if (GUILayout.Button("Cancel Blind Fire"))
            {
                playerCommandInput.CancelBlindFire();
                lastCommandResult = "Blind Fire arming cancelled.";
            }
        }
        else if (GUILayout.Button("Arm Blind Fire (Next Right Click)"))
        {
            if (playerCommandInput.TryArmBlindFire())
            {
                targetShipRoot =
                    playerCommandInput.SelectedBlindFireShooterRoot;
                lastCommandResult = "Blind Fire armed for one valid "
                    + "world/sea right click.";
            }
            else
            {
                lastCommandResult = "Blind Fire could not arm: select "
                    + "exactly one active Combat ship.";
            }

            SceneView.RepaintAll();
        }

        BlindFirePlayerCommandResult result =
            playerCommandInput.LastBlindFireCommandResult;

        if (!result.Attempted)
        {
            EditorGUILayout.LabelField("Last Attempt", "None");
            return;
        }

        EditorGUILayout.HelpBox(
            BuildBlindFireSummary(result),
            result.Accepted ? MessageType.Info : MessageType.Warning
        );
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


    private void DrawTargetedFireSection()
    {
        EditorGUILayout.LabelField(
            "Targeted Physical Broadside",
            EditorStyles.boldLabel
        );
        EditorGUILayout.LabelField(
            "Shooter",
            targetShipRoot != null ? targetShipRoot.name : "None"
        );
        EditorGUILayout.LabelField(
            "Target",
            eligibilityTargetShipRoot != null
                ? eligibilityTargetShipRoot.name
                : "None"
        );

        useFixedTargetedFireSeed = EditorGUILayout.Toggle(
            "Use Fixed Seed",
            useFixedTargetedFireSeed
        );

        if (useFixedTargetedFireSeed)
        {
            long enteredSeed = EditorGUILayout.LongField(
                "Broadside Seed",
                targetedFireSeed
            );
            targetedFireSeed = enteredSeed <= 0L
                ? 0u
                : enteredSeed >= uint.MaxValue
                    ? uint.MaxValue
                    : (uint)enteredSeed;
        }

        if (GUILayout.Button("Execute Targeted Broadside"))
        {
            ExecuteTargetedBroadsideForDebug();
        }

        if (!hasTargetedFireResult)
        {
            EditorGUILayout.LabelField("Last Execution", "None");
            return;
        }

        EditorGUILayout.HelpBox(
            BuildTargetedFireSummary(
                lastTargetedFireResult,
                targetShipRoot != null
                    ? targetShipRoot.GetComponent<
                        ShipBroadsideFireExecutor
                    >()
                    : null
            ),
            lastTargetedFireResult.Accepted
                ? MessageType.Info
                : MessageType.Warning
        );
    }


    private void ExecuteTargetedBroadsideForDebug()
    {
        hasTargetedFireResult = true;
        ShipFireEligibility eligibility = targetShipRoot != null
            ? targetShipRoot.GetComponent<ShipFireEligibility>()
            : null;
        ShipBroadsideFireExecutor executor = targetShipRoot != null
            ? targetShipRoot.GetComponent<ShipBroadsideFireExecutor>()
            : null;
        ShipTargetedFireCommand command = new ShipTargetedFireCommand(
            eligibility,
            executor
        );

        Physics.SyncTransforms();

        if (useFixedTargetedFireSeed)
        {
            command.TryExecute(
                eligibilityTargetShipRoot,
                targetRelationshipAllowsFire,
                targetedFireSeed,
                out lastTargetedFireResult
            );
        }
        else
        {
            command.TryExecuteWithRuntimeSeed(
                eligibilityTargetShipRoot,
                targetRelationshipAllowsFire,
                out lastTargetedFireResult
            );
        }

        SceneView.RepaintAll();
    }


    private void DrawAutoTargetSection()
    {
        EditorGUILayout.LabelField(
            "Auto Target Selection",
            EditorStyles.boldLabel
        );

        DrawAutoTargetCandidateInputs();

        IReadOnlyList<AutoTargetCandidate> candidates =
            BuildAutoTargetCandidates();
        EditorGUILayout.LabelField(
            "Candidate Count",
            candidates.Count.ToString()
        );

        if (!TrySelectAutoTargetForDebug(
            targetShipRoot,
            candidates,
            out AutoTargetSelectionResult selection,
            out string message
        ))
        {
            DrawAutoTargetSideSelection(
                CombatSide.Port,
                default
            );
            EditorGUILayout.Space();
            DrawAutoTargetSideSelection(
                CombatSide.Starboard,
                default
            );
            EditorGUILayout.HelpBox(message, MessageType.Info);
            return;
        }

        DrawAutoTargetSideSelection(
            CombatSide.Port,
            selection.PortSelection
        );
        EditorGUILayout.Space();
        DrawAutoTargetSideSelection(
            CombatSide.Starboard,
            selection.StarboardSelection
        );
    }


    private void DrawAutoTargetCandidateInputs()
    {
        if (autoTargetCandidates == null)
        {
            autoTargetCandidates = new List<AutoTargetDebugCandidate>();
        }

        int removeIndex = -1;
        int moveFromIndex = -1;
        int moveToIndex = -1;

        for (int index = 0; index < autoTargetCandidates.Count; index++)
        {
            AutoTargetDebugCandidate candidate =
                autoTargetCandidates[index];

            if (candidate == null)
            {
                candidate = new AutoTargetDebugCandidate();
                autoTargetCandidates[index] = candidate;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Candidate {index + 1}");

            EditorGUI.BeginDisabledGroup(index == 0);

            if (GUILayout.Button("Up", GUILayout.Width(45f)))
            {
                moveFromIndex = index;
                moveToIndex = index - 1;
            }

            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(
                index == autoTargetCandidates.Count - 1
            );

            if (GUILayout.Button("Down", GUILayout.Width(50f)))
            {
                moveFromIndex = index;
                moveToIndex = index + 1;
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                removeIndex = index;
            }

            EditorGUILayout.EndHorizontal();
            candidate.targetShipRoot =
                (GameObject)EditorGUILayout.ObjectField(
                    "Ship Root",
                    candidate.targetShipRoot,
                    typeof(GameObject),
                    true
                );
            candidate.relationshipAllowsFire = EditorGUILayout.Toggle(
                "Relationship Allows Fire",
                candidate.relationshipAllowsFire
            );
            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
        {
            autoTargetCandidates.RemoveAt(removeIndex);
            SceneView.RepaintAll();
        }
        else if (moveFromIndex >= 0 && moveToIndex >= 0)
        {
            if (TryMoveAutoTargetCandidate(
                autoTargetCandidates,
                moveFromIndex,
                moveToIndex
            ))
            {
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("Add Candidate"))
        {
            autoTargetCandidates.Add(new AutoTargetDebugCandidate());
        }
    }


    private static bool TryMoveAutoTargetCandidate(
        List<AutoTargetDebugCandidate> candidates,
        int fromIndex,
        int toIndex
    )
    {
        if (candidates == null
            || fromIndex < 0
            || fromIndex >= candidates.Count
            || toIndex < 0
            || toIndex >= candidates.Count
            || fromIndex == toIndex)
        {
            return false;
        }

        AutoTargetDebugCandidate movedCandidate = candidates[fromIndex];
        candidates.RemoveAt(fromIndex);
        candidates.Insert(toIndex, movedCandidate);
        return true;
    }


    private static void DrawAutoTargetSideSelection(
        CombatSide side,
        AutoTargetSideSelectionResult selection
    )
    {
        string sideName = side.ToString().ToUpperInvariant();

        EditorGUILayout.LabelField(
            $"AUTO TARGET — {sideName}",
            EditorStyles.boldLabel
        );

        if (!selection.HasTarget)
        {
            EditorGUILayout.LabelField("Target", "None");
            EditorGUILayout.LabelField("Side", sideName);
            return;
        }

        EditorGUILayout.LabelField(
            "Target",
            selection.TargetShipRoot.name
        );
        EditorGUILayout.LabelField(
            "Side",
            selection.FireEligibility.Side.HasValue
                ? selection.FireEligibility.Side.Value
                    .ToString()
                    .ToUpperInvariant()
                : "NONE"
        );
        EditorGUILayout.LabelField(
            "Exposure E",
            selection.Score.ExposureNormalized.ToString("F3")
        );
        EditorGUILayout.LabelField(
            "Range Quality R",
            selection.Score.RangeQualityNormalized.ToString("F3")
        );
        EditorGUILayout.LabelField(
            "Visibility V",
            selection.Score.VisibilityQualityNormalized.ToString("F3")
        );
        EditorGUILayout.LabelField(
            "Final Score (E x R x V)",
            selection.Score.FinalScore.ToString("F3")
        );
        EditorGUILayout.HelpBox(
            BuildEligibilitySummary(
                selection.TargetShipRoot,
                selection.FireEligibility
            ),
            MessageType.Info
        );
    }


    private void DrawSceneDebug(SceneView sceneView)
    {
        if (sceneView == null
            || Event.current == null
            || Event.current.type != EventType.Repaint)
        {
            return;
        }

        DrawBlindFireVisualization();

        if (targetShipRoot == null)
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
        DrawTargetOwnershipLines();

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


    private void DrawBlindFireVisualization()
    {
        if (playerCommandInput == null)
        {
            return;
        }

        BlindFirePlayerCommandResult result =
            playerCommandInput.LastBlindFireCommandResult;

        if (!result.Attempted || result.ShooterShipRoot == null)
        {
            return;
        }

        Color previousColor = Handles.color;
        Handles.color = result.Accepted
            ? AcceptedBlindFireColor
            : RejectedBlindFireColor;
        Handles.DrawAAPolyLine(
            BlindFireLineWidth,
            result.ShooterShipRoot.transform.position,
            result.WorldAimPoint
        );
        Handles.Label(
            result.WorldAimPoint + Vector3.up * 2f,
            BuildBlindFireSummary(result)
        );
        Handles.color = previousColor;
    }


    private void DrawTargetOwnershipLines()
    {
        if (!TryGetCombatState(
            targetShipRoot,
            out ShipCombatState combatState,
            out string _
        ))
        {
            return;
        }

        if (combatState.ManualTarget != null)
        {
            DrawTargetOwnershipLine(
                targetShipRoot.transform.position,
                combatState.ManualTarget,
                ManualTargetLineColor,
                "MANUAL TARGET"
            );
        }

        if (TrySelectAutoTargetForDebug(
            targetShipRoot,
            BuildAutoTargetCandidates(),
            out AutoTargetSelectionResult selection,
            out string _
        ))
        {
            if (selection.PortSelection.HasTarget)
            {
                DrawTargetOwnershipLine(
                    targetShipRoot.transform.position,
                    selection.PortSelection.TargetShipRoot,
                    PortArcColor,
                    "AUTO PORT TARGET"
                );
            }

            if (selection.StarboardSelection.HasTarget)
            {
                DrawTargetOwnershipLine(
                    targetShipRoot.transform.position,
                    selection.StarboardSelection.TargetShipRoot,
                    StarboardArcColor,
                    "AUTO STARBOARD TARGET"
                );
            }
        }
    }


    private static void DrawTargetOwnershipLine(
        Vector3 shooterPosition,
        GameObject targetRoot,
        Color color,
        string label
    )
    {
        if (targetRoot == null)
        {
            return;
        }

        Color previousColor = Handles.color;
        Handles.color = color;
        Handles.DrawAAPolyLine(
            TargetLineWidth,
            shooterPosition,
            targetRoot.transform.position
        );
        Handles.Label(
            targetRoot.transform.position + Vector3.up * 3f,
            label
        );
        Handles.color = previousColor;
    }


    private IReadOnlyList<AutoTargetCandidate> BuildAutoTargetCandidates()
    {
        if (autoTargetCandidates == null)
        {
            return new AutoTargetCandidate[0];
        }

        List<AutoTargetCandidate> candidates =
            new List<AutoTargetCandidate>(autoTargetCandidates.Count);

        foreach (AutoTargetDebugCandidate candidate in autoTargetCandidates)
        {
            if (candidate != null && candidate.targetShipRoot != null)
            {
                candidates.Add(new AutoTargetCandidate(
                    candidate.targetShipRoot,
                    candidate.relationshipAllowsFire
                ));
            }
        }

        return candidates;
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


    private static bool TrySelectAutoTargetForDebug(
        GameObject shooterRoot,
        IReadOnlyList<AutoTargetCandidate> candidates,
        out AutoTargetSelectionResult result,
        out string message
    )
    {
        result = default;

        if (!TryGetCombatState(
            shooterRoot,
            out ShipCombatState combatState,
            out message
        ))
        {
            return false;
        }

        if (!combatState.AutoFireEnabled)
        {
            message = "Auto Fire is OFF.";
            return false;
        }

        if (combatState.ManualTarget != null)
        {
            message = "Manual Target owns targeting; Auto Target is inactive.";
            return false;
        }

        if (candidates == null || candidates.Count == 0)
        {
            message = "Supply at least one explicit Auto Target candidate.";
            return false;
        }

        Physics.SyncTransforms();

        if (!ShipAutoTargetSelector.TrySelect(
            shooterRoot,
            candidates,
            out result
        ))
        {
            message = "No supplied candidate is currently legal/selectable.";
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


    private static string BuildTargetedFireSummary(
        TargetedFireExecutionResult result,
        ShipBroadsideFireExecutor executor
    )
    {
        StringBuilder summary = new StringBuilder();
        summary.AppendLine("Targeted Broadside Execution");
        summary.Append("Accepted: ")
            .AppendLine(FormatYesNo(result.Accepted));
        summary.Append("Side: ").AppendLine(
            result.BroadsideExecution.Side.HasValue
                ? result.BroadsideExecution.Side.Value
                    .ToString()
                    .ToUpperInvariant()
                : "NONE"
        );
        summary.Append("Seed: ")
            .AppendLine(
                result.BroadsideExecution.BroadsideSeed.ToString()
            );
        summary.Append("Shot Samples: ")
            .AppendLine(
                result.BroadsideExecution.ShotCount.ToString()
            );
        summary.Append("Projectiles Spawned: ")
            .AppendLine(
                result.BroadsideExecution.SpawnedProjectileCount
                    .ToString()
            );
        summary.Append("Nominal Range: ")
            .Append(
                result.BroadsideExecution.AimBasis
                    .AimDistanceMeters
                    .ToString("F1")
            )
            .AppendLine(" m");

        DispersionEllipse ellipse = result.BroadsideExecution
            .SamplingResult
            .DispersionEllipse;
        summary.Append("Dispersion Semi-Axes H/V: ")
            .Append(ellipse.HorizontalSemiAxisMeters.ToString("F2"))
            .Append(" / ")
            .Append(ellipse.VerticalSemiAxisMeters.ToString("F2"))
            .AppendLine(" m");

        if (executor != null)
        {
            summary.Append("Last Outcomes H/W/E: ")
                .Append(executor.LastHullHitCount)
                .Append(" / ")
                .Append(executor.LastWaterMissCount)
                .Append(" / ")
                .AppendLine(executor.LastExpiredCount.ToString());
        }

        if (!result.Accepted)
        {
            summary.Append("Command Failure: ")
                .AppendLine(
                    result.FailureReasons.ToString().ToUpperInvariant()
                );
            summary.Append("Aim Failure: ")
                .AppendLine(
                    result.AimBasisFailure.ToString().ToUpperInvariant()
                );
            summary.Append("Execution Failure: ")
                .AppendLine(
                    result.BroadsideExecution.FailureReasons
                        .ToString()
                        .ToUpperInvariant()
                );
            summary.Append("Sampling Failure: ")
                .Append(
                    result.BroadsideExecution.SamplingFailure
                        .ToString()
                        .ToUpperInvariant()
                );
        }

        return summary.ToString();
    }


    private static string BuildBlindFireSummary(
        BlindFirePlayerCommandResult command
    )
    {
        StringBuilder summary = new StringBuilder();
        summary.AppendLine("Blind Fire");
        summary.Append("Shooter: ").AppendLine(
            command.ShooterShipRoot != null
                ? command.ShooterShipRoot.name
                : "NONE"
        );
        summary.Append("Requested Point: ")
            .AppendLine(command.WorldAimPoint.ToString("F1"));

        if (!command.EligibilityAvailable)
        {
            summary.AppendLine("Eligibility: UNAVAILABLE");
            summary.Append("Accepted: NO\nReasons: ")
                .Append(FormatPlayerCommandFailures(
                    command.FailureReasons
                ));
            return summary.ToString();
        }

        BlindFireEligibilityResult result = command.Eligibility;
        summary.Append("Resolved Direction: ")
            .AppendLine(result.Aim.WorldAimDirection.ToString("F3"));
        summary.Append("Side: ").AppendLine(
            result.Side.HasValue
                ? result.Side.Value.ToString().ToUpperInvariant()
                : "NONE"
        );
        summary.Append("Arc: ").AppendLine(
            FormatPassFail(result.InBroadsideArc)
        );

        if (result.Aim.AimPointDistanceMeters.HasValue)
        {
            summary.Append("Distance: ")
                .Append(result.Aim.AimPointDistanceMeters.Value.ToString(
                    "F1"
                ))
                .AppendLine(" m");
        }

        summary.Append("Maximum: ").AppendLine(
            result.WithinMaximumRange.HasValue
                ? FormatPassFail(result.WithinMaximumRange.Value)
                : "N/A"
        );
        summary.Append("Reload: ").AppendLine(
            result.ReloadReady ? "READY" : "NOT READY"
        );
        summary.Append("Lifecycle: ").AppendLine(
            result.LifecycleAllowsFire
                ? "AVAILABLE (BRIDGE)"
                : "BLOCKED"
        );
        summary.Append("CAN BLIND FIRE: ").AppendLine(
            FormatYesNo(result.CanBlindFire)
        );
        summary.Append("Execution: ").AppendLine(
            command.Accepted ? "ACCEPTED" : "REJECTED"
        );

        if (!command.Accepted)
        {
            summary.Append("Reasons: ")
                .Append(FormatBlindFireFailures(command));
        }

        return summary.ToString();
    }


    private static string FormatBlindFireFailures(
        BlindFirePlayerCommandResult command
    )
    {
        StringBuilder reasons = new StringBuilder();
        AppendBlindFireEligibilityFailures(
            reasons,
            command.Eligibility.FailureReasons
        );
        AppendBlindFireExecutionFailures(
            reasons,
            command.Execution.FailureReasons
        );

        if (reasons.Length == 0)
        {
            reasons.Append(FormatPlayerCommandFailures(
                command.FailureReasons
            ));
        }

        return reasons.ToString();
    }


    private static void AppendBlindFireEligibilityFailures(
        StringBuilder reasons,
        BlindFireEligibilityFailure failures
    )
    {
        AppendFailure(reasons, failures,
            BlindFireEligibilityFailure.InvalidAim, "INVALID_AIM");
        AppendFailure(reasons, failures,
            BlindFireEligibilityFailure.NoBroadsideArc, "OUT_OF_ARC");
        AppendFailure(reasons, failures,
            BlindFireEligibilityFailure.BeyondMaximumRange,
            "BEYOND_MAXIMUM_RANGE");
        AppendFailure(reasons, failures,
            BlindFireEligibilityFailure.BroadsideReloading, "RELOADING");
        AppendFailure(reasons, failures,
            BlindFireEligibilityFailure.LifecycleDisallowsFire,
            "LIFECYCLE_BLOCKED");
    }


    private static void AppendBlindFireExecutionFailures(
        StringBuilder reasons,
        BlindFireExecutionFailure failures
    )
    {
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.InvalidAim, "INVALID_AIM");
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.EligibilityUnavailable,
            "ELIGIBILITY_UNAVAILABLE");
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.EligibilityRejected,
            "ELIGIBILITY_REJECTED");
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.BroadsideCommitFailed,
            "COMMIT_FAILED");
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.FiniteAimPointRequired,
            "FINITE_AIM_POINT_REQUIRED");
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.AimBasisUnavailable,
            "AIM_BASIS_UNAVAILABLE");
        AppendFailure(reasons, failures,
            BlindFireExecutionFailure.BroadsideExecutionRejected,
            "BROADSIDE_EXECUTION_REJECTED");
    }


    private static string FormatPlayerCommandFailures(
        BlindFirePlayerCommandFailure failures
    )
    {
        return failures == BlindFirePlayerCommandFailure.None
            ? "NONE"
            : failures.ToString().ToUpperInvariant();
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


    private static void AppendFailure(
        StringBuilder reasons,
        BlindFireEligibilityFailure failures,
        BlindFireEligibilityFailure candidate,
        string label
    )
    {
        if ((failures & candidate) == 0)
        {
            return;
        }

        AppendFailureLabel(reasons, label);
    }


    private static void AppendFailure(
        StringBuilder reasons,
        BlindFireExecutionFailure failures,
        BlindFireExecutionFailure candidate,
        string label
    )
    {
        if ((failures & candidate) == 0)
        {
            return;
        }

        AppendFailureLabel(reasons, label);
    }


    private static void AppendFailureLabel(
        StringBuilder reasons,
        string label
    )
    {
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
