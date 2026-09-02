using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    internal static class NeighborGraphBuilder
    {
        #region fields
        private const float MinPositionTolerance = 0.00001f;
        private const float PositionToleranceScale = 0.00001f;
        private const float ParallelTolerance = 0.0001f;
        #endregion

        #region methods
        internal static void Generate(Vector2 panelSize, IReadOnlyList<DelaunayTriangle> triangleList, List<DestructibleCell> cellList)
        {
            if (cellList.Count < 2)
            {
                return;
            }

            float panelExtent = Mathf.Max(panelSize.x, panelSize.y);
            // scale the tolerance with the panel size to avoid a fixed tolerance becoming too small for large panels.
            float positionTolerance = Mathf.Max(MinPositionTolerance, panelExtent * PositionToleranceScale);

            // Delaunay edges are the only possible Voronoi neighbor pairs. The
            // final polygon check rejects zero-length or panel-clipped-away edges.
            if (triangleList.Count > 0)
            {
                HashSet<CellPair> candidatePairSet = new HashSet<CellPair>();
                for (int triangleIndex = 0; triangleIndex < triangleList.Count; triangleIndex++)
                {
                    DelaunayTriangle triangle = triangleList[triangleIndex];
                    candidatePairSet.Add(new CellPair(triangle.A, triangle.B));
                    candidatePairSet.Add(new CellPair(triangle.B, triangle.C));
                    candidatePairSet.Add(new CellPair(triangle.C, triangle.A));
                }

                foreach (CellPair candidatePair in candidatePairSet)
                {
                    TryAddNeighborPair(candidatePair.A, candidatePair.B, positionTolerance, cellList);
                }
            }
            else
            {
                // Fewer than three or collinear sites have no Delaunay triangles.
                // Testing all pairs preserves valid strip-shaped Voronoi cells.
                for (int firstCellIndex = 0; firstCellIndex < cellList.Count - 1; firstCellIndex++)
                {
                    for (int secondCellIndex = firstCellIndex + 1; secondCellIndex < cellList.Count; secondCellIndex++)
                    {
                        TryAddNeighborPair(firstCellIndex, secondCellIndex, positionTolerance, cellList);
                    }
                }
            }

            for (int cellIndex = 0; cellIndex < cellList.Count; cellIndex++)
            {
                cellList[cellIndex].NeighborList.Sort();
            }
        }

        private static void TryAddNeighborPair(int firstCellIndex, int secondCellIndex, float positionTolerance, List<DestructibleCell> cellList)
        {
            DestructibleCell firstCell = cellList[firstCellIndex];
            DestructibleCell secondCell = cellList[secondCellIndex];
            if (SharesPolygonEdge(firstCell.Polygon, secondCell.Polygon, positionTolerance) == false)
            {
                return;
            }

            // Candidate pairs are unique, so these two writes create a symmetric,
            // duplicate-free graph without an additional search per insertion.
            firstCell.NeighborList.Add(secondCell.Id);
            secondCell.NeighborList.Add(firstCell.Id);
        }

        private static bool SharesPolygonEdge(IReadOnlyList<Vector2> firstPolygon, IReadOnlyList<Vector2> secondPolygon, float positionTolerance)
        {
            if (firstPolygon.Count < 3 || secondPolygon.Count < 3)
            {
                return false;
            }

            float minLengthSquared = positionTolerance * positionTolerance;
            for (int firstEdgeIndex = 0; firstEdgeIndex < firstPolygon.Count; firstEdgeIndex++)
            {
                Vector2 firstStart = firstPolygon[firstEdgeIndex];
                Vector2 firstEnd = firstPolygon[(firstEdgeIndex + 1) % firstPolygon.Count];
                Vector2 firstDirection = firstEnd - firstStart;
                float firstLengthSquared = firstDirection.sqrMagnitude;
                if (firstLengthSquared <= minLengthSquared)
                {
                    continue;
                }

                float firstLength = Mathf.Sqrt(firstLengthSquared);
                for (int secondEdgeIndex = 0; secondEdgeIndex < secondPolygon.Count; secondEdgeIndex++)
                {
                    Vector2 secondStart = secondPolygon[secondEdgeIndex];
                    Vector2 secondEnd = secondPolygon[(secondEdgeIndex + 1) % secondPolygon.Count];
                    Vector2 secondDirection = secondEnd - secondStart;
                    float secondLengthSquared = secondDirection.sqrMagnitude;
                    if (secondLengthSquared <= minLengthSquared)
                    {
                        continue;
                    }

                    // Check whether the two edges are parallel.
                    // Non-parallel edges cannot represent the same shared edge.
                    float directionCross = Mathf.Abs(Cross(firstDirection, secondDirection));
                    if (directionCross > ParallelTolerance * Mathf.Sqrt(firstLengthSquared * secondLengthSquared))
                    {
                        continue;
                    }

                    // Both endpoints must lie on the same supporting line. This
                    // prevents nearby parallel fragment edges from becoming linked.
                    // distance = |Cross(lineDirection, point - lineStart)| / |lineDirection| is 2d point to line formula
                    float secondStartDistance = Mathf.Abs(Cross(firstDirection, secondStart - firstStart)) / firstLength;
                    float secondEndDistance = Mathf.Abs(Cross(firstDirection, secondEnd - firstStart)) / firstLength;
                    // two edges is parallel but not the same edge
                    if (secondStartDistance > positionTolerance || secondEndDistance > positionTolerance)
                    {
                        continue;
                    }

                    float secondStartProjection = Vector2.Dot(secondStart - firstStart, firstDirection) / firstLengthSquared;
                    float secondEndProjection = Vector2.Dot(secondEnd - firstStart, firstDirection) / firstLengthSquared;
                    float overlapStart = Mathf.Max(0f, Mathf.Min(secondStartProjection, secondEndProjection));
                    float overlapEnd = Mathf.Min(1f, Mathf.Max(secondStartProjection, secondEndProjection));

                    // Point-only contact is not a neighbor relationship.
                    if ((overlapEnd - overlapStart) * firstLength > positionTolerance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
        #endregion

        #region nested types
        private readonly struct CellPair : IEquatable<CellPair>
        {
            #region fields
            public readonly int A;
            public readonly int B;
            #endregion

            #region constructors
            public CellPair(int a, int b)
            {
                if (a <= b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }
            #endregion

            #region methods
            public bool Equals(CellPair other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is CellPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
            #endregion
        }
        #endregion
    }
}
