using UnityEngine;

public class ShipArtDefinition : MonoBehaviour
{
    [Header("Ship Art References")]

    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private Transform waterlineReference;

    [SerializeField]
    private Transform centerReference;

    [SerializeField]
    private Transform bowReference;

    [SerializeField]
    private Transform sternReference;

    [SerializeField]
    private Transform portReference;

    [SerializeField]
    private Transform starboardReference;

    [SerializeField]
    private Transform deckReference;


    public Transform VisualRoot => visualRoot;

    public Transform WaterlineReference => waterlineReference;

    public Transform CenterReference => centerReference;

    public Transform BowReference => bowReference;

    public Transform SternReference => sternReference;

    public Transform PortReference => portReference;

    public Transform StarboardReference => starboardReference;

    public Transform DeckReference => deckReference;


    public bool TryGetLength(out float length)
    {
        length = 0f;

        if (bowReference == null || sternReference == null)
        {
            return false;
        }

        Vector3 bowLocalPosition = transform.InverseTransformPoint(
            bowReference.position
        );
        Vector3 sternLocalPosition = transform.InverseTransformPoint(
            sternReference.position
        );

        length = Mathf.Abs(bowLocalPosition.z - sternLocalPosition.z);
        return true;
    }


    public bool TryGetBeam(out float beam)
    {
        beam = 0f;

        if (portReference == null || starboardReference == null)
        {
            return false;
        }

        Vector3 portLocalPosition = transform.InverseTransformPoint(
            portReference.position
        );
        Vector3 starboardLocalPosition = transform.InverseTransformPoint(
            starboardReference.position
        );

        beam = Mathf.Abs(starboardLocalPosition.x - portLocalPosition.x);
        return true;
    }
}
