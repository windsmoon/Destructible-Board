using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Windsmoon.DesctructibleBoard.Samples
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CapsuleWalker : MonoBehaviour
    {
        #region fields
        [SerializeField, Min(0.1f)]
        private float _moveSpeed = 4.2f;
        [SerializeField, Min(0.1f)]
        private float _acceleration = 28f;
        [SerializeField, Min(0.1f)]
        private float _turnSpeed = 12f;

        private Rigidbody _rigidbody;
        private Vector3 _spawnPosition;
        private Vector2 _moveInput;
        #endregion

        #region properties
        public Vector3 WorldPosition => _rigidbody != null ? _rigidbody.position : transform.position;
        #endregion

        #region unity methods
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _spawnPosition = transform.position;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            float horizontal = ReadAxis(keyboard.aKey, keyboard.dKey) +
                               ReadAxis(keyboard.leftArrowKey, keyboard.rightArrowKey);
            float vertical = ReadAxis(keyboard.sKey, keyboard.wKey) +
                             ReadAxis(keyboard.downArrowKey, keyboard.upArrowKey);
            _moveInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void FixedUpdate()
        {
            Vector3 targetPlanarVelocity = new Vector3(_moveInput.x, 0f, _moveInput.y) * _moveSpeed;
            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 currentPlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 nextPlanarVelocity = Vector3.MoveTowards(
                currentPlanarVelocity,
                targetPlanarVelocity,
                _acceleration * Time.fixedDeltaTime);

            _rigidbody.linearVelocity = new Vector3(nextPlanarVelocity.x, velocity.y, nextPlanarVelocity.z);

            if (targetPlanarVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetPlanarVelocity, Vector3.up);
                _rigidbody.MoveRotation(Quaternion.Slerp(
                    _rigidbody.rotation,
                    targetRotation,
                    _turnSpeed * Time.fixedDeltaTime));
            }
        }
        #endregion

        #region methods
        public void Configure(float moveSpeed, float acceleration, float turnSpeed)
        {
            _moveSpeed = Mathf.Max(0.1f, moveSpeed);
            _acceleration = Mathf.Max(0.1f, acceleration);
            _turnSpeed = Mathf.Max(0.1f, turnSpeed);
        }

        public void ResetToSpawn()
        {
            // Clear interpolation history around the teleport. Otherwise the
            // rendered Transform can remain below the reset threshold for several
            // frames and repeatedly trigger another reset.
            RigidbodyInterpolation interpolation = _rigidbody.interpolation;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.position = _spawnPosition;
            _rigidbody.rotation = Quaternion.identity;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            _rigidbody.interpolation = interpolation;
            _rigidbody.WakeUp();
        }

        private static float ReadAxis(KeyControl negative, KeyControl positive)
        {
            return (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
        }
        #endregion
    }
}
