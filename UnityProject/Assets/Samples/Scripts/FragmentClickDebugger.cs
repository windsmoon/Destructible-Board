using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Windsmoon.DesctructibleBoard.Samples
{
    public class FragmentClickDebugger : MonoBehaviour
    {
        #region fields
        [SerializeField]
        private Camera _camera;
        [SerializeField, Min(0.01f)]
        private float _maxRayDistance = 1000f;
        [SerializeField]
        private LayerMask _layerMask = Physics.DefaultRaycastLayers;

        [Header("Collapse")]
        [SerializeField, Min(0), Tooltip("Maximum neighbor depth for left-click collapse. Right-click has no depth limit.")]
        private int _maxDepth = 2;
        [SerializeField, Min(0f), Tooltip("Delay between neighbor layers for left-click collapse.")]
        private float _depthInterval = 0.25f;
        [SerializeField, Min(0f), Tooltip("Delay between random queue drops for right-click collapse.")]
        private float _dropInterval = 0.15f;
        [SerializeField, Min(1), Tooltip("Minimum cells dropped from the queue each interval.")]
        private int _minDropCount = 1;
        [SerializeField, Min(1), Tooltip("Maximum cells dropped from the queue each interval.")]
        private int _maxDropCount = 5;
        [SerializeField]
        private bool _destroyedCellsBlockPropagation = false;
        [SerializeField, Min(0.001f)]
        private float _fragmentMass = 1f;
        [SerializeField, Min(0f), Tooltip("One-time downward impulse in world space when a fragment detaches.")]
        private float _downwardImpulse = 5f;
        private readonly List<List<int>> _islands = new List<List<int>>();
        #endregion

        #region unity methods
        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                Debug.LogError("FragmentClickDebugger requires a camera.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (_camera == null || mouse == null)
            {
                return;
            }

            bool rightClicked = mouse.rightButton.wasPressedThisFrame;
            if (!rightClicked && !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _layerMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // The hierarchy identifies the owner; the cached lookup identifies
            // the cell without scanning every fragment or parsing object names.
            DestructibleBoard board = hit.collider.GetComponentInParent<DestructibleBoard>();
            if (board == null || !board.TryGetCell(hit.collider, out DestructibleCell cell))
            {
                return;
            }

            if (rightClicked)
            {
                StartCoroutine(DropCellsFromQueue(board, cell.Id));
                return;
            }

            List<CellSearchResult> searchResults = new List<CellSearchResult>();
            board.CollectCellsByDepth(
                cell.Id,
                _maxDepth,
                searchResults,
                _destroyedCellsBlockPropagation);

            if (searchResults.Count > 0)
            {
                StartCoroutine(DropCellsByDepth(board, searchResults));
            }
        }
        #endregion

        #region methods
        private IEnumerator DropCellsFromQueue(DestructibleBoard board, int startCellId)
        {
            if (board == null || !board.TryGetCell(startCellId, out DestructibleCell startCell))
            {
                yield break;
            }

            // Cell IDs are stable indices within a generation, so a visited flag
            // array sized to the current cell count safely tracks enqueued cells.
            int cellCount = board.CellList.Count;
            bool[] enqueued = new bool[cellCount];
            Queue<int> pending = new Queue<int>(cellCount);
            List<int> startNeighbors = startCell.NeighborList;
            WaitForSeconds dropDelay = _dropInterval > 0f ? new WaitForSeconds(_dropInterval) : null;

            // Start from the hit cell: capture its surviving neighbors into the
            // queue before dropping it so our own destruction never blocks the wave.
            enqueued[startCellId] = true;
            EnqueueAliveNeighbors(board, startCell, enqueued, pending);
            DropCell(board, startCellId);

            while (pending.Count > 0)
            {
                // Generate replaces each cell's neighbor list. Stop an old wave
                // instead of applying its queued IDs to a newly generated board.
                if (board == null || board.CellList.Count != cellCount ||
                    !board.TryGetCell(startCellId, out startCell) ||
                    !ReferenceEquals(startCell.NeighborList, startNeighbors))
                {
                    yield break;
                }

                if (dropDelay != null)
                {
                    yield return dropDelay;
                }

                int dropCount = Random.Range(_minDropCount, _maxDropCount + 1);
                for (int index = 0; index < dropCount && pending.Count > 0; index++)
                {
                    int cellId = pending.Dequeue();
                    if (!board.TryGetCell(cellId, out DestructibleCell cell) || cell.Destroyed)
                    {
                        continue;
                    }

                    // Capture this cell's surviving neighbors before dropping it.
                    EnqueueAliveNeighbors(board, cell, enqueued, pending);
                    DropCell(board, cellId);
                }
            }
        }

        /// <summary>
        /// Adds every still-existing, not-yet-enqueued neighbor to the pending
        /// queue. Destroyed neighbors are skipped so holes block propagation.
        /// </summary>
        private void EnqueueAliveNeighbors(
            DestructibleBoard board,
            DestructibleCell cell,
            bool[] enqueued,
            Queue<int> pending)
        {
            foreach (int neighborId in cell.NeighborList)
            {
                if (enqueued[neighborId])
                {
                    continue;
                }

                if (!board.TryGetCell(neighborId, out DestructibleCell neighbor) || neighbor.Destroyed)
                {
                    continue;
                }

                enqueued[neighborId] = true;
                pending.Enqueue(neighborId);
            }
        }

        private IEnumerator DropCellsByDepth(
            DestructibleBoard board,
            List<CellSearchResult> searchResults)
        {
            int currentDepth = searchResults[0].Depth;

            foreach (CellSearchResult result in searchResults)
            {
                if (result.Depth != currentDepth)
                {
                    int depthDelta = result.Depth - currentDepth;
                    currentDepth = result.Depth;
                    if (_depthInterval > 0f)
                    {
                        // Preserve elapsed time for empty rings whose cells were
                        // already destroyed and therefore omitted from the results.
                        yield return new WaitForSeconds(_depthInterval * depthDelta);
                    }
                }

                DropCell(board, result.CellId);
            }
        }

        private void DropCell(DestructibleBoard board, int cellId)
        {
            if (!TryDropCell(board, cellId))
            {
                return;
            }

            _islands.Clear();
            if (board.TryGetIslands(_islands))
            {
                // Islands are already disconnected from every supported component.
                // Drop the entire snapshot without recursively querying each fragment.
                foreach (List<int> island in _islands)
                {
                    foreach (int islandCellId in island)
                    {
                        TryDropCell(board, islandCellId);
                    }
                }
            }

            _islands.Clear();
        }

        private bool TryDropCell(DestructibleBoard board, int cellId)
        {
            if (board == null ||
                board.TryGetCell(cellId, out DestructibleCell cell) == false ||
                cell.Destroyed ||
                cell.GameObject == null)
            {
                return false;
            }

            // The board only owns the logical state. This sample takes ownership
            // of the existing fragment object; the scene's trigger handles cleanup.
            if (board.DestroyCellLogically(cellId) == false)
            {
                return false;
            }

            GameObject fallingFragment = cell.GameObject;
            fallingFragment.name = $"Falling Fragment {cell.Id}";
            fallingFragment.transform.SetParent(null, true);

            if (fallingFragment.TryGetComponent(out Rigidbody rigidbody) == false)
            {
                rigidbody = fallingFragment.AddComponent<Rigidbody>();
            }

            rigidbody.mass = _fragmentMass;
            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;
            rigidbody.AddForce(Vector3.down * _downwardImpulse, ForceMode.Impulse);
            return true;
        }
        #endregion
    }
}
