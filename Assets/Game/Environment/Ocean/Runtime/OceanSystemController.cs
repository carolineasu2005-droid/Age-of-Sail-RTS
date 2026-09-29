using System;
using System.Text;
using UnityEngine;

namespace AgeOfSailRTS.Environment.Ocean
{
    /// <summary>Positions the fixed OceanRoot grid from the camera's logical X/Z cell.</summary>
    [DisallowMultipleComponent]
    public sealed class OceanSystemController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera followCamera;
        [SerializeField] private Transform oceanRoot;

        [Header("Grid")]
        [SerializeField] private float tileSizeX = OceanGridMath.TileSizeX;
        [SerializeField] private float tileSizeZ = OceanGridMath.TileSizeZ;
        [SerializeField] private int gridRadius = OceanGridMath.GridRadius;

        [Header("Recenter")]
        [SerializeField] private float recenterThresholdRatio = OceanGridMath.DefaultRecenterThresholdRatio;

        [Header("World")]
        [SerializeField] private Vector2 oceanOriginXZ;
        [SerializeField] private float seaLevelY = -0.21f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private Vector2Int currentCell;
        private bool warnedMissingCamera;
        private bool warnedMissingRoot;
        private bool warnedInvalidOwnership;
        private bool warnedInvalidConfiguration;
        private bool warnedInvalidEvaluation;
        private bool hasGridResult;
        private OceanGridResult lastGridResult;

        public Vector2Int CurrentCell { get { return currentCell; } }

        private void Start()
        {
            EvaluateAndApply();
        }

        private void LateUpdate()
        {
            EvaluateAndApply();
        }

        private void EvaluateAndApply()
        {
            hasGridResult = false;

            if (followCamera == null)
            {
                WarnOnce(ref warnedMissingCamera,
                    "FollowCamera is not assigned; OceanRoot will not move.");
                return;
            }

            if (oceanRoot == null)
            {
                WarnOnce(ref warnedMissingRoot,
                    "OceanRoot is not assigned; ocean recentering is disabled.");
                return;
            }

            if (followCamera.transform == oceanRoot || followCamera.transform.IsChildOf(oceanRoot))
            {
                WarnOnce(ref warnedInvalidOwnership,
                    "FollowCamera must not be inside OceanRoot; moving OceanRoot would move the camera.");
                return;
            }

            if (!ConfigurationIsValid())
            {
                WarnOnce(ref warnedInvalidConfiguration,
                    "Invalid ocean grid configuration: gridRadius must be 1, tile sizes must be positive and finite, " +
                    "threshold ratio must be in [0.5, 1), and origin/sea level must be finite.");
                return;
            }

            Vector3 cameraPosition = followCamera.transform.position;
            OceanGridResult result;
            try
            {
                result = OceanGridMath.Evaluate(
                    new Vector2(cameraPosition.x, cameraPosition.z),
                    oceanOriginXZ,
                    currentCell,
                    tileSizeX,
                    tileSizeZ,
                    recenterThresholdRatio);
            }
            catch (ArgumentException exception)
            {
                WarnOnce(ref warnedInvalidEvaluation, "Ocean grid evaluation failed: " + exception.Message);
                return;
            }
            catch (OverflowException exception)
            {
                WarnOnce(ref warnedInvalidEvaluation, "Ocean grid evaluation failed: " + exception.Message);
                return;
            }

            if (!IsFinite(result.OceanRootXZ.x) || !IsFinite(result.OceanRootXZ.y))
            {
                WarnOnce(ref warnedInvalidEvaluation,
                    "Ocean grid evaluation produced a position outside the float range.");
                return;
            }

            lastGridResult = result;
            hasGridResult = true;

            Vector3 targetPosition = new Vector3(result.OceanRootXZ.x, seaLevelY, result.OceanRootXZ.y);
            bool cellChanged = result.CurrentCell != currentCell;
            currentCell = result.CurrentCell;

            Vector3 rootPosition = oceanRoot.position;
            if (cellChanged || rootPosition.x != targetPosition.x ||
                rootPosition.y != targetPosition.y || rootPosition.z != targetPosition.z)
            {
                oceanRoot.position = targetPosition;
            }

            if (showDebugInfo && cellChanged)
                Debug.Log("Ocean grid cell: " + currentCell, this);
        }

        private void OnGUI()
        {
            if (!showDebugInfo)
                return;

            GUI.Box(new Rect(12f, 12f, 390f, 240f), "Ocean Grid");
            GUI.Label(new Rect(22f, 36f, 370f, 206f), BuildDebugText());
        }

        private string BuildDebugText()
        {
            var text = new StringBuilder(320);
            text.AppendLine("CurrentCell: " + currentCell);

            if (followCamera == null)
            {
                text.AppendLine("FollowCamera: MISSING");
            }
            else
            {
                Vector3 cameraPosition = followCamera.transform.position;
                text.AppendLine("Camera X/Z: " + new Vector2(cameraPosition.x, cameraPosition.z).ToString("F4"));
            }

            if (hasGridResult)
            {
                text.AppendLine("Cell center X/Z: " + lastGridResult.CellCenterXZ.ToString("F4"));
                text.AppendLine("Camera offset X/Z: " + lastGridResult.CameraOffsetXZ.ToString("F4"));
                text.AppendLine("Threshold X: " + lastGridResult.RecenterThresholdXZ.x.ToString("F4"));
                text.AppendLine("Threshold Z: " + lastGridResult.RecenterThresholdXZ.y.ToString("F4"));
            }
            else
            {
                text.AppendLine("Cell center X/Z: unavailable");
                text.AppendLine("Camera offset X/Z: unavailable");
                text.AppendLine("Threshold X/Z: unavailable");
            }

            text.AppendLine(oceanRoot == null
                ? "OceanRoot: MISSING"
                : "OceanRoot world: " + oceanRoot.position.ToString("F4"));
            text.AppendLine("TileSize X/Z: " + tileSizeX.ToString("F4") + " / " + tileSizeZ.ToString("F4"));
            text.Append("SeaLevelY: " + seaLevelY.ToString("F4"));
            return text.ToString();
        }

        private bool ConfigurationIsValid()
        {
            return gridRadius == OceanGridMath.GridRadius &&
                   IsPositiveFinite(tileSizeX) && IsPositiveFinite(tileSizeZ) &&
                   IsFinite(recenterThresholdRatio) &&
                   recenterThresholdRatio >= 0.5f && recenterThresholdRatio < 1f &&
                   IsFinite(oceanOriginXZ.x) && IsFinite(oceanOriginXZ.y) &&
                   IsFinite(seaLevelY);
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void WarnOnce(ref bool alreadyWarned, string message)
        {
            if (alreadyWarned)
                return;

            alreadyWarned = true;
            Debug.LogWarning("OceanSystemController: " + message, this);
        }
    }
}
