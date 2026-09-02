using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public static class VoronoiGenerator
    {
        #region constants
        private const float InsideEpsilon = 0.000001f;
        private const float DuplicatePointEpsilonSquared = 0.0000000001f;
        #endregion

        #region methods
        internal static void Generate(Vector2 panelSize, IReadOnlyList<Vector2> siteList, IReadOnlyList<DelaunayTriangle> triangleList, List<DestructibleCell> cellList)
        {
            List<int> neighborList = new List<int>();
            List<Vector2> currentPolygon = new List<Vector2>(8);
            List<Vector2> clippedPolygon = new List<Vector2>(8);
            Vector2 halfPanelSize = panelSize * 0.5f;

            for (int siteIndex = 0; siteIndex < siteList.Count; siteIndex++)
            {
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

                SetPanelPolygon(currentPolygon, halfPanelSize);
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

        private static void SetPanelPolygon(List<Vector2> polygon, Vector2 halfPanelSize)
        {
            polygon.Clear();
            polygon.Add(new Vector2(-halfPanelSize.x, -halfPanelSize.y));
            polygon.Add(new Vector2(halfPanelSize.x, -halfPanelSize.y));
            polygon.Add(new Vector2(halfPanelSize.x, halfPanelSize.y));
            polygon.Add(new Vector2(-halfPanelSize.x, halfPanelSize.y));
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
