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
        /// Counter-clockwise vertices of the first convex piece in panel-local XY.
        /// Ring cells can have additional pieces after subtracting the central hole.
        /// </summary>
        public List<Vector2> Polygon;
        public List<FragmentPart> AdditionalParts;
        public Mesh Mesh;
        public GameObject GameObject;
        public List<int> NeighborList;
        public Collider Collider;
        public bool Destroyed;
        /// <summary>
        /// The clipped polygon shares a non-zero-length edge with the original
        /// panel outline, within the generator's geometric tolerance.
        /// </summary>
        public bool IsBoundary;
        #endregion

        #region constructors
        public DestructibleCell(int id, Vector2 site)
        {
            Id = id;
            Site = site;
            Polygon = new List<Vector2>();
            AdditionalParts = null;
            Mesh = null;
            GameObject = null;
            NeighborList = new List<int>();
            Collider = null;
            Destroyed = false;
            IsBoundary = false;
        }
        #endregion

        public int PartCount => (Polygon != null && Polygon.Count >= 3 ? 1 : 0) + (AdditionalParts?.Count ?? 0);

        public List<Vector2> GetPolygon(int partIndex) => partIndex == 0 ? Polygon : AdditionalParts[partIndex - 1].Polygon;
        internal Mesh GetMesh(int partIndex) => partIndex == 0 ? Mesh : AdditionalParts[partIndex - 1].Mesh;
        internal Collider GetCollider(int partIndex) => partIndex == 0 ? Collider : AdditionalParts[partIndex - 1].Collider;
    }
}
