using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShipAutoTargetSelectorTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdRoots = new List<GameObject>();
    private GameObject shooterRoot;
    private GameObject starboardTarget;
    private GameObject portTarget;
    private ShipCombatState shooterState;
    private ShipFireEligibility eligibility;
    private AutoTargetScoringProfile scoringProfile;


    [SetUp]
    public void SetUp()
    {
        shooterRoot = CreateShip("Shooter Root", true);
        starboardTarget = CreateShip("Starboard Target", true);
        portTarget = CreateShip("Port Target", true);
        starboardTarget.transform.position = Vector3.right * 50f;
        portTarget.transform.position = Vector3.left * 150f;
        shooterState = shooterRoot.GetComponent<ShipCombatState>();
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        scoringProfile = ScriptableObject.CreateInstance<
            AutoTargetScoringProfile
        >();
        ShipAutoTargetScoringConfiguration scoringConfiguration =
            shooterRoot.AddComponent<
                ShipAutoTargetScoringConfiguration
            >();
        SetPrivateField(
            scoringConfiguration,
            "scoringProfile",
            scoringProfile
        );
        SetPrivateField(eligibility, "effectiveRangeMeters", 100f);
        SetPrivateField(eligibility, "maximumRangeMeters", 200f);
    }


    [TearDown]
    public void TearDown()
    {
        for (int index = createdRoots.Count - 1; index >= 0; index--)
        {
            Object.DestroyImmediate(createdRoots[index]);
        }

        createdRoots.Clear();
        Object.DestroyImmediate(scoringProfile);
    }


    [Test]
    public void AutoFireOff_ReturnsNoTargetForEitherSide()
    {
        bool selected = TrySelect(
            new[] { Candidate(portTarget), Candidate(starboardTarget) },
            out AutoTargetSelectionResult result
        );

        Assert.That(selected, Is.False);
        AssertNoSelection(result);
    }


    [Test]
    public void ManualTargetPresent_ReturnsNoAutoTargetForEitherSide()
    {
        shooterState.SetAutoFireEnabled(true);
        bool assigned = shooterState.AssignManualTarget(starboardTarget);

        bool selected = TrySelect(
            new[] { Candidate(portTarget), Candidate(starboardTarget) },
            out AutoTargetSelectionResult result
        );

        Assert.That(assigned, Is.True);
        Assert.That(shooterState.ManualTarget, Is.SameAs(starboardTarget));
        Assert.That(shooterState.AutoFireEnabled, Is.False);
        Assert.That(selected, Is.False);
        AssertNoSelection(result);
    }


    [Test]
    public void ZeroLegalCandidates_ReturnsNoTargetForEitherSide()
    {
        EnableAutoFire();
        starboardTarget.transform.position = Vector3.forward * 50f;
        portTarget.transform.position = Vector3.back * 50f;

        bool selected = TrySelect(
            new[] { Candidate(starboardTarget), Candidate(portTarget) },
            out AutoTargetSelectionResult result
        );

        Assert.That(selected, Is.False);
        AssertNoSelection(result);
    }


    [Test]
    public void OneLegalPortCandidate_SelectsPortOnly()
    {
        EnableAutoFire();

        AutoTargetSelectionResult result = Select(Candidate(portTarget));

        AssertSelection(result.PortSelection, portTarget, 0, CombatSide.Port);
        AssertNoSideSelection(result.StarboardSelection);
    }


    [Test]
    public void OneLegalStarboardCandidate_SelectsStarboardOnly()
    {
        EnableAutoFire();

        AutoTargetSelectionResult result = Select(Candidate(starboardTarget));

        AssertNoSideSelection(result.PortSelection);
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            0,
            CombatSide.Starboard
        );
    }


    [Test]
    public void OneLegalCandidateOnEachSide_SelectsBothSimultaneously()
    {
        EnableAutoFire();

        AutoTargetSelectionResult result = Select(
            Candidate(portTarget),
            Candidate(starboardTarget)
        );

        AssertSelection(result.PortSelection, portTarget, 0, CombatSide.Port);
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void TwoPortCandidatesAndOneStarboard_SelectBestPortAndStarboard()
    {
        EnableAutoFire();
        GameObject weakerPort = CreateShip("Weaker Port", true);
        weakerPort.transform.position = Vector3.left * 50f;
        weakerPort.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        AutoTargetSelectionResult result = Select(
            Candidate(weakerPort),
            Candidate(starboardTarget),
            Candidate(portTarget)
        );

        AssertSelection(result.PortSelection, portTarget, 2, CombatSide.Port);
        Assert.That(
            result.PortSelection.Score.FinalScore,
            Is.EqualTo(0.5f).Within(0.0001f)
        );
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void SameRangeExposureCompetition_SelectsBroadsideAndKeepsStarboard()
    {
        EnableAutoFire();
        portTarget.transform.position = Vector3.left * 75f;
        portTarget.transform.rotation = Quaternion.identity;
        GameObject bowOnPort = CreateShip("Bow-On Port", true);
        bowOnPort.transform.position = Vector3.left * 75f;
        bowOnPort.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        AutoTargetSelectionResult result = Select(
            Candidate(bowOnPort),
            Candidate(starboardTarget),
            Candidate(portTarget)
        );

        AssertSelection(result.PortSelection, portTarget, 2, CombatSide.Port);
        Assert.That(
            result.PortSelection.Score.ExposureNormalized,
            Is.EqualTo(1f).Within(0.0001f)
        );
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void PortRangeCompetitionSwitch_DoesNotChangeStarboardSelection()
    {
        EnableAutoFire();
        portTarget.transform.position = Vector3.left * 75f;
        GameObject secondPort = CreateShip("Second Port", true);
        secondPort.transform.position = Vector3.left * 150f;
        AutoTargetSelectionResult initial = Select(
            Candidate(portTarget),
            Candidate(secondPort),
            Candidate(starboardTarget)
        );

        portTarget.transform.position = Vector3.left * 190f;
        AutoTargetSelectionResult changed = Select(
            Candidate(portTarget),
            Candidate(secondPort),
            Candidate(starboardTarget)
        );

        AssertSelection(initial.PortSelection, portTarget, 0, CombatSide.Port);
        AssertSelection(changed.PortSelection, secondPort, 1, CombatSide.Port);
        Assert.That(
            initial.StarboardSelection.TargetShipRoot,
            Is.SameAs(starboardTarget)
        );
        AssertSelection(
            changed.StarboardSelection,
            starboardTarget,
            2,
            CombatSide.Starboard
        );
    }


    [Test]
    public void StarboardRangeCompetitionSwitch_DoesNotChangePortSelection()
    {
        EnableAutoFire();
        starboardTarget.transform.position = Vector3.right * 75f;
        GameObject secondStarboard = CreateShip("Second Starboard", true);
        secondStarboard.transform.position = Vector3.right * 150f;
        AutoTargetSelectionResult initial = Select(
            Candidate(portTarget),
            Candidate(starboardTarget),
            Candidate(secondStarboard)
        );

        starboardTarget.transform.position = Vector3.right * 190f;
        AutoTargetSelectionResult changed = Select(
            Candidate(portTarget),
            Candidate(starboardTarget),
            Candidate(secondStarboard)
        );

        AssertSelection(
            initial.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
        AssertSelection(
            changed.StarboardSelection,
            secondStarboard,
            2,
            CombatSide.Starboard
        );
        Assert.That(
            initial.PortSelection.TargetShipRoot,
            Is.SameAs(portTarget)
        );
        AssertSelection(changed.PortSelection, portTarget, 0, CombatSide.Port);
    }


    [Test]
    public void BeyondMaximumRangeCandidate_IsNotSelected()
    {
        EnableAutoFire();
        starboardTarget.transform.position = Vector3.right * 201f;

        bool selected = TrySelect(
            new[] { Candidate(starboardTarget) },
            out AutoTargetSelectionResult result
        );

        Assert.That(selected, Is.False);
        AssertNoSelection(result);
    }


    [Test]
    public void PortBestBecomesIllegal_ReplacesPortWithoutChangingStarboard()
    {
        EnableAutoFire();
        GameObject weakerPort = CreateShip("Weaker Port", true);
        weakerPort.transform.position = Vector3.left * 50f;
        weakerPort.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        AutoTargetSelectionResult initial = Select(
            Candidate(portTarget),
            Candidate(weakerPort),
            Candidate(starboardTarget)
        );

        AutoTargetSelectionResult next = Select(
            Candidate(portTarget, false),
            Candidate(weakerPort),
            Candidate(starboardTarget)
        );

        Assert.That(initial.PortSelection.TargetShipRoot, Is.SameAs(portTarget));
        AssertSelection(next.PortSelection, weakerPort, 1, CombatSide.Port);
        AssertSelection(
            next.StarboardSelection,
            starboardTarget,
            2,
            CombatSide.Starboard
        );
    }


    [Test]
    public void StarboardBestBecomesIllegal_ReplacesStarboardWithoutChangingPort()
    {
        EnableAutoFire();
        starboardTarget.transform.position = Vector3.right * 150f;
        GameObject weakerStarboard = CreateShip("Weaker Starboard", true);
        weakerStarboard.transform.position = Vector3.right * 50f;
        weakerStarboard.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        AutoTargetSelectionResult initial = Select(
            Candidate(starboardTarget),
            Candidate(weakerStarboard),
            Candidate(portTarget)
        );

        AutoTargetSelectionResult next = Select(
            Candidate(starboardTarget, false),
            Candidate(weakerStarboard),
            Candidate(portTarget)
        );

        Assert.That(
            initial.StarboardSelection.TargetShipRoot,
            Is.SameAs(starboardTarget)
        );
        AssertSelection(next.PortSelection, portTarget, 2, CombatSide.Port);
        AssertSelection(
            next.StarboardSelection,
            weakerStarboard,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void PortReloading_RemovesPortButKeepsReadyStarboard()
    {
        EnableAutoFire();
        Assert.That(
            shooterState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );

        AutoTargetSelectionResult result = Select(
            Candidate(portTarget),
            Candidate(starboardTarget)
        );

        AssertNoSideSelection(result.PortSelection);
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void StarboardReloading_RemovesStarboardButKeepsReadyPort()
    {
        EnableAutoFire();
        Assert.That(
            shooterState.TryCommitBroadsideFire(CombatSide.Starboard),
            Is.True
        );

        AutoTargetSelectionResult result = Select(
            Candidate(portTarget),
            Candidate(starboardTarget)
        );

        AssertSelection(result.PortSelection, portTarget, 0, CombatSide.Port);
        AssertNoSideSelection(result.StarboardSelection);
    }


    [Test]
    public void RelationshipDisallowedOnOneSide_RejectsOnlyThatCandidate()
    {
        EnableAutoFire();

        AutoTargetSelectionResult result = Select(
            Candidate(portTarget, false),
            Candidate(starboardTarget)
        );

        AssertNoSideSelection(result.PortSelection);
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void BlockedCandidate_IsRejectedOnlyFromApplicableSide()
    {
        EnableAutoFire();
        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Port Combat Geometry Blocker";
        blocker.layer = 8;
        blocker.transform.position = Vector3.left * 75f;
        createdRoots.Add(blocker);

        AutoTargetSelectionResult result = Select(
            Candidate(portTarget),
            Candidate(starboardTarget)
        );

        AssertNoSideSelection(result.PortSelection);
        AssertSelection(
            result.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
    }


    [Test]
    public void OneCandidate_CannotOccupyBothSideResults()
    {
        EnableAutoFire();

        AutoTargetSelectionResult result = Select(Candidate(starboardTarget));

        Assert.That(result.PortSelection.HasTarget, Is.False);
        Assert.That(result.StarboardSelection.HasTarget, Is.True);
        Assert.That(
            result.StarboardSelection.TargetShipRoot,
            Is.SameAs(starboardTarget)
        );
    }


    [Test]
    public void CandidateCollectionLargerThanTwo_ConsidersLateCandidates()
    {
        EnableAutoFire();
        GameObject invalidArc = CreateShip("Invalid Arc", true);
        invalidArc.transform.position = Vector3.forward * 50f;
        GameObject weakStarboard = CreateShip("Weak Starboard", true);
        weakStarboard.transform.position = Vector3.right * 50f;
        weakStarboard.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        GameObject bestStarboard = CreateShip("Best Starboard", true);
        bestStarboard.transform.position = Vector3.right * 75f;
        GameObject bestPort = CreateShip("Best Port", true);
        bestPort.transform.position = Vector3.left * 75f;

        AutoTargetSelectionResult result = Select(
            Candidate(null),
            Candidate(invalidArc),
            Candidate(weakStarboard),
            Candidate(portTarget),
            Candidate(bestStarboard),
            Candidate(bestPort)
        );

        AssertSelection(result.PortSelection, bestPort, 5, CombatSide.Port);
        AssertSelection(
            result.StarboardSelection,
            bestStarboard,
            4,
            CombatSide.Starboard
        );
    }


    [Test]
    public void SelectorContract_AcceptsArbitraryReadOnlyCandidateList()
    {
        MethodInfo method = typeof(ShipAutoTargetSelector).GetMethod(
            "TrySelect",
            BindingFlags.Public | BindingFlags.Static
        );
        Assert.That(method, Is.Not.Null);
        ParameterInfo[] parameters = method.GetParameters();

        Assert.That(
            parameters[1].ParameterType,
            Is.EqualTo(typeof(IReadOnlyList<AutoTargetCandidate>))
        );

        string source = ReadSelectorSource();
        Assert.That(source, Does.Not.Contain("Candidate1"));
        Assert.That(source, Does.Not.Contain("Candidate2"));
        Assert.That(source, Does.Not.Contain("candidates.Count == 2"));
    }


    [Test]
    public void PortTieBreak_PrefersShorterDistanceThenEarlierOrder()
    {
        EnableAutoFire();
        GameObject fartherPort = CreateShip("Farther Port", true);
        fartherPort.transform.position = Vector3.left * 75f;
        GameObject equalPort = CreateShip("Equal Port", true);
        equalPort.transform.position = Vector3.left * 50f;
        portTarget.transform.position = Vector3.left * 50f;

        AutoTargetSelectionResult shorter = Select(
            Candidate(fartherPort),
            Candidate(portTarget)
        );
        AutoTargetSelectionResult earlier = Select(
            Candidate(equalPort),
            Candidate(portTarget)
        );

        AssertSelection(shorter.PortSelection, portTarget, 1, CombatSide.Port);
        AssertSelection(earlier.PortSelection, equalPort, 0, CombatSide.Port);
    }


    [Test]
    public void StarboardTieBreak_PrefersShorterDistanceThenEarlierOrder()
    {
        EnableAutoFire();
        GameObject fartherStarboard = CreateShip("Farther Starboard", true);
        fartherStarboard.transform.position = Vector3.right * 75f;
        GameObject equalStarboard = CreateShip("Equal Starboard", true);
        equalStarboard.transform.position = Vector3.right * 50f;

        AutoTargetSelectionResult shorter = Select(
            Candidate(fartherStarboard),
            Candidate(starboardTarget)
        );
        AutoTargetSelectionResult earlier = Select(
            Candidate(equalStarboard),
            Candidate(starboardTarget)
        );

        AssertSelection(
            shorter.StarboardSelection,
            starboardTarget,
            1,
            CombatSide.Starboard
        );
        AssertSelection(
            earlier.StarboardSelection,
            equalStarboard,
            0,
            CombatSide.Starboard
        );
    }


    [Test]
    public void ManualTargetAssignment_DisablesWholeAutoMode()
    {
        EnableAutoFire();
        AutoTargetSelectionResult initial = Select(
            Candidate(portTarget),
            Candidate(starboardTarget)
        );

        bool assigned = shooterState.AssignManualTarget(starboardTarget);
        bool selected = TrySelect(
            new[] { Candidate(portTarget), Candidate(starboardTarget) },
            out AutoTargetSelectionResult next
        );

        Assert.That(initial.PortSelection.HasTarget, Is.True);
        Assert.That(initial.StarboardSelection.HasTarget, Is.True);
        Assert.That(assigned, Is.True);
        Assert.That(shooterState.AutoFireEnabled, Is.False);
        Assert.That(shooterState.ManualTarget, Is.SameAs(starboardTarget));
        Assert.That(selected, Is.False);
        AssertNoSelection(next);
    }


    [Test]
    public void Selection_DoesNotWriteRootPositionOrHeading()
    {
        EnableAutoFire();
        Vector3 shooterPosition = shooterRoot.transform.position;
        Quaternion shooterRotation = shooterRoot.transform.rotation;
        Vector3 portPosition = portTarget.transform.position;
        Quaternion portRotation = portTarget.transform.rotation;
        Vector3 starboardPosition = starboardTarget.transform.position;
        Quaternion starboardRotation = starboardTarget.transform.rotation;

        Select(Candidate(portTarget), Candidate(starboardTarget));

        Assert.That(shooterRoot.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooterRoot.transform.rotation, Is.EqualTo(shooterRotation));
        Assert.That(portTarget.transform.position, Is.EqualTo(portPosition));
        Assert.That(portTarget.transform.rotation, Is.EqualTo(portRotation));
        Assert.That(starboardTarget.transform.position, Is.EqualTo(starboardPosition));
        Assert.That(starboardTarget.transform.rotation, Is.EqualTo(starboardRotation));
    }


    [Test]
    public void Selection_DoesNotCommitEitherReload()
    {
        EnableAutoFire();

        Select(Candidate(portTarget), Candidate(starboardTarget));

        Assert.That(
            shooterState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            shooterState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(shooterState.PortReloadRemainingSeconds, Is.Zero);
        Assert.That(shooterState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void SelectorSource_HasNoDiscoveryFiringMovementOrPresentationPath()
    {
        string source = ReadSelectorSource();

        Assert.That(source, Does.Not.Contain("FindObjects"));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("Projectile"));
        Assert.That(source, Does.Not.Contain("CombatVFX"));
        Assert.That(source, Does.Not.Contain("ShotSample"));
        Assert.That(source, Does.Not.Contain("Accuracy"));
        Assert.That(source, Does.Not.Contain("Dispersion"));
        Assert.That(source, Does.Not.Contain("CannonPrecision"));
        Assert.That(source, Does.Not.Contain("MuzzleSpread"));
        Assert.That(source, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("ShipDestinationController"));
        Assert.That(source, Does.Not.Contain("transform.position ="));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
        Assert.That(source, Does.Contain("ShipAutoTargetScorer.TryEvaluate"));
        Assert.That(source, Does.Not.Contain("ExposureNormalized"));
        Assert.That(source, Does.Not.Contain("RangeQualityNormalized"));
        Assert.That(source, Does.Not.Contain("VisibilityQualityNormalized"));
        Assert.That(source, Does.Not.Contain("TryCalculateExposure"));
    }


    [Test]
    public void CandidateAndPerSideResultSurfaces_AreImmutable()
    {
        AssertGetterOnlyProperties(typeof(AutoTargetCandidate));
        AssertGetterOnlyProperties(typeof(AutoTargetSideSelectionResult));
        AssertGetterOnlyProperties(typeof(AutoTargetSelectionResult));

        Assert.That(
            typeof(AutoTargetSelectionResult).GetProperty("TargetShipRoot"),
            Is.Null
        );
        Assert.That(
            typeof(AutoTargetSelectionResult).GetProperty("Selected"),
            Is.Null
        );
    }


    private void EnableAutoFire()
    {
        shooterState.SetAutoFireEnabled(true);
    }


    private AutoTargetSelectionResult Select(
        params AutoTargetCandidate[] candidates
    )
    {
        bool selected = TrySelect(
            candidates,
            out AutoTargetSelectionResult result
        );

        Assert.That(selected, Is.True);
        Assert.That(result.HasAnyTarget, Is.True);
        return result;
    }


    private bool TrySelect(
        IReadOnlyList<AutoTargetCandidate> candidates,
        out AutoTargetSelectionResult result
    )
    {
        Physics.SyncTransforms();
        return ShipAutoTargetSelector.TrySelect(
            shooterRoot,
            candidates,
            out result
        );
    }


    private static AutoTargetCandidate Candidate(
        GameObject target,
        bool relationshipAllowsFire = true
    )
    {
        return new AutoTargetCandidate(target, relationshipAllowsFire);
    }


    private static void AssertSelection(
        AutoTargetSideSelectionResult result,
        GameObject expectedTarget,
        int expectedIndex,
        CombatSide expectedSide
    )
    {
        Assert.That(result.HasTarget, Is.True);
        Assert.That(result.TargetShipRoot, Is.SameAs(expectedTarget));
        Assert.That(result.CandidateIndex, Is.EqualTo(expectedIndex));
        Assert.That(result.FireEligibility.CanFire, Is.True);
        Assert.That(result.FireEligibility.Side, Is.EqualTo(expectedSide));
        Assert.That(result.Score.Selectable, Is.True);
    }


    private static void AssertNoSelection(AutoTargetSelectionResult result)
    {
        Assert.That(result.HasAnyTarget, Is.False);
        AssertNoSideSelection(result.PortSelection);
        AssertNoSideSelection(result.StarboardSelection);
    }


    private static void AssertNoSideSelection(
        AutoTargetSideSelectionResult result
    )
    {
        Assert.That(result.HasTarget, Is.False);
        Assert.That(result.TargetShipRoot, Is.Null);
        Assert.That(result.Score.Selectable, Is.False);
    }


    private static void AssertGetterOnlyProperties(System.Type type)
    {
        foreach (PropertyInfo property in type.GetProperties())
        {
            Assert.That(property.GetMethod, Is.Not.Null, property.Name);
            Assert.That(property.SetMethod, Is.Null, property.Name);
        }
    }


    private static string ReadSelectorSource()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipAutoTargetSelector.cs"
        );
        return File.ReadAllText(sourcePath);
    }


    private GameObject CreateShip(string name, bool includeExposure)
    {
        GameObject root = new GameObject(name);
        createdRoots.Add(root);
        root.AddComponent<ShipCombatState>();
        ShipArtDefinition artDefinition = root.AddComponent<ShipArtDefinition>();
        SetArtReferences(root, artDefinition);
        root.AddComponent<ShipCombatGeometry>();

        if (includeExposure)
        {
            ShipExposureReference exposure = root.AddComponent<ShipExposureReference>();
            SetPrivateField(exposure, "shipArtDefinition", artDefinition);
        }

        return root;
    }


    private static void SetArtReferences(
        GameObject root,
        ShipArtDefinition artDefinition
    )
    {
        SetArtReference(root, artDefinition, "centerReference", Vector3.zero);
        SetArtReference(root, artDefinition, "waterlineReference", Vector3.zero);
        SetArtReference(root, artDefinition, "deckReference", Vector3.up * 4f);
        SetArtReference(root, artDefinition, "bowReference", Vector3.forward * 15f);
        SetArtReference(root, artDefinition, "sternReference", Vector3.back * 15f);
        SetArtReference(root, artDefinition, "portReference", Vector3.left * 5f);
        SetArtReference(root, artDefinition, "starboardReference", Vector3.right * 5f);
    }


    private static void SetArtReference(
        GameObject root,
        ShipArtDefinition artDefinition,
        string fieldName,
        Vector3 localPosition
    )
    {
        GameObject referenceObject = new GameObject(fieldName);
        referenceObject.transform.SetParent(root.transform, false);
        referenceObject.transform.localPosition = localPosition;
        SetPrivateField(artDefinition, fieldName, referenceObject.transform);
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
