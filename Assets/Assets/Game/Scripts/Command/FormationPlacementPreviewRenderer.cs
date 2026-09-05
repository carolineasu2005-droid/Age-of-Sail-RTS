using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class FormationPlacementPreviewRenderer : MonoBehaviour
{
    private const float GhostOpacity = 0.35f;


    private sealed class GhostMesh
    {
        public Mesh mesh;
        public Material material;
        public int subMeshIndex;
        public Matrix4x4 shipLocalMatrix;
        public float localX;
        public float localZ;
        public Vector3 shipScale;
        public Matrix4x4 worldMatrix;
    }


    private readonly List<GhostMesh> ghostMeshes = new();
    private readonly List<Material> ghostMaterials = new();
    private readonly Dictionary<Material, Material> materialCopies = new();

    private FormationGeometrySnapshot geometrySnapshot;
    private Camera previewCamera;
    private bool previewActive;


    public bool IsPreviewActive => previewActive;

    public int GhostMeshCount => ghostMeshes.Count;


    public void ShowPreview(
        FormationGeometrySnapshot snapshot,
        Vector3 formationCenter,
        float formationHeading,
        Camera camera
    )
    {
        if (snapshot == null || camera == null)
        {
            HidePreview();
            return;
        }

        if (geometrySnapshot != snapshot)
        {
            BuildGhostMeshes(snapshot);
            geometrySnapshot = snapshot;
        }

        previewCamera = camera;
        previewActive = true;
        UpdatePreviewPose(formationCenter, formationHeading);
    }


    public void UpdatePreviewPose(
        Vector3 formationCenter,
        float formationHeading
    )
    {
        if (!previewActive)
        {
            return;
        }

        Quaternion formationRotation = Quaternion.Euler(
            0f,
            formationHeading,
            0f
        );

        foreach (GhostMesh ghostMesh in ghostMeshes)
        {
            Vector3 targetSlotPosition =
                FormationGeometrySnapshot.GetSlotWorldPosition(
                    formationCenter,
                    formationHeading,
                    ghostMesh.localX,
                    ghostMesh.localZ
                );
            Matrix4x4 shipMatrix = Matrix4x4.TRS(
                targetSlotPosition,
                formationRotation,
                ghostMesh.shipScale
            );
            ghostMesh.worldMatrix = shipMatrix
                * ghostMesh.shipLocalMatrix;
        }
    }


    public void HidePreview()
    {
        previewActive = false;
        previewCamera = null;
        geometrySnapshot = null;
        ClearGhostMeshes();
    }


    private void LateUpdate()
    {
        if (!previewActive || previewCamera == null)
        {
            return;
        }

        foreach (GhostMesh ghostMesh in ghostMeshes)
        {
            if (ghostMesh.mesh == null || ghostMesh.material == null)
            {
                continue;
            }

            Graphics.DrawMesh(
                ghostMesh.mesh,
                ghostMesh.worldMatrix,
                ghostMesh.material,
                0,
                previewCamera,
                ghostMesh.subMeshIndex,
                null,
                ShadowCastingMode.Off,
                false
            );
        }
    }


    private void OnDisable()
    {
        HidePreview();
    }


    private void OnDestroy()
    {
        ClearGhostMeshes();
    }


    private void BuildGhostMeshes(FormationGeometrySnapshot snapshot)
    {
        ClearGhostMeshes();

        foreach (FormationGeometryMember member in snapshot.Members)
        {
            ShipDestinationController ship = member.Ship;

            if (ship == null)
            {
                continue;
            }

            MeshFilter[] meshFilters = ship.GetComponentsInChildren<MeshFilter>(
                false
            );

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                MeshRenderer meshRenderer =
                    meshFilter.GetComponent<MeshRenderer>();

                if (mesh == null || meshRenderer == null)
                {
                    continue;
                }

                Material[] sourceMaterials = meshRenderer.sharedMaterials;

                for (int subMeshIndex = 0;
                     subMeshIndex < mesh.subMeshCount;
                     subMeshIndex++)
                {
                    Material sourceMaterial = sourceMaterials.Length > 0
                        ? sourceMaterials[Mathf.Min(
                            subMeshIndex,
                            sourceMaterials.Length - 1
                        )]
                        : null;
                    Material ghostMaterial = GetGhostMaterial(sourceMaterial);

                    if (ghostMaterial == null)
                    {
                        continue;
                    }

                    ghostMeshes.Add(new GhostMesh
                    {
                        mesh = mesh,
                        material = ghostMaterial,
                        subMeshIndex = subMeshIndex,
                        shipLocalMatrix = ship.transform.worldToLocalMatrix
                            * meshFilter.transform.localToWorldMatrix,
                        localX = member.LocalX,
                        localZ = member.LocalZ,
                        shipScale = ship.transform.lossyScale
                    });
                }
            }
        }
    }


    private Material GetGhostMaterial(Material sourceMaterial)
    {
        if (sourceMaterial != null
            && materialCopies.TryGetValue(
                sourceMaterial,
                out Material existingCopy
            ))
        {
            return existingCopy;
        }

        Material ghostMaterial = CreateGhostMaterial(sourceMaterial);

        if (ghostMaterial == null)
        {
            return null;
        }

        ghostMaterials.Add(ghostMaterial);

        if (sourceMaterial != null)
        {
            materialCopies.Add(sourceMaterial, ghostMaterial);
        }

        return ghostMaterial;
    }


    private static Material CreateGhostMaterial(Material sourceMaterial)
    {
        Shader ghostShader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (ghostShader == null)
        {
            ghostShader = Shader.Find("Unlit/Transparent");
        }

        Material ghostMaterial = ghostShader != null
            ? new Material(ghostShader)
            : sourceMaterial != null
                ? new Material(sourceMaterial)
                : null;

        if (ghostMaterial == null)
        {
            return null;
        }

        Texture sourceTexture = sourceMaterial != null
            ? sourceMaterial.mainTexture
            : null;
        Color sourceColor = GetSourceColor(sourceMaterial);
        sourceColor.a = GhostOpacity;

        if (ghostMaterial.HasProperty("_BaseMap"))
        {
            ghostMaterial.SetTexture("_BaseMap", sourceTexture);
        }

        if (ghostMaterial.HasProperty("_MainTex"))
        {
            ghostMaterial.SetTexture("_MainTex", sourceTexture);
        }

        if (ghostMaterial.HasProperty("_BaseColor"))
        {
            ghostMaterial.SetColor("_BaseColor", sourceColor);
        }

        if (ghostMaterial.HasProperty("_Color"))
        {
            ghostMaterial.SetColor("_Color", sourceColor);
        }

        ghostMaterial.SetFloat("_Surface", 1f);
        ghostMaterial.SetFloat("_Blend", 0f);
        ghostMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        ghostMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        ghostMaterial.SetFloat("_ZWrite", 0f);
        ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
        return ghostMaterial;
    }


    private static Color GetSourceColor(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return Color.white;
        }

        if (sourceMaterial.HasProperty("_BaseColor"))
        {
            return sourceMaterial.GetColor("_BaseColor");
        }

        return sourceMaterial.HasProperty("_Color")
            ? sourceMaterial.GetColor("_Color")
            : Color.white;
    }


    private void ClearGhostMeshes()
    {
        ghostMeshes.Clear();
        materialCopies.Clear();

        foreach (Material ghostMaterial in ghostMaterials)
        {
            if (ghostMaterial != null)
            {
                Destroy(ghostMaterial);
            }
        }

        ghostMaterials.Clear();
    }
}
