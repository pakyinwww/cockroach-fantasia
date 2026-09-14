using System;
using System.Threading.Tasks;

namespace CockroachFantasia.Networking
{
    public interface IPrivateSession
    {
        string Code { get; }
        int PlayerCount { get; }
        bool IsHost { get; }
        bool IsMember { get; }

        event Action Changed;
        event Action Removed;
        event Action Deleted;
        event Action<PrivateSessionState> StateChanged;

        Task LeaveAsync();
        Task SetLockedAsync(bool locked);
    }
}
