using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace CockroachFantasia.App
{
    public sealed class UnityServicesGateway : IUnityServicesGateway
    {
        public bool ServicesAreInitialized => UnityServices.State == ServicesInitializationState.Initialized;
        public bool PlayerIsSignedIn => ServicesAreInitialized && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => PlayerIsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        public Task InitializeServicesAsync()
        {
            return ServicesAreInitialized ? Task.CompletedTask : UnityServices.InitializeAsync();
        }

        public Task SignInAnonymouslyAsync()
        {
            return PlayerIsSignedIn ? Task.CompletedTask : AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
