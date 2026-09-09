using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Windsmoon.DesctructibleBoard.Editor
{
    [CustomEditor(typeof(DestructibleBoard))]
    public class LocalDestructibleBehaviourEditor : UnityEditor.Editor
    {
        #region fields
        private readonly List<List<int>> _islands = new List<List<int>>();
        private readonly Dictionary<int, Vector3[]> _islandVertices = new Dictionary<int, Vector3[]>();
        private readonly List<string> _islandLabels = new List<string>();
        private bool _showIslands;
        private int _previewColliderCount;
        private IReadOnlyList<Vector2> _previewFirstPolygonVertices;
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
            Shape selectedShape = (Shape)shape.intValue;
            if (selectedShape == Shape.Circle)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_radius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_circleSegments"), new GUIContent("Curve Segments"));
            }
            else if (selectedShape == Shape.Ellipse)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_ellipseHorizontalRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_ellipseVerticalRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_circleSegments"), new GUIContent("Curve Segments"));
            }
            else if (selectedShape == Shape.Capsule)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_capsuleWidth"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_capsuleHeight"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_circleSegments"), new GUIContent("Curve Segments"));
            }
            else if (selectedShape == Shape.Sector)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_sectorRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_sectorAngle"), new GUIContent("Sector Angle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_circleSegments"), new GUIContent("Curve Segments"));
            }
            else if (selectedShape == Shape.RegularPolygon)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_regularPolygonEdgeCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_regularPolygonRadius"));
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_width"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_height"));
            }

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "_shape",
                "_width",
                "_height",
                "_radius",
                "_ellipseHorizontalRadius",
                "_ellipseVerticalRadius",
                "_capsuleWidth",
                "_capsuleHeight",
                "_sectorRadius",
                "_sectorAngle",
                "_regularPolygonEdgeCount",
                "_regularPolygonRadius",
                "_circleSegments");
            serializedObject.ApplyModifiedProperties();

            DestructibleBoard board = (DestructibleBoard)target;
            EditorGUILayout.Space();
            string generationButtonLabel = Application.isPlaying ? "Generate" : board.BakeMode == BakeMode.NoBake ? "Show Preview" : "Bake";
            if (GUILayout.Button(generationButtonLabel))
            {
                board.GenerateAll();
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(board);
                }

                SceneView.RepaintAll();
            }

            string clearButtonLabel = Application.isPlaying ? "Clear" : "Clear Bake";
            if (GUILayout.Button(clearButtonLabel))
            {
                board.Clear();
                if (Application.isPlaying == false)
                {
                    EditorUtility.SetDirty(board);
                }

                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(board)))
            {
                if (GUILayout.Button("Clone"))
                {
                    CloneBoard(board);
                    GUIUtility.ExitGUI();
                }
            }

            if (!board.IsCellDataGenerated)
            {
                _showIslands = false;
            }

            if (board.IsCellDataGenerated && GUILayout.Button("Show Islands"))
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
            if (!board.IsCellDataGenerated)
            {
                _showIslands = false;
                return;
            }

            IReadOnlyList<Vector2> firstPolygonVertices = board.TryGetCell(0, out DestructibleCell firstCell) ? firstCell.PolygonVertices : null;
            // Logical destruction removes collider entries; Generate replaces polygons.
            // Refresh on those changes instead of allocating query results every repaint.
            if (board.ColliderCount != _previewColliderCount || !ReferenceEquals(firstPolygonVertices, _previewFirstPolygonVertices))
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
                            if (!board.TryGetCell(cellId, out DestructibleCell cell) || cell.IsDestroyed ||
                                !_islandVertices.TryGetValue(cellId, out Vector3[] vertices))
                            {
                                continue;
                            }

                            Handles.color = new Color(color.r, color.g, color.b, 0.3f);
                            Handles.DrawAAConvexPolygon(vertices);
                            Handles.color = color;
                            Handles.DrawAAPolyLine(3f, vertices);
                            Handles.DrawLine(vertices[vertices.Length - 1], vertices[0]);
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
        private static void CloneBoard(DestructibleBoard source)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clone Destructible Board");

            Transform sourceTransform = source.transform;
            GameObject clone = Instantiate(source.gameObject, sourceTransform.parent, false);
            clone.name = GameObjectUtility.GetUniqueNameForSibling(sourceTransform.parent, source.name);
            if (sourceTransform.parent == null && clone.scene != source.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(clone, source.gameObject.scene);
            }
            clone.transform.SetSiblingIndex(sourceTransform.GetSiblingIndex() + 1);

            Dictionary<Mesh, Mesh> meshCopies = new Dictionary<Mesh, Mesh>();
            foreach (DestructibleBoard clonedBoard in clone.GetComponentsInChildren<DestructibleBoard>(true))
            {
                using (SerializedObject clonedData = new SerializedObject(clonedBoard))
                {
                    SerializedProperty cells = clonedData.FindProperty("_cellList");
                    for (int cellIndex = 0; cellIndex < cells.arraySize; cellIndex++)
                    {
                        SerializedProperty meshProperty = cells.GetArrayElementAtIndex(cellIndex).FindPropertyRelative("_mesh");
                        Mesh originalMesh = meshProperty.objectReferenceValue as Mesh;
                        if (originalMesh == null)
                        {
                            continue;
                        }

                        if (!meshCopies.TryGetValue(originalMesh, out Mesh copiedMesh))
                        {
                            copiedMesh = Instantiate(originalMesh);
                            copiedMesh.name = originalMesh.name;
                            copiedMesh.hideFlags = originalMesh.hideFlags;
                            meshCopies.Add(originalMesh, copiedMesh);
                            Undo.RegisterCreatedObjectUndo(copiedMesh, "Clone Fragment Mesh");
                        }

                        // Never regenerate here: clearing the cloned cells would destroy
                        // the meshes still shared with the source immediately after Instantiate.
                        meshProperty.objectReferenceValue = copiedMesh;
                    }

                    clonedData.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Unity remaps hierarchy references when cloning, but sharedMesh remains
            // external. Rendering, collision and cell ownership must use the same copy.
            foreach (MeshFilter meshFilter in clone.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null && meshCopies.TryGetValue(meshFilter.sharedMesh, out Mesh copiedMesh))
                {
                    meshFilter.sharedMesh = copiedMesh;
                }
            }

            foreach (MeshCollider meshCollider in clone.GetComponentsInChildren<MeshCollider>(true))
            {
                if (meshCollider.sharedMesh != null && meshCopies.TryGetValue(meshCollider.sharedMesh, out Mesh copiedMesh))
                {
                    meshCollider.sharedMesh = copiedMesh;
                }
            }

            Undo.RegisterCreatedObjectUndo(clone, "Clone Destructible Board");
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(clone.scene);
            Selection.activeGameObject = clone;
            EditorGUIUtility.PingObject(clone);
            SceneView.RepaintAll();
        }

        private void RefreshIslandPreview(DestructibleBoard board)
        {
            _islands.Clear();
            _islandVertices.Clear();
            _islandLabels.Clear();
            board.TryGetIslands(_islands);
            _previewColliderCount = board.ColliderCount;
            _previewFirstPolygonVertices = board.TryGetCell(0, out DestructibleCell firstCell) ? firstCell.PolygonVertices : null;

            for (int islandIndex = 0; islandIndex < _islands.Count; islandIndex++)
            {
                List<int> island = _islands[islandIndex];
                _islandLabels.Add($"Island {islandIndex + 1} ({island.Count} cells)");
                foreach (int cellId in island)
                {
                    if (!board.TryGetCell(cellId, out DestructibleCell cell) || cell.PolygonVertices == null || cell.PolygonVertices.Count < 3)
                    {
                        continue;
                    }

                    Vector3[] vertices = new Vector3[cell.PolygonVertices.Count];
                    for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                    {
                        Vector2 point = cell.PolygonVertices[vertexIndex];
                        vertices[vertexIndex] = new Vector3(point.x, point.y, 0f);
                    }

                    _islandVertices.Add(cellId, vertices);
                }
            }
        }
        #endregion
    }
}
