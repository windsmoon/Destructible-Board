using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard.Editor
{
    [CustomEditor(typeof(DestructibleBoard))]
    public class LocalDestructibleBehaviourEditor : UnityEditor.Editor
    {
        #region fields
        private readonly List<List<int>> _islands = new List<List<int>>();
        private readonly Dictionary<int, List<Vector3[]>> _islandVertices = new Dictionary<int, List<Vector3[]>>();
        private readonly List<string> _islandLabels = new List<string>();
        private bool _showIslands;
        private int _previewColliderCount;
        private List<Vector2> _previewFirstPolygon;
        #endregion

        #region unity methods
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            SerializedProperty shape = serializedObject.FindProperty("_shape");
            EditorGUILayout.PropertyField(shape);
            if ((Shape)shape.intValue == Shape.Circle || (Shape)shape.intValue == Shape.Ring)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_radius"),
                    new GUIContent((Shape)shape.intValue == Shape.Ring ? "Outer Radius" : "Radius"));
                if ((Shape)shape.intValue == Shape.Ring)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_innerRadius"));
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_circleSegments"));
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_width"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_height"));
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "_shape", "_width", "_height", "_radius", "_innerRadius", "_circleSegments");
            serializedObject.ApplyModifiedProperties();

            DestructibleBoard board = (DestructibleBoard)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Generate"))
            {
                board.Generate();
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(board);
                }

                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Show Islands"))
            {
                _showIslands = true;
                RefreshIslandPreview(board);
                if (_islands.Count > 0)
                {
                    StringBuilder message = new StringBuilder($"Islands: {_islands.Count}");
                    for (int islandIndex = 0; islandIndex < _islands.Count; islandIndex++)
                    {
                        message.AppendLine();
                        message.Append($"Island {islandIndex + 1}: [{string.Join(", ", _islands[islandIndex])}]");
                    }

                    Debug.Log(message.ToString(), board);
                }
                else
                {
                    Debug.Log("No islands found.", board);
                }

                SceneView.RepaintAll();
            }

            if (_showIslands)
            {
                EditorGUILayout.LabelField("Visible Islands", _islands.Count.ToString());
                if (GUILayout.Button("Hide Islands"))
                {
                    _showIslands = false;
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Debug Info", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Sample Points", board.SamplePointCount);
                EditorGUILayout.IntField("Delaunay Triangles", board.DelaunayTriangleCount);
                EditorGUILayout.IntField("Voronoi Regions", board.VoronoiRegionCount);
                EditorGUILayout.IntField("Fragment Vertices", board.FragmentVertexCount);
                EditorGUILayout.IntField("Fragment Triangles", board.FragmentTriangleCount);
                EditorGUILayout.IntField("Fragment Colliders", board.ColliderCount);
            }
        }

        private void OnSceneGUI()
        {
            if (!_showIslands)
            {
                return;
            }

            DestructibleBoard board = (DestructibleBoard)target;
            List<Vector2> firstPolygon = board.TryGetCell(0, out DestructibleCell firstCell) ? firstCell.Polygon : null;
            // Logical destruction removes collider entries; Generate replaces polygons.
            // Refresh on those changes instead of allocating query results every repaint.
            if (board.ColliderCount != _previewColliderCount || !ReferenceEquals(firstPolygon, _previewFirstPolygon))
            {
                RefreshIslandPreview(board);
                Repaint();
            }

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            UnityEngine.Rendering.CompareFunction previousDepthTest = Handles.zTest;
            try
            {
                // The polygons lie on the panel's middle plane, behind its opaque faces.
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                using (new Handles.DrawingScope(board.transform.localToWorldMatrix))
                {
                    for (int islandIndex = 0; islandIndex < _islands.Count; islandIndex++)
                    {
                        Color color = Color.HSVToRGB(Mathf.Repeat(islandIndex * 0.618034f, 1f), 0.7f, 1f);
                        Vector3 labelPosition = Vector3.zero;
                        int visibleCellCount = 0;
                        foreach (int cellId in _islands[islandIndex])
                        {
                            if (!board.TryGetCell(cellId, out DestructibleCell cell) || cell.Destroyed ||
                                !_islandVertices.TryGetValue(cellId, out List<Vector3[]> parts))
                            {
                                continue;
                            }

                            foreach (Vector3[] vertices in parts)
                            {
                                Handles.color = new Color(color.r, color.g, color.b, 0.3f);
                                Handles.DrawAAConvexPolygon(vertices);
                                Handles.color = color;
                                Handles.DrawAAPolyLine(3f, vertices);
                                Handles.DrawLine(vertices[vertices.Length - 1], vertices[0]);
                            }
                            labelPosition += new Vector3(cell.Site.x, cell.Site.y, 0f);
                            visibleCellCount++;
                        }

                        if (visibleCellCount > 0)
                        {
                            Handles.Label(labelPosition / visibleCellCount, _islandLabels[islandIndex], EditorStyles.whiteLabel);
                        }
                    }
                }
            }
            finally
            {
                Handles.zTest = previousDepthTest;
            }
        }
        #endregion

        #region methods
        private void RefreshIslandPreview(DestructibleBoard board)
        {
            _islands.Clear();
            _islandVertices.Clear();
            _islandLabels.Clear();
            board.TryGetIslands(_islands);
            _previewColliderCount = board.ColliderCount;
            _previewFirstPolygon = board.TryGetCell(0, out DestructibleCell firstCell) ? firstCell.Polygon : null;

            for (int islandIndex = 0; islandIndex < _islands.Count; islandIndex++)
            {
                List<int> island = _islands[islandIndex];
                _islandLabels.Add($"Island {islandIndex + 1} ({island.Count} cells)");
                foreach (int cellId in island)
                {
                    if (!board.TryGetCell(cellId, out DestructibleCell cell) || cell.Polygon == null || cell.Polygon.Count < 3)
                    {
                        continue;
                    }

                    List<Vector3[]> parts = new List<Vector3[]>(cell.PartCount);
                    for (int partIndex = 0; partIndex < cell.PartCount; partIndex++)
                    {
                        List<Vector2> polygon = cell.GetPolygon(partIndex);
                        Vector3[] vertices = new Vector3[polygon.Count];
                        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                        {
                            Vector2 point = polygon[vertexIndex];
                            vertices[vertexIndex] = new Vector3(point.x, point.y, 0f);
                        }
                        parts.Add(vertices);
                    }

                    _islandVertices.Add(cellId, parts);
                }
            }
        }
        #endregion
    }
}
