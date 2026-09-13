using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GelderlandExposurePlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const float Tolerance = 0.001f;

    private static readonly Type[] MovementBehaviourTypes =
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

    private GameObject instance;
    private ShipExposureReference exposureReference;
    private ShipCombatGeometry combatGeometry;


    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        instance = UnityEngine.Object.Instantiate(prefab);
        exposureReference = instance.GetComponent<ShipExposureReference>();
        combatGeometry = instance.GetComponent<ShipCombatGeometry>();
        Assert.That(exposureReference, Is.Not.Null);
        Assert.That(combatGeometry, Is.Not.Null);
        DisableMovementBehaviours();
        yield return null;
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (instance != null)
        {
            UnityEngine.Object.Destroy(instance);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator BowOnWidth_EqualsBeam()
    {
        ExposureRect exposure = Query(instance.transform.forward, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(11f).Within(Tolerance));
        yield return null;
    }


    [UnityTest]
    public IEnumerator SternOnWidth_EqualsBeam()
    {
        ExposureRect exposure = Query(-instance.transform.forward, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(11f).Within(Tolerance));
        yield return null;
    }


    [UnityTest]
    public IEnumerator PortBroadsideWidth_EqualsLength()
    {
        ExposureRect exposure = Query(-instance.transform.right, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(45f).Within(Tolerance));
        yield return null;
    }


    [UnityTest]
    public IEnumerator StarboardBroadsideWidth_EqualsLength()
    {
        ExposureRect exposure = Query(instance.transform.right, 100f);

        Assert.That(exposure.WidthMeters, Is.EqualTo(45f).Within(Tolerance));
        yield return null;
    }


    [UnityTest]
    public IEnumerator BearingSamples_FollowContinuousProjectionFormula()
    {
        Assert.That(
            exposureReference.TryGetReferenceDimensions(
                out Vector3 dimensionsMeters
            ),
            Is.True
        );
        float[] degrees = { 0f, 15f, 30f, 45f, 60f, 75f, 90f, 180f };

        foreach (float angle in degrees)
        {
            Vector3 bearing = Quaternion.AngleAxis(angle, Vector3.up)
                * instance.transform.forward;
            ExposureRect exposure = Query(bearing, 100f);
            Vector3 sightDirection = -bearing.normalized;
            float expectedWidth =
                Mathf.Abs(
                    Vector3.Dot(sightDirection, instance.transform.forward)
                ) * dimensionsMeters.x
                + Mathf.Abs(
                    Vector3.Dot(sightDirection, instance.transform.right)
                ) * dimensionsMeters.z;

            Assert.That(
                exposure.WidthMeters,
                Is.EqualTo(expectedWidth).Within(Tolerance),
                $"Unexpected projected width at {angle} degrees."
            );
            Assert.That(
                exposure.HeightMeters,
                Is.EqualTo(dimensionsMeters.y).Within(Tolerance)
            );
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator TargetRootYaw_ChangesExposureForFixedWorldBearing()
    {
        Vector3 observer = GetCenterWorld() + Vector3.forward * 100f;
        ExposureRect before = QueryFromPosition(observer);

        instance.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        ExposureRect after = QueryFromPosition(observer);

        Assert.That(before.WidthMeters, Is.EqualTo(11f).Within(Tolerance));
        Assert.That(after.WidthMeters, Is.EqualTo(45f).Within(Tolerance));
        Assert.That(after.HeightMeters, Is.EqualTo(before.HeightMeters).Within(Tolerance));
        yield return null;
    }


    [UnityTest]
    public IEnumerator ObserverDistance_DoesNotScaleExposure()
    {
        Vector3 bearing = (instance.transform.forward + instance.transform.right)
            .normalized;
        ExposureRect near = Query(bearing, 25f);
        ExposureRect far = Query(bearing, 2500f);

        AssertSameDimensions(near, far);
        yield return null;
    }


    [UnityTest]
    public IEnumerator DisabledVisualRoot_DoesNotChangeExposure()
    {
        Vector3 bearing = new Vector3(0.6f, 0f, 0.8f).normalized;
        ExposureRect before = Query(bearing, 100f);
        Transform visualRoot = instance.transform.Find("VisualRoot");

        Assert.That(visualRoot, Is.Not.Null);
        visualRoot.gameObject.SetActive(false);
        ExposureRect after = Query(bearing, 100f);

        AssertSameDimensions(before, after);
        yield return null;
    }


    [UnityTest]
    public IEnumerator ResizedCombatGeometry_DoesNotChangeExposure()
    {
        Vector3 bearing = new Vector3(-0.4f, 0f, 0.9f).normalized;
        ExposureRect before = Query(bearing, 100f);

        ResizeRegion(combatGeometry.BowRegion);
        ResizeRegion(combatGeometry.MidshipRegion);
        ResizeRegion(combatGeometry.SternRegion);
        ExposureRect after = Query(bearing, 100f);

        AssertSameDimensions(before, after);
        yield return null;
    }


    [UnityTest]
    public IEnumerator ResizedGenericShipCollider_DoesNotChangeExposure()
    {
        Vector3 bearing = new Vector3(0.25f, 0f, -0.8f).normalized;
        ExposureRect before = Query(bearing, 100f);
        Transform colliderTransform = instance.transform.Find(
            "CollisionRoot/ShipCollider"
        );
        BoxCollider shipCollider = colliderTransform != null
            ? colliderTransform.GetComponent<BoxCollider>()
            : null;

        Assert.That(shipCollider, Is.Not.Null);
        shipCollider.size = new Vector3(1000f, 1000f, 1000f);
        ExposureRect after = Query(bearing, 100f);

        AssertSameDimensions(before, after);
        yield return null;
    }


    [UnityTest]
    public IEnumerator RootTranslation_MovesCenterButPreservesDimensions()
    {
        Vector3 bearing = new Vector3(0.3f, 0f, 0.7f).normalized;
        Vector3 observer = GetCenterWorld() + bearing * 100f;
        ExposureRect before = QueryFromPosition(observer);
        Vector3 translation = new Vector3(140f, 8f, -73f);

        instance.transform.position += translation;
        ExposureRect after = QueryFromPosition(observer + translation);

        Assert.That(
            Vector3.Distance(
                after.CenterWorld,
                before.CenterWorld + translation
            ),
            Is.LessThan(Tolerance)
        );
        AssertSameDimensions(before, after);
        yield return null;
    }


    [UnityTest]
    public IEnumerator RepeatedObserverQueries_DoNotContaminateSharedState()
    {
        ExposureRect bow = Query(instance.transform.forward, 100f);
        ExposureRect side = Query(instance.transform.right, 100f);
        ExposureRect bowAgain = Query(instance.transform.forward, 100f);

        Assert.That(bow.WidthMeters, Is.EqualTo(11f).Within(Tolerance));
        Assert.That(side.WidthMeters, Is.EqualTo(45f).Within(Tolerance));
        AssertSameDimensions(bow, bowAgain);
        yield return null;
    }


    private ExposureRect Query(Vector3 worldBearing, float distance)
    {
        return QueryFromPosition(
            GetCenterWorld() + worldBearing.normalized * distance
        );
    }


    private ExposureRect QueryFromPosition(Vector3 observerWorldPosition)
    {
        bool available = exposureReference.TryCalculateExposure(
            observerWorldPosition,
            out ExposureRect exposure
        );

        Assert.That(available, Is.True);
        return exposure;
    }


    private Vector3 GetCenterWorld()
    {
        Assert.That(
            exposureReference.TryGetReferenceCenter(out Vector3 centerWorld),
            Is.True
        );
        return centerWorld;
    }


    private static void ResizeRegion(CombatHitRegion region)
    {
        BoxCollider collider = region.QueryCollider as BoxCollider;

        Assert.That(collider, Is.Not.Null);
        collider.size = new Vector3(1000f, 1000f, 1000f);
    }


    private static void AssertSameDimensions(
        ExposureRect expected,
        ExposureRect actual
    )
    {
        Assert.That(
            actual.WidthMeters,
            Is.EqualTo(expected.WidthMeters).Within(Tolerance)
        );
        Assert.That(
            actual.HeightMeters,
            Is.EqualTo(expected.HeightMeters).Within(Tolerance)
        );
        Assert.That(
            actual.AreaSquareMeters,
            Is.EqualTo(expected.AreaSquareMeters).Within(Tolerance)
        );
    }


    private void DisableMovementBehaviours()
    {
        foreach (Type componentType in MovementBehaviourTypes)
        {
            Behaviour behaviour = instance.GetComponent(componentType) as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }
    }
}
