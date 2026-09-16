using System.Threading;
using CockroachFantasia.App;
using UnityEngine;
using UnityEngine.UI;

namespace CockroachFantasia.UI
{
    public sealed class ServicesStatusPresenter : MonoBehaviour
    {
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button retryButton;

        private CancellationTokenSource cancellation;

        public void Configure(Text status, Button retry)
        {
            statusLabel = status;
            retryButton = retry;
        }

        private void OnEnable()
        {
            cancellation = new CancellationTokenSource();
            if (ServicesBootstrap.Instance == null)
            {
                SetStatus("Preparing online services…", false);
                return;
            }

            ServicesBootstrap.Instance.StatusChanged += HandleStatusChanged;
            retryButton?.onClick.AddListener(Retry);
            Refresh();
        }

        private void OnDisable()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;

            if (ServicesBootstrap.Instance != null)
            {
                ServicesBootstrap.Instance.StatusChanged -= HandleStatusChanged;
            }
            retryButton?.onClick.RemoveListener(Retry);
        }

        public async void Retry()
        {
            if (ServicesBootstrap.Instance == null || cancellation == null)
            {
                return;
            }

            await ServicesBootstrap.Instance.RetryAsync(cancellation.Token);
        }

        private void HandleStatusChanged(ServicesState state, string message)
        {
            SetStatus(message, state == ServicesState.Failed || state == ServicesState.Cancelled);
        }

        private void Refresh()
        {
            var services = ServicesBootstrap.Instance;
            SetStatus(
                services.StatusMessage,
                services.State == ServicesState.Failed || services.State == ServicesState.Cancelled);
        }

        private void SetStatus(string message, bool canRetry)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(canRetry);
                retryButton.interactable = canRetry;
            }
        }
    }
}
