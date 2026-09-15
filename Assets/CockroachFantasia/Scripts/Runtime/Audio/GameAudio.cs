using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CockroachFantasia.Audio
{
    public sealed class GameAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const int MaxVoices = 10;
        private static GameAudio instance;
        private readonly Dictionary<GameAudioCue, AudioClip> clips = new();
        private readonly Dictionary<GameAudioCue, float> lastPlayedAt = new();
        private readonly List<AudioSource> voices = new();
        private AudioMixerGroup outputGroup;
        private int nextVoice;

        public static GameAudio Instance => EnsureInstance();
        public int PlayedCueCount { get; private set; }
        public GameAudioCue LastCue { get; private set; }
        public bool IsRoutedToMixer => outputGroup != null && voices.TrueForAll(source =>
            source.outputAudioMixerGroup == outputGroup);
        public int ActiveVoiceCount
        {
            get
            {
                var count = 0;
                foreach (var source in voices)
                    if (source.isPlaying) count++;
                return count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => EnsureInstance();

        private static GameAudio EnsureInstance()
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<GameAudio>();
            if (instance != null) return instance;
            var host = new GameObject("GameAudio");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GameAudio>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            var mixer = Resources.Load<AudioMixer>("Audio/CockroachMixer");
            var groups = mixer != null ? mixer.FindMatchingGroups("Master") : null;
            outputGroup = groups != null && groups.Length > 0 ? groups[0] : null;
            for (var index = 0; index < MaxVoices; index++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.outputAudioMixerGroup = outputGroup;
                voices.Add(source);
            }
        }

        public static bool Play(GameAudioCue cue, Vector3? worldPosition = null, float delay = 0f)
        {
            var audio = EnsureInstance();
            if (delay > 0f)
            {
                audio.StartCoroutine(audio.PlayDelayed(cue, worldPosition, delay));
                return true;
            }
            return audio.PlayNow(cue, worldPosition);
        }

        private IEnumerator PlayDelayed(GameAudioCue cue, Vector3? position, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayNow(cue, position);
        }

        private bool PlayNow(GameAudioCue cue, Vector3? worldPosition)
        {
            var now = Time.unscaledTime;
            var cooldown = cue is GameAudioCue.CockroachStep or GameAudioCue.FoodRustle ? 0.08f : 0.045f;
            if (lastPlayedAt.TryGetValue(cue, out var previous) && now - previous < cooldown) return false;
            lastPlayedAt[cue] = now;
            var source = voices[nextVoice++ % voices.Count];
            source.Stop();
            source.transform.position = worldPosition ?? Vector3.zero;
            source.spatialBlend = worldPosition.HasValue ? 0.7f : 0f;
            source.minDistance = 1f;
            source.maxDistance = 18f;
            source.pitch = 0.97f + Random.value * 0.06f;
            source.clip = GetClip(cue);
            source.Play();
            LastCue = cue;
            PlayedCueCount++;
            return true;
        }

        private AudioClip GetClip(GameAudioCue cue)
        {
            if (clips.TryGetValue(cue, out var clip)) return clip;
            var duration = cue switch
            {
                GameAudioCue.CockroachVictory or GameAudioCue.HumanVictory => 0.75f,
                GameAudioCue.Deposit or GameAudioCue.Score => 0.42f,
                _ => 0.16f
            };
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            for (var index = 0; index < samples.Length; index++)
            {
                var t = index / (float)SampleRate;
                var progress = index / (float)samples.Length;
                var envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress * 0.35f);
                samples[index] = Sample(cue, t, progress) * envelope * 0.24f;
            }
            clip = AudioClip.Create($"Procedural_{cue}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            clips[cue] = clip;
            return clip;
        }

        private static float Sample(GameAudioCue cue, float t, float progress)
        {
            var frequency = cue switch
            {
                GameAudioCue.CockroachStep => 760f,
                GameAudioCue.HumanStep => 105f,
                GameAudioCue.FoodRustle => 1250f,
                GameAudioCue.Pickup => 480f + progress * 520f,
                GameAudioCue.Drop => 520f - progress * 300f,
                GameAudioCue.SwatterSwing => 190f + progress * 900f,
                GameAudioCue.HarmlessImpact => 620f - progress * 380f,
                GameAudioCue.Deposit => 330f + progress * 440f,
                GameAudioCue.Score => progress < 0.5f ? 660f : 880f,
                GameAudioCue.CountdownTick => 940f,
                GameAudioCue.CockroachVictory => 440f * (progress < 0.33f ? 1f : progress < 0.66f ? 1.25f : 1.5f),
                GameAudioCue.HumanVictory => 330f * (progress < 0.33f ? 1f : progress < 0.66f ? 1.5f : 2f),
                _ => 440f
            };
            var wave = Mathf.Sin(Mathf.PI * 2f * frequency * t);
            if (cue is GameAudioCue.FoodRustle or GameAudioCue.SwatterSwing)
                wave = Mathf.Lerp(wave, Random.value * 2f - 1f, 0.55f);
            if (cue == GameAudioCue.HumanStep) wave = Mathf.Sign(wave) * 0.7f;
            return wave;
        }
    }
}
