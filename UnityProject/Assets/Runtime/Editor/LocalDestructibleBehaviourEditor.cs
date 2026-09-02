using UnityEditor;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard.Editor
{
    [CustomEditor(typeof(LocalDestructibleBehaviour))]
    public class LocalDestructibleBehaviourEditor : UnityEditor.Editor
    {
        #region unity methods
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LocalDestructibleBehaviour behaviour = (LocalDestructibleBehaviour)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Debug Info", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Sample Points", behaviour.SamplePointCount);
                EditorGUILayout.IntField("Delaunay Triangles", behaviour.DelaunayTriangleCount);
                EditorGUILayout.IntField("Voronoi Regions", behaviour.VoronoiRegionCount);
                EditorGUILayout.IntField("Fragment Vertices", behaviour.FragmentVertexCount);
                EditorGUILayout.IntField("Fragment Triangles", behaviour.FragmentTriangleCount);
            }
        }
        #endregion
    }
}
