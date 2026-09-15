using System.Threading.Tasks;

namespace CockroachFantasia.App
{
    public interface IUnityServicesGateway
    {
        bool ServicesAreInitialized { get; }
        bool PlayerIsSignedIn { get; }
        string PlayerId { get; }

        Task InitializeServicesAsync();
        Task SignInAnonymouslyAsync();
    }
}
