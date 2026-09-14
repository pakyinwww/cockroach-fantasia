using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace CockroachFantasia.Networking
{
    public sealed class UnityPrivateSessionGateway : IPrivateSessionGateway
    {
        public async Task<IPrivateSession> CreateAsync(int maximumPlayers, string sessionType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new SessionOptions
            {
                Name = $"Kitchen-{Guid.NewGuid():N}",
                Type = sessionType,
                MaxPlayers = maximumPlayers,
                IsPrivate = true,
                IsLocked = false
            }.WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.DTLS })
             .WithRelayNetwork();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            cancellationToken.ThrowIfCancellationRequested();
            return new UnityPrivateSession(session);
        }

        public async Task<IPrivateSession> JoinAsync(string roomCode, string sessionType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new JoinSessionOptions { Type = sessionType }
                .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.DTLS });
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(roomCode, options);
            cancellationToken.ThrowIfCancellationRequested();
            return new UnityPrivateSession(session);
        }

        private sealed class UnityPrivateSession : IPrivateSession
        {
            private readonly ISession session;

            public UnityPrivateSession(ISession session)
            {
                this.session = session;
                session.Changed += () => Changed?.Invoke();
                session.RemovedFromSession += () => Removed?.Invoke();
                session.Deleted += () => Deleted?.Invoke();
                session.StateChanged += state => StateChanged?.Invoke(MapState(state));
            }

            public string Code => session.Code;
            public int PlayerCount => session.PlayerCount;
            public bool IsHost => session.IsHost;
            public bool IsMember => session.IsMember;

            public event Action Changed;
            public event Action Removed;
            public event Action Deleted;
            public event Action<PrivateSessionState> StateChanged;

            public Task LeaveAsync() => session.LeaveAsync();

            public async Task SetLockedAsync(bool locked)
            {
                if (session is not IHostSession hostSession || !session.IsHost)
                    throw new InvalidOperationException("Only the Session host can change the room lock.");
                hostSession.IsLocked = locked;
                await hostSession.SavePropertiesAsync();
            }

            private static PrivateSessionState MapState(SessionState state)
            {
                return state switch
                {
                    SessionState.Deleted => PrivateSessionState.Deleted,
                    SessionState.Disconnected => PrivateSessionState.Disconnected,
                    _ => PrivateSessionState.Connected
                };
            }
        }
    }
}
