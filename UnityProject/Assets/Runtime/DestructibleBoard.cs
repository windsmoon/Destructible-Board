using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    public class DestructibleBoard : MonoBehaviour
    {
        #region fields
        [Header("Panel")]
        [SerializeField]
        private Shape _shape = Shape.Rectangle;
        [SerializeField, Min(0.01f)] 
        private float _width = 10f;
        [SerializeField, Min(0.01f)] 
        private float _height = 6f;
        [SerializeField, Min(0.01f)]
        private float _radius = 3f;
        [SerializeField, Min(0.01f)]
        private float _ellipseHorizontalRadius = 4f;
        [SerializeField, Min(0.01f)]
        private float _ellipseVerticalRadius = 2.5f;
        [SerializeField, Min(0.01f)]
        private float _capsuleWidth = 6f;
        [SerializeField, Min(0.01f)]
        private float _capsuleHeight = 3f;
        [SerializeField, Min(0.01f)]
        private float _sectorRadius = 3f;
        [SerializeField, Range(1f, 180f)]
        private float _sectorAngle = 90f;
        [SerializeField, Range(3, 32)]
        private int _regularPolygonEdgeCount = 6;
        [SerializeField, Min(0.01f)]
        private float _regularPolygonRadius = 3f;
        [SerializeField, Range(8, 64), Tooltip("Number of straight edges used to approximate curved panel outlines.")]
        private int _circleSegments = 64;
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
        private readonly List<Vector2> _panelPolygon = new List<Vector2>(64);
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
        private Vector2 PanelSize => _shape switch
        {
            Shape.Circle => Vector2.one * (_radius * 2f),
            Shape.Ellipse => new Vector2(_ellipseHorizontalRadius * 2f, _ellipseVerticalRadius * 2f),
            Shape.Capsule => new Vector2(_capsuleWidth, _capsuleHeight),
            // Sector rotation can place its arc anywhere around the origin, so use
            // the containing circle as the centered sampling bounds.
            Shape.Sector => Vector2.one * (_sectorRadius * 2f),
            // Rotation changes the exact AABB, so keep a stable centered bound.
            Shape.RegularPolygon => Vector2.one * (_regularPolygonRadius * 2f),
            _ => new Vector2(_width, _height),
        };
        private int CurvedSegmentCount => Mathf.Clamp(_circleSegments, 8, 64);
        private int CapsuleHalfArcSegmentCount => Mathf.Max(2, CurvedSegmentCount / 2);
        private int SectorArcSegmentCount => Mathf.Max(1, Mathf.CeilToInt(CurvedSegmentCount * (_sectorAngle / 360f)));
        private int PanelVertexCount => _shape switch
        {
            Shape.Circle => CurvedSegmentCount,
            Shape.Ellipse => CurvedSegmentCount,
            // one semicircle has (CapsuleHalfArcSegmentCount + 1) vertices, Capsule has two semicircles 
            Shape.Capsule => Mathf.Approximately(_capsuleWidth, _capsuleHeight) ? CurvedSegmentCount : (CapsuleHalfArcSegmentCount + 1) * 2,
            // One center vertex plus both endpoints of the subdivided arc.
            Shape.Sector => SectorArcSegmentCount + 2,
            Shape.RegularPolygon => Mathf.Clamp(_regularPolygonEdgeCount, 3, 64),
            _ => 4,
        };
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
            DebugPanelOutline();

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
        /// Configures a circular panel before runtime generation. This is useful for
        /// samples and other procedurally assembled scenes that cannot serialize a
        /// preconfigured component.
        /// </summary>
        public void ConfigureCircle(float radius, float thickness, float fragmentSize, int seed, int maxFragmentCount, int circleSegments, Material material)
        {
            _shape = Shape.Circle;
            _radius = Mathf.Max(0.01f, radius);
            _thickness = Mathf.Max(0.01f, thickness);
            _fragmentSize = Mathf.Max(0.01f, fragmentSize);
            _seed = seed;
            _maxFragmentCount = Mathf.Max(1, maxFragmentCount);
            _circleSegments = Mathf.Clamp(circleSegments, 8, 64);
            _material = material;
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
        /// Marks a generated cell as destroyed without changing or destroying its
        /// GameObject, Collider, Mesh, or topology data.
        /// </summary>
        public bool DestroyCellLogically(int cellId)
        {
            if (TryGetCell(cellId, out DestructibleCell cell) == false || cell.Destroyed)
            {
                return false;
            }

            cell.Destroyed = true;
            // DestructibleCell is a value type, so persist the changed state.
            _cellList[cellId] = cell;

            // A logically destroyed collider no longer represents an active board cell.
            if (cell.Collider != null)
            {
                _cellIndexByCollider.Remove(cell.Collider);
            }

            return true;
        }

        /// <summary>
        /// Resolves a generated fragment collider and marks its cell as logically destroyed.
        /// </summary>
        public bool DestroyCellLogically(Collider collider)
        {
            return TryGetCellId(collider, out int cellId) && DestroyCellLogically(cellId);
        }

        /// <summary>
        /// Collects non-destroyed cells from the start cell's neighbor rings in
        /// breadth-first order. Destroyed cells are never returned and can optionally
        /// block traversal through existing holes. Depth zero is the start cell, and
        /// the supplied list is cleared first.
        /// </summary>
        public int CollectCellsByDepth(int startCellId, int maxDepth, List<CellSearchResult> results, bool destroyedCellsBlockPropagation)
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

                    if (depth == maxDepth || (cell.Destroyed && destroyedCellsBlockPropagation))
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
        public bool CollectCellsByDepth(Collider collider, int maxDepth, List<CellSearchResult> results, bool destroyedCellsBlockPropagation = false)
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

            CollectCellsByDepth(startCellId, maxDepth, results, destroyedCellsBlockPropagation);
            return true;
        }

        /// <summary>
        /// Returns each surviving connected component that contains no boundary
        /// cell. Destroyed cells block connectivity and are excluded from results.
        /// Each island's IDs are sorted, and islands are ordered by their lowest ID.
        /// Results are independent snapshots; querying never destroys or detaches cells.
        /// Returns an empty list before generation or when no islands remain.
        /// </summary>
        public bool TryGetIslands(List<List<int>> islands)
        {
            if (_cellList == null || _cellList.Count == 0)
            {
                return false;
            }

            BeginCellSearch();
            for (int startCellId = 0; startCellId < _cellList.Count; startCellId++)
            {
                if (_cellList[startCellId].Destroyed || _searchVisitVersions[startCellId] == _searchVersion)
                {
                    continue;
                }

                // Reuse the search buffer as a BFS queue and component accumulator.
                _currentSearchLayer.Clear();
                _currentSearchLayer.Add(startCellId);
                _searchVisitVersions[startCellId] = _searchVersion;
                bool containsBoundary = false;

                for (int queueIndex = 0; queueIndex < _currentSearchLayer.Count; queueIndex++)
                {
                    DestructibleCell cell = _cellList[_currentSearchLayer[queueIndex]];
                    containsBoundary |= cell.IsBoundary;

                    // Finish traversing even after finding support, so the rest of
                    // this component cannot be mistaken for a separate island.
                    foreach (int neighborId in cell.NeighborList)
                    {
                        if (_cellList[neighborId].Destroyed || _searchVisitVersions[neighborId] == _searchVersion)
                        {
                            continue;
                        }

                        _searchVisitVersions[neighborId] = _searchVersion;
                        _currentSearchLayer.Add(neighborId);
                    }
                }

                if (containsBoundary == false)
                {
                    // Copy before reusing the buffer, preserving earlier query results.
                    _currentSearchLayer.Sort();
                    islands.Add(new List<int>(_currentSearchLayer));
                }
            }

            return islands.Count > 0;
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

            // Sampling, clipping and preview all use the same local-space outline.
            _panelPolygon.Clear();
            for (int vertexIndex = 0; vertexIndex < PanelVertexCount; vertexIndex++)
            {
                _panelPolygon.Add(GetPanelVertex(vertexIndex));
            }
            
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
            PoissonDiskSampler.Generate(PanelSize, _panelPolygon, _fragmentSize, _seed, _maxFragmentCount, _siteList);

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
            VoronoiGenerator.Generate(_panelPolygon, _siteList, _delaunayTriangleList, _cellList);
        }

        private void GenerateNeighborGraph()
        {
            NeighborGraphBuilder.Generate(PanelSize, _delaunayTriangleList, _cellList);
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

        private Vector2 GetPanelVertex(int vertexIndex)
        {
            if (_shape == Shape.Circle || _shape == Shape.Ellipse)
            {
                // Increasing angles produce the counter-clockwise convex outline
                // required by both half-plane clipping and fragment extrusion.
                float angle = vertexIndex * (Mathf.PI * 2f / PanelVertexCount);
                Vector2 radii = _shape == Shape.Circle ? Vector2.one * _radius : new Vector2(_ellipseHorizontalRadius, _ellipseVerticalRadius);
                return new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
            }

            if (_shape == Shape.Capsule)
            {
                return GetCapsuleVertex(vertexIndex);
            }

            if (_shape == Shape.Sector)
            {
                return GetSectorVertex(vertexIndex);
            }

            if (_shape == Shape.RegularPolygon)
            {
                return GetRegularPolygonVertex(vertexIndex);
            }

            Vector2 halfSize = PanelSize * 0.5f;
            return vertexIndex switch
            {
                0 => new Vector2(-halfSize.x, -halfSize.y),
                1 => new Vector2(halfSize.x, -halfSize.y),
                2 => new Vector2(halfSize.x, halfSize.y),
                _ => new Vector2(-halfSize.x, halfSize.y),
            };
        }

        private Vector2 GetCapsuleVertex(int vertexIndex)
        {
            float halfWidth = _capsuleWidth * 0.5f;
            float halfHeight = _capsuleHeight * 0.5f;
            if (Mathf.Approximately(_capsuleWidth, _capsuleHeight))
            {
                float circleAngle = vertexIndex * (Mathf.PI * 2f / PanelVertexCount);
                return new Vector2(Mathf.Cos(circleAngle), Mathf.Sin(circleAngle)) * halfWidth;
            }

            int halfArcSegments = CapsuleHalfArcSegmentCount;
            bool isFirstArc = vertexIndex <= halfArcSegments;
            int vertexIndexInArc = isFirstArc ? vertexIndex : vertexIndex - halfArcSegments - 1;
            float arcProgress = vertexIndexInArc / (float)halfArcSegments;

            // first semicircle is at right
            // second is at left
            if (_capsuleWidth > _capsuleHeight)
            {
                float radius = halfHeight;
                float centerOffset = halfWidth - radius;
                // we need the counterclockwise vertex order
                float angle = (isFirstArc ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f) + arcProgress * Mathf.PI;
                float centerX = isFirstArc ? centerOffset : -centerOffset;
                return new Vector2(centerX + Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            // same as before
            // but first semicircle is at up, second is at down
            float verticalRadius = halfWidth;
            float verticalCenterOffset = halfHeight - verticalRadius;
            // we need the counterclockwise vertex order
            float verticalAngle = (isFirstArc ? 0f : Mathf.PI) + arcProgress * Mathf.PI;
            float centerY = isFirstArc ? verticalCenterOffset : -verticalCenterOffset;
            return new Vector2(Mathf.Cos(verticalAngle) * verticalRadius, centerY + Mathf.Sin(verticalAngle) * verticalRadius);
        }

        private Vector2 GetSectorVertex(int vertexIndex)
        {
            if (vertexIndex == 0)
            {
                return Vector2.zero;
            }

            int arcVertexIndex = vertexIndex - 1;
            float arcProgress = arcVertexIndex / (float)SectorArcSegmentCount;
            float angle = arcProgress * _sectorAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _sectorRadius;
        }

        private Vector2 GetRegularPolygonVertex(int vertexIndex)
        {
            float angleStep = Mathf.PI * 2f / PanelVertexCount;
            float angle = vertexIndex * angleStep;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _regularPolygonRadius;
        }

        private void DebugPanelOutline()
        {
            float halfThickness = _thickness * 0.5f;
            int vertexCount = PanelVertexCount;
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                Vector2 current = GetPanelVertex(vertexIndex);
                Vector2 next = GetPanelVertex((vertexIndex + 1) % vertexCount);
                Vector3 front = new Vector3(current.x, current.y, halfThickness);
                Vector3 back = new Vector3(current.x, current.y, -halfThickness);
                Gizmos.DrawLine(front, new Vector3(next.x, next.y, halfThickness));
                Gizmos.DrawLine(back, new Vector3(next.x, next.y, -halfThickness));
                if (vertexIndex == 0 || vertexIndex == vertexCount / 4 ||
                    vertexIndex == vertexCount / 2 || vertexIndex == vertexCount * 3 / 4)
                {
                    Gizmos.DrawLine(front, back);
                }
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
            foreach (DestructibleCell cell in _cellList)
            {
                if (cell.Polygon == null || cell.Polygon.Count < 2)
                {
                    continue;
                }

                Gizmos.color = cell.IsBoundary ? Color.magenta : Color.green;
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
