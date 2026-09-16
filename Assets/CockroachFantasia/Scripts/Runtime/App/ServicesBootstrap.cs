using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;

namespace CockroachFantasia.App
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ServicesBootstrap : MonoBehaviour
    {
        private const float DefaultTimeoutSeconds = 15f;

        private static ServicesBootstrap instance;

        [SerializeField, Min(1f)] private float timeoutSeconds = DefaultTimeoutSeconds;
        [SerializeField] private bool initializeOnStart = true;

        private readonly object initializationLock = new object();
        private CancellationTokenSource lifetimeCancellation;
        private IUnityServicesGateway gateway;
        private Task<ServicesInitializationResult> currentInitialization;

        public static ServicesBootstrap Instance => instance;
        public ServicesState State { get; private set; } = ServicesState.NotStarted;
        public ServicesErrorKind LastErrorKind { get; private set; } = ServicesErrorKind.None;
        public string StatusMessage { get; private set; } = "Preparing online services…";
        public string PlayerId => gateway?.PlayerId ?? string.Empty;

        public event Action<ServicesState, string> StatusChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance != null || FindFirstObjectByType<ServicesBootstrap>() != null)
            {
                return;
            }

            var root = new GameObject("ServicesBootstrap");
            root.AddComponent<ServicesBootstrap>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            lifetimeCancellation = new CancellationTokenSource();
            gateway ??= new UnityServicesGateway();
        }

        private async void Start()
        {
            if (initializeOnStart && !IsAutomatedTestRun())
            {
                await InitializeAsync(lifetimeCancellation.Token);
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
            instance = null;
        }

        public Task<ServicesInitializationResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            lock (initializationLock)
            {
                if (State == ServicesState.Ready)
                {
                    return Task.FromResult(ServicesInitializationResult.Success(StatusMessage));
                }

                if (currentInitialization == null || currentInitialization.IsCompleted)
                {
                    currentInitialization = RunInitializationAsync(cancellationToken);
                }

                return currentInitialization;
            }
        }

        public Task<ServicesInitializationResult> RetryAsync(CancellationToken cancellationToken = default)
        {
            if (State != ServicesState.Failed && State != ServicesState.Cancelled)
            {
                return InitializeAsync(cancellationToken);
            }

            lock (initializationLock)
            {
                currentInitialization = RunInitializationAsync(cancellationToken);
                return currentInitialization;
            }
        }

        public void ConfigureForTests(IUnityServicesGateway testGateway, TimeSpan timeout)
        {
            gateway = testGateway ?? throw new ArgumentNullException(nameof(testGateway));
            timeoutSeconds = Mathf.Max(0.01f, (float)timeout.TotalSeconds);
            initializeOnStart = false;
            currentInitialization = null;
            SetState(ServicesState.NotStarted, ServicesErrorKind.None, "Test services are not initialized.");
        }

        private async Task<ServicesInitializationResult> RunInitializationAsync(CancellationToken cancellationToken)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellation?.Token ?? CancellationToken.None);

            try
            {
                SetState(ServicesState.InitializingServices, ServicesErrorKind.None, "Connecting to Unity Services…");
                await AwaitWithTimeout(gateway.InitializeServicesAsync(), linkedCancellation.Token);

                SetState(ServicesState.SigningIn, ServicesErrorKind.None, "Signing in…");
                await AwaitWithTimeout(gateway.SignInAnonymouslyAsync(), linkedCancellation.Token);

                var result = ServicesInitializationResult.Success();
                SetState(ServicesState.Ready, result.ErrorKind, result.Message);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                      (lifetimeCancellation?.IsCancellationRequested ?? false))
            {
                var result = ServicesInitializationResult.Failure(
                    ServicesErrorKind.Cancelled,
                    "Online setup was cancelled.");
                SetState(ServicesState.Cancelled, result.ErrorKind, result.Message);
                return result;
            }
            catch (TimeoutException)
            {
                var result = ServicesInitializationResult.Failure(
                    ServicesErrorKind.TimedOut,
                    "Unity Services took too long to respond. Check your connection and retry.");
                SetState(ServicesState.Failed, result.ErrorKind, result.Message);
                return result;
            }
            catch (Exception exception) when (
                string.Equals(exception.GetType().Name, "UnityProjectNotLinkedException", StringComparison.Ordinal))
            {
                Debug.LogWarning(exception.Message);
                var result = ServicesInitializationResult.Failure(
                    ServicesErrorKind.Configuration,
                    "This build is not linked to a Unity Cloud project. Check the project configuration.");
                SetState(ServicesState.Failed, result.ErrorKind, result.Message);
                return result;
            }
            catch (RequestFailedException exception)
            {
                var kind = State == ServicesState.SigningIn
                    ? ServicesErrorKind.Authentication
                    : ServicesErrorKind.Offline;
                var message = kind == ServicesErrorKind.Authentication
                    ? "Could not sign in. Please retry in a moment."
                    : "Could not reach Unity Services. Check your connection and retry.";
                Debug.LogWarning($"Unity Services request failed ({exception.ErrorCode}): {exception.Message}");
                var result = ServicesInitializationResult.Failure(kind, message);
                SetState(ServicesState.Failed, result.ErrorKind, result.Message);
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                var result = ServicesInitializationResult.Failure(
                    ServicesErrorKind.Unknown,
                    "Online setup failed unexpectedly. Please retry.");
                SetState(ServicesState.Failed, result.ErrorKind, result.Message);
                return result;
            }
        }

        private async Task AwaitWithTimeout(Task operation, CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(operation, timeoutTask);

            if (completed == operation)
            {
                await operation;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }

        private void SetState(ServicesState state, ServicesErrorKind errorKind, string message)
        {
            State = state;
            LastErrorKind = errorKind;
            StatusMessage = message;
            StatusChanged?.Invoke(state, message);
        }

        private static bool IsAutomatedTestRun()
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(argument, "-runTests", StringComparison.OrdinalIgnoreCase));
        }
    }
}
