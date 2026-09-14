using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CockroachFantasia.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.EditMode
{
    public sealed class ServicesBootstrapTests
    {
        private GameObject root;
        private ServicesBootstrap bootstrap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ServicesBootstrapTests");
            bootstrap = root.AddComponent<ServicesBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator ConcurrentInitializationSharesOneOperation()
        {
            var initializeGate = new TaskCompletionSource<bool>();
            var gateway = new FakeUnityServicesGateway(_ => initializeGate.Task);
            bootstrap.ConfigureForTests(gateway, TimeSpan.FromSeconds(2));

            var first = bootstrap.InitializeAsync();
            var second = bootstrap.InitializeAsync();

            Assert.That(second, Is.SameAs(first));
            initializeGate.SetResult(true);
            yield return new WaitUntil(() => first.IsCompleted);

            Assert.That(first.Result.Succeeded, Is.True);
            Assert.That(bootstrap.State, Is.EqualTo(ServicesState.Ready));
            Assert.That(gateway.InitializeCalls, Is.EqualTo(1));
            Assert.That(gateway.SignInCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailureCanRetryWithoutRecreatingBootstrap()
        {
            var gateway = new FakeUnityServicesGateway(
                signIn: call => call == 1
                    ? Task.FromException(new InvalidOperationException("planned failure"))
                    : Task.CompletedTask);
            bootstrap.ConfigureForTests(gateway, TimeSpan.FromSeconds(2));

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: planned failure"));
            var first = bootstrap.InitializeAsync();
            yield return new WaitUntil(() => first.IsCompleted);

            Assert.That(first.Result.Succeeded, Is.False);
            Assert.That(bootstrap.State, Is.EqualTo(ServicesState.Failed));

            var retry = bootstrap.RetryAsync();
            yield return new WaitUntil(() => retry.IsCompleted);

            Assert.That(retry.Result.Succeeded, Is.True);
            Assert.That(bootstrap.State, Is.EqualTo(ServicesState.Ready));
            Assert.That(gateway.InitializeCalls, Is.EqualTo(2));
            Assert.That(gateway.SignInCalls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator HungRequestTimesOutWithActionableState()
        {
            var neverCompletes = new TaskCompletionSource<bool>();
            var gateway = new FakeUnityServicesGateway(_ => neverCompletes.Task);
            bootstrap.ConfigureForTests(gateway, TimeSpan.FromMilliseconds(20));

            var result = bootstrap.InitializeAsync();
            yield return new WaitUntil(() => result.IsCompleted);

            Assert.That(result.Result.Succeeded, Is.False);
            Assert.That(result.Result.ErrorKind, Is.EqualTo(ServicesErrorKind.TimedOut));
            Assert.That(bootstrap.StatusMessage, Does.Contain("retry").IgnoreCase);
        }
    }
}
