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
        private Vector2 _minVertex;
        [SerializeField]
        private Vector2 _maxVertex;
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
        internal Vector2 MinVertex => _minVertex;
        internal Vector2 MaxVertex => _maxVertex;
        internal int ColumnCount => _columnCount;
        internal int RowCount => _rowCount;
        #endregion

        #region methods
        internal void Build(IReadOnlyList<DestructibleCell> destructibleCellList)
        {
            double totalArea = 0d;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int cellIndex = 0; cellIndex < destructibleCellList.Count; cellIndex++)
            {
                IReadOnlyList<Vector2> polygonVertices = destructibleCellList[cellIndex].PolygonVertices;
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

            _minVertex = minimum;
            _maxVertex = maximum;
            _gridCellSize = Mathf.Sqrt((float)(totalArea / destructibleCellList.Count));

            // Use the actual clipped panel bounds so sparse outer sampling bounds do not create empty grid regions.
            Vector2 boundsSize = _maxVertex - _minVertex;
            _columnCount = Mathf.CeilToInt(boundsSize.x / _gridCellSize);
            _rowCount = Mathf.CeilToInt(boundsSize.y / _gridCellSize);
            int bucketCount = _columnCount * _rowCount;

            _bucketOffsetList = new List<int>(bucketCount + 1);
            for (int bucketIndex = 0; bucketIndex <= bucketCount; bucketIndex++)
            {
                _bucketOffsetList.Add(0);
            }

            for (int cellIndex = 0; cellIndex < destructibleCellList.Count; cellIndex++)
            {
                CalculateBucketRange(destructibleCellList[cellIndex].PolygonVertices, out int minColumn, out int maxColumn, out int minRow, out int maxRow);

                for (int row = minRow; row <= maxRow; row++)
                {
                    for (int column = minColumn; column <= maxColumn; column++)
                    {
                        int bucketIndex = row * _columnCount + column;
                        // Counts start at index one so the prefix sum becomes bucket offsets.
                        // now per element in list means the count of the bucket
                        _bucketOffsetList[bucketIndex + 1]++;
                    }
                }
            }

            // caculate the prefix sum
            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                _bucketOffsetList[bucketIndex + 1] += _bucketOffsetList[bucketIndex];
            }

            int cellIdCount = _bucketOffsetList[bucketCount];
            _cellIdList = new List<int>(cellIdCount);
            for (int cellIdIndex = 0; cellIdIndex < cellIdCount; cellIdIndex++)
            {
                _cellIdList.Add(0);
            }

            int[] bucketWriteOffsets = new int[bucketCount];
            for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
            {
                bucketWriteOffsets[bucketIndex] = _bucketOffsetList[bucketIndex];
            }

            for (int cellIndex = 0; cellIndex < destructibleCellList.Count; cellIndex++)
            {
                CalculateBucketRange(destructibleCellList[cellIndex].PolygonVertices, out int minColumn, out int maxColumn, out int minRow, out int maxRow);

                for (int row = minRow; row <= maxRow; row++)
                {
                    for (int column = minColumn; column <= maxColumn; column++)
                    {
                        int bucketIndex = row * _columnCount + column;
                        int writeOffset = bucketWriteOffsets[bucketIndex];
                        _cellIdList[writeOffset] = destructibleCellList[cellIndex].Id;
                        bucketWriteOffsets[bucketIndex] = writeOffset + 1;
                    }
                }
            }
        }

        public bool TryGetCellIndex(Vector2 position, out int index)
        {
            index = -1;
            return false;
        }

        private void CalculateBucketRange(IReadOnlyList<Vector2> polygonVertices, out int minColumn, out int maxColumn, out int minRow, out int maxRow)
        {
            Vector2 min = polygonVertices[0];
            Vector2 max = polygonVertices[0];
            for (int vertexIndex = 1; vertexIndex < polygonVertices.Count; vertexIndex++)
            {
                min = Vector2.Min(min, polygonVertices[vertexIndex]);
                max = Vector2.Max(max, polygonVertices[vertexIndex]);
            }

            minColumn = Mathf.FloorToInt((min.x - _minVertex.x) / _gridCellSize);
            maxColumn = Mathf.Min(_columnCount - 1, Mathf.FloorToInt((max.x - _minVertex.x) / _gridCellSize));
            minRow = Mathf.FloorToInt((min.y - _minVertex.y) / _gridCellSize);
            maxRow = Mathf.Min(_rowCount - 1, Mathf.FloorToInt((max.y - _minVertex.y) / _gridCellSize));
        }
        #endregion
    }
}
