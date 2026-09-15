using System.Threading;
using System.Threading.Tasks;

namespace CockroachFantasia.Networking
{
    public interface IPrivateSessionGateway
    {
        Task<IPrivateSession> CreateAsync(int maximumPlayers, string sessionType,
            CancellationToken cancellationToken = default);
        Task<IPrivateSession> JoinAsync(string roomCode, string sessionType,
            CancellationToken cancellationToken = default);
    }
}
