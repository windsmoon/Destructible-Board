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
        private int[] _bucketOffsets;
        [SerializeField]
        private int[] _cellIds;
        #endregion

        #region methods

        internal void Build(List<DestructibleCell> cellList)
        {
        }

        public bool TryGetCellIndex(Vector2 position, out int index)
        {
            index = -1;
            return false;
        }
        #endregion
    }
}