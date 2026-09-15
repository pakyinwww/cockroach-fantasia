using CockroachFantasia.App;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CockroachFantasia.UI
{
    public sealed class ServicesSceneNavigator : MonoBehaviour
    {
        private void OnEnable()
        {
            if (ServicesBootstrap.Instance != null)
                ServicesBootstrap.Instance.StatusChanged += OnStatusChanged;
        }

        private void OnDisable()
        {
            if (ServicesBootstrap.Instance != null)
                ServicesBootstrap.Instance.StatusChanged -= OnStatusChanged;
        }

        private void Start()
        {
            if (ServicesBootstrap.Instance != null && ServicesBootstrap.Instance.State == ServicesState.Ready)
                LoadFrontEnd();
        }

        private void OnStatusChanged(ServicesState state, string message)
        {
            if (state == ServicesState.Ready) LoadFrontEnd();
        }

        private static void LoadFrontEnd()
        {
            if (SceneManager.GetActiveScene().name == "Bootstrap")
                SceneManager.LoadScene("FrontEnd", LoadSceneMode.Single);
        }
    }
}
