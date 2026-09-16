using System.Collections;
using CockroachFantasia.App;
using CockroachFantasia.Networking;
using CockroachFantasia.SinglePlayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class BootstrapSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapOpensOfflineCapableMenuAndKeepsRuntimeServicesPersistent()
        {
            var load = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return load;
            yield return null;

            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "FrontEnd");
            Assert.That(ServicesBootstrap.Instance, Is.Not.Null);
            Assert.That(SessionCoordinator.Instance, Is.Not.Null);
            Assert.That(NetworkRuntimeBootstrap.Instance, Is.Not.Null);
            Assert.That(SinglePlayerCoordinator.Instance, Is.Not.Null);
            Assert.That(ServicesBootstrap.Instance.State, Is.EqualTo(ServicesState.NotStarted),
                "Automated tests must not contact production Unity Gaming Services.");
        }
    }
}
