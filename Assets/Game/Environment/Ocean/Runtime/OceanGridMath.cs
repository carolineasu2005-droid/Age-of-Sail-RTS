using System;
using UnityEngine;

namespace AgeOfSailRTS.Environment.Ocean
{
    /// <summary>Logical state calculated for a fixed 3x3 ocean grid.</summary>
    public readonly struct OceanGridResult
    {
        public Vector2Int CurrentCell { get; }
        public Vector2 CellCenterXZ { get; }
        public Vector2 OceanRootXZ { get; }
        public Vector2 CameraOffsetXZ { get; }
        public Vector2 RecenterThresholdXZ { get; }

        internal OceanGridResult(
            Vector2Int currentCell,
            Vector2 cellCenterXZ,
            Vector2 cameraOffsetXZ,
            Vector2 recenterThresholdXZ)
        {
            CurrentCell = currentCell;
            CellCenterXZ = cellCenterXZ;
            OceanRootXZ = cellCenterXZ;
            CameraOffsetXZ = cameraOffsetXZ;
            RecenterThresholdXZ = recenterThresholdXZ;
        }
    }

    /// <summary>Pure calculations for the logical ocean cell; no scene state is read or changed.</summary>
    public static class OceanGridMath
    {
        public const int GridRadius = 1;
        public const int TileCount = 9;
        public const float TileSizeX = 623.2f;
        public const float TileSizeZ = 649.9934f;
        public const float DefaultRecenterThresholdRatio = 0.55f;

        public static OceanGridResult Evaluate(
            Vector2 cameraXZ,
            Vector2 oceanOriginXZ,
            Vector2Int currentCell,
            float tileSizeX = TileSizeX,
            float tileSizeZ = TileSizeZ,
            float recenterThresholdRatio = DefaultRecenterThresholdRatio)
        {
            RequireFinite(cameraXZ.x, nameof(cameraXZ));
            RequireFinite(cameraXZ.y, nameof(cameraXZ));
            RequireFinite(oceanOriginXZ.x, nameof(oceanOriginXZ));
            RequireFinite(oceanOriginXZ.y, nameof(oceanOriginXZ));
            RequirePositiveFinite(tileSizeX, nameof(tileSizeX));
            RequirePositiveFinite(tileSizeZ, nameof(tileSizeZ));

            // A ratio below 0.5 leaves gaps between valid ranges of neighboring cells.
            // A ratio of 1 or more would allow the camera past a whole tile before recentering.
            if (!IsFinite(recenterThresholdRatio) ||
                recenterThresholdRatio < 0.5f || recenterThresholdRatio >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(recenterThresholdRatio),
                    "The recenter threshold ratio must be in [0.5, 1).");
            }

            double thresholdX = (double)tileSizeX * recenterThresholdRatio;
            double thresholdZ = (double)tileSizeZ * recenterThresholdRatio;

            int cellX = ResolveCell(cameraXZ.x, oceanOriginXZ.x, currentCell.x, tileSizeX, thresholdX);
            int cellZ = ResolveCell(cameraXZ.y, oceanOriginXZ.y, currentCell.y, tileSizeZ, thresholdZ);

            double centerX = (double)oceanOriginXZ.x + (double)cellX * tileSizeX;
            double centerZ = (double)oceanOriginXZ.y + (double)cellZ * tileSizeZ;
            var center = new Vector2((float)centerX, (float)centerZ);
            var offset = new Vector2((float)(cameraXZ.x - centerX), (float)(cameraXZ.y - centerZ));
            var thresholds = new Vector2((float)thresholdX, (float)thresholdZ);

            return new OceanGridResult(new Vector2Int(cellX, cellZ), center, offset, thresholds);
        }

        private static int ResolveCell(
            double cameraCoordinate,
            double originCoordinate,
            int currentCell,
            double tileSize,
            double threshold)
        {
            double center = originCoordinate + currentCell * tileSize;
            double offset = cameraCoordinate - center;
            double steps = 0;

            if (offset > threshold)
                steps = Math.Floor((offset - threshold) / tileSize) + 1;
            else if (offset < -threshold)
                steps = -(Math.Floor((-offset - threshold) / tileSize) + 1);

            double nextCell = currentCell + steps;
            if (nextCell < int.MinValue || nextCell > int.MaxValue)
                throw new OverflowException("The resulting ocean cell is outside Vector2Int range.");

            return (int)nextCell;
        }

        private static void RequirePositiveFinite(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(parameterName, "Tile size must be positive and finite.");
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
                throw new ArgumentException("Coordinates must be finite.", parameterName);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
