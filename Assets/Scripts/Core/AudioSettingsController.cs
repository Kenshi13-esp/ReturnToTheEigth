using System.Collections.Generic;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReturnToTheEigth.Core
{
    /// <summary>Applies persistent volume settings and synchronized music for both timeline eras.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioSettingsController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string PianoPuzzleSceneName = "PianoPuzzle";
        private const string BoxPuzzleSceneName = "BoxPuzzle";
        private const string ElectricityPuzzleId = "ElectricityPuzzle";
        private const string MusicVolumeKey = "MusicVolume";
        private const string SoundVolumeKey = "SoundVolume";
        private const string MasterVolumeKey = "MasterVolume";
        private const string MusicSliderName = "SliderMusic";
        private const string SoundSliderName = "SliderSound";
        private const string MasterSliderName = "SliderMaster";
        private const float DefaultVolume = 0.8f;
        private const float MinimumVolume = 0f;
        private const float MaximumVolume = 1f;
        private const float ScheduledMusicStartDelay = 0.1f;
        private const float MusicTrackBaseVolume = 0.25f;
        private const float FullVolume = 1f;
        private const float ZeroVolume = 0f;
        private const float TwoDimensionalAudio = 0f;

        private static readonly List<RegisteredAudioSource> RegisteredAudioSources = new List<RegisteredAudioSource>();
        private static readonly List<AudioSourcePauseState> AudioSourcePauseStates = new List<AudioSourcePauseState>();

        private static AudioSettingsController instance;
        private static float musicVolume = DefaultVolume;
        private static float soundVolume = DefaultVolume;
        private static float masterVolume = DefaultVolume;

        [SerializeField] private AudioClip presentEraMusicClip;
        [SerializeField] private AudioClip pastEraMusicClip;
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;

        private AudioSource menuMusicSource;
        private AudioSource presentEraMusicSource;
        private AudioSource pastEraMusicSource;
        private TimelineEra currentEra = TimelineEra.Present;
        private bool eraMusicScheduled;
        private bool suppressEraMusicUntilPowerRestored;
        private bool isCinematicAudioSuspended;
        private bool previousAudioListenerPauseState;
        private AudioSource cinematicAudioSource;

        private sealed class AudioSourcePauseState
        {
            public AudioSource Source;
            public bool IgnoreListenerPause;
        }

        private sealed class RegisteredAudioSource
        {
            public AudioSource Source;
            public float BaseVolume;
            public bool IsMusic;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);
            soundVolume = PlayerPrefs.GetFloat(SoundVolumeKey, DefaultVolume);
            masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);

            menuMusicSource = GetComponent<AudioSource>();
            menuMusicSource.playOnAwake = false;
            menuMusicSource.volume = MusicTrackBaseVolume;
            RegisterAudioSource(menuMusicSource, true);
            presentEraMusicSource = CreateEraMusicSource(presentEraMusicClip);
            pastEraMusicSource = CreateEraMusicSource(pastEraMusicClip);
            AudioListener.volume = masterVolume;

            if (timelineChangedChannel != null)
            {
                timelineChangedChannel.OnTimelineChanged += HandleTimelineChanged;
            }
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RegisterSceneAudioSources();
            BindVolumeSliders();
            UpdateMusicForScene(SceneManager.GetActiveScene());
            ApplyCategoryVolumes();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (timelineChangedChannel != null)
            {
                timelineChangedChannel.OnTimelineChanged -= HandleTimelineChanged;
            }
            if (instance == this)
            {
                if (isCinematicAudioSuspended)
                {
                    RestoreAudioAfterCinematic();
                }

                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }

        /// <summary>Registers a sound-effects AudioSource so its original level is scaled by the sound slider.</summary>
        public static void RegisterSoundSource(AudioSource source)
        {
            RegisterAudioSource(source, false);
        }

        /// <summary>Registers a music AudioSource so its original level is scaled by the music slider.</summary>
        public static void RegisterMusicSource(AudioSource source)
        {
            RegisterAudioSource(source, true);
        }

        /// <summary>Pauses every audio source except the supplied cinematic track and stops background music.</summary>
        public static void SuspendAudioForCinematic(AudioSource cinematicSource)
        {
            if (instance == null || cinematicSource == null || instance.isCinematicAudioSuspended)
            {
                return;
            }

            instance.previousAudioListenerPauseState = AudioListener.pause;
            instance.isCinematicAudioSuspended = true;
            instance.cinematicAudioSource = cinematicSource;
            instance.StopBackgroundMusic();
            instance.PauseNonCinematicAudioSources();
        }

        /// <summary>Restores audio immediately if a cinematic is cancelled before its scene transition.</summary>
        public static void ResumeAudioAfterCinematic()
        {
            if (instance != null)
            {
                instance.ResumeBackgroundMusicAfterCinematic();
            }
        }

        /// <summary>Restarts scene-appropriate background music and restores audio after a cinematic transition.</summary>
        private void ResumeBackgroundMusicAfterCinematic()
        {
            if (!isCinematicAudioSuspended)
            {
                return;
            }

            RestoreAudioAfterCinematic();
            UpdateMusicForScene(SceneManager.GetActiveScene());
        }

        private void PauseNonCinematicAudioSources()
        {
            AudioSourcePauseStates.Clear();
            AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            for (int index = 0; index < audioSources.Length; index++)
            {
                StoreAndSetCinematicPause(audioSources[index]);
            }

            AudioListener.pause = true;
        }

        private void StoreAndSetCinematicPause(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < AudioSourcePauseStates.Count; index++)
            {
                if (AudioSourcePauseStates[index].Source == source)
                {
                    source.ignoreListenerPause = source == cinematicAudioSource;
                    return;
                }
            }

            AudioSourcePauseStates.Add(new AudioSourcePauseState
            {
                Source = source,
                IgnoreListenerPause = source.ignoreListenerPause
            });
            source.ignoreListenerPause = source == cinematicAudioSource;
        }

        private void RestoreAudioAfterCinematic()
        {
            AudioListener.pause = previousAudioListenerPauseState;
            for (int index = 0; index < AudioSourcePauseStates.Count; index++)
            {
                AudioSourcePauseState pauseState = AudioSourcePauseStates[index];
                if (pauseState.Source != null)
                {
                    pauseState.Source.ignoreListenerPause = pauseState.IgnoreListenerPause;
                }
            }

            AudioSourcePauseStates.Clear();
            cinematicAudioSource = null;
            isCinematicAudioSuspended = false;
        }

        /// <summary>Sets the music volume from its options slider and saves the preference.</summary>
        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp(value, MinimumVolume, MaximumVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            ApplyCategoryVolumes();
        }

        /// <summary>Sets the sound-effects volume from its options slider and saves the preference.</summary>
        public void SetSoundVolume(float value)
        {
            soundVolume = Mathf.Clamp(value, MinimumVolume, MaximumVolume);
            PlayerPrefs.SetFloat(SoundVolumeKey, soundVolume);
            ApplyCategoryVolumes();
        }

        /// <summary>Sets the master volume for all audio from its options slider and saves the preference.</summary>
        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp(value, MinimumVolume, MaximumVolume);
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            AudioListener.volume = masterVolume;
        }

        private AudioSource CreateEraMusicSource(AudioClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.volume = MusicTrackBaseVolume;
            source.spatialBlend = TwoDimensionalAudio;
            RegisterMusicSource(source);
            return source;
        }

        private static void RegisterAudioSource(AudioSource source, bool isMusic)
        {
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < RegisteredAudioSources.Count; index++)
            {
                if (RegisteredAudioSources[index].Source == source)
                {
                    return;
                }
            }

            RegisteredAudioSources.Add(new RegisteredAudioSource
            {
                Source = source,
                BaseVolume = source.volume,
                IsMusic = isMusic
            });

            if (instance != null)
            {
                if (instance.isCinematicAudioSuspended)
                {
                    instance.StoreAndSetCinematicPause(source);
                }

                instance.ApplyCategoryVolumes();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterSceneAudioSources();
            BindVolumeSliders();
            if (isCinematicAudioSuspended)
            {
                ResumeBackgroundMusicAfterCinematic();
                return;
            }

            UpdateMusicForScene(scene);
        }

        private void HandleTimelineChanged(TimelineEra era)
        {
            if (!System.Enum.IsDefined(typeof(TimelineEra), era))
            {
                return;
            }

            currentEra = era;
            ApplyCategoryVolumes();
        }

        private void RegisterSceneAudioSources()
        {
            AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            for (int index = 0; index < audioSources.Length; index++)
            {
                RegisterSoundSource(audioSources[index]);
            }
        }

        private void BindVolumeSliders()
        {
            Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include);
            for (int index = 0; index < sliders.Length; index++)
            {
                Slider slider = sliders[index];
                if (slider == null)
                {
                    continue;
                }

                switch (slider.gameObject.name)
                {
                    case MusicSliderName:
                        slider.onValueChanged.RemoveListener(SetMusicVolume);
                        slider.onValueChanged.AddListener(SetMusicVolume);
                        slider.SetValueWithoutNotify(musicVolume);
                        break;
                    case SoundSliderName:
                        slider.onValueChanged.RemoveListener(SetSoundVolume);
                        slider.onValueChanged.AddListener(SetSoundVolume);
                        slider.SetValueWithoutNotify(soundVolume);
                        break;
                    case MasterSliderName:
                        slider.onValueChanged.RemoveListener(SetMasterVolume);
                        slider.onValueChanged.AddListener(SetMasterVolume);
                        slider.SetValueWithoutNotify(masterVolume);
                        break;
                }
            }
        }

        private void UpdateMusicForScene(Scene scene)
        {
            if (menuMusicSource == null)
            {
                return;
            }

            if (isCinematicAudioSuspended)
            {
                StopBackgroundMusic();
                return;
            }

            if (scene.name == MainMenuSceneName)
            {
                suppressEraMusicUntilPowerRestored = false;
                StopEraMusic();
                if (!menuMusicSource.isPlaying)
                {
                    menuMusicSource.Play();
                }
                return;
            }

            if (menuMusicSource.isPlaying)
            {
                menuMusicSource.Stop();
            }

            if (scene.name == PianoPuzzleSceneName)
            {
                suppressEraMusicUntilPowerRestored = true;
            }
            else if (scene.name == BoxPuzzleSceneName)
            {
                suppressEraMusicUntilPowerRestored = false;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsPuzzleCompleted(ElectricityPuzzleId))
            {
                suppressEraMusicUntilPowerRestored = false;
            }

            if (suppressEraMusicUntilPowerRestored)
            {
                StopEraMusic();
                return;
            }

            StartEraMusic();
        }

        private void StartEraMusic()
        {
            if (eraMusicScheduled || presentEraMusicSource == null || pastEraMusicSource == null)
            {
                return;
            }

            double startTime = AudioSettings.dspTime + ScheduledMusicStartDelay;
            presentEraMusicSource.PlayScheduled(startTime);
            pastEraMusicSource.PlayScheduled(startTime);
            eraMusicScheduled = true;
            ApplyCategoryVolumes();
        }

        private void StopBackgroundMusic()
        {
            if (menuMusicSource != null && menuMusicSource.isPlaying)
            {
                menuMusicSource.Stop();
            }

            StopEraMusic();
        }

        private void StopEraMusic()
        {
            if (presentEraMusicSource != null)
            {
                presentEraMusicSource.Stop();
            }
            if (pastEraMusicSource != null)
            {
                pastEraMusicSource.Stop();
            }
            eraMusicScheduled = false;
        }

        private void ApplyCategoryVolumes()
        {
            for (int index = RegisteredAudioSources.Count - 1; index >= 0; index--)
            {
                RegisteredAudioSource registeredSource = RegisteredAudioSources[index];
                if (registeredSource.Source == null)
                {
                    RegisteredAudioSources.RemoveAt(index);
                    continue;
                }

                float categoryVolume = registeredSource.IsMusic ? musicVolume : soundVolume;
                if (registeredSource.Source == presentEraMusicSource)
                {
                    categoryVolume *= currentEra == TimelineEra.Present ? FullVolume : ZeroVolume;
                }
                else if (registeredSource.Source == pastEraMusicSource)
                {
                    categoryVolume *= currentEra == TimelineEra.Past ? FullVolume : ZeroVolume;
                }

                registeredSource.Source.volume = registeredSource.BaseVolume * categoryVolume;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            AudioListener.pause = false;
            AudioSourcePauseStates.Clear();
            RegisteredAudioSources.Clear();
            instance = null;
            musicVolume = DefaultVolume;
            soundVolume = DefaultVolume;
            masterVolume = DefaultVolume;
        }
    }
}
