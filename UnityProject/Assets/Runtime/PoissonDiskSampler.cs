using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    internal static class PoissonDiskSampler
    {
        #region fields
        private const int CandidateAttempts = 30;
        private static int[][] s_gridCache;
        private static readonly List<int> s_activePointIndexList = new List<int>();
        private static Unity.Mathematics.Random s_random;
        #endregion

        #region methods
        internal static void Generate(Vector2 panelSize, IReadOnlyList<Vector2> panelPolygon, float minDistance, int seed, int maxPointCount, List<Vector2> pointList,
            IReadOnlyList<Vector2> holePolygon = null)
        {
            if (panelSize.x <= 0f || panelSize.y <= 0f || panelPolygon.Count < 3)
            {
                return;
            }

            if (pointList.Capacity < maxPointCount)
            {
                pointList.Capacity = maxPointCount;
            }

            float gridCellSize = minDistance / Mathf.Sqrt(2f);
            int gridWidth = Mathf.CeilToInt(panelSize.x / gridCellSize);
            int gridHeight = Mathf.CeilToInt(panelSize.y / gridCellSize);
            int[][] grid = GetGrid(gridWidth, gridHeight);
            s_activePointIndexList.Clear();
            if (s_activePointIndexList.Capacity < maxPointCount)
            {
                s_activePointIndexList.Capacity = maxPointCount;
            }

            uint randomState = unchecked((uint)seed) + 0x9E3779B9u;
            s_random.state = randomState == 0u ? 1u : randomState;
            Vector2 halfPanelSize = panelSize * 0.5f;

            // Start Bridson's algorithm with one random active point inside the panel.
            Vector2 firstPoint;
            if (holePolygon != null)
            {
                firstPoint = SampleRingPoint(panelPolygon, holePolygon);
            }
            else
            {
                do
                {
                    firstPoint = new Vector2(
                        Mathf.Lerp(-halfPanelSize.x, halfPanelSize.x, s_random.NextFloat()),
                        Mathf.Lerp(-halfPanelSize.y, halfPanelSize.y, s_random.NextFloat()));
                }
                while (IsInsidePanel(firstPoint, halfPanelSize, panelPolygon) == false);
            }
            AddPoint(firstPoint, halfPanelSize, gridCellSize, gridWidth, gridHeight, pointList, s_activePointIndexList, grid);

            float minDistanceSquared = minDistance * minDistance;
            while (pointList.Count < maxPointCount)
            {
                if (s_activePointIndexList.Count == 0)
                {
                    // A thin annulus can exhaust the local Bridson front before
                    // reaching the far side. Try bounded global reseeding there.
                    bool reseeded = false;
                    if (holePolygon != null)
                    {
                        for (int attempt = 0; attempt < CandidateAttempts; attempt++)
                        {
                            Vector2 candidate = SampleRingPoint(panelPolygon, holePolygon);
                            if (IsFarEnough(candidate, halfPanelSize, gridCellSize, gridWidth, gridHeight, minDistanceSquared, pointList, grid))
                            {
                                AddPoint(candidate, halfPanelSize, gridCellSize, gridWidth, gridHeight, pointList, s_activePointIndexList, grid);
                                reseeded = true;
                                break;
                            }
                        }
                    }

                    if (!reseeded) break;
                }

                int activeListIndex = s_random.NextInt(0, s_activePointIndexList.Count);
                Vector2 sourcePoint = pointList[s_activePointIndexList[activeListIndex]];
                bool candidateAccepted = false;

                for (int attempt = 0; attempt < CandidateAttempts; attempt++)
                {
                    float angle = s_random.NextFloat() * Mathf.PI * 2f;
                    float radius = minDistance * (1f + s_random.NextFloat());
                    Vector2 candidate = sourcePoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                    if (IsInsidePanel(candidate, halfPanelSize, panelPolygon) == false ||
                        (holePolygon != null && IsInsideConvexPolygon(candidate, holePolygon)) ||
                        IsFarEnough(candidate, halfPanelSize, gridCellSize, gridWidth, gridHeight, minDistanceSquared, pointList, grid) == false)
                    {
                        continue;
                    }

                    AddPoint(candidate, halfPanelSize, gridCellSize, gridWidth, gridHeight, pointList, s_activePointIndexList, grid);
                    candidateAccepted = true;
                    break;
                }

                if (candidateAccepted == false)
                {
                    int lastIndex = s_activePointIndexList.Count - 1;
                    s_activePointIndexList[activeListIndex] = s_activePointIndexList[lastIndex];
                    s_activePointIndexList.RemoveAt(lastIndex);
                }
            }
        }

        private static Vector2 SampleRingPoint(IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner)
        {
            int edge = s_random.NextInt(0, outer.Count);
            int next = (edge + 1) % outer.Count;
            float along = s_random.NextFloat();
            Vector2 outerPoint = Vector2.Lerp(outer[edge], outer[next], along);
            Vector2 innerPoint = Vector2.Lerp(inner[edge], inner[next], along);
            // Stay strictly between matching polygon edges, even for narrow rings.
            return Vector2.Lerp(innerPoint, outerPoint, Mathf.Lerp(0.0001f, 0.9999f, s_random.NextFloat()));
        }

        private static int[][] GetGrid(int gridWidth, int gridHeight)
        {
            if (s_gridCache == null || s_gridCache.Length < gridWidth)
            {
                System.Array.Resize(ref s_gridCache, gridWidth);
            }

            for (int gridX = 0; gridX < gridWidth; gridX++)
            {
                if (s_gridCache[gridX] == null || s_gridCache[gridX].Length < gridHeight)
                {
                    s_gridCache[gridX] = new int[gridHeight];
                }
                else
                {
                    System.Array.Clear(s_gridCache[gridX], 0, gridHeight);
                }
            }

            return s_gridCache;
        }

        private static void AddPoint(Vector2 point, Vector2 halfPanelSize, float gridCellSize, int gridWidth, int gridHeight, List<Vector2> points, List<int> activePointIndices, int[][] grid)
        {
            int pointIndex = points.Count;
            points.Add(point);
            activePointIndices.Add(pointIndex);

            Vector2 gridPosition = (point + halfPanelSize) / gridCellSize;
            int gridX = Mathf.Clamp(Mathf.FloorToInt(gridPosition.x), 0, gridWidth - 1);
            int gridY = Mathf.Clamp(Mathf.FloorToInt(gridPosition.y), 0, gridHeight - 1);
            grid[gridX][gridY] = pointIndex + 1;
        }

        // TODO pass the shape to reduce the caculation
        private static bool IsInsidePanel(Vector2 point, Vector2 halfPanelSize, IReadOnlyList<Vector2> panelPolygon)
        {
            if (point.x < -halfPanelSize.x || point.x >= halfPanelSize.x ||
                point.y < -halfPanelSize.y || point.y >= halfPanelSize.y)
            {
                return false;
            }

            // Every interior point lies to the left of each counter-clockwise edge.
            return IsInsideConvexPolygon(point, panelPolygon);
        }

        private static bool IsInsideConvexPolygon(Vector2 point, IReadOnlyList<Vector2> panelPolygon)
        {
            for (int edgeIndex = 0; edgeIndex < panelPolygon.Count; edgeIndex++)
            {
                Vector2 start = panelPolygon[edgeIndex];
                Vector2 edge = panelPolygon[(edgeIndex + 1) % panelPolygon.Count] - start;
                Vector2 offset = point - start;
                if (edge.x * offset.y - edge.y * offset.x < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFarEnough(Vector2 candidate, Vector2 halfPanelSize, float gridCellSize, int gridWidth, int gridHeight, float minimumDistanceSquared, List<Vector2> points, int[][] grid)
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
                    int storedPointIndex = grid[gridX][gridY] - 1;
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
