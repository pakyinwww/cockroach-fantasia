using CockroachFantasia.Food;
using CockroachFantasia.Audio;
using Unity.Netcode;
using UnityEngine;

namespace CockroachFantasia.Characters
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class StylizedCharacterAnimator : NetworkBehaviour
    {
        [SerializeField] private Transform artRoot;
        [SerializeField] private Transform leftAntenna;
        [SerializeField] private Transform rightAntenna;

        private Vector3 restPosition;
        private Vector3 restScale;
        private Vector3 previousPosition;
        private bool isCockroach;
        private float stepDistance;

        public Transform ArtRoot => artRoot;

        public void Configure(Transform root, Transform antennaLeft = null, Transform antennaRight = null)
        {
            artRoot = root;
            leftAntenna = antennaLeft;
            rightAntenna = antennaRight;
        }

        private void Awake()
        {
            isCockroach = GetComponent<CockroachMotor>() != null;
            if (artRoot == null) return;
            restPosition = artRoot.localPosition;
            restScale = artRoot.localScale;
            previousPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            if (!isCockroach && IsOwner)
                foreach (var itemRenderer in artRoot.GetComponentsInChildren<Renderer>(true))
                    itemRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (artRoot == null) return;
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;
            var speed = new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            stepDistance += new Vector2(delta.x, delta.z).magnitude;
            var stride = isCockroach ? 0.38f : 1.15f;
            if (speed > 0.15f && stepDistance >= stride)
            {
                stepDistance = 0f;
                GameAudio.Play(isCockroach ? GameAudioCue.CockroachStep : GameAudioCue.HumanStep,
                    transform.position);
                if (isCockroach && GetComponent<CockroachFoodCarrier>()?.IsCarrying == true)
                    GameAudio.Play(GameAudioCue.FoodRustle, transform.position, 0.035f);
            }
            var moving = Mathf.Clamp01(speed / (isCockroach ? 3.2f : 4.5f));
            var phase = Time.time * (isCockroach ? 15f : 9f);
            artRoot.localPosition = restPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * 0.025f * moving);
            artRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * (isCockroach ? 5f : 2f) * moving);
            var carrying = GetComponent<CockroachFoodCarrier>()?.IsCarrying == true;
            var targetScale = carrying
                ? Vector3.Scale(restScale, new Vector3(1.08f, 0.9f, 1.12f))
                : restScale;
            artRoot.localScale = Vector3.Lerp(artRoot.localScale, targetScale, 12f * Time.deltaTime);
            if (leftAntenna != null)
                leftAntenna.localRotation = Quaternion.Euler(38f + Mathf.Sin(Time.time * 9f) * 12f, -18f, 0f);
            if (rightAntenna != null)
                rightAntenna.localRotation = Quaternion.Euler(38f + Mathf.Sin(Time.time * 9f + 1.4f) * 12f, 18f, 0f);
        }
    }
}
