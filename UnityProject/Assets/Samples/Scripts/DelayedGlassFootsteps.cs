using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard.Samples
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class DelayedGlassFootsteps : MonoBehaviour
    {
        #region fields
        [Header("Delayed break")]
        [SerializeField, Min(0f), Tooltip("Time from the first foot contact until the touched cell detaches.")]
        private float _breakDelay = 0.65f;
        [SerializeField, Range(0, 2), Tooltip("Neighbor rings primed by each foot contact.")]
        private int _neighborDepth = 1;
        [SerializeField, Min(0f), Tooltip("Extra delay for every neighboring ring.")]
        private float _neighborDelay = 0.16f;
        [SerializeField, Min(0f), Tooltip("Small deterministic timing variation that keeps the break wave organic.")]
        private float _delayJitter = 0.12f;
        [SerializeField, Min(0.02f), Tooltip("Minimum interval between new foot-contact samples.")]
        private float _stepInterval = 0.12f;
        [SerializeField, Min(0f), Tooltip("The capsule must be moving at least this fast before it primes glass.")]
        private float _minimumPlanarSpeed = 0.2f;

        [Header("Falling fragments")]
        [SerializeField, Min(0.001f)]
        private float _fragmentMass = 0.35f;
        [SerializeField, Min(0.1f)]
        private float _fragmentLifetime = 7f;
        [SerializeField]
        private Color _warningColor = new Color(1f, 0.58f, 0.06f, 0.92f);
        [SerializeField, Tooltip("Drop every surviving component that no longer reaches the panel boundary.")]
        private bool _dropUnsupportedIslands = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly HashSet<int> _pendingCellIds = new HashSet<int>();
        private readonly Dictionary<int, GameObject> _pendingObjects = new Dictionary<int, GameObject>();
        private readonly List<CellSearchResult> _searchResults = new List<CellSearchResult>();
        private readonly List<List<int>> _islands = new List<List<int>>();
        private readonly List<GameObject> _fallingFragments = new List<GameObject>();
        private MaterialPropertyBlock _propertyBlock;
        private Rigidbody _rigidbody;
        private float _nextStepTime;
        private int _brokenCellCount;
        #endregion

        #region properties
        public int PendingCellCount => _pendingCellIds.Count;
        public int BrokenCellCount => _brokenCellCount;
        #endregion

        #region unity methods
        private void Awake()
        {
            // UnityEngine.Object-backed helpers must be created after the
            // MonoBehaviour constructor has finished.
            _propertyBlock = new MaterialPropertyBlock();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (Time.time < _nextStepTime || !IsWalking())
            {
                return;
            }

            // The ray begins inside the capsule, so Unity ignores the player's
            // own collider and returns the exact fragment directly underfoot.
            if (Physics.Raycast(
                    transform.position + Vector3.up * 0.1f,
                    Vector3.down,
                    out RaycastHit hit,
                    1.4f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                TryPrimeCollider(hit.collider);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryPrimeFootContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryPrimeFootContact(collision);
        }
        #endregion

        #region methods
        public void Configure(
            float breakDelay,
            int neighborDepth,
            float neighborDelay,
            float delayJitter,
            float stepInterval,
            float minimumPlanarSpeed,
            float fragmentMass,
            float fragmentLifetime,
            Color warningColor,
            bool dropUnsupportedIslands)
        {
            _breakDelay = Mathf.Max(0f, breakDelay);
            _neighborDepth = Mathf.Clamp(neighborDepth, 0, 2);
            _neighborDelay = Mathf.Max(0f, neighborDelay);
            _delayJitter = Mathf.Max(0f, delayJitter);
            _stepInterval = Mathf.Max(0.02f, stepInterval);
            _minimumPlanarSpeed = Mathf.Max(0f, minimumPlanarSpeed);
            _fragmentMass = Mathf.Max(0.001f, fragmentMass);
            _fragmentLifetime = Mathf.Max(0.1f, fragmentLifetime);
            _warningColor = warningColor;
            _dropUnsupportedIslands = dropUnsupportedIslands;
        }

        public void ResetState()
        {
            StopAllCoroutines();

            foreach (GameObject pendingObject in _pendingObjects.Values)
            {
                if (pendingObject != null && pendingObject.TryGetComponent(out Renderer renderer))
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            foreach (GameObject fragment in _fallingFragments)
            {
                if (fragment == null)
                {
                    continue;
                }

                fragment.SetActive(false);
                Destroy(fragment);
            }

            _pendingCellIds.Clear();
            _pendingObjects.Clear();
            _fallingFragments.Clear();
            _searchResults.Clear();
            _islands.Clear();
            _brokenCellCount = 0;
            _nextStepTime = 0f;
        }

        private void TryPrimeFootContact(Collision collision)
        {
            if (Time.time < _nextStepTime ||
                !IsWalking() ||
                collision.collider == null ||
                !HasSupportingContact(collision))
            {
                return;
            }

            TryPrimeCollider(collision.collider);
        }

        private void TryPrimeCollider(Collider collider)
        {
            DestructibleBoard board = collider.GetComponentInParent<DestructibleBoard>();
            if (board == null || !board.TryGetCellId(collider, out int cellId))
            {
                return;
            }

            _nextStepTime = Time.time + _stepInterval;
            board.CollectCellsByDepth(cellId, _neighborDepth, _searchResults, true);

            foreach (CellSearchResult result in _searchResults)
            {
                if (!board.TryGetCell(result.CellId, out DestructibleCell cell) ||
                    cell.Destroyed ||
                    cell.GameObject == null ||
                    !_pendingCellIds.Add(result.CellId))
                {
                    continue;
                }

                _pendingObjects.Add(result.CellId, cell.GameObject);
                ShowWarning(cell.GameObject);

                float jitter = Deterministic01(result.CellId) * _delayJitter;
                float delay = _breakDelay + result.Depth * _neighborDelay + jitter;
                StartCoroutine(BreakAfterDelay(board, result.CellId, cell.GameObject, delay));
            }
        }

        private IEnumerator BreakAfterDelay(
            DestructibleBoard board,
            int cellId,
            GameObject scheduledObject,
            float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            _pendingCellIds.Remove(cellId);
            _pendingObjects.Remove(cellId);

            if (board == null ||
                !board.TryGetCell(cellId, out DestructibleCell cell) ||
                cell.Destroyed ||
                cell.GameObject == null ||
                cell.GameObject != scheduledObject)
            {
                yield break;
            }

            if (TryDropCell(board, cellId) && _dropUnsupportedIslands)
            {
                DropUnsupportedIslands(board);
            }
        }

        private void DropUnsupportedIslands(DestructibleBoard board)
        {
            _islands.Clear();
            if (!board.TryGetIslands(_islands))
            {
                return;
            }

            // TryGetIslands returns independent snapshots, so detaching one island
            // cannot invalidate the IDs of the remaining islands in this pass.
            foreach (List<int> island in _islands)
            {
                foreach (int islandCellId in island)
                {
                    TryDropCell(board, islandCellId);
                }
            }

            _islands.Clear();
        }

        private bool TryDropCell(DestructibleBoard board, int cellId)
        {
            if (!board.TryGetCell(cellId, out DestructibleCell cell) ||
                cell.Destroyed ||
                cell.GameObject == null ||
                !board.DestroyCellLogically(cellId))
            {
                return false;
            }

            _pendingCellIds.Remove(cellId);
            _pendingObjects.Remove(cellId);

            GameObject fragment = cell.GameObject;
            fragment.name = $"Falling Glass Fragment {cell.Id}";
            fragment.transform.SetParent(null, true);

            if (!fragment.TryGetComponent(out Rigidbody rigidbody))
            {
                rigidbody = fragment.AddComponent<Rigidbody>();
            }

            rigidbody.mass = _fragmentMass;
            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            _fallingFragments.Add(fragment);
            _brokenCellCount++;
            Destroy(fragment, _fragmentLifetime);
            return true;
        }

        private void ShowWarning(GameObject fragment)
        {
            if (!fragment.TryGetComponent(out Renderer renderer))
            {
                return;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, _warningColor);
            _propertyBlock.SetColor(ColorId, _warningColor);
            _propertyBlock.SetColor(EmissionColorId, _warningColor * 1.8f);
            renderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
        }

        private bool HasSupportingContact(Collision collision)
        {
            for (int contactIndex = 0; contactIndex < collision.contactCount; contactIndex++)
            {
                ContactPoint contact = collision.GetContact(contactIndex);
                // Unity reports the contact normal from the perspective of the
                // receiving collider, so its sign can differ between collision
                // callbacks. Position plus absolute verticality identifies feet.
                if (contact.point.y < transform.position.y - 0.35f &&
                    Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) > 0.35f)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Deterministic01(int cellId)
        {
            uint value = (uint)cellId;
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215f;
        }

        private bool IsWalking()
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            float minimumSpeedSquared = _minimumPlanarSpeed * _minimumPlanarSpeed;
            return velocity.x * velocity.x + velocity.z * velocity.z >= minimumSpeedSquared;
        }
        #endregion
    }
}
