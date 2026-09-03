using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    internal static class RingCellClipper
    {
        /// <summary>
        /// Subtracts the convex inner outline from each outer Voronoi cell. Taking
        /// the outside of one edge, then passing only the inside to the next edge,
        /// partitions the difference into disjoint convex pieces for MeshCollider.
        /// </summary>
        internal static void SubtractHole(IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> hole, List<DestructibleCell> cells)
        {
            float tolerance = Mathf.Max(0.000001f, outer[0].magnitude * 0.000001f);
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                DestructibleCell cell = cells[cellIndex];
                bool outsideHole = false;
                int firstEdge = 0;
                double nearestDistance = double.PositiveInfinity;
                for (int edgeIndex = 0; edgeIndex < hole.Count; edgeIndex++)
                {
                    Vector2 start = hole[edgeIndex];
                    Vector2 end = hole[(edgeIndex + 1) % hole.Count];
                    bool allOutside = true;
                    foreach (Vector2 point in cell.Polygon)
                    {
                        allOutside &= SignedDistance(start, end, point) <= 0d;
                    }

                    if (allOutside)
                    {
                        outsideHole = true;
                        break;
                    }

                    double distance = SignedDistance(start, end, cell.Site);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        firstEdge = edgeIndex;
                    }
                }

                if (outsideHole)
                {
                    cell.IsBoundary |= VoronoiGenerator.SharesPanelBoundaryEdge(cell.Polygon, hole, tolerance);
                    cells[cellIndex] = cell;
                    continue;
                }

                List<List<Vector2>> pieces = new List<List<Vector2>>();
                List<Vector2> remaining = cell.Polygon;
                for (int offset = 0; offset < hole.Count && remaining.Count >= 3; offset++)
                {
                    int edgeIndex = (firstEdge + offset) % hole.Count;
                    Split(remaining, hole[edgeIndex], hole[(edgeIndex + 1) % hole.Count], out List<Vector2> inside, out List<Vector2> outside);
                    if (HasArea(outside))
                    {
                        pieces.Add(outside);
                    }

                    remaining = inside;
                }

                cell.Polygon = pieces.Count > 0 ? pieces[0] : new List<Vector2>();
                cell.AdditionalParts = null;
                cell.IsBoundary = false;
                for (int partIndex = 0; partIndex < pieces.Count; partIndex++)
                {
                    List<Vector2> polygon = pieces[partIndex];
                    if (partIndex > 0)
                    {
                        cell.AdditionalParts ??= new List<FragmentPart>();
                        cell.AdditionalParts.Add(new FragmentPart(polygon));
                    }

                    // Both original outlines are boundary edges; internal partition
                    // seams are not supports and do not create extra logical cells.
                    cell.IsBoundary |= VoronoiGenerator.SharesPanelBoundaryEdge(polygon, outer, tolerance) ||
                                       VoronoiGenerator.SharesPanelBoundaryEdge(polygon, hole, tolerance);
                }

                cells[cellIndex] = cell;
            }
        }

        private static void Split(IReadOnlyList<Vector2> polygon, Vector2 start, Vector2 end,
            out List<Vector2> inside, out List<Vector2> outside)
        {
            inside = new List<Vector2>(polygon.Count + 1);
            outside = new List<Vector2>(polygon.Count + 1);
            Vector2 previous = polygon[polygon.Count - 1];
            double previousDistance = SignedDistance(start, end, previous);
            foreach (Vector2 current in polygon)
            {
                double distance = SignedDistance(start, end, current);
                if ((previousDistance < 0d && distance > 0d) || (previousDistance > 0d && distance < 0d))
                {
                    double t = previousDistance / (previousDistance - distance);
                    Vector2 intersection = new Vector2(
                        (float)(previous.x + ((double)current.x - previous.x) * t),
                        (float)(previous.y + ((double)current.y - previous.y) * t));
                    AddDistinct(inside, intersection);
                    AddDistinct(outside, intersection);
                }

                if (distance >= 0d) AddDistinct(inside, current);
                if (distance <= 0d) AddDistinct(outside, current);
                previous = current;
                previousDistance = distance;
            }

            RemoveClosingDuplicate(inside);
            RemoveClosingDuplicate(outside);
        }

        private static double SignedDistance(Vector2 start, Vector2 end, Vector2 point)
        {
            return ((double)end.x - start.x) * ((double)point.y - start.y) -
                   ((double)end.y - start.y) * ((double)point.x - start.x);
        }

        private static void AddDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 1e-10f)
            {
                points.Add(point);
            }
        }

        private static void RemoveClosingDuplicate(List<Vector2> points)
        {
            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= 1e-10f)
            {
                points.RemoveAt(points.Count - 1);
            }
        }

        private static bool HasArea(IReadOnlyList<Vector2> polygon)
        {
            double area = 0d;
            for (int index = 1; index + 1 < polygon.Count; index++)
            {
                area += SignedDistance(polygon[0], polygon[index], polygon[index + 1]);
            }

            return area > 1e-12d;
        }
    }
}
