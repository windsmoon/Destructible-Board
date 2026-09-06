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
        private Vector2 _minimum;
        [SerializeField]
        private Vector2 _maximum;
        [SerializeField]
        private int _columnCount;
        [SerializeField]
        private int _rowCount;
        [SerializeField]
        private List<int> _bucketOffsetList;
        [SerializeField]
        private List<int> _cellIdList;
        #endregion

        #region properties
        internal float GridCellSize => _gridCellSize;
        internal Vector2 Minimum => _minimum;
        internal Vector2 Maximum => _maximum;
        internal int ColumnCount => _columnCount;
        internal int RowCount => _rowCount;
        #endregion

        #region methods
        internal void Build(IReadOnlyList<DestructibleCell> cellList)
        {
            double totalArea = 0d;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

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
                    minimum = Vector2.Min(minimum, current);
                    maximum = Vector2.Max(maximum, current);
                }

                totalArea += Math.Abs(twiceArea) * 0.5d;
            }

            _minimum = minimum;
            _maximum = maximum;
            _gridCellSize = Mathf.Sqrt((float)(totalArea / cellList.Count));

            // Use the actual clipped panel bounds so sparse outer sampling bounds do not create empty grid regions.
            Vector2 boundsSize = _maximum - _minimum;
            _columnCount = Mathf.CeilToInt(boundsSize.x / _gridCellSize);
            _rowCount = Mathf.CeilToInt(boundsSize.y / _gridCellSize);

            int bucketCount = _columnCount * _rowCount;
            _bucketOffsetList = new List<int>(bucketCount + 1);
            _cellIdList = new List<int>(bucketCount + 1);
        }

        public bool TryGetCellIndex(Vector2 position, out int index)
        {
            index = -1;
            return false;
        }
        #endregion
    }
}
