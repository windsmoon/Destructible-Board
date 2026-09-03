using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    /// <summary>
    /// An additional convex piece of a cell clipped by the ring's hole. All pieces
    /// share one cell ID and detach together under the cell's GameObject.
    /// </summary>
    [Serializable]
    public class FragmentPart
    {
        public List<Vector2> Polygon;
        public Mesh Mesh;
        public Collider Collider;

        public FragmentPart(List<Vector2> polygon)
        {
            Polygon = polygon;
        }
    }
}
