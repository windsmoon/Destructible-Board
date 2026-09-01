using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public class LocalDestructiblePanel : MonoBehaviour
    {
        #region fields
        [Header("Panel")]
        [SerializeField, Min(0.01f)] 
        private float _width = 10f;
        [SerializeField, Min(0.01f)] 
        private float _height = 6f;
        [SerializeField, Min(0.01f)] 
        private float _thickness = 0.2f;

        [Header("Fragments")]
        [SerializeField, Min(0.01f)] 
        private float _fragmentSize = 0.5f;
        [SerializeField] 
        private int _seed = 1;
        [SerializeField, Min(1)] 
        private int _maxFragmentCount = 300;

        [SerializeField, HideInInspector] 
        private List<DestructibleCell> _cellList = new List<DestructibleCell>();
        #endregion

        #region properties
        public IReadOnlyList<DestructibleCell> CellList => _cellList;
        #endregion

        #region unity methods
        private void Awake()
        {
            Init();
        }

        private void OnDrawGizmos()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width, _height, _thickness));
            Gizmos.matrix = previousMatrix;
        }
        #endregion

        #region methods
        public void Init()
        {
            _cellList.Clear();
        }
        #endregion
    }
}
