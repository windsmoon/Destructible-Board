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
                cellList[cellIndex].MutableNeighborIdList.Sort();
            }
        }

        private static void TryAddNeighborPair(int firstCellIndex, int secondCellIndex, float positionTolerance, List<DestructibleCell> cellList)
        {
            DestructibleCell firstCell = cellList[firstCellIndex];
            DestructibleCell secondCell = cellList[secondCellIndex];
            if (SharesPolygonEdge(firstCell.PolygonVertices, secondCell.PolygonVertices, positionTolerance) == false)
            {
                return;
            }

            // Candidate pairs are unique, so these two writes create a symmetric,
            // duplicate-free graph without an additional search per insertion.
            firstCell.MutableNeighborIdList.Add(secondCell.Id);
            secondCell.MutableNeighborIdList.Add(firstCell.Id);
        }

        private static bool SharesPolygonEdge(IReadOnlyList<Vector2> firstPolygonVertices, IReadOnlyList<Vector2> secondPolygonVertices, float positionTolerance)
        {
            if (firstPolygonVertices.Count < 3 || secondPolygonVertices.Count < 3)
            {
                return false;
            }

            float positionToleranceSquared = positionTolerance * positionTolerance;
            for (int firstEdgeIndex = 0; firstEdgeIndex < firstPolygonVertices.Count; firstEdgeIndex++)
            {
                Vector2 firstStart = firstPolygonVertices[firstEdgeIndex];
                Vector2 firstEnd = firstPolygonVertices[(firstEdgeIndex + 1) % firstPolygonVertices.Count];
                for (int secondEdgeIndex = 0; secondEdgeIndex < secondPolygonVertices.Count; secondEdgeIndex++)
                {
                    Vector2 secondStart = secondPolygonVertices[secondEdgeIndex];
                    Vector2 secondEnd = secondPolygonVertices[(secondEdgeIndex + 1) % secondPolygonVertices.Count];
                    bool sameDirection =
                        (firstStart - secondStart).sqrMagnitude <= positionToleranceSquared &&
                        (firstEnd - secondEnd).sqrMagnitude <= positionToleranceSquared;
                    bool oppositeDirection =
                        (firstStart - secondEnd).sqrMagnitude <= positionToleranceSquared &&
                        (firstEnd - secondStart).sqrMagnitude <= positionToleranceSquared;
                    if (sameDirection || oppositeDirection)
                    {
                        return true;
                    }
                }
            }

            return false;
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
