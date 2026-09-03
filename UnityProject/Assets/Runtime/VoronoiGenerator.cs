using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    internal static class VoronoiGenerator
    {
        #region constants
        private const float InsideEpsilon = 0.000001f;
        private const float DuplicatePointEpsilonSquared = 0.0000000001f;
        #endregion

        #region methods
        internal static void Generate(IReadOnlyList<Vector2> panelPolygon, IReadOnlyList<Vector2> siteList, IReadOnlyList<DelaunayTriangle> triangleList, List<DestructibleCell> cellList)
        {
            List<int> neighborList = new List<int>();
            List<Vector2> currentPolygon = new List<Vector2>(Mathf.Max(8, panelPolygon.Count));
            List<Vector2> clippedPolygon = new List<Vector2>(Mathf.Max(8, panelPolygon.Count));

            MarkBoundaryCells(triangleList, cellList);

            for (int siteIndex = 0; siteIndex < siteList.Count; siteIndex++)
            {
                // Each cell needs its own constraints, including the two-site fallback.
                neighborList.Clear();
                CollectDelaunayNeighbors(siteIndex, triangleList, neighborList);

                // Collinear inputs have no Delaunay triangles, but their Voronoi
                // cells are still valid strips. Fall back to all other sites.
                if (neighborList.Count == 0 && siteList.Count > 1)
                {
                    for (int otherSiteIndex = 0; otherSiteIndex < siteList.Count; otherSiteIndex++)
                    {
                        if (otherSiteIndex != siteIndex)
                        {
                            neighborList.Add(otherSiteIndex);
                        }
                    }
                }

                currentPolygon.Clear();
                for (int vertexIndex = 0; vertexIndex < panelPolygon.Count; vertexIndex++)
                {
                    currentPolygon.Add(panelPolygon[vertexIndex]);
                }
                foreach (var neighborSiteIndex in neighborList)
                {
                    ClipToCloserHalfPlane(currentPolygon, clippedPolygon, siteList[siteIndex], siteList[neighborSiteIndex]);
                    (currentPolygon, clippedPolygon) = (clippedPolygon, currentPolygon);

                    if (currentPolygon.Count == 0)
                    {
                        break;
                    }
                }

                DestructibleCell cell = cellList[siteIndex];
                cell.Polygon ??= new List<Vector2>(currentPolygon.Count);
                CopyCleanCounterClockwisePolygon(currentPolygon, cell.Polygon);
                cellList[siteIndex] = cell;
            }
        }

        /// <summary>
        /// Marks cells whose Voronoi regions touch the panel outline. A region is
        /// unbounded exactly when its site lies on the convex hull of the sample
        /// points, which the Delaunay triangulation exposes as an edge referenced
        /// by a single triangle. The panel is convex and strictly contains every
        /// site, so unbounded regions are precisely the boundary cells and no
        /// positional check against the outline is required.
        /// </summary>
        private static void MarkBoundaryCells(IReadOnlyList<DelaunayTriangle> triangleList, List<DestructibleCell> cellList)
        {
            if (cellList == null || cellList.Count == 0)
            {
                return;
            }

            // Clear stale flags before recomputing for a fresh generation.
            for (int cellIndex = 0; cellIndex < cellList.Count; cellIndex++)
            {
                DestructibleCell cell = cellList[cellIndex];
                cell.IsBoundary = false;
                cellList[cellIndex] = cell;
            }

            // Fewer than three sites, or all sites collinear, produce no Delaunay
            // triangles. The fallback below then yields full-panel strips, so every
            // surviving cell touches the boundary.
            if (triangleList == null || triangleList.Count == 0)
            {
                for (int cellIndex = 0; cellIndex < cellList.Count; cellIndex++)
                {
                    DestructibleCell cell = cellList[cellIndex];
                    cell.IsBoundary = true;
                    cellList[cellIndex] = cell;
                }

                return;
            }

            Dictionary<long, int> edgeReferenceCount = new Dictionary<long, int>(triangleList.Count * 3);
            foreach (DelaunayTriangle triangle in triangleList)
            {
                AddEdgeReference(edgeReferenceCount, triangle.A, triangle.B);
                AddEdgeReference(edgeReferenceCount, triangle.B, triangle.C);
                AddEdgeReference(edgeReferenceCount, triangle.C, triangle.A);
            }

            foreach (KeyValuePair<long, int> pair in edgeReferenceCount)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                // Convex-hull edges are referenced by exactly one triangle.
                UnpackEdge(pair.Key, out int firstCellIndex, out int secondCellIndex);
                SetBoundaryCell(cellList, firstCellIndex);
                SetBoundaryCell(cellList, secondCellIndex);
            }
        }

        private static void AddEdgeReference(Dictionary<long, int> edgeReferenceCount, int firstCellIndex, int secondCellIndex)
        {
            long key = PackEdge(firstCellIndex, secondCellIndex);
            edgeReferenceCount.TryGetValue(key, out int count);
            edgeReferenceCount[key] = count + 1;
        }

        private static long PackEdge(int firstCellIndex, int secondCellIndex)
        {
            int minIndex = Mathf.Min(firstCellIndex, secondCellIndex);
            int maxIndex = Mathf.Max(firstCellIndex, secondCellIndex);
            // Cell indices are non-negative and fit in 32 bits, so a single long
            // keeps edge counting allocation-free and symmetric regardless of order.
            return ((long)minIndex << 32) | (uint)maxIndex;
        }

        private static void UnpackEdge(long key, out int firstCellIndex, out int secondCellIndex)
        {
            firstCellIndex = (int)(key >> 32);
            secondCellIndex = (int)(key & 0xFFFFFFFFL);
        }

        private static void SetBoundaryCell(List<DestructibleCell> cellList, int cellIndex)
        {
            DestructibleCell cell = cellList[cellIndex];
            cell.IsBoundary = true;
            // DestructibleCell is a value type, so persist the changed state.
            cellList[cellIndex] = cell;
        }

        private static void CollectDelaunayNeighbors(int siteIndex, IReadOnlyList<DelaunayTriangle> triangleList, List<int> neighborList)
        {
            for (int triangleIndex = 0; triangleIndex < triangleList.Count; triangleIndex++)
            {
                DelaunayTriangle triangle = triangleList[triangleIndex];
                if (triangle.A == siteIndex)
                {
                    AddUnique(neighborList, triangle.B);
                    AddUnique(neighborList, triangle.C);
                }
                else if (triangle.B == siteIndex)
                {
                    AddUnique(neighborList, triangle.A);
                    AddUnique(neighborList, triangle.C);
                }
                else if (triangle.C == siteIndex)
                {
                    AddUnique(neighborList, triangle.A);
                    AddUnique(neighborList, triangle.B);
                }
            }
        }

        private static void AddUnique(List<int> valueList, int value)
        {
            foreach (var v in valueList)
            {
                if (v == value)
                {
                    return;
                }
            }

            valueList.Add(value);
        }

        private static void ClipToCloserHalfPlane(IReadOnlyList<Vector2> inputPolygon, List<Vector2> outputPolygon, Vector2 site, Vector2 neighborSite)
        {
            outputPolygon.Clear();
            if (inputPolygon.Count == 0)
            {
                return;
            }

            Vector2 planeNormal = neighborSite - site;
            if (planeNormal.sqrMagnitude <= DuplicatePointEpsilonSquared)
            {
                for (int pointIndex = 0; pointIndex < inputPolygon.Count; pointIndex++)
                {
                    outputPolygon.Add(inputPolygon[pointIndex]);
                }

                return;
            }

            float planeOffset = (neighborSite.sqrMagnitude - site.sqrMagnitude) * 0.5f;
            Vector2 previousPoint = inputPolygon[^1];
            float previousDistance = Vector2.Dot(previousPoint, planeNormal) - planeOffset;
            bool previousInside = previousDistance <= InsideEpsilon;

            for (int pointIndex = 0; pointIndex < inputPolygon.Count; pointIndex++)
            {
                Vector2 currentPoint = inputPolygon[pointIndex];
                float currentDistance = Vector2.Dot(currentPoint, planeNormal) - planeOffset;
                bool currentInside = currentDistance <= InsideEpsilon;

                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;
                    if (Mathf.Abs(denominator) > Mathf.Epsilon)
                    {
                        float interpolation = previousDistance / denominator;
                        outputPolygon.Add(Vector2.LerpUnclamped(previousPoint, currentPoint, interpolation));
                    }
                }

                if (currentInside)
                {
                    outputPolygon.Add(currentPoint);
                }

                previousPoint = currentPoint;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }
        }

        private static void CopyCleanCounterClockwisePolygon(IReadOnlyList<Vector2> sourcePolygon, List<Vector2> destinationPolygon)
        {
            destinationPolygon.Clear();

            for (int pointIndex = 0; pointIndex < sourcePolygon.Count; pointIndex++)
            {
                Vector2 point = sourcePolygon[pointIndex];
                if (destinationPolygon.Count == 0 ||
                    (destinationPolygon[^1] - point).sqrMagnitude > DuplicatePointEpsilonSquared)
                {
                    destinationPolygon.Add(point);
                }
            }

            if (destinationPolygon.Count > 1 &&
                (destinationPolygon[0] - destinationPolygon[^1]).sqrMagnitude <= DuplicatePointEpsilonSquared)
            {
                destinationPolygon.RemoveAt(destinationPolygon.Count - 1);
            }

            if (CalculateSignedAreaTwice(destinationPolygon) < 0d)
            {
                destinationPolygon.Reverse();
            }
        }

        private static double CalculateSignedAreaTwice(IReadOnlyList<Vector2> polygon)
        {
            double areaTwice = 0d;
            for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
            {
                Vector2 current = polygon[pointIndex];
                Vector2 next = polygon[(pointIndex + 1) % polygon.Count];
                areaTwice += (double)current.x * next.y - (double)current.y * next.x;
            }

            return areaTwice;
        }
        #endregion
    }
}
