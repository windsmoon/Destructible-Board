using UnityEngine;

namespace Windsmoon.DesctructibleBoard.Samples
{
    [RequireComponent(typeof(BoxCollider))]
    public class DestroyOnTrigger : MonoBehaviour
    {
        #region unity methods
        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // A child collider belongs to its attached Rigidbody's whole object.
            Rigidbody body = other.attachedRigidbody;
            Destroy(body != null ? body.gameObject : other.gameObject);
        }
        #endregion
    }
}
