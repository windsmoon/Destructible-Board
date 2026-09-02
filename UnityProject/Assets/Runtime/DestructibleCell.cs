using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
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
        public Mesh Mesh;
        public GameObject GameObject;
        #endregion
    }
}
