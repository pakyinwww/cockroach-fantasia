using System.Collections;
using CockroachFantasia.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CockroachFantasia.Tests.PlayMode
{
    public sealed class GameAudioTests
    {
        [UnityTest]
        public IEnumerator ProceduralCuesUseMixerAndSurviveSceneLoads()
        {
            var audio = GameAudio.Instance;
            Assert.That(audio.IsRoutedToMixer, Is.True);
            var before = audio.PlayedCueCount;
            Assert.That(GameAudio.Play(GameAudioCue.Pickup), Is.True);
            Assert.That(audio.PlayedCueCount, Is.EqualTo(before + 1));
            var objectBefore = audio.gameObject;
            yield return SceneManager.LoadSceneAsync("FrontEnd", LoadSceneMode.Single);
            Assert.That(GameAudio.Instance.gameObject, Is.SameAs(objectBefore));
            Assert.That(GameAudio.Instance.IsRoutedToMixer, Is.True);
        }

        [UnityTest]
        public IEnumerator RapidDuplicateCuesAreRateLimited()
        {
            yield return new WaitForSecondsRealtime(0.1f);
            var audio = GameAudio.Instance;
            var before = audio.PlayedCueCount;
            Assert.That(GameAudio.Play(GameAudioCue.HarmlessImpact), Is.True);
            Assert.That(GameAudio.Play(GameAudioCue.HarmlessImpact), Is.False);
            Assert.That(audio.PlayedCueCount, Is.EqualTo(before + 1));
        }
    }
}
