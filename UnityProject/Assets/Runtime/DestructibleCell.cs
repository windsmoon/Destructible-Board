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
        public Mesh Mesh;
        public GameObject GameObject;
        public List<int> NeighborList;
        public Collider Collider;
        public bool Destroyed;
        #endregion

        #region constructors
        public DestructibleCell(int id, Vector2 site)
        {
            Id = id;
            Site = site;
            Polygon = new List<Vector2>();
            Mesh = null;
            GameObject = null;
            NeighborList = new List<int>();
            Collider = null;
            Destroyed = false;
        }
        #endregion
    }
}
