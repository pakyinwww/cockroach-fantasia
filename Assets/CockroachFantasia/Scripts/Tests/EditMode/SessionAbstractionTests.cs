using System.Threading.Tasks;
using CockroachFantasia.Networking;
using NUnit.Framework;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class SessionAbstractionTests
    {
        [Test]
        public async Task FakeCanHostAndLockWithoutUgs()
        {
            var sessions = new FakePrivateSessionGateway();
            var session = await sessions.CreateAsync(4, SessionCoordinator.SessionType);
            Assert.That(sessions.CreateCalls, Is.EqualTo(1));
            Assert.That(session.Code, Is.EqualTo("BCDF67"));
            Assert.That(sessions.Session.MaximumPlayers, Is.EqualTo(4));

            await session.SetLockedAsync(true);
            Assert.That(sessions.Session.Locked, Is.True);
        }
    }
}
