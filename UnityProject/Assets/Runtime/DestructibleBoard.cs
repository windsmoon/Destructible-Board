using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public class DestructibleBoard : MonoBehaviour
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
        [SerializeField]
        private Material _material;
        
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugMode = false;
        [SerializeField]
        private bool _enableDelaunayDebug = false;
        [SerializeField]
        private bool _enableVoronoiDebug = false;

        [SerializeField, Tooltip("Generated cell data. Replaced when Generate is called.")]
        private List<DestructibleCell> _cellList;
        private List<Vector2> _siteList;
        private List<DelaunayTriangle> _delaunayTriangleList;
        private readonly Dictionary<Collider, int> _cellIndexByCollider = new Dictionary<Collider, int>();
        private List<int> _currentSearchLayer = new List<int>();
        private List<int> _nextSearchLayer = new List<int>();
        private int[] _searchVisitVersions = Array.Empty<int>();
        private int _searchVersion;
        private int _fragmentVertexCount;
        private int _fragmentTriangleCount;
        private Transform _root;
        #endregion

        #region properties
        internal IReadOnlyList<DestructibleCell> CellList => _cellList;
        public int SamplePointCount => _siteList?.Count ?? 0;
        public int DelaunayTriangleCount => _delaunayTriangleList?.Count ?? 0;
        public int FragmentVertexCount => _fragmentVertexCount;
        public int FragmentTriangleCount => _fragmentTriangleCount;
        public int ColliderCount => _cellIndexByCollider.Count;
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
        private void Awake()
        {
            Generate();
        }

        private void OnDestroy()
        {
            ReleaseGameObjects();
            ReleaseFragmentMeshes();
        }

        private void OnDrawGizmos()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width, _height, _thickness));

            if (_enableDebugMode && _cellList != null)
            {
                // Serialized cells can load before the transient triangulation caches exist.
                if (_enableDelaunayDebug && _siteList != null && _delaunayTriangleList != null)
                {
                    DebugDelaunay();

                }

                if (_enableVoronoiDebug)
                {
                    DebugVoronoi();  
                }

            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
        #endregion

        #region methods
        /// <summary>
        /// Gets the cell represented by a generated fragment collider.
        /// </summary>
        public bool TryGetCell(Collider collider, out DestructibleCell cell)
        {
            if (collider != null && _cellIndexByCollider.TryGetValue(collider, out int cellIndex))
            {
                cell = _cellList[cellIndex];
                return true;
            }

            cell = default;
            return false;
        }

        /// <summary>
        /// Gets a cell by its stable ID without scanning the cell collection.
        /// </summary>
        public bool TryGetCell(int cellId, out DestructibleCell cell)
        {
            // Cell IDs are assigned from their list indices during generation.
            if (_cellList != null && cellId >= 0 && cellId < _cellList.Count && _cellList[cellId].Id == cellId)
            {
                cell = _cellList[cellId];
                return true;
            }

            cell = default;
            return false;
        }

        /// <summary>
        /// Gets the stable cell ID represented by a generated fragment collider.
        /// </summary>
        public bool TryGetCellId(Collider collider, out int cellId)
        {
            if (collider != null && _cellIndexByCollider.TryGetValue(collider, out int cellIndex))
            {
                cellId = _cellList[cellIndex].Id;
                return true;
            }

            cellId = -1;
            return false;
        }

        /// <summary>
        /// Marks a generated cell as destroyed and destroys its fragment GameObject.
        /// The cell and its original neighbor relationships remain available as data.
        /// </summary>
        public bool DestroyCell(int cellId)
        {
            if (TryGetCell(cellId, out DestructibleCell cell) == false || cell.Destroyed)
            {
                return false;
            }

            GameObject fragmentObject = cell.GameObject;
            Collider fragmentCollider = cell.Collider;
            
            cell.GameObject = null;
            cell.Collider = null;
            cell.Destroyed = true;
            // DestructibleCell is a value type, so persist the changed state.
            _cellList[cellId] = cell;

            if (fragmentCollider != null)
            {
                _cellIndexByCollider.Remove(fragmentCollider);
            }

            if (fragmentObject != null)
            {
                if (Application.isPlaying)
                {
                    // Hide and disable collision immediately; Unity performs the
                    // actual destruction at the end of the frame.
                    fragmentObject.SetActive(false);
                    Destroy(fragmentObject);
                }
                else
                {
                    DestroyImmediate(fragmentObject);
                }
            }

            return true;
        }

        /// <summary>
        /// Resolves a generated fragment collider and marks its cell as destroyed.
        /// </summary>
        public bool DestroyCell(Collider collider)
        {
            return TryGetCellId(collider, out int cellId) && DestroyCell(cellId);
        }

        /// <summary>
        /// Collects non-destroyed cells from the start cell's neighbor rings in
        /// breadth-first order. Destroyed cells remain traversal links so existing
        /// holes do not block propagation. Depth zero is the start cell, and the
        /// supplied list is cleared first.
        /// </summary>
        public int CollectCellsByDepth(int startCellId, int maxDepth, List<CellSearchResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (maxDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth cannot be negative.");
            }

            results.Clear();
            if (TryGetCell(startCellId, out _) == false)
            {
                return 0;
            }

            BeginCellSearch();
            _currentSearchLayer.Add(startCellId);
            _searchVisitVersions[startCellId] = _searchVersion;

            for (int depth = 0; depth <= maxDepth && _currentSearchLayer.Count > 0; depth++)
            {
                // Global sorting per layer makes output deterministic even when
                // several parents discover the same outer ring in different orders.
                _currentSearchLayer.Sort();
                foreach (var cellId in _currentSearchLayer)
                {
                    DestructibleCell cell = _cellList[cellId];
                    if (cell.Destroyed == false)
                    {
                        results.Add(new CellSearchResult(cellId, depth));
                    }

                    if (depth == maxDepth)
                    {
                        continue;
                    }

                    List<int> neighborList = cell.NeighborList;
                    foreach (var neighborId in neighborList)
                    {
                        // has found
                        if (_searchVisitVersions[neighborId] == _searchVersion)
                        {
                            continue;
                        }

                        _searchVisitVersions[neighborId] = _searchVersion;
                        _nextSearchLayer.Add(neighborId);
                    }
                }

                (_currentSearchLayer, _nextSearchLayer) = (_nextSearchLayer, _currentSearchLayer);
                _nextSearchLayer.Clear();
            }

            return results.Count;
        }
        
        /// <summary>
        /// Finds a cell from a generated fragment collider, then collects its neighbor rings.
        /// </summary>
        public bool CollectCellsByDepth(Collider collider, int maxDepth, List<CellSearchResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (maxDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth cannot be negative.");
            }

            results.Clear();
            if (TryGetCellId(collider, out int startCellId) == false)
            {
                return false;
            }

            CollectCellsByDepth(startCellId, maxDepth, results);
            return true;
        }

        public void Generate()
        {
            _cellList ??= new List<DestructibleCell>(_maxFragmentCount);
            _siteList ??= new List<Vector2>(_maxFragmentCount);
            _delaunayTriangleList ??= new List<DelaunayTriangle>(_maxFragmentCount);

            ReleaseGameObjects();
            ReleaseFragmentMeshes();
            _cellList.Clear();
            _siteList.Clear();
            _delaunayTriangleList.Clear();
            
            GenerateSamplePoints();
            GenerateDelaunayTriangles();
            GenerateVoronoiCells();
            GenerateNeighborGraph();
            CalculateFragmentMeshDebugInfo();

            // Editor preview only needs deterministic geometry data and counts.
            // Allocate Unity Mesh objects only during runtime initialization.
            if (Application.isPlaying)
            {
                GenerateFragmentMeshes();
                CreateGameObjects();
            }
        }
        
        private void GenerateSamplePoints()
        {
            PoissonDiskSampler.Generate(new Vector2(_width, _height), _fragmentSize, _seed, _maxFragmentCount, _siteList);

            foreach (Vector2 site in _siteList)
            {
                DestructibleCell destructibleCell = new DestructibleCell(_cellList.Count, site);
                _cellList.Add(destructibleCell);
            }
        }

        private void BeginCellSearch()
        {
            _currentSearchLayer.Clear();
            _nextSearchLayer.Clear();

            if (_searchVisitVersions.Length != _cellList.Count)
            {
                _searchVisitVersions = new int[_cellList.Count];
                _searchVersion = 1;
                return;
            }

            if (_searchVersion == int.MaxValue)
            {
                Array.Clear(_searchVisitVersions, 0, _searchVisitVersions.Length);
                _searchVersion = 1;
                return;
            }

            _searchVersion++;
        }

        private void GenerateDelaunayTriangles()
        {
            DelaunayTriangulator.Generate(_siteList, _delaunayTriangleList);
        }

        private void GenerateVoronoiCells()
        {
            VoronoiGenerator.Generate(new Vector2(_width, _height), _siteList, _delaunayTriangleList, _cellList);
        }

        private void GenerateNeighborGraph()
        {
            NeighborGraphBuilder.Generate(new Vector2(_width, _height), _delaunayTriangleList, _cellList);
        }

        private void CalculateFragmentMeshDebugInfo()
        {
            _fragmentVertexCount = 0;
            _fragmentTriangleCount = 0;

            for (int cellIndex = 0; cellIndex < _cellList.Count; cellIndex++)
            {
                List<Vector2> polygon = _cellList[cellIndex].Polygon;
                // Use the generator's topology formulas without allocating vertex
                // arrays, triangle indices, or a Unity Mesh object.
                _fragmentVertexCount += FragmentMeshGenerator.CalculateVertexCount(polygon.Count);
                _fragmentTriangleCount += FragmentMeshGenerator.CalculateTriangleCount(polygon.Count);
            }
        }

        private void GenerateFragmentMeshes()
        {
            for (int cellIndex = 0; cellIndex < _cellList.Count; cellIndex++)
            {
                DestructibleCell cell = _cellList[cellIndex];
                cell.Mesh = FragmentMeshGenerator.Generate(cell.Polygon, _thickness);
                cell.Mesh.name = $"Fragment Mesh {cell.Id}";
                // DestructibleCell is a value type, so persist the updated Mesh
                // reference by assigning the modified copy back into the list.
                _cellList[cellIndex] = cell;
            }
        }

        private void CreateGameObjects()
        {
            GameObject fragmentRootObject = new GameObject("Fragments");
            fragmentRootObject.layer = gameObject.layer;
            _root = fragmentRootObject.transform;
            _root.SetParent(transform, false);

            for (int cellIndex = 0; cellIndex < _cellList.Count; cellIndex++)
            {
                DestructibleCell cell = _cellList[cellIndex];
                GameObject fragmentObject = new GameObject($"Fragment {cell.Id}");
                fragmentObject.layer = gameObject.layer;
                fragmentObject.transform.SetParent(_root, false);

                MeshFilter meshFilter = fragmentObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = cell.Mesh;

                MeshRenderer meshRenderer = fragmentObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _material;
                cell.GameObject = fragmentObject;

                MeshCollider meshCollider = fragmentObject.AddComponent<MeshCollider>();
                meshCollider.convex = true;
                meshCollider.sharedMesh = cell.Mesh;
                cell.Collider = meshCollider;
                // Site indices and cell-list indices are aligned during generation.
                _cellIndexByCollider.Add(meshCollider, cellIndex);

                // DestructibleCell is a value type, so persist the updated object
                // reference by assigning the modified copy back into the list.
                _cellList[cellIndex] = cell;
            }
        }

        private void ReleaseGameObjects()
        {
            // Invalidate old lookups before deferred GameObject destruction.
            _cellIndexByCollider.Clear();
            if (_cellList != null)
            {
                for (int cellIndex = 0; cellIndex < _cellList.Count; cellIndex++)
                {
                    DestructibleCell cell = _cellList[cellIndex];
                    cell.GameObject = null;
                    cell.Collider = null;
                    _cellList[cellIndex] = cell;
                }
            }

            if (_root == null)
            {
                return;
            }

            GameObject fragmentRootObject = _root.gameObject;
            _root = null;

            if (Application.isPlaying)
            {
                // Disable immediately so repeated generation in the same frame does
                // not leave the old fragments visible until deferred destruction.
                fragmentRootObject.SetActive(false);
                Destroy(fragmentRootObject);
            }
            else
            {
                DestroyImmediate(fragmentRootObject);
            }
        }

        private void ReleaseFragmentMeshes()
        {
            if (_cellList == null)
            {
                return;
            }

            for (int cellIndex = 0; cellIndex < _cellList.Count; cellIndex++)
            {
                DestructibleCell cell = _cellList[cellIndex];
                if (cell.Mesh == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(cell.Mesh);
                }
                else
                {
                    DestroyImmediate(cell.Mesh);
                }

                cell.Mesh = null;
                _cellList[cellIndex] = cell;
            }
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
