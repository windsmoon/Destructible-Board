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

            Debug.Log($"[{board.name}] Cell {cell.Id}, Neighbors: [{string.Join(", ", cell.NeighborList)}]", cell.GameObject);
        }
        #endregion
    }
}
