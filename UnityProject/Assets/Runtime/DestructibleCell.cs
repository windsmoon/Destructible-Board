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
        /// <summary>
        /// Counter-clockwise vertices of the Voronoi cell, clipped to the panel bounds
        /// and expressed in panel-local XY coordinates.
        /// </summary>
        public List<Vector2> Polygon;
        #endregion
    }
}
