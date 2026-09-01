using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public static class PoissonDiskSampler
    {
        #region fields
        private const int CandidateAttempts = 30;
        #endregion

        #region methods
        public static List<Vector2> Generate(Vector2 panelSize, float minDistance, int seed, int maxPointCount)
        {
            var points = new List<Vector2>();
            if (panelSize.x <= 0f || panelSize.y <= 0f || minDistance <= 0f || maxPointCount <= 0)
            {
                return points;
            }

            float gridCellSize = minDistance / Mathf.Sqrt(2f);
            int gridWidth = Mathf.CeilToInt(panelSize.x / gridCellSize);
            int gridHeight = Mathf.CeilToInt(panelSize.y / gridCellSize);
            var grid = new int[gridWidth * gridHeight]; // TODO GC
            var activePointIndices = new List<int>();
            var random = new System.Random(seed); // TODO GC
            Vector2 halfPanelSize = panelSize * 0.5f;

            // Start Bridson's algorithm with one random active point inside the panel.
            Vector2 firstPoint = new Vector2(
                Mathf.Lerp(-halfPanelSize.x, halfPanelSize.x, (float)random.NextDouble()),
                Mathf.Lerp(-halfPanelSize.y, halfPanelSize.y, (float)random.NextDouble()));
            AddPoint(firstPoint, halfPanelSize, gridCellSize, gridWidth, points, activePointIndices, grid);

            float minDistanceSquared = minDistance * minDistance;
            while (activePointIndices.Count > 0 && points.Count < maxPointCount)
            {
                int activeListIndex = random.Next(activePointIndices.Count);
                Vector2 sourcePoint = points[activePointIndices[activeListIndex]];
                bool candidateAccepted = false;

                for (int attempt = 0; attempt < CandidateAttempts; attempt++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float radius = minDistance * (1f + (float)random.NextDouble());
                    Vector2 candidate = sourcePoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    if (IsInsidePanel(candidate, halfPanelSize) == false ||
                        IsFarEnough(candidate, halfPanelSize, gridCellSize, gridWidth, gridHeight, minDistanceSquared, points, grid) == false)
                    {
                        continue;
                    }

                    AddPoint(candidate, halfPanelSize, gridCellSize, gridWidth, points, activePointIndices, grid);
                    candidateAccepted = true;
                    break;
                }

                if (candidateAccepted == false)
                {
                    int lastIndex = activePointIndices.Count - 1;
                    activePointIndices[activeListIndex] = activePointIndices[lastIndex];
                    activePointIndices.RemoveAt(lastIndex);
                }
            }

            return points;
        }

        private static void AddPoint(Vector2 point, Vector2 halfPanelSize, float gridCellSize, int gridWidth, List<Vector2> points, List<int> activePointIndices, int[] grid)
        {
            int pointIndex = points.Count;
            points.Add(point);
            activePointIndices.Add(pointIndex);

            Vector2 gridPosition = (point + halfPanelSize) / gridCellSize;
            int gridX = Mathf.FloorToInt(gridPosition.x);
            int gridY = Mathf.FloorToInt(gridPosition.y);
            grid[gridY * gridWidth + gridX] = pointIndex + 1;
        }

        private static bool IsInsidePanel(Vector2 point, Vector2 halfPanelSize)
        {
            return point.x >= -halfPanelSize.x && point.x < halfPanelSize.x &&
                   point.y >= -halfPanelSize.y && point.y < halfPanelSize.y;
        }

        private static bool IsFarEnough(Vector2 candidate, Vector2 halfPanelSize, float gridCellSize, int gridWidth, int gridHeight, float minimumDistanceSquared, List<Vector2> points, int[] grid)
        {
            Vector2 gridPosition = (candidate + halfPanelSize) / gridCellSize;
            int candidateGridX = Mathf.FloorToInt(gridPosition.x);
            int candidateGridY = Mathf.FloorToInt(gridPosition.y);

            int minGridX = Mathf.Max(0, candidateGridX - 2);
            int maxGridX = Mathf.Min(gridWidth - 1, candidateGridX + 2);
            int minGridY = Mathf.Max(0, candidateGridY - 2);
            int maxGridY = Mathf.Min(gridHeight - 1, candidateGridY + 2);

            for (int gridY = minGridY; gridY <= maxGridY; gridY++)
            {
                for (int gridX = minGridX; gridX <= maxGridX; gridX++)
                {
                    int storedPointIndex = grid[gridY * gridWidth + gridX] - 1;
                    if (storedPointIndex >= 0 && (points[storedPointIndex] - candidate).sqrMagnitude < minimumDistanceSquared)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        #endregion
    }
}
