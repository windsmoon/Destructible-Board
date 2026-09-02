using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public class LocalDestructibleBehaviour : MonoBehaviour
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
        [SerializeField]
        private bool _enableDelaunayDebug = false;
        [SerializeField]
        private bool _enableVoronoiDebug = false;

        // [SerializeField, HideInInspector] 
        private List<DestructibleCell> _cellList;
        private List<Vector2> _siteList;
        private List<DelaunayTriangle> _delaunayTriangleList;
        #endregion

        #region properties
        public IReadOnlyList<DestructibleCell> CellList => _cellList;
        public int SamplePointCount => _siteList?.Count ?? 0;
        public int DelaunayTriangleCount => _delaunayTriangleList?.Count ?? 0;
        public int VoronoiRegionCount
        {
            get
            {
                if (_cellList == null)
                {
                    return 0;
                }

                int regionCount = 0;
                for (int cellIndex = 0; cellIndex < _cellList.Count; cellIndex++)
                {
                    List<Vector2> polygon = _cellList[cellIndex].Polygon;
                    if (polygon.Count >= 3)
                    {
                        regionCount++;
                    }
                }

                return regionCount;
            }
        }
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

            if (_enableDebugMode)
            {
                if (_enableDelaunayDebug)
                {
                    DebugDelaunay();

                }

                if (_enableVoronoiDebug)
                {
                    DebugVoronoi();  
                }

                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }
        #endregion

        #region methods
        public void Generate()
        {
            _cellList ??= new List<DestructibleCell>(_maxFragmentCount);
            _siteList ??= new List<Vector2>(_maxFragmentCount);
            _delaunayTriangleList ??= new List<DelaunayTriangle>(_maxFragmentCount);

            _cellList.Clear();
            _siteList.Clear();
            _delaunayTriangleList.Clear();
            
            GenerateSamplePoints();
            GenerateDelaunayTriangles();
            GenerateVoronoiCells();
        }
        
        private void GenerateSamplePoints()
        {
            PoissonDiskSampler.Generate(new Vector2(_width, _height), _fragmentSize, _seed, _maxFragmentCount, _siteList);

            foreach (Vector2 site in _siteList)
            {
                DestructibleCell destructibleCell = new DestructibleCell()
                {
                    Id = _cellList.Count,
                    Site = site,
                    Polygon = new List<Vector2>(),
                };
                _cellList.Add(destructibleCell);
            }
        }

        private void GenerateDelaunayTriangles()
        {
            DelaunayTriangulator.Generate(_siteList, _delaunayTriangleList);
        }

        private void GenerateVoronoiCells()
        {
            VoronoiGenerator.Generate(new Vector2(_width, _height), _siteList, _delaunayTriangleList, _cellList);
        }

        private void DebugDelaunay()
        {
            float siteRadius = Mathf.Max(0.01f, _fragmentSize * 0.08f);
            foreach (DestructibleCell cell in _cellList)
            {
                Gizmos.DrawSphere(new Vector3(cell.Site.x, cell.Site.y, 0f), siteRadius);
            }

            Gizmos.color = Color.yellow;
            foreach (DelaunayTriangle triangle in _delaunayTriangleList)
            {
                Vector2 a = _siteList[triangle.A];
                Vector2 b = _siteList[triangle.B];
                Vector2 c = _siteList[triangle.C];
                Gizmos.DrawLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
                Gizmos.DrawLine(new Vector3(b.x, b.y, 0f), new Vector3(c.x, c.y, 0f));
                Gizmos.DrawLine(new Vector3(c.x, c.y, 0f), new Vector3(a.x, a.y, 0f));
            }
        }

        private void DebugVoronoi()
        {
            Gizmos.color = Color.green;
            foreach (DestructibleCell cell in _cellList)
            {
                if (cell.Polygon == null || cell.Polygon.Count < 2)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < cell.Polygon.Count; pointIndex++)
                {
                    Vector2 current = cell.Polygon[pointIndex];
                    Vector2 next = cell.Polygon[(pointIndex + 1) % cell.Polygon.Count];
                    Gizmos.DrawLine(new Vector3(current.x, current.y, 0f), new Vector3(next.x, next.y, 0f));
                }
            } 
        }
        #endregion
    }
}
