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
        [SerializeField, Min(0)]
        private int _maxDepth = 2;
        [SerializeField, Min(0f)]
        private float _depthInterval = 0.25f;
        [SerializeField]
        private bool _destroyedCellsBlockPropagation = false;
        [SerializeField, Min(0.001f)]
        private float _fragmentMass = 1f;
        [SerializeField, Min(0.01f)]
        private float _fragmentLifetime = 5f;
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
            if (_camera == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
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
            if (board == null ||
                board.TryGetCell(cellId, out DestructibleCell cell) == false ||
                cell.Destroyed ||
                cell.GameObject == null)
            {
                return;
            }

            // The board only owns the logical state. This sample takes ownership
            // of the existing fragment object and controls its physical lifetime.
            if (board.DestroyCellLogically(cellId) == false)
            {
                return;
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

            Destroy(fallingFragment, _fragmentLifetime);
        }
        #endregion
    }
}
