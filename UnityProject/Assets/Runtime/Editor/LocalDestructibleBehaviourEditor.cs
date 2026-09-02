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
            DrawDefaultInspector();

            DestructibleBoard board = (DestructibleBoard)target;
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
