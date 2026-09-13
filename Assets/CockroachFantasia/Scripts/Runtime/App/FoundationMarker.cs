using UnityEngine;

namespace CockroachFantasia.App
{
    /// <summary>
    /// Marks the root of each foundation scene until its dedicated controller is added.
    /// </summary>
    public sealed class FoundationMarker : MonoBehaviour
    {
        [SerializeField] private string scenePurpose = string.Empty;

        public string ScenePurpose => scenePurpose;

        public void Configure(string purpose)
        {
            scenePurpose = purpose;
        }
    }
}
