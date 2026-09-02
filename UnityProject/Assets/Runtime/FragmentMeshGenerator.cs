using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public static class FragmentMeshGenerator
    {
        #region methods
        /// <summary>
        /// Extrudes a counter-clockwise convex polygon in panel-local XY space
        /// into a closed mesh whose thickness extends equally along local Z.
        /// </summary>
        internal static Mesh Generate(IReadOnlyList<Vector2> polygon, float thickness)
        {
            if (polygon.Count < 3 || thickness <= 0f)
            {
                return null;
            }

            int polygonVertexCount = polygon.Count;
            int vertexCount = polygonVertexCount * 6;
            // Each cap uses n - 2 triangles, and the n side quads use 2n triangles.
            // The closed mesh therefore needs (4n - 4) triangles, with 3 indices each.
            int triangleIndexCount = (polygonVertexCount * 4 - 4) * 3;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[triangleIndexCount];
            float halfThickness = thickness * 0.5f;

            WriteFrontAndBackVertices(polygon, halfThickness, vertices, normals, uv);
            WriteFrontAndBackTriangles(polygonVertexCount, triangles, out int triangleIndex);
            WriteSideGeometry(polygon, halfThickness, vertices, normals, uv, triangles, ref triangleIndex);

            Mesh mesh = new Mesh
            {
                name = "Fragment Mesh",
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void WriteFrontAndBackVertices(IReadOnlyList<Vector2> polygon, float halfThickness, Vector3[] vertices, Vector3[] normals, Vector2[] uv)
        {
            int polygonVertexCount = polygon.Count;
            for (int pointIndex = 0; pointIndex < polygonVertexCount; pointIndex++)
            {
                Vector2 point = polygon[pointIndex];
                int frontVertexIndex = pointIndex;
                int backVertexIndex = polygonVertexCount + pointIndex;

                vertices[frontVertexIndex] = new Vector3(point.x, point.y, halfThickness);
                normals[frontVertexIndex] = Vector3.forward;
                uv[frontVertexIndex] = point; // local xy pos as uv

                vertices[backVertexIndex] = new Vector3(point.x, point.y, -halfThickness);
                normals[backVertexIndex] = Vector3.back;
                uv[backVertexIndex] = point; // local xy pos as uv
            }
        }

        private static void WriteFrontAndBackTriangles(int polygonVertexCount, int[] triangles, out int triangleIndex)
        {
            /*
            polygon[0] ─ polygon[1]
                │  \          │
                │    \        │
                │      \      │
            polygon[3] ─ polygon[2]
            */
            triangleIndex = 0;
            int backVertexStartIndex = polygonVertexCount;

            // A counter-clockwise polygon faces local +Z. Reverse the back face
            // winding so that its triangles face local -Z.
            for (int pointIndex = 1; pointIndex < polygonVertexCount - 1; pointIndex++)
            {
                triangles[triangleIndex++] = 0; // 0 is first polygon point, not site point
                triangles[triangleIndex++] = pointIndex;
                triangles[triangleIndex++] = pointIndex + 1;

                triangles[triangleIndex++] = backVertexStartIndex; // backVertexStartIndex is first polygon point, not site point
                triangles[triangleIndex++] = backVertexStartIndex + pointIndex + 1;
                triangles[triangleIndex++] = backVertexStartIndex + pointIndex;
            }
        }

        private static void WriteSideGeometry(IReadOnlyList<Vector2> polygon, float halfThickness, Vector3[] vertices, Vector3[] normals, Vector2[] uv, int[] triangles, ref int triangleIndex)
        {
            int polygonVertexCount = polygon.Count;
         
            // 0 ～ n-1  front vertices
            // n ～ 2n-1 back vertices
            int sideVertexStart = polygonVertexCount * 2;
            float stripU = 0f;

            for (int pointIndex = 0; pointIndex < polygonVertexCount; pointIndex++)
            {
                Vector2 current = polygon[pointIndex];
                // The last edge will connected to the first vertex by the %
                Vector2 next = polygon[(pointIndex + 1) % polygonVertexCount];
                Vector2 edge = next - current;
                float nextStripU = stripU + edge.magnitude;
                Vector3 sideNormal = new Vector3(edge.y, -edge.x, 0f).normalized;
                int vertexIndex = sideVertexStart + pointIndex * 4; // Every face has 4 new vertices to get hard surface normal

                // Side vertices are separate from both caps and neighboring sides
                // so every polygon edge retains a hard normal.
                /*
                 currentFront ---- nextFront
                    |                |
                    |                |
                currentBack  ---- nextBack
                 */
                vertices[vertexIndex] = new Vector3(current.x, current.y, halfThickness);
                vertices[vertexIndex + 1] = new Vector3(current.x, current.y, -halfThickness);
                vertices[vertexIndex + 2] = new Vector3(next.x, next.y, -halfThickness);
                vertices[vertexIndex + 3] = new Vector3(next.x, next.y, halfThickness);

                normals[vertexIndex] = sideNormal;
                normals[vertexIndex + 1] = sideNormal;
                normals[vertexIndex + 2] = sideNormal;
                normals[vertexIndex + 3] = sideNormal;

                uv[vertexIndex] = new Vector2(stripU, 1f);
                uv[vertexIndex + 1] = new Vector2(stripU, 0f);
                uv[vertexIndex + 2] = new Vector2(nextStripU, 0f);
                uv[vertexIndex + 3] = new Vector2(nextStripU, 1f);

                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + 1;
                triangles[triangleIndex++] = vertexIndex + 2;
                triangles[triangleIndex++] = vertexIndex;
                triangles[triangleIndex++] = vertexIndex + 2;
                triangles[triangleIndex++] = vertexIndex + 3;

                stripU = nextStripU;
            }
        }
        #endregion
    }
}
