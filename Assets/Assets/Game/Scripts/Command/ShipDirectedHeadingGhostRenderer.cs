using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws captured ship meshes only. It creates no ship, collider, or movement component.
internal sealed class ShipDirectedHeadingGhostRenderer
{
    private const float GhostOpacity = 0.35f;

    private sealed class GhostMesh
    {
        public Mesh Mesh;
        public Material Material;
        public int SubMeshIndex;
        public Matrix4x4 ShipLocalMatrix;
        public Matrix4x4 WorldMatrix;
    }

    private readonly List<GhostMesh> meshes = new();
    private readonly List<Material> materialCopies = new();
    private readonly Dictionary<Material, Material> materialsBySource = new();
    private Vector3 shipScale;

    public bool IsVisible { get; private set; }
    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; } = Quaternion.identity;

    public void Show(ShipDestinationController ship)
    {
        Hide();
        if (ship == null)
        {
            return;
        }

        shipScale = ship.transform.lossyScale;
        foreach (MeshFilter filter in ship.GetComponentsInChildren<MeshFilter>(false))
        {
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !renderer.enabled)
            {
                continue;
            }

            Material[] sourceMaterials = renderer.sharedMaterials;
            for (int index = 0; index < mesh.subMeshCount; index++)
            {
                Material source = sourceMaterials.Length > 0
                    ? sourceMaterials[Mathf.Min(index, sourceMaterials.Length - 1)]
                    : null;
                Material copy = GetGhostMaterial(source);
                if (copy == null)
                {
                    continue;
                }

                meshes.Add(new GhostMesh
                {
                    Mesh = mesh,
                    Material = copy,
                    SubMeshIndex = index,
                    ShipLocalMatrix = ship.transform.worldToLocalMatrix
                        * filter.transform.localToWorldMatrix
                });
            }
        }

        IsVisible = true;
    }

    public void SetPose(Vector3 position, float heading)
    {
        if (!IsVisible)
        {
            return;
        }

        Position = position;
        Rotation = Quaternion.Euler(0f, heading, 0f);
        Matrix4x4 rootMatrix = Matrix4x4.TRS(Position, Rotation, shipScale);
        foreach (GhostMesh mesh in meshes)
        {
            mesh.WorldMatrix = rootMatrix * mesh.ShipLocalMatrix;
        }
    }

    public void Draw()
    {
        if (!IsVisible)
        {
            return;
        }

        foreach (GhostMesh mesh in meshes)
        {
            if (mesh.Mesh == null || mesh.Material == null)
            {
                continue;
            }

            Graphics.DrawMesh(
                mesh.Mesh, mesh.WorldMatrix, mesh.Material, 0, null,
                mesh.SubMeshIndex, null, ShadowCastingMode.Off, false);
        }
    }

    public void Hide()
    {
        IsVisible = false;
        Position = default;
        Rotation = Quaternion.identity;
        meshes.Clear();
        materialsBySource.Clear();
        foreach (Material material in materialCopies)
        {
            if (material == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }

        materialCopies.Clear();
    }

    private Material GetGhostMaterial(Material source)
    {
        if (source != null && materialsBySource.TryGetValue(source, out Material copy))
        {
            return copy;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        Material material = shader != null
            ? new Material(shader)
            : source != null ? new Material(source) : null;
        if (material == null)
        {
            return null;
        }

        Texture texture = source != null ? source.mainTexture : null;
        Color color = Color.white;
        if (source != null)
        {
            if (source.HasProperty("_BaseColor"))
            {
                color = source.GetColor("_BaseColor");
            }
            else if (source.HasProperty("_Color"))
            {
                color = source.GetColor("_Color");
            }
        }

        color.a = GhostOpacity;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        materialCopies.Add(material);
        if (source != null)
        {
            materialsBySource.Add(source, material);
        }

        return material;
    }
}
