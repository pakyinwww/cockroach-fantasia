using System;
using System.Threading;
using System.Threading.Tasks;
using CockroachFantasia.Networking;

namespace CockroachFantasia.Tests.EditMode
{
    internal sealed class FakePrivateSessionGateway : IPrivateSessionGateway
    {
        public FakePrivateSession Session { get; } = new FakePrivateSession();
        public int CreateCalls { get; private set; }
        public int JoinCalls { get; private set; }

        public Task<IPrivateSession> CreateAsync(int maximumPlayers, string sessionType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCalls++;
            Session.MaximumPlayers = maximumPlayers;
            Session.SessionType = sessionType;
            Session.IsHostValue = true;
            return Task.FromResult<IPrivateSession>(Session);
        }

        public Task<IPrivateSession> JoinAsync(string roomCode, string sessionType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JoinCalls++;
            Session.CodeValue = roomCode;
            Session.SessionType = sessionType;
            Session.IsHostValue = false;
            return Task.FromResult<IPrivateSession>(Session);
        }
    }

    internal sealed class FakePrivateSession : IPrivateSession
    {
        public string CodeValue { get; set; } = "BCDF67";
        public bool IsHostValue { get; set; }
        public int MaximumPlayers { get; set; }
        public string SessionType { get; set; }
        public bool Locked { get; private set; }
        public bool Left { get; private set; }
        public string Code => CodeValue;
        public int PlayerCount { get; set; } = 1;
        public bool IsHost => IsHostValue;
        public bool IsMember => !Left;

        public event Action Changed;
        public event Action Removed;
        public event Action Deleted;
        public event Action<PrivateSessionState> StateChanged;

        public Task LeaveAsync()
        {
            Left = true;
            return Task.CompletedTask;
        }

        public Task SetLockedAsync(bool locked)
        {
            Locked = locked;
            return Task.CompletedTask;
        }

        public void RaiseChanged() => Changed?.Invoke();
        public void RaiseRemoved() => Removed?.Invoke();
        public void RaiseDeleted() => Deleted?.Invoke();
        public void RaiseState(PrivateSessionState state) => StateChanged?.Invoke(state);
    }
}
