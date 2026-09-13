using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class ShipMuzzleSockets : MonoBehaviour
{
    private const float RootLocalZTieTolerance = 0.0001f;

    private static readonly IReadOnlyList<Transform> EmptySockets =
        Array.AsReadOnly(Array.Empty<Transform>());

    [Header("Muzzle Socket Containers")]

    [SerializeField]
    private Transform portMuzzlesContainer;

    [SerializeField]
    private Transform starboardMuzzlesContainer;

    private Transform cachedPortMuzzlesContainer;
    private Transform cachedStarboardMuzzlesContainer;
    private IReadOnlyList<Transform> portMuzzles = EmptySockets;
    private IReadOnlyList<Transform> starboardMuzzles = EmptySockets;
    private bool cacheInitialized;


    public IReadOnlyList<Transform> PortMuzzles
    {
        get
        {
            EnsureCache();
            return portMuzzles;
        }
    }

    public IReadOnlyList<Transform> StarboardMuzzles
    {
        get
        {
            EnsureCache();
            return starboardMuzzles;
        }
    }


    private void Awake()
    {
        RebuildCache();
    }


    private void OnValidate()
    {
        RebuildCache();
    }


    private void EnsureCache()
    {
        if (!cacheInitialized
            || cachedPortMuzzlesContainer != portMuzzlesContainer
            || cachedStarboardMuzzlesContainer != starboardMuzzlesContainer)
        {
            RebuildCache();
        }
    }


    private void RebuildCache()
    {
        cachedPortMuzzlesContainer = portMuzzlesContainer;
        cachedStarboardMuzzlesContainer = starboardMuzzlesContainer;
        portMuzzles = CollectOrderedSockets(portMuzzlesContainer);
        starboardMuzzles = CollectOrderedSockets(starboardMuzzlesContainer);
        cacheInitialized = true;
    }


    private IReadOnlyList<Transform> CollectOrderedSockets(Transform container)
    {
        if (container == null)
        {
            return EmptySockets;
        }

        List<Transform> orderedSockets = new List<Transform>(
            container.childCount
        );

        for (int index = 0; index < container.childCount; index++)
        {
            orderedSockets.Add(container.GetChild(index));
        }

        orderedSockets.Sort(CompareCanonicalOrder);
        return new ReadOnlyCollection<Transform>(orderedSockets);
    }


    private int CompareCanonicalOrder(Transform first, Transform second)
    {
        float firstRootLocalZ = transform.InverseTransformPoint(
            first.position
        ).z;
        float secondRootLocalZ = transform.InverseTransformPoint(
            second.position
        ).z;
        float difference = firstRootLocalZ - secondRootLocalZ;

        if (Mathf.Abs(difference) > RootLocalZTieTolerance)
        {
            return secondRootLocalZ.CompareTo(firstRootLocalZ);
        }

        return first.GetSiblingIndex().CompareTo(second.GetSiblingIndex());
    }
}
