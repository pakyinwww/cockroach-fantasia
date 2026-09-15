using System;
using System.Threading.Tasks;
using CockroachFantasia.App;

namespace CockroachFantasia.Tests.EditMode
{
    internal sealed class FakeUnityServicesGateway : IUnityServicesGateway
    {
        private readonly Func<int, Task> initialize;
        private readonly Func<int, Task> signIn;

        public FakeUnityServicesGateway(
            Func<int, Task> initialize = null,
            Func<int, Task> signIn = null)
        {
            this.initialize = initialize ?? (_ => Task.CompletedTask);
            this.signIn = signIn ?? (_ => Task.CompletedTask);
        }

        public bool ServicesAreInitialized { get; private set; }
        public bool PlayerIsSignedIn { get; private set; }
        public string PlayerId => PlayerIsSignedIn ? "fake-player" : string.Empty;
        public int InitializeCalls { get; private set; }
        public int SignInCalls { get; private set; }

        public async Task InitializeServicesAsync()
        {
            InitializeCalls++;
            await initialize(InitializeCalls);
            ServicesAreInitialized = true;
        }

        public async Task SignInAnonymouslyAsync()
        {
            SignInCalls++;
            await signIn(SignInCalls);
            PlayerIsSignedIn = true;
        }
    }
}
