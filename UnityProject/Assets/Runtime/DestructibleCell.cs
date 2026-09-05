using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Serialization;

namespace Windsmoon.DesctructibleBoard
{
    [Serializable]
    public struct DestructibleCell : ISerializationCallbackReceiver
    {
        #region fields
        [SerializeField]
        private int _id;
        [SerializeField]
        private Vector2 _site;
        [SerializeField]
        private List<Vector2> _polygon;
        [SerializeField]
        private List<int> _neighborList;
        [SerializeField]
        private bool _isBoundary;
        [NonSerialized]
        private ReadOnlyCollection<Vector2> _polygonView;
        [NonSerialized]
        private ReadOnlyCollection<int> _neighborView;

        [NonSerialized]
        public Mesh Mesh;
        [NonSerialized]
        public GameObject GameObject;
        [NonSerialized]
        public Collider Collider;
        [NonSerialized]
        public bool Destroyed;

        #endregion

        #region constructors
        public DestructibleCell(int id, Vector2 site) : this()
        {
            _id = id;
            _site = site;
            _polygon = new List<Vector2>();
            _neighborList = new List<int>();
            _polygonView = _polygon.AsReadOnly();
            _neighborView = _neighborList.AsReadOnly();
        }
        #endregion

        #region properties
        public int Id => _id;
        public Vector2 Site => _site;
        // Prepared views avoid lazy initialization on struct copies and mutable-list casts.
        public IReadOnlyList<Vector2> Polygon => _polygonView;
        public IReadOnlyList<int> NeighborList => _neighborView;
        public bool IsBoundary => _isBoundary;
        // Geometry builders populate these lists before the layout is used by instances.
        internal List<Vector2> MutablePolygon => _polygon;
        internal List<int> MutableNeighborList => _neighborList;
        #endregion

        #region methods
        internal void SetBoundary(bool value)
        {
            _isBoundary = value;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _polygon ??= new List<Vector2>();
            _neighborList ??= new List<int>();
            _polygonView = _polygon.AsReadOnly();
            _neighborView = _neighborList.AsReadOnly();
        }
        #endregion
    }
}
