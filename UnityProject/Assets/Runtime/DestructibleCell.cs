using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public struct DestructibleCell
    {
        #region fields
        public int Id;
        public Vector2 Site;
        public List<Vector2> Polygon;
        public List<int> Neighbors;
        public Mesh Mesh;
        public Collider Collider;
        public bool Destroyed;
        #endregion
    }
}
