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
        [SerializeField, Min(1)] 
        private int _seed = 1;
        [SerializeField, Min(1)] 
        private int _maxFragmentCount = 300;
        
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugMode = false;

        // [SerializeField, HideInInspector] 
        private List<DestructibleCell> _cellList = new List<DestructibleCell>();
        private List<Vector2> _siteListCache = new List<Vector2>();
        #endregion

        #region properties
        public IReadOnlyList<DestructibleCell> CellList => _cellList;
        #endregion

        #region unity methods
        private void OnValidate()
        {
            if (_enableDebugMode)
            {
                Generate();
            }
        }

        private void OnDrawGizmos()
        {

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width, _height, _thickness));

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
            
            if (_enableDebugMode == false)
            {
                return;
            }
            
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            float siteRadius = Mathf.Max(0.01f, _fragmentSize * 0.08f);
            foreach (DestructibleCell cell in _cellList)
            {
                Gizmos.DrawSphere(new Vector3(cell.Site.x, cell.Site.y, 0f), siteRadius);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
        #endregion

        #region methods
        public void Generate()
        {
            GenerateSamplePoints();    
        }
        
        private void GenerateSamplePoints()
        {
            _cellList.Clear();
            PoissonDiskSampler.Generate(new Vector2(_width, _height), _fragmentSize, _seed, _maxFragmentCount, _siteListCache);
            if (_cellList.Capacity < _siteListCache.Count)
            {
                _cellList.Capacity = _siteListCache.Count;
            }

            foreach (Vector2 site in _siteListCache)
            {
                DestructibleCell destructibleCell = new DestructibleCell()
                {
                    Id = _cellList.Count,
                    Site = site,
                };
                _cellList.Add(destructibleCell);
            }
        }
        #endregion
    }
}
