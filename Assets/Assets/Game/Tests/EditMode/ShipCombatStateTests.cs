using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipCombatStateTests
{
    private const string GelderlandCombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private static readonly string[] GenericMovementProxyPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };

    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject shipRoot;
    private GameObject targetShipRoot;
    private ShipCombatState combatState;


    [SetUp]
    public void SetUp()
    {
        shipRoot = new GameObject("Ship Root");
        targetShipRoot = new GameObject("Target Ship Root");
        combatState = shipRoot.AddComponent<ShipCombatState>();
        targetShipRoot.AddComponent<ShipCombatState>();
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(targetShipRoot);
        UnityEngine.Object.DestroyImmediate(shipRoot);
    }


    [Test]
    public void NewOwner_HasDisabledModesAndNoManualTarget()
    {
        Assert.That(combatState.AutoFireEnabled, Is.False);
        Assert.That(combatState.ManualTarget, Is.Null);
        Assert.That(combatState.BlindFireEnabled, Is.False);
    }


    [Test]
    public void AutoFire_ChangesOnlyThroughCombatCommand()
    {
        combatState.SetAutoFireEnabled(true);

        Assert.That(combatState.AutoFireEnabled, Is.True);

        combatState.SetAutoFireEnabled(false);

        Assert.That(combatState.AutoFireEnabled, Is.False);
    }


    [Test]
    public void ManualTarget_AssignsAndClearsAuthoritativeRootIdentity()
    {
        Assert.That(
            combatState.AssignManualTarget(targetShipRoot),
            Is.True
        );

        Assert.That(combatState.ManualTarget, Is.SameAs(targetShipRoot));

        combatState.ClearManualTarget();

        Assert.That(combatState.ManualTarget, Is.Null);
    }


    [Test]
    public void BlindFire_ChangesOnlyThroughCombatCommand()
    {
        combatState.SetBlindFireEnabled(true);

        Assert.That(combatState.BlindFireEnabled, Is.True);

        combatState.SetBlindFireEnabled(false);

        Assert.That(combatState.BlindFireEnabled, Is.False);
    }


    [Test]
    public void Phase3AStates_ApplyAutoManualArbitrationOnly()
    {
        combatState.SetAutoFireEnabled(true);
        combatState.SetBlindFireEnabled(true);
        Assert.That(
            combatState.AssignManualTarget(targetShipRoot),
            Is.True
        );

        Assert.That(combatState.AutoFireEnabled, Is.False);
        Assert.That(combatState.BlindFireEnabled, Is.True);
        Assert.That(combatState.ManualTarget, Is.SameAs(targetShipRoot));

        combatState.ClearManualTarget();

        Assert.That(combatState.AutoFireEnabled, Is.False);
        Assert.That(combatState.BlindFireEnabled, Is.True);
        Assert.That(combatState.ManualTarget, Is.Null);
    }


    [Test]
    public void NewOwner_HasBothBroadsidesReadyWithZeroRemainingTime()
    {
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void CommitPort_BeginsOnlyPortReload()
    {
        SetReloadDuration(12f);

        bool committed = combatState.TryCommitBroadsideFire(
            CombatSide.Port
        );

        Assert.That(committed, Is.True);
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.EqualTo(12f));
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void CommitStarboard_BeginsOnlyStarboardReload()
    {
        SetReloadDuration(9f);

        bool committed = combatState.TryCommitBroadsideFire(
            CombatSide.Starboard
        );

        Assert.That(committed, Is.True);
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            combatState.StarboardReloadRemainingSeconds,
            Is.EqualTo(9f)
        );
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void Broadsides_ReloadIndependentlyWithDifferentRemainingTimes()
    {
        SetReloadDuration(10f);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );
        AdvanceReloads(3f);

        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Starboard),
            Is.True
        );

        Assert.That(combatState.PortReloadRemainingSeconds, Is.EqualTo(7f));
        Assert.That(
            combatState.StarboardReloadRemainingSeconds,
            Is.EqualTo(10f)
        );
        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
    }


    [Test]
    public void IndependentCompletion_AllowsPortToFinishBeforeStarboard()
    {
        SetReloadDuration(10f);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );
        AdvanceReloads(3f);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Starboard),
            Is.True
        );

        AdvanceReloads(7f);

        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            combatState.StarboardReloadRemainingSeconds,
            Is.EqualTo(3f)
        );

        AdvanceReloads(3f);

        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void RepeatedCommitWhileReloading_DoesNotRestartOrExtendTimer()
    {
        SetReloadDuration(10f);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );
        AdvanceReloads(2.5f);
        float remainingBeforeRepeat =
            combatState.PortReloadRemainingSeconds;

        bool repeatedCommit = combatState.TryCommitBroadsideFire(
            CombatSide.Port
        );

        Assert.That(repeatedCommit, Is.False);
        Assert.That(
            combatState.PortReloadRemainingSeconds,
            Is.EqualTo(remainingBeforeRepeat)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void ReloadCompletion_ClampsRemainingTimeAndReturnsReady()
    {
        SetReloadDuration(5f);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );

        AdvanceReloads(5f);

        Assert.That(
            combatState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.PortReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void InvalidSideAndNonPositiveElapsedTime_AreSafeNoOps()
    {
        SetReloadDuration(8f);

        Assert.That(
            combatState.TryCommitBroadsideFire((CombatSide)999),
            Is.False
        );
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );
        float remaining = combatState.PortReloadRemainingSeconds;

        AdvanceReloads(0f);
        AdvanceReloads(-2f);
        AdvanceReloads(float.NaN);

        Assert.That(
            combatState.PortReloadRemainingSeconds,
            Is.EqualTo(remaining)
        );
        Assert.That(
            combatState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(combatState.StarboardReloadRemainingSeconds, Is.Zero);
    }


    [Test]
    public void CombatSideAndReloadState_HaveOnlyFoundationValues()
    {
        Assert.That(
            Enum.GetValues(typeof(CombatSide)),
            Is.EqualTo(new[] { CombatSide.Port, CombatSide.Starboard })
        );
        Assert.That(
            Enum.GetValues(typeof(BroadsideReloadState)),
            Is.EqualTo(new[]
            {
                BroadsideReloadState.Ready,
                BroadsideReloadState.Reloading
            })
        );
    }


    [Test]
    public void ReloadDuration_IsCentralizedFiniteAndPositive()
    {
        SetPrivateField(
            combatState,
            "broadsideReloadDurationSeconds",
            float.NaN
        );
        InvokePrivate(combatState, "OnValidate");

        Assert.That(
            float.IsNaN(combatState.BroadsideReloadDurationSeconds),
            Is.False
        );
        Assert.That(
            float.IsInfinity(combatState.BroadsideReloadDurationSeconds),
            Is.False
        );
        Assert.That(combatState.BroadsideReloadDurationSeconds, Is.Positive);
    }


    [Test]
    public void PublicStateSurface_IsGetterOnlyAndHasNoWritableFields()
    {
        string[] propertyNames =
        {
            "AutoFireEnabled",
            "ManualTarget",
            "BlindFireEnabled",
            "BroadsideReloadDurationSeconds",
            "PortBroadsideState",
            "PortReloadRemainingSeconds",
            "StarboardBroadsideState",
            "StarboardReloadRemainingSeconds"
        };

        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = typeof(ShipCombatState).GetProperty(
                propertyName
            );

            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.GetMethod, Is.Not.Null, propertyName);
            Assert.That(property.GetMethod.IsPublic, Is.True, propertyName);
            Assert.That(property.SetMethod, Is.Null, propertyName);
        }

        FieldInfo[] publicFields = typeof(ShipCombatState).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.DeclaredOnly
        );
        Assert.That(publicFields, Is.Empty);

        FieldInfo[] stateFields = typeof(ShipCombatState).GetFields(
            PrivateInstance | BindingFlags.DeclaredOnly
        );
        Assert.That(stateFields, Has.Length.EqualTo(8));
        Assert.That(stateFields.All(field => field.IsPrivate), Is.True);

        FieldInfo[] serializedFields = stateFields
            .Where(field => field.IsDefined(typeof(SerializeField), false))
            .ToArray();
        Assert.That(
            serializedFields.Select(field => field.Name),
            Is.EqualTo(new[] { "broadsideReloadDurationSeconds" })
        );
    }


    [Test]
    public void Commands_DoNotModifyShipRootOrMovementState()
    {
        shipRoot.transform.SetPositionAndRotation(
            new Vector3(27f, 3f, -14f),
            Quaternion.Euler(0f, 63f, 0f)
        );
        shipRoot.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);
        ShipSailingSpeed sailingSpeed =
            shipRoot.AddComponent<ShipSailingSpeed>();
        SetPrivateField(sailingSpeed, "currentSpeed", 2.75f);
        SetPrivateField(
            sailingSpeed,
            "actualVelocity",
            new Vector3(1.25f, 0f, 2.5f)
        );
        SetPrivateField(sailingSpeed, "courseSpeed", 2.8f);
        SetPrivateField(sailingSpeed, "courseHeading", 26.5f);

        Vector3 position = shipRoot.transform.position;
        Quaternion rotation = shipRoot.transform.rotation;
        Vector3 scale = shipRoot.transform.localScale;
        float currentSpeed = sailingSpeed.CurrentSpeed;
        Vector3 actualVelocity = sailingSpeed.ActualVelocity;
        float courseSpeed = sailingSpeed.CourseSpeed;
        float courseHeading = sailingSpeed.CourseHeading;

        combatState.SetAutoFireEnabled(true);
        combatState.AssignManualTarget(targetShipRoot);
        combatState.SetBlindFireEnabled(true);
        combatState.ClearManualTarget();
        SetReloadDuration(6f);
        Assert.That(
            combatState.TryCommitBroadsideFire(CombatSide.Port),
            Is.True
        );
        AdvanceReloads(2f);

        Assert.That(shipRoot.transform.position, Is.EqualTo(position));
        Assert.That(shipRoot.transform.rotation, Is.EqualTo(rotation));
        Assert.That(shipRoot.transform.localScale, Is.EqualTo(scale));
        Assert.That(sailingSpeed.CurrentSpeed, Is.EqualTo(currentSpeed));
        Assert.That(sailingSpeed.ActualVelocity, Is.EqualTo(actualVelocity));
        Assert.That(sailingSpeed.CourseSpeed, Is.EqualTo(courseSpeed));
        Assert.That(sailingSpeed.CourseHeading, Is.EqualTo(courseHeading));
    }


    [Test]
    public void Commands_DoNotModifyCombatArtReferences()
    {
        ShipArtDefinition artDefinition =
            shipRoot.AddComponent<ShipArtDefinition>();
        string[] fieldNames =
        {
            "visualRoot",
            "waterlineReference",
            "centerReference",
            "bowReference",
            "sternReference",
            "portReference",
            "starboardReference",
            "deckReference"
        };
        Transform[] references = new Transform[fieldNames.Length];

        for (int index = 0; index < fieldNames.Length; index++)
        {
            GameObject referenceObject = new GameObject(fieldNames[index]);
            referenceObject.transform.SetParent(shipRoot.transform, false);
            referenceObject.transform.localPosition = new Vector3(
                index,
                index * 0.5f,
                -index
            );
            references[index] = referenceObject.transform;
            SetPrivateField(
                artDefinition,
                fieldNames[index],
                references[index]
            );
        }

        Vector3[] localPositions = references
            .Select(reference => reference.localPosition)
            .ToArray();

        combatState.SetAutoFireEnabled(true);
        combatState.AssignManualTarget(targetShipRoot);
        combatState.SetBlindFireEnabled(true);
        combatState.ClearManualTarget();

        Assert.That(artDefinition.VisualRoot, Is.SameAs(references[0]));
        Assert.That(
            artDefinition.WaterlineReference,
            Is.SameAs(references[1])
        );
        Assert.That(artDefinition.CenterReference, Is.SameAs(references[2]));
        Assert.That(artDefinition.BowReference, Is.SameAs(references[3]));
        Assert.That(artDefinition.SternReference, Is.SameAs(references[4]));
        Assert.That(artDefinition.PortReference, Is.SameAs(references[5]));
        Assert.That(
            artDefinition.StarboardReference,
            Is.SameAs(references[6])
        );
        Assert.That(artDefinition.DeckReference, Is.SameAs(references[7]));

        for (int index = 0; index < references.Length; index++)
        {
            Assert.That(
                references[index].localPosition,
                Is.EqualTo(localPositions[index])
            );
        }
    }


    [Test]
    public void Contract_HasNoProjectileCombatArtOrMovementDependency()
    {
        Type[] forbiddenTypes =
        {
            typeof(ShipArtDefinition),
            typeof(ShipMuzzleSockets),
            typeof(ShipCombatGeometry),
            typeof(ShipExposureReference),
            typeof(ICombatVFXEventReceiver),
            typeof(ShipSailingSpeed),
            typeof(ShipTurning),
            typeof(ShipTacking),
            typeof(ShipWearing),
            typeof(ShipLeeway),
            typeof(ShipDestinationController),
            typeof(ShipManeuverPlanner)
        };

        Type[] contractTypes = typeof(ShipCombatState)
            .GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
            .Select(field => field.FieldType)
            .Concat(typeof(ShipCombatState)
                .GetProperties(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.DeclaredOnly
                )
                .Select(property => property.PropertyType))
            .Concat(typeof(ShipCombatState)
                .GetMethods(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.DeclaredOnly
                )
                .SelectMany(method => method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType)))
            .ToArray();

        foreach (Type forbiddenType in forbiddenTypes)
        {
            Assert.That(contractTypes, Has.No.Member(forbiddenType));
        }

        Assert.That(
            contractTypes.Any(type => type.Name.Contains("Projectile")),
            Is.False
        );
        Assert.That(
            typeof(ShipCombatState).GetEvents(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ),
            Is.Empty
        );
    }


    [Test]
    public void Component_DisallowsCompetingOwnerOnSameShipRoot()
    {
        Assert.That(
            typeof(ShipCombatState).GetCustomAttribute<
                DisallowMultipleComponent
            >(),
            Is.Not.Null
        );
        Assert.That(combatState.gameObject, Is.SameAs(shipRoot));
        Assert.That(combatState.transform, Is.SameAs(shipRoot.transform));
    }


    [Test]
    public void GelderlandCombatPrefab_HasExactlyOneOwnerOnAuthoritativeRoot()
    {
        GameObject prefab = LoadPrefab(GelderlandCombatPrefabPath);
        ShipCombatState[] owners = prefab.GetComponentsInChildren<
            ShipCombatState
        >(true);

        Assert.That(owners, Has.Length.EqualTo(1));
        Assert.That(owners[0].gameObject, Is.SameAs(prefab));
        Assert.That(owners[0].transform, Is.SameAs(prefab.transform));
        Assert.That(
            owners[0].PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            owners[0].StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    [Test]
    public void GenericMovementProxies_HaveNoCombatGameplayOwner()
    {
        foreach (string prefabPath in GenericMovementProxyPaths)
        {
            GameObject prefab = LoadPrefab(prefabPath);

            Assert.That(
                prefab.GetComponentsInChildren<ShipCombatState>(true),
                Is.Empty,
                prefabPath
            );
        }
    }


    private void SetReloadDuration(float durationSeconds)
    {
        SetPrivateField(
            combatState,
            "broadsideReloadDurationSeconds",
            durationSeconds
        );
        InvokePrivate(combatState, "OnValidate");
    }


    private void AdvanceReloads(float elapsedSeconds)
    {
        InvokePrivate(combatState, "AdvanceReloads", elapsedSeconds);
    }


    private static GameObject LoadPrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath
        );
        Assert.That(prefab, Is.Not.Null, prefabPath);
        return prefab;
    }


    private static void InvokePrivate(
        object target,
        string methodName,
        params object[] arguments
    )
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            PrivateInstance
        );

        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        method.Invoke(target, arguments);
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            PrivateInstance
        );

        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
