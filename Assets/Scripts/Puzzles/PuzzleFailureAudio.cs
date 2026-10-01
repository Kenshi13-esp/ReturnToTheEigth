using ReturnToTheEigth.Core;

using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Generates and plays the shared puzzle failure tone.</summary>
    public static class PuzzleFailureAudio
    {
        private const string AudioObjectName = "PuzzleFailureAudio";
        private const int SampleRate = 44100;
        private const int ChannelCount = 1;
        /// <summary>Duration of the generated failure clip in seconds.</summary>
        public const float ToneDuration = 0.45f;
        private const float FirstFrequency = 185f;
        private const float SecondFrequency = 147f;
        private const float ToneAmplitude = 0.16f;
        private const float TwoPi = Mathf.PI * 2f;
        private const int FirstSample = 0;
        private static AudioClip failureClip;
        private static AudioSource sharedAudioSource;

        /// <summary>Plays the shared failure tone through the supplied source, or a persistent 2D source when none is supplied.</summary>
        public static void Play(AudioSource audioSource = null)
        {
            AudioClip clip = GetFailureClip();
            if (audioSource != null)
            {
                AudioSettingsController.RegisterSoundSource(audioSource);
                audioSource.PlayOneShot(clip);
                return;
            }

            if (sharedAudioSource == null)
            {
                GameObject audioObject = new GameObject(AudioObjectName);
                audioObject.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(audioObject);
                sharedAudioSource = audioObject.AddComponent<AudioSource>();
                sharedAudioSource.playOnAwake = false;
                sharedAudioSource.spatialBlend = 0f;
                sharedAudioSource.ignoreListenerPause = true;
                AudioSettingsController.RegisterSoundSource(sharedAudioSource);
            }

            sharedAudioSource.PlayOneShot(clip);
        }

        private static AudioClip GetFailureClip()
        {
            if (failureClip != null) return failureClip;

            int sampleCount = Mathf.CeilToInt(SampleRate * ToneDuration);
            float[] samples = new float[sampleCount];
            for (int sampleIndex = FirstSample; sampleIndex < sampleCount; sampleIndex++)
            {
                float time = sampleIndex / (float)SampleRate;
                float progress = sampleIndex / (float)sampleCount;
                float envelope = 1f - progress;
                float lowTone = Mathf.Sin(TwoPi * FirstFrequency * time);
                float highTone = Mathf.Sin(TwoPi * SecondFrequency * time);
                samples[sampleIndex] = (lowTone + highTone) * ToneAmplitude * envelope;
            }

            failureClip = AudioClip.Create(AudioObjectName, sampleCount, ChannelCount, SampleRate, false);
            failureClip.SetData(samples, FirstSample);
            return failureClip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            failureClip = null;
            sharedAudioSource = null;
        }
    }
}
