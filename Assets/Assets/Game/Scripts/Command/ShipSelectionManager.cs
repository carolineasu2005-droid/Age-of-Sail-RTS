using System.Collections.Generic;
using UnityEngine;

public class ShipSelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]

    [SerializeField]
    [Range(1f, 200f)]
    [Tooltip("Maximum screen-space distance in pixels for selecting a ship.")]
    private float selectionRadiusPixels = 45f;


    [Header("Runtime Debug")]

    [SerializeField]
    private List<ShipDestinationController> selectedShips = new();

    [SerializeField]
    private int selectedCount;

    [SerializeField]
    private ShipDestinationController primarySelectedShip;

    [SerializeField]
    private ShipDestinationController lastPickedShip;

    [SerializeField]
    private Vector2 lastPickScreenPosition;

    [SerializeField]
    private bool lastPickSuccessful;

    private readonly HashSet<ShipDestinationController> uniqueShips = new();

    public event System.Action SelectionMembershipChanged;


    private struct ScreenSelectionCandidate
    {
        public ShipDestinationController ship;
        public Vector2 screenPosition;

        public ScreenSelectionCandidate(
            ShipDestinationController ship,
            Vector2 screenPosition
        )
        {
            this.ship = ship;
            this.screenPosition = screenPosition;
        }
    }


    public IReadOnlyList<ShipDestinationController> SelectedShips
    {
        get
        {
            RefreshSelectionData();
            return selectedShips;
        }
    }

    public int SelectedCount
    {
        get
        {
            RefreshSelectionData();
            return selectedCount;
        }
    }

    public ShipDestinationController PrimarySelectedShip
    {
        get
        {
            RefreshSelectionData();
            return primarySelectedShip;
        }
    }


    private void Awake()
    {
        RefreshSelectionData();
    }


    private void LateUpdate()
    {
        RefreshSelectionData();
    }


    public void SelectSingle(ShipDestinationController ship)
    {
        bool membershipChanged = selectedShips.Count != 1
            || selectedShips[0] != ship;

        if (ship == null)
        {
            ClearSelection();
            return;
        }

        selectedShips.Clear();
        selectedShips.Add(ship);
        RefreshSelectionData();
        NotifySelectionMembershipChanged(membershipChanged);
    }


    public void ToggleSelection(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        RefreshSelectionData();
        NotifySelectionMembershipChanged(true);

        if (selectedShips.Contains(ship))
        {
            selectedShips.Remove(ship);
        }
        else
        {
            selectedShips.Add(ship);
        }

        RefreshSelectionData();
    }


    public void AddSelection(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        RefreshSelectionData();

        bool membershipChanged = !selectedShips.Contains(ship);

        if (membershipChanged)
        {
            selectedShips.Add(ship);
        }

        RefreshSelectionData();
        NotifySelectionMembershipChanged(membershipChanged);
    }


    public void RemoveSelection(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        bool membershipChanged = selectedShips.Remove(ship);
        RefreshSelectionData();
        NotifySelectionMembershipChanged(membershipChanged);
    }


    public void ClearSelection()
    {
        bool membershipChanged = selectedShips.Count > 0;
        selectedShips.Clear();
        RefreshSelectionData();
        NotifySelectionMembershipChanged(membershipChanged);
    }


    public int SelectShipsInScreenRect(
        Camera camera,
        Rect screenRect,
        bool addToSelection
    )
    {
        if (camera == null)
        {
            return 0;
        }

        RefreshSelectionData();
        List<ShipDestinationController> previousSelection =
            new List<ShipDestinationController>(selectedShips);

        List<ScreenSelectionCandidate> matchingCandidates =
            new List<ScreenSelectionCandidate>();
        ShipDestinationController[] candidates =
            FindObjectsByType<ShipDestinationController>(
                FindObjectsInactive.Exclude
            );

        foreach (ShipDestinationController candidate in candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 projectedPosition = camera.WorldToScreenPoint(
                candidate.transform.position
            );

            if (projectedPosition.z <= 0f)
            {
                continue;
            }

            Vector2 projectedScreenPosition = new Vector2(
                projectedPosition.x,
                projectedPosition.y
            );

            if (!IsPointInsideScreenRect(
                projectedScreenPosition,
                screenRect
            ))
            {
                continue;
            }

            matchingCandidates.Add(new ScreenSelectionCandidate(
                candidate,
                projectedScreenPosition
            ));
        }

        matchingCandidates.Sort(CompareScreenSelectionCandidates);

        if (!addToSelection)
        {
            selectedShips.Clear();
        }

        foreach (ScreenSelectionCandidate matchingCandidate
                 in matchingCandidates)
        {
            if (!selectedShips.Contains(matchingCandidate.ship))
            {
                selectedShips.Add(matchingCandidate.ship);
            }
        }

        RefreshSelectionData();
        NotifySelectionMembershipChanged(!HasSameMembership(previousSelection));
        return matchingCandidates.Count;
    }


    public bool TryPickShip(
        Camera camera,
        Vector2 mouseScreenPosition,
        out ShipDestinationController ship
    )
    {
        ship = null;
        lastPickedShip = null;
        lastPickScreenPosition = mouseScreenPosition;
        lastPickSuccessful = false;

        if (camera == null)
        {
            return false;
        }

        float closestDistanceSquared = selectionRadiusPixels
            * selectionRadiusPixels;
        ShipDestinationController[] candidates =
            FindObjectsByType<ShipDestinationController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        foreach (ShipDestinationController candidate in candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 projectedPosition = camera.WorldToScreenPoint(
                candidate.transform.position
            );

            if (projectedPosition.z <= 0f)
            {
                continue;
            }

            Vector2 projectedScreenPosition = new Vector2(
                projectedPosition.x,
                projectedPosition.y
            );
            float distanceSquared = (
                projectedScreenPosition - mouseScreenPosition
            ).sqrMagnitude;

            if (distanceSquared > closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            ship = candidate;
        }

        lastPickedShip = ship;
        lastPickSuccessful = ship != null;
        return lastPickSuccessful;
    }


    public bool TryGetSelectionNavigationY(out float navigationY)
    {
        RefreshSelectionData();

        if (selectedShips.Count == 0)
        {
            navigationY = 0f;
            return false;
        }

        float totalY = 0f;

        foreach (ShipDestinationController ship in selectedShips)
        {
            totalY += ship.transform.position.y;
        }

        navigationY = totalY / selectedShips.Count;
        return true;
    }


    private void RefreshSelectionData()
    {
        if (selectedShips == null)
        {
            selectedShips = new List<ShipDestinationController>();
        }

        uniqueShips.Clear();

        for (int index = 0; index < selectedShips.Count; index++)
        {
            ShipDestinationController ship = selectedShips[index];

            if (ship == null || !uniqueShips.Add(ship))
            {
                selectedShips.RemoveAt(index);
                index--;
            }
        }

        selectedCount = selectedShips.Count;
        primarySelectedShip = selectedCount > 0
            ? selectedShips[0]
            : null;
    }


    private bool HasSameMembership(
        IReadOnlyList<ShipDestinationController> previousSelection
    )
    {
        if (previousSelection == null
            || previousSelection.Count != selectedShips.Count)
        {
            return false;
        }

        foreach (ShipDestinationController ship in previousSelection)
        {
            if (!selectedShips.Contains(ship))
            {
                return false;
            }
        }

        return true;
    }


    private void NotifySelectionMembershipChanged(bool membershipChanged)
    {
        if (membershipChanged)
        {
            SelectionMembershipChanged?.Invoke();
        }
    }


    private static bool IsPointInsideScreenRect(
        Vector2 point,
        Rect screenRect
    )
    {
        return point.x >= screenRect.xMin
            && point.x <= screenRect.xMax
            && point.y >= screenRect.yMin
            && point.y <= screenRect.yMax;
    }


    private static int CompareScreenSelectionCandidates(
        ScreenSelectionCandidate first,
        ScreenSelectionCandidate second
    )
    {
        int horizontalComparison = first.screenPosition.x.CompareTo(
            second.screenPosition.x
        );

        if (horizontalComparison != 0)
        {
            return horizontalComparison;
        }

        int verticalComparison = second.screenPosition.y.CompareTo(
            first.screenPosition.y
        );

        if (verticalComparison != 0)
        {
            return verticalComparison;
        }

        return first.ship.GetEntityId().CompareTo(
            second.ship.GetEntityId()
        );
    }


    private void OnDrawGizmos()
    {
        if (selectedShips == null)
        {
            return;
        }

        foreach (ShipDestinationController ship in selectedShips)
        {
            if (ship == null)
            {
                continue;
            }

            Gizmos.color = ship == primarySelectedShip
                ? Color.yellow
                : Color.cyan;
            Gizmos.DrawWireSphere(ship.transform.position, 2f);
        }
    }
}
