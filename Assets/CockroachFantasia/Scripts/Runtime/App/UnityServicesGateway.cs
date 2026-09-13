using System;
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
            if (ServicesAreInitialized)
            {
                return Task.CompletedTask;
            }

            var profile = GetCommandLineValue("-playerProfile");
            return string.IsNullOrWhiteSpace(profile)
                ? UnityServices.InitializeAsync()
                : UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
        }

        public Task SignInAnonymouslyAsync()
        {
            return PlayerIsSignedIn ? Task.CompletedTask : AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static string GetCommandLineValue(string key)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }
    }
}
