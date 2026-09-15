using UnityEngine;

namespace CockroachFantasia.UI
{
    public static class ComicVfx
    {
        public static GameObject SpawnBurst(Vector3 position, Color color, string caption)
        {
            var root = new GameObject("ComicBurst", typeof(ComicVfxInstance));
            root.transform.position = position;
            foreach (var candidate in Camera.allCameras)
            {
                if (!candidate.enabled || candidate.name != "OwnerCamera") continue;
                root.transform.rotation = candidate.transform.rotation;
                break;
            }
            for (var index = 0; index < 8; index++)
            {
                var ray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ray.name = "BurstRay";
                ray.transform.SetParent(root.transform, false);
                var angle = index * 45f;
                ray.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.28f;
                ray.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                ray.transform.localScale = new Vector3(0.035f, 0.035f, 0.3f);
                var rayCollider = ray.GetComponent<Collider>();
                rayCollider.enabled = false;
                Object.Destroy(rayCollider);
                ray.GetComponent<Renderer>().material.color = color;
            }
            var text = new GameObject("Caption", typeof(TextMesh)).GetComponent<TextMesh>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = Vector3.up * 0.35f;
            text.text = caption;
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
            Object.Destroy(root, 0.9f);
            return root;
        }
    }

    public sealed class ComicVfxInstance : MonoBehaviour
    {
        private void Update()
        {
            transform.localScale += Vector3.one * (1.8f * Time.deltaTime);
            transform.Rotate(0f, 70f * Time.deltaTime, 0f);
        }
    }
}
