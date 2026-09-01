using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public static class DelaunayTriangulator
    {
        #region methods
        public static void Generate(IReadOnlyList<Vector2> siteList, List<DelaunayTriangle> outputList)
        {
            if (siteList == null)
            {
                throw new System.ArgumentNullException(nameof(siteList));
            }

            if (outputList == null)
            {
                throw new System.ArgumentNullException(nameof(outputList));
            }

            outputList.Clear();
            if (siteList.Count < 3)
            {
                return;
            }

            List<Vector2> pointList = new List<Vector2>(siteList.Count + 3);
            Vector2 minimum = siteList[0];
            Vector2 maximum = siteList[0];

            for (int siteIndex = 0; siteIndex < siteList.Count; siteIndex++)
            {
                Vector2 site = siteList[siteIndex];
                if (IsFinite(site) == false)
                {
                    throw new System.ArgumentException("Delaunay sites must contain only finite coordinates.", nameof(siteList));
                }

                pointList.Add(site);
                minimum = Vector2.Min(minimum, site);
                maximum = Vector2.Max(maximum, site);
            }

            float extent = Mathf.Max(maximum.x - minimum.x, maximum.y - minimum.y);
            if (extent <= 0f)
            {
                return;
            }

            Vector2 center = (minimum + maximum) * 0.5f;
            float superExtent = extent * 32f;
            int firstSuperVertexIndex = pointList.Count;
            pointList.Add(center + new Vector2(-superExtent, -superExtent));
            pointList.Add(center + new Vector2(superExtent, -superExtent));
            pointList.Add(center + new Vector2(0f, superExtent));

            List<WorkingTriangle> triangleList = new List<WorkingTriangle>(siteList.Count * 2 + 1)
            {
                new WorkingTriangle(firstSuperVertexIndex, firstSuperVertexIndex + 1, firstSuperVertexIndex + 2)
            };
            List<Edge> cavityBoundary = new List<Edge>();

            // Bowyer-Watson insertion. Shared edges are removed, leaving the
            // boundary of the cavity that must connect to the new site.
            for (int siteIndex = 0; siteIndex < siteList.Count; siteIndex++)
            {
                cavityBoundary.Clear();

                for (int triangleIndex = triangleList.Count - 1; triangleIndex >= 0; triangleIndex--)
                {
                    WorkingTriangle triangle = triangleList[triangleIndex];
                    if (IsInsideCircumcircle(pointList[siteIndex], triangle, pointList) == false)
                    {
                        continue;
                    }

                    AddOrRemoveBoundaryEdge(cavityBoundary, triangle.A, triangle.B);
                    AddOrRemoveBoundaryEdge(cavityBoundary, triangle.B, triangle.C);
                    AddOrRemoveBoundaryEdge(cavityBoundary, triangle.C, triangle.A);
                    triangleList.RemoveAt(triangleIndex);
                }

                for (int edgeIndex = 0; edgeIndex < cavityBoundary.Count; edgeIndex++)
                {
                    Edge edge = cavityBoundary[edgeIndex];
                    AddCounterClockwiseTriangle(edge.A, edge.B, siteIndex, pointList, triangleList);
                }
            }

            int estimatedTriangleCount = Mathf.Max(0, siteList.Count * 2 - 2);
            if (outputList.Capacity < estimatedTriangleCount)
            {
                outputList.Capacity = estimatedTriangleCount;
            }

            for (int triangleIndex = 0; triangleIndex < triangleList.Count; triangleIndex++)
            {
                WorkingTriangle triangle = triangleList[triangleIndex];
                if (triangle.A >= firstSuperVertexIndex ||
                    triangle.B >= firstSuperVertexIndex ||
                    triangle.C >= firstSuperVertexIndex)
                {
                    continue;
                }

                outputList.Add(new DelaunayTriangle(triangle.A, triangle.B, triangle.C));
            }
        }

        private static void AddOrRemoveBoundaryEdge(List<Edge> edgeList, int a, int b)
        {
            for (int edgeIndex = 0; edgeIndex < edgeList.Count; edgeIndex++)
            {
                Edge edge = edgeList[edgeIndex];
                if ((edge.A == a && edge.B == b) || (edge.A == b && edge.B == a))
                {
                    edgeList.RemoveAt(edgeIndex);
                    return;
                }
            }

            edgeList.Add(new Edge(a, b));
        }

        private static void AddCounterClockwiseTriangle(int a, int b, int c, IReadOnlyList<Vector2> pointList, List<WorkingTriangle> triangleList)
        {
            double signedAreaTwice = Cross(pointList[a], pointList[b], pointList[c]);
            if (System.Math.Abs(signedAreaTwice) <= 1e-12)
            {
                return;
            }

            triangleList.Add(signedAreaTwice > 0d
                ? new WorkingTriangle(a, b, c)
                : new WorkingTriangle(b, a, c));
        }

        private static bool IsInsideCircumcircle(Vector2 point, WorkingTriangle triangle, IReadOnlyList<Vector2> pointList)
        {
            Vector2 a = pointList[triangle.A];
            Vector2 b = pointList[triangle.B];
            Vector2 c = pointList[triangle.C];

            double ax = a.x - point.x;
            double ay = a.y - point.y;
            double bx = b.x - point.x;
            double by = b.y - point.y;
            double cx = c.x - point.x;
            double cy = c.y - point.y;

            double determinant =
                (ax * ax + ay * ay) * (bx * cy - by * cx) -
                (bx * bx + by * by) * (ax * cy - ay * cx) +
                (cx * cx + cy * cy) * (ax * by - ay * bx);

            // Working triangles are counter-clockwise, therefore a positive
            // determinant places the point inside the circumcircle.
            return determinant > 1e-12;
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((double)b.x - a.x) * ((double)c.y - a.y) -
                   ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        private static bool IsFinite(Vector2 point)
        {
            return float.IsNaN(point.x) == false && float.IsInfinity(point.x) == false &&
                   float.IsNaN(point.y) == false && float.IsInfinity(point.y) == false;
        }
        #endregion
        
        #region nested types
        private readonly struct WorkingTriangle
        {
            #region fields
            public readonly int A;
            public readonly int B;
            public readonly int C;
            #endregion

            #region constructors
            public WorkingTriangle(int a, int b, int c)
            {
                A = a;
                B = b;
                C = c;
            }
            #endregion
        }

        private readonly struct Edge
        {
            #region fields
            public readonly int A;
            public readonly int B;
            #endregion

            #region constructors
            public Edge(int a, int b)
            {
                A = a;
                B = b;
            }
            #endregion
        }
        #endregion
    }
}
