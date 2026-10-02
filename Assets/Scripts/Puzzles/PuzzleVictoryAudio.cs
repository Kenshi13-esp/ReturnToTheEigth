using ReturnToTheEigth.Core;

using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Plays the shared puzzle-completion sound from a persistent 2D audio source.</summary>
    public static class PuzzleVictoryAudio
    {
        private const string ResourcePath = "Sounds/X/PuzleWin";
        private const string AudioObjectName = "PuzzleVictoryAudio";
        private const string MissingClipWarning = "Puzzle victory audio could not load Resources/Sounds/X/PuzleWin.";
        private static AudioClip victoryClip;
        private static AudioSource victorySource;
        private static bool hasLoadedClip;
        private static bool hasWarnedAboutClip;

        /// <summary>Gets whether the shared victory sound is still playing.</summary>
        public static bool IsPlaying => victorySource != null && victorySource.isPlaying;

        /// <summary>Plays the victory sound once; the persistent source survives immediate scene changes.</summary>
        public static void Play()
        {
            if (!hasLoadedClip)
            {
                victoryClip = Resources.Load<AudioClip>(ResourcePath);
                hasLoadedClip = true;
            }

            if (victoryClip == null)
            {
                if (!hasWarnedAboutClip)
                {
                    Debug.LogWarning(MissingClipWarning);
                    hasWarnedAboutClip = true;
                }
                return;
            }

            if (victorySource == null)
            {
                GameObject audioObject = new GameObject(AudioObjectName);
                audioObject.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(audioObject);
                victorySource = audioObject.AddComponent<AudioSource>();
                victorySource.playOnAwake = false;
                victorySource.spatialBlend = 0f;
                victorySource.ignoreListenerPause = true;
                AudioSettingsController.RegisterSoundSource(victorySource);
            }

            victorySource.PlayOneShot(victoryClip);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            victoryClip = null;
            victorySource = null;
            hasLoadedClip = false;
            hasWarnedAboutClip = false;
        }
    }
}