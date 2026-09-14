using UnityEngine;

namespace CockroachFantasia.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NestZone : MonoBehaviour
    {
        [SerializeField] private Transform entrance;
        public Transform Entrance => entrance;

        public void Configure(Transform entranceMarker)
        {
            entrance = entranceMarker;
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.15f, 0.75f, 0.22f);
            var box = GetComponent<BoxCollider>();
            if (box != null)
                Gizmos.DrawCube(transform.TransformPoint(box.center), Vector3.Scale(box.size, transform.lossyScale));
        }
    }
}
