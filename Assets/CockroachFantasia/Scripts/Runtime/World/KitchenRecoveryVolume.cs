using UnityEngine;

namespace CockroachFantasia.World
{
    public interface IKitchenRecoverable
    {
        void RecoverTo(Vector3 position);
    }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class KitchenRecoveryVolume : MonoBehaviour
    {
        [SerializeField] private Transform recoveryPoint;
        public Transform RecoveryPoint => recoveryPoint;

        public void Configure(Transform point)
        {
            recoveryPoint = point;
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (recoveryPoint == null) return;
            foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is not IKitchenRecoverable recoverable) continue;
                recoverable.RecoverTo(recoveryPoint.position);
                return;
            }
        }
    }
}
