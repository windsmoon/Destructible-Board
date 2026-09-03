using UnityEditor;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard.Editor
{
    [CustomEditor(typeof(DestructibleBoard))]
    public class LocalDestructibleBehaviourEditor : UnityEditor.Editor
    {
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
            if ((Shape)shape.intValue == Shape.Circle)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_radius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_circleSegments"));
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_width"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_height"));
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "_shape", "_width", "_height", "_radius", "_circleSegments");
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
        #endregion
    }
}
