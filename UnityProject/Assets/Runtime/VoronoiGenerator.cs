using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    internal static class VoronoiGenerator
    {
        #region constants
        private const float InsideEpsilon = 0.000001f;
        private const float DuplicatePointEpsilonSquared = 0.0000000001f;
        private const float MinBoundaryTolerance = 0.00001f;
        private const float BoundaryToleranceScale = 0.00001f;
        #endregion

        #region methods
        internal static void Generate(IReadOnlyList<Vector2> panelPolygonVertices, IReadOnlyList<Vector2> siteList, IReadOnlyList<DelaunayTriangle> triangleList, List<DestructibleCell> cellList)
        {
            List<int> neighborList = new List<int>();
            List<Vector2> currentVertices = new List<Vector2>(Mathf.Max(8, panelPolygonVertices.Count));
            List<Vector2> clippedVertices = new List<Vector2>(Mathf.Max(8, panelPolygonVertices.Count));

            float boundaryTolerance = CalculateBoundaryTolerance(panelPolygonVertices);

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

                currentVertices.Clear();
                for (int vertexIndex = 0; vertexIndex < panelPolygonVertices.Count; vertexIndex++)
                {
                    currentVertices.Add(panelPolygonVertices[vertexIndex]);
                }
                foreach (var neighborSiteIndex in neighborList)
                {
                    ClipToCloserHalfPlane(currentVertices, clippedVertices, siteList[siteIndex], siteList[neighborSiteIndex]);
                    (currentVertices, clippedVertices) = (clippedVertices, currentVertices);

                    if (currentVertices.Count == 0)
                    {
                        break;
                    }
                }

                DestructibleCell cell = cellList[siteIndex];
                CopyCleanCounterClockwisePolygon(currentVertices, cell.MutablePolygonVertices);
                cell.SetBoundary(SharesPanelBoundaryEdge(cell.PolygonVertices, panelPolygonVertices, boundaryTolerance));
                cellList[siteIndex] = cell;
            }
        }

        private static float CalculateBoundaryTolerance(IReadOnlyList<Vector2> panelPolygonVertices)
        {
            if (panelPolygonVertices.Count == 0)
            {
                return MinBoundaryTolerance;
            }

            Vector2 minimum = panelPolygonVertices[0];
            Vector2 maximum = panelPolygonVertices[0];
            for (int vertexIndex = 1; vertexIndex < panelPolygonVertices.Count; vertexIndex++)
            {
                minimum = Vector2.Min(minimum, panelPolygonVertices[vertexIndex]);
                maximum = Vector2.Max(maximum, panelPolygonVertices[vertexIndex]);
            }

            Vector2 size = maximum - minimum;
            return Mathf.Max(MinBoundaryTolerance, Mathf.Max(size.x, size.y) * BoundaryToleranceScale);
        }

        /// <summary>
        /// Tests the clipped cell against the actual panel outline. Bounded
        /// Voronoi regions can reach the outline even when their sites are not
        /// on the sample convex hull. A point contact alone is not a boundary edge.
        /// </summary>
        private static bool SharesPanelBoundaryEdge(IReadOnlyList<Vector2> polygonVertices, IReadOnlyList<Vector2> panelPolygonVertices, float tolerance)
        {
            if (polygonVertices.Count < 3 || panelPolygonVertices.Count < 3)
            {
                return false;
            }

            for (int panelEdgeIndex = 0; panelEdgeIndex < panelPolygonVertices.Count; panelEdgeIndex++)
            {
                Vector2 panelStart = panelPolygonVertices[panelEdgeIndex];
                Vector2 panelEdge = panelPolygonVertices[(panelEdgeIndex + 1) % panelPolygonVertices.Count] - panelStart;
                float panelEdgeLength = panelEdge.magnitude;
                if (panelEdgeLength <= tolerance)
                {
                    continue;
                }

                // Unit direction makes the cross products distances in panel-local
                // units, so the same tolerance works for long and short outline edges.
                Vector2 direction = panelEdge / panelEdgeLength;
                for (int cellEdgeIndex = 0; cellEdgeIndex < polygonVertices.Count; cellEdgeIndex++)
                {
                    Vector2 startOffset = polygonVertices[cellEdgeIndex] - panelStart;
                    Vector2 endOffset = polygonVertices[(cellEdgeIndex + 1) % polygonVertices.Count] - panelStart;
                    float startDistance = direction.x * startOffset.y - direction.y * startOffset.x;
                    float endDistance = direction.x * endOffset.y - direction.y * endOffset.x;
                    if (Mathf.Abs(startDistance) > tolerance || Mathf.Abs(endDistance) > tolerance)
                    {
                        continue;
                    }

                    // Intersect the projected segments, excluding zero-length edges,
                    // corner-only contacts and the extension beyond a panel segment.
                    float startProjection = Vector2.Dot(startOffset, direction);
                    float endProjection = Vector2.Dot(endOffset, direction);
                    float overlapStart = Mathf.Max(0f, Mathf.Min(startProjection, endProjection));
                    float overlapEnd = Mathf.Min(panelEdgeLength, Mathf.Max(startProjection, endProjection));
                    if (overlapEnd - overlapStart > tolerance)
                    {
                        return true;
                    }
                }
            }

            return false;
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

        private static void ClipToCloserHalfPlane(IReadOnlyList<Vector2> inputVertices, List<Vector2> outputVertices, Vector2 site, Vector2 neighborSite)
        {
            outputVertices.Clear();
            if (inputVertices.Count == 0)
            {
                return;
            }

            Vector2 planeNormal = neighborSite - site;
            if (planeNormal.sqrMagnitude <= DuplicatePointEpsilonSquared)
            {
                for (int pointIndex = 0; pointIndex < inputVertices.Count; pointIndex++)
                {
                    outputVertices.Add(inputVertices[pointIndex]);
                }

                return;
            }

            float planeOffset = (neighborSite.sqrMagnitude - site.sqrMagnitude) * 0.5f;
            Vector2 previousPoint = inputVertices[^1];
            float previousDistance = Vector2.Dot(previousPoint, planeNormal) - planeOffset;
            bool previousInside = previousDistance <= InsideEpsilon;

            for (int pointIndex = 0; pointIndex < inputVertices.Count; pointIndex++)
            {
                Vector2 currentPoint = inputVertices[pointIndex];
                float currentDistance = Vector2.Dot(currentPoint, planeNormal) - planeOffset;
                bool currentInside = currentDistance <= InsideEpsilon;

                if (currentInside != previousInside)
                {
                    float denominator = previousDistance - currentDistance;
                    if (Mathf.Abs(denominator) > Mathf.Epsilon)
                    {
                        float interpolation = previousDistance / denominator;
                        outputVertices.Add(Vector2.LerpUnclamped(previousPoint, currentPoint, interpolation));
                    }
                }

                if (currentInside)
                {
                    outputVertices.Add(currentPoint);
                }

                previousPoint = currentPoint;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }
        }

        private static void CopyCleanCounterClockwisePolygon(IReadOnlyList<Vector2> sourceVertices, List<Vector2> destinationVertices)
        {
            destinationVertices.Clear();

            for (int pointIndex = 0; pointIndex < sourceVertices.Count; pointIndex++)
            {
                Vector2 point = sourceVertices[pointIndex];
                if (destinationVertices.Count == 0 ||
                    (destinationVertices[^1] - point).sqrMagnitude > DuplicatePointEpsilonSquared)
                {
                    destinationVertices.Add(point);
                }
            }

            if (destinationVertices.Count > 1 &&
                (destinationVertices[0] - destinationVertices[^1]).sqrMagnitude <= DuplicatePointEpsilonSquared)
            {
                destinationVertices.RemoveAt(destinationVertices.Count - 1);
            }

            if (CalculateSignedAreaTwice(destinationVertices) < 0d)
            {
                destinationVertices.Reverse();
            }
        }

        private static double CalculateSignedAreaTwice(IReadOnlyList<Vector2> polygonVertices)
        {
            double areaTwice = 0d;
            for (int pointIndex = 0; pointIndex < polygonVertices.Count; pointIndex++)
            {
                Vector2 current = polygonVertices[pointIndex];
                Vector2 next = polygonVertices[(pointIndex + 1) % polygonVertices.Count];
                areaTwice += (double)current.x * next.y - (double)current.y * next.x;
            }

            return areaTwice;
        }
        #endregion
    }
}
