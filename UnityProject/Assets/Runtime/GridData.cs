using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    [Serializable]
    internal class GridData
    {
        #region fields
        [SerializeField]
        private float _gridCellSize;
        [SerializeField]
        private int[] _bucketOffsets;
        [SerializeField]
        private int[] _cellIds;
        #endregion

        #region properties
        internal float GridCellSize => _gridCellSize;
        #endregion

        #region methods
        internal void Build(IReadOnlyList<DestructibleCell> cellList)
        {
            double totalArea = 0d;
            for (int cellIndex = 0; cellIndex < cellList.Count; cellIndex++)
            {
                IReadOnlyList<Vector2> polygonVertices = cellList[cellIndex].PolygonVertices;
                // Shoelace area keeps the estimate based on the final clipped cells.
                double twiceArea = 0d;
                for (int vertexIndex = 0; vertexIndex < polygonVertices.Count; vertexIndex++)
                {
                    Vector2 current = polygonVertices[vertexIndex];
                    Vector2 next = polygonVertices[(vertexIndex + 1) % polygonVertices.Count];
                    twiceArea += (double)current.x * next.y - (double)current.y * next.x;
                }

                totalArea += Math.Abs(twiceArea) * 0.5d;
            }

            _gridCellSize = Mathf.Sqrt((float)(totalArea / cellList.Count));
        }

        public bool TryGetCellIndex(Vector2 position, out int index)
        {
            index = -1;
            return false;
        }
        #endregion
    }
}