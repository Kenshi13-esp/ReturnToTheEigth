using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReturnToTheEigth.Core
{
    /// <summary>Plays configured sound effects with per-effect levels and the shared sound slider.</summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class SoundManager : MonoBehaviour
    {
        private const float DefaultEffectVolume = 1f;
        private const float MinimumEffectVolume = 0f;
        private const float MaximumEffectVolume = 1f;
        private const float MinimumPitch = 0.1f;
        private const float MaximumPitch = 3f;
        private const float DefaultPitch = 1f;
        private const float TwoDimensionalAudio = 0f;
        private const string MissingEffectWarningFormat = "Sound effect '{0}' is not configured.";
        private const string DuplicateEffectWarningFormat = "Sound effect ID '{0}' is configured more than once.";

        private static readonly Dictionary<string, SoundEffectDefinition> EffectsById =
            new Dictionary<string, SoundEffectDefinition>(StringComparer.OrdinalIgnoreCase);

        [SerializeField] private SoundEffectDefinition[] soundEffects = Array.Empty<SoundEffectDefinition>();

        private readonly Dictionary<string, AudioSource> oneShotAudioSources =
            new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioSource> loopingAudioSources =
            new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioSource> durationAudioSources =
            new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Rebuilds the effect lookup and registers a dedicated source with the shared sound-volume controls.</summary>
        private void Awake()
        {
            EffectsById.Clear();
            for (int index = 0; index < soundEffects.Length; index++)
            {
                SoundEffectDefinition effect = soundEffects[index];
                if (effect == null || string.IsNullOrWhiteSpace(effect.EffectId) || effect.Clip == null)
                {
                    continue;
                }

                if (!EffectsById.TryAdd(effect.EffectId.Trim(), effect))
                {
                    Debug.LogWarning(string.Format(DuplicateEffectWarningFormat, effect.EffectId), this);
                }
            }
        }

        /// <summary>Plays a configured sound effect once using its individual volume setting.</summary>
        public static bool Play(string effectId)
        {
            return Play(effectId, DefaultPitch);
        }

        /// <summary>Plays a configured sound effect once with its individual volume and requested pitch.</summary>
        public static bool Play(string effectId, float pitch)
        {
            if (string.IsNullOrWhiteSpace(effectId)
                || !EffectsById.TryGetValue(effectId.Trim(), out SoundEffectDefinition effect)
                || effect == null
                || effect.Clip == null)
            {
                return false;
            }

            SoundManager manager = FindAnyObjectByType<SoundManager>();
            if (manager == null)
            {
                return false;
            }

            float clampedPitch = Mathf.Clamp(pitch, MinimumPitch, MaximumPitch);
            AudioSource source = manager.GetOneShotAudioSource(effect, clampedPitch);
            source.PlayOneShot(effect.Clip, Mathf.Clamp(effect.Volume, MinimumEffectVolume, MaximumEffectVolume));
            return true;
        }

        /// <summary>Plays a configured effect accelerated toward a target duration; the caller stops it at its actual end.</summary>
        public static bool PlayForDuration(string effectId, float duration)
        {
            if (string.IsNullOrWhiteSpace(effectId) || duration <= 0f
                || !EffectsById.TryGetValue(effectId.Trim(), out SoundEffectDefinition effect)
                || effect == null || effect.Clip == null)
            {
                return false;
            }

            SoundManager manager = FindAnyObjectByType<SoundManager>();
            if (manager == null)
            {
                return false;
            }

            AudioSource source = manager.GetDurationAudioSource(effect);
            source.Stop();
            source.clip = effect.Clip;
            source.pitch = Mathf.Clamp(effect.Clip.length / duration, MinimumPitch, MaximumPitch);
            source.Play();
            return true;
        }

        /// <summary>Stops an effect currently played through <see cref="PlayForDuration(string, float)"/>.</summary>
        public static bool StopTimed(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            SoundManager manager = FindAnyObjectByType<SoundManager>();
            if (manager == null || !manager.durationAudioSources.TryGetValue(effectId.Trim(), out AudioSource source)
                || source == null)
            {
                return false;
            }

            source.Stop();
            source.pitch = DefaultPitch;
            return true;
        }

        private AudioSource GetOneShotAudioSource(SoundEffectDefinition effect, float pitch)
        {
            string effectId = effect.EffectId.Trim();
            if (!oneShotAudioSources.TryGetValue(effectId, out AudioSource source) || source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = TwoDimensionalAudio;
                source.pitch = pitch;
                oneShotAudioSources[effectId] = source;
                AudioSettingsController.RegisterSoundSource(source);
            }
            else
            {
                source.pitch = pitch;
            }

            return source;
        }

        private AudioSource GetDurationAudioSource(SoundEffectDefinition effect)
        {
            string effectId = effect.EffectId.Trim();
            if (!durationAudioSources.TryGetValue(effectId, out AudioSource source) || source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = TwoDimensionalAudio;
                source.volume = Mathf.Clamp(effect.Volume, MinimumEffectVolume, MaximumEffectVolume);
                durationAudioSources[effectId] = source;
                AudioSettingsController.RegisterSoundSource(source);
            }

            return source;
        }

        /// <summary>Plays a configured effect continuously until <see cref="StopLoop(string)"/> is called.</summary>
        public static bool PlayLoop(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId)
                || !EffectsById.TryGetValue(effectId.Trim(), out SoundEffectDefinition effect)
                || effect == null
                || effect.Clip == null)
            {
                return false;
            }

            SoundManager manager = FindAnyObjectByType<SoundManager>();
            if (manager == null)
            {
                return false;
            }

            string normalizedEffectId = effect.EffectId.Trim();
            if (!manager.loopingAudioSources.TryGetValue(normalizedEffectId, out AudioSource source)
                || source == null)
            {
                source = manager.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = TwoDimensionalAudio;
                source.loop = true;
                source.clip = effect.Clip;
                source.volume = Mathf.Clamp(effect.Volume, MinimumEffectVolume, MaximumEffectVolume);
                manager.loopingAudioSources[normalizedEffectId] = source;
                AudioSettingsController.RegisterSoundSource(source);
            }

            if (!source.isPlaying)
            {
                source.Play();
            }
            return true;
        }

        /// <summary>Stops a configured looping sound effect without affecting other effects.</summary>
        public static bool StopLoop(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            SoundManager manager = FindAnyObjectByType<SoundManager>();
            if (manager == null
                || !manager.loopingAudioSources.TryGetValue(effectId.Trim(), out AudioSource source)
                || source == null)
            {
                return false;
            }

            source.Stop();
            return true;
        }

        /// <summary>Sets and clamps an effect's individual volume without changing the global sound slider.</summary>
        public static bool SetEffectVolume(string effectId, float volume)
        {
            if (string.IsNullOrWhiteSpace(effectId)
                || !EffectsById.TryGetValue(effectId.Trim(), out SoundEffectDefinition effect)
                || effect == null)
            {
                return false;
            }

            effect.Volume = Mathf.Clamp(volume, MinimumEffectVolume, MaximumEffectVolume);
            return true;
        }

        /// <summary>Defines an effect's lookup ID, imported audio clip, and independent volume multiplier.</summary>
        [Serializable]
        public sealed class SoundEffectDefinition
        {
            [SerializeField] private string effectId;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(MinimumEffectVolume, MaximumEffectVolume)] private float volume = DefaultEffectVolume;

            /// <summary>Gets the ID used by <see cref="Play(string)"/>.</summary>
            public string EffectId => effectId;

            /// <summary>Gets the imported audio clip.</summary>
            public AudioClip Clip => clip;

            /// <summary>Gets or sets this effect's independent volume multiplier.</summary>
            public float Volume
            {
                get => volume;
                set => volume = value;
            }
        }
    }
}
