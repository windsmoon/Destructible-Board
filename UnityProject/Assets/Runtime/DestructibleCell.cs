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
        private List<Vector2> _polygonVertices;
        [SerializeField]
        private List<int> _neighborIdList;
        [SerializeField]
        private bool _isBoundary;
        
        private ReadOnlyCollection<Vector2> _polygonVerticesView;
        private ReadOnlyCollection<int> _neighborIdView;
        private bool _isDestroyed;
        
        private Mesh _mesh;
        private GameObject _gameObject;
        private Collider _collider;
        #endregion

        #region constructors
        public DestructibleCell(int id, Vector2 site) : this()
        {
            _id = id;
            _site = site;
            _polygonVertices = new List<Vector2>();
            _neighborIdList = new List<int>();
            _polygonVerticesView = _polygonVertices.AsReadOnly();
            _neighborIdView = _neighborIdList.AsReadOnly();
        }
        #endregion

        #region properties
        public int Id => _id;
        public Vector2 Site => _site;
        // Prepared views avoid lazy initialization on struct copies and mutable-list casts.
        public IReadOnlyList<Vector2> PolygonVertices => _polygonVerticesView;
        public IReadOnlyList<int> NeighborIdList => _neighborIdView;

        public bool IsBoundary
        {
            get => _isBoundary;
            internal set => _isBoundary = value;
        }
        
        public bool IsDestroyed
        {
            get => _isDestroyed;
            internal set => _isDestroyed = value;
        }

        public Mesh Mesh
        {
            get => _mesh;
            internal set => _mesh = value;
        }
        
        public GameObject GameObject
        {
            get => _gameObject;
            internal set => _gameObject = value;
        }
        
        public Collider Collider
        {
            get => _collider;
            internal set => _collider = value;
        }
        
        // Geometry builders populate these lists before the layout is used by instances.
        internal List<Vector2> MutablePolygonVertices => _polygonVertices;
        internal List<int> MutableNeighborIdList => _neighborIdList;
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
            _polygonVertices ??= new List<Vector2>();
            _neighborIdList ??= new List<int>();
            _polygonVerticesView = _polygonVertices.AsReadOnly();
            _neighborIdView = _neighborIdList.AsReadOnly();
        }
        #endregion
    }
}
