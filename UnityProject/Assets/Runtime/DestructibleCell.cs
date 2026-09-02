using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    [Serializable]
    public struct DestructibleCell
    {
        #region fields
        public int Id;
        public Vector2 Site;
        public List<Vector2> Polygon;
        #endregion
    }
}
