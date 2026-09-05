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
        if (ship == null)
        {
            ClearSelection();
            return;
        }

        selectedShips.Clear();
        selectedShips.Add(ship);
        RefreshSelectionData();
    }


    public void ToggleSelection(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        RefreshSelectionData();

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

        if (!selectedShips.Contains(ship))
        {
            selectedShips.Add(ship);
        }

        RefreshSelectionData();
    }


    public void RemoveSelection(ShipDestinationController ship)
    {
        if (ship == null)
        {
            return;
        }

        selectedShips.Remove(ship);
        RefreshSelectionData();
    }


    public void ClearSelection()
    {
        selectedShips.Clear();
        RefreshSelectionData();
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
