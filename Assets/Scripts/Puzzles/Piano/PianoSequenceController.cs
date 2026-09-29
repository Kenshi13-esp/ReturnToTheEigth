using System;
using System.Collections;
using System.Collections.Generic;
using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Puzzles;
using ReturnToTheEigth.UI;
using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Puzzles.Piano
{
    /// <summary>Captures seven piano-key actions, validates the sequence and records completion when confirmed.</summary>
    [DisallowMultipleComponent]
    public sealed class PianoSequenceController : MonoBehaviour
    {
        private const string PuzzleId = "PianoPuzzle";
        private const string MissingActionsWarning = "PianoSequenceController could not find piano left/right and confirm actions.";
        private const string MissingAudioSourceWarning = "PianoSequenceController has no note AudioSource; piano notes will be silent.";
        private const string MissingCameraShakeWarning = "PianoSequenceController has no CameraShake; wrong sequences will still reset.";
        private const string MissingNoteClipWarning = "PianoSequenceController is missing a note clip; that key will be silent.";
        private const string PianoActionPrefix = "Player/";
        private const string ConfirmActionPath = "Player/Interact";
        private const string PianoRewardNotice = "Has conseguido un fragmento de la foto familiar.";
        private const int SequenceLength = 5;
        private const int KeyCount = 7;
        private const int NavigationActionCount = 2;
        private const int FirstIndex = 0;
        private const float DefaultFailureShakeDuration = 0.5f;
        private const float DefaultFailureShakeMagnitude = 0.18f;
        private const float DefaultFailureResetDelay = 0.5f;
        private const float DefaultDemonstrationNoteDuration = 0.45f;
        private const float MinimumDemonstrationNoteDuration = 0.1f;
        private const float DemonstrationNoteGap = 0.15f;
        private const int FailureToneSampleRate = 44100;
        private const int FailureToneChannelCount = 1;
        private const float FailureToneDuration = 0.45f;
        private const float FailureToneFrequency = 185f;
        private const float FailureToneSecondFrequency = 147f;
        private const float FailureToneAmplitude = 0.16f;
        private const float TwoPi = Mathf.PI * 2f;
        private const float Zero = 0f;
        private const float PanelWidth = 620f;
        private const float KeyboardHeight = 170f;
        private const float KeyGap = 4f;
        private const float PanelPadding = 18f;
        private static readonly Color PianoWhite = Color.white;
        private static readonly Color SelectionColor = PuzzleInteractionPalette.GetHighlightedColor(Color.white);
        private static readonly Color DemonstrationColor = PuzzleInteractionPalette.GetHighlightedColor(Color.white);
        private static readonly Color OutlineColor = Color.black;
        private static readonly string[] PianoActionNames = { "PianoLeft", "PianoRight" };
        private static readonly PianoKey[] TargetSequence =
        {
            PianoKey.D,
            PianoKey.F,
            PianoKey.D,
            PianoKey.A,
            PianoKey.H
        };
        private static readonly PuzzleReward[] CompletionRewards =
        {
            new PuzzleReward(PuzzleItemIds.PianoPuzzlePaintingFragment, 1)
        };

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private AudioSource noteAudioSource;
        [SerializeField] private AudioClip nota1;
        [SerializeField] private AudioClip nota2;
        [SerializeField] private AudioClip nota3;
        [SerializeField] private AudioClip nota4;
        [SerializeField] private AudioClip nota5;
        [SerializeField] private AudioClip nota6;
        [SerializeField] private CameraShake cameraShake;
        [SerializeField] private SpriteRenderer[] keyVisuals = Array.Empty<SpriteRenderer>();
        [SerializeField, Min(Zero)] private float failureShakeDuration = DefaultFailureShakeDuration;
        [SerializeField, Min(Zero)] private float failureShakeMagnitude = DefaultFailureShakeMagnitude;
        [SerializeField, Min(Zero)] private float failureResetDelay = DefaultFailureResetDelay;

        private readonly List<PianoKey> enteredSequence = new List<PianoKey>(SequenceLength);
        private readonly InputAction[] navigationActions = new InputAction[NavigationActionCount];
        private InputAction confirmAction;
        private Coroutine resetRoutine;
        private Coroutine demonstrationRoutine;
        private AudioClip failureClip;
        private Texture2D whiteTexture;
        private Texture2D blackTexture;
        private Texture2D selectionTexture;
        private int selectedKeyIndex;
        private int demonstrationKeyIndex = -1;
        private bool inputLocked;
        private bool isDemonstrating;

        /// <summary>Raised once after the player confirms the correct sequence.</summary>
        public event Action Solved;

        /// <summary>Raised whenever the player confirms an incorrect complete sequence.</summary>
        public event Action Failed;

        /// <summary>Gets whether the correct sequence has been confirmed.</summary>
        public bool IsSolved { get; private set; }

        private enum PianoKey
        {
            A,
            S,
            D,
            F,
            G,
            H,
            J
        }

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogWarning(MissingActionsWarning, this);
            }
            else
            {
                for (int index = FirstIndex; index < PianoActionNames.Length; index++)
                {
                    InputAction sourceAction = inputActions.FindAction(PianoActionPrefix + PianoActionNames[index], false);
                    navigationActions[index] = sourceAction != null ? sourceAction.Clone() : null;
                    if (navigationActions[index] == null)
                    {
                        Debug.LogWarning(MissingActionsWarning, this);
                        break;
                    }
                }
                InputAction sourceConfirmAction = inputActions.FindAction(ConfirmActionPath, false);
                confirmAction = sourceConfirmAction != null ? sourceConfirmAction.Clone() : null;
                if (confirmAction == null)
                {
                    Debug.LogWarning(MissingActionsWarning, this);
                }
            }

            if (noteAudioSource == null)
            {
                noteAudioSource = GetComponent<AudioSource>();
            }
            if (noteAudioSource == null)
            {
                Debug.LogWarning(MissingAudioSourceWarning, this);
            }
            if (cameraShake == null)
            {
                cameraShake = FindAnyObjectByType<CameraShake>();
            }
            failureClip = CreateFailureClip();
        }

        private void Start()
        {
            inputLocked = true;
            isDemonstrating = true;
            RefreshKeyVisuals();
            GameManager.Instance?.SetGameState(GameState.Puzzle);
            demonstrationRoutine = StartCoroutine(PlayTargetSequence());
        }

        private IEnumerator PlayTargetSequence()
        {
            for (int index = FirstIndex; index < TargetSequence.Length; index++)
            {
                PianoKey key = TargetSequence[index];
                demonstrationKeyIndex = (int)key;
                RefreshKeyVisuals();
                AudioClip playedClip = PlayKeySound(key, index);
                float noteDuration = playedClip != null
                    ? Mathf.Max(MinimumDemonstrationNoteDuration, playedClip.length)
                    : DefaultDemonstrationNoteDuration;
                yield return new WaitForSeconds(noteDuration);
                demonstrationKeyIndex = -1;
                RefreshKeyVisuals();
                yield return new WaitForSeconds(DemonstrationNoteGap);
            }

            demonstrationKeyIndex = -1;
            isDemonstrating = false;
            RefreshKeyVisuals();
            inputLocked = false;
            demonstrationRoutine = null;
        }

        private void OnEnable()
        {
            for (int index = FirstIndex; index < navigationActions.Length; index++)
            {
                navigationActions[index]?.Enable();
            }
            confirmAction?.Enable();
        }

        private void OnDisable()
        {
            for (int index = FirstIndex; index < navigationActions.Length; index++)
            {
                navigationActions[index]?.Disable();
            }
            confirmAction?.Disable();
            if (demonstrationRoutine != null)
            {
                StopCoroutine(demonstrationRoutine);
                demonstrationRoutine = null;
                demonstrationKeyIndex = -1;
                isDemonstrating = false;
                RefreshKeyVisuals();
                inputLocked = false;
            }
            if (resetRoutine != null)
            {
                StopCoroutine(resetRoutine);
                resetRoutine = null;
                inputLocked = false;
            }
        }

        private void OnDestroy()
        {
            for (int index = FirstIndex; index < navigationActions.Length; index++)
            {
                navigationActions[index]?.Dispose();
            }
            confirmAction?.Dispose();
            if (failureClip != null) Destroy(failureClip);
            if (whiteTexture != null) Destroy(whiteTexture);
            if (blackTexture != null) Destroy(blackTexture);
            if (selectionTexture != null) Destroy(selectionTexture);
        }

        private void Update()
        {
            if (inputLocked || IsSolved)
            {
                return;
            }

            if (navigationActions[FirstIndex] != null && navigationActions[FirstIndex].WasPerformedThisFrame())
            {
                SetSelectedKey(selectedKeyIndex - 1);
                return;
            }
            if (navigationActions[FirstIndex + 1] != null && navigationActions[FirstIndex + 1].WasPerformedThisFrame())
            {
                SetSelectedKey(selectedKeyIndex + 1);
                return;
            }
            if (confirmAction != null && confirmAction.WasPerformedThisFrame())
            {
                AcceptSelectedKey();
            }
        }

        private void SetSelectedKey(int index)
        {
            selectedKeyIndex = Mathf.Clamp(index, FirstIndex, KeyCount - 1);
            RefreshKeyVisuals();
        }

        private void AcceptSelectedKey()
        {
            if (enteredSequence.Count >= SequenceLength)
            {
                return;
            }

            PianoKey selectedKey = (PianoKey)selectedKeyIndex;
            int sequenceIndex = enteredSequence.Count;
            if (selectedKey != TargetSequence[sequenceIndex])
            {
                HandleWrongSequence();
                return;
            }

            AudioClip playedClip = PlayKeySound(selectedKey, sequenceIndex);
            enteredSequence.Add(selectedKey);
            if (enteredSequence.Count == SequenceLength)
            {
                ValidateSequence(playedClip);
                return;
            }

        }

        private void ValidateSequence(AudioClip finalNoteClip)
        {
            for (int index = FirstIndex; index < SequenceLength; index++)
            {
                if (enteredSequence[index] != TargetSequence[index])
                {
                    HandleWrongSequence();
                    return;
                }
            }

            IsSolved = true;
            inputLocked = true;
            GameManager.Instance?.SetGameState(GameState.Puzzle);
            if (finalNoteClip != null && noteAudioSource != null && noteAudioSource.isPlaying)
            {
                StartCoroutine(CompleteSolvedSequenceAfterFinalNote());
                return;
            }

            CompleteSolvedSequence();
        }

        private IEnumerator CompleteSolvedSequenceAfterFinalNote()
        {
            while (noteAudioSource != null && noteAudioSource.isPlaying)
            {
                yield return null;
            }

            CompleteSolvedSequence();
        }

        private void CompleteSolvedSequence()
        {
            PuzzleVictoryAudio.Play();
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.MarkPuzzleCompleted(PuzzleId);
                if (gameManager.TryGrantPuzzleRewards(PuzzleId, CompletionRewards))
                {
                    gameManager.SetPendingRewardNotice(PianoRewardNotice);
                }
            }
            Solved?.Invoke();
        }

        private void HandleWrongSequence()
        {
            inputLocked = true;
            demonstrationKeyIndex = -1;
            Failed?.Invoke();
            PlayFailureSound();
            if (cameraShake != null)
            {
                cameraShake.Shake(failureShakeDuration, failureShakeMagnitude);
            }
            else
            {
                Debug.LogWarning(MissingCameraShakeWarning, this);
            }

            resetRoutine = StartCoroutine(ResetAfterFailure());
        }

        private IEnumerator ResetAfterFailure()
        {
            float failureFeedbackDuration = Mathf.Max(failureResetDelay, Mathf.Max(FailureToneDuration, failureShakeDuration));
            yield return new WaitForSeconds(failureFeedbackDuration);
            enteredSequence.Clear();
            selectedKeyIndex = FirstIndex;
            demonstrationKeyIndex = -1;
            isDemonstrating = true;
            RefreshKeyVisuals();
            resetRoutine = null;
            demonstrationRoutine = StartCoroutine(PlayTargetSequence());
        }

        private void PlayFailureSound()
        {
            if (noteAudioSource != null && failureClip != null)
            {
                noteAudioSource.PlayOneShot(failureClip);
            }
        }

        private static AudioClip CreateFailureClip()
        {
            int sampleCount = Mathf.CeilToInt(FailureToneSampleRate * FailureToneDuration);
            float[] samples = new float[sampleCount];
            for (int sampleIndex = FirstIndex; sampleIndex < sampleCount; sampleIndex++)
            {
                float time = sampleIndex / (float)FailureToneSampleRate;
                float progress = sampleIndex / (float)sampleCount;
                float envelope = 1f - progress;
                float lowTone = Mathf.Sin(TwoPi * FailureToneFrequency * time);
                float highTone = Mathf.Sin(TwoPi * FailureToneSecondFrequency * time);
                samples[sampleIndex] = (lowTone + highTone) * FailureToneAmplitude * envelope;
            }

            AudioClip clip = AudioClip.Create(
                "PianoPuzzleError",
                sampleCount,
                FailureToneChannelCount,
                FailureToneSampleRate,
                false);
            clip.SetData(samples, FirstIndex);
            return clip;
        }

        private AudioClip PlayKeySound(PianoKey key, int sequenceIndex)
        {
            if (noteAudioSource == null)
            {
                return null;
            }

            AudioClip clip = GetClipForKey(key, sequenceIndex);
            if (clip == null)
            {
                Debug.LogWarning(MissingNoteClipWarning, this);
                return null;
            }

            noteAudioSource.PlayOneShot(clip);
            return clip;
        }

        private AudioClip GetClipForKey(PianoKey key, int sequenceIndex)
        {
            switch (key)
            {
                case PianoKey.D:
                    if (sequenceIndex == 0) return nota1;
                    if (sequenceIndex == 2) return nota3;
                    return nota6;
                case PianoKey.F:
                    return nota2;
                case PianoKey.A:
                    return nota4;
                case PianoKey.H:
                    return nota5;
                default:
                    return nota6;
            }
        }

        private void OnGUI()
        {
            if (HasSceneKeyboardArt()) return;

            EnsureGuiTextures();
            float panelWidth = Mathf.Min(PanelWidth, Screen.width - PanelPadding * 2f);
            float keyboardWidth = panelWidth - PanelPadding * 2f;
            float keyWidth = (keyboardWidth - KeyGap * (KeyCount - 1)) / KeyCount;
            float keyHeight = Mathf.Min(KeyboardHeight, Screen.height - PanelPadding * 2f);
            float keyY = (Screen.height - keyHeight) * 0.5f;
            float panelX = (Screen.width - panelWidth) * 0.5f;

            for (int index = FirstIndex; index < KeyCount; index++)
            {
                float keyX = panelX + PanelPadding + index * (keyWidth + KeyGap);
                Rect keyRect = new Rect(keyX, keyY, keyWidth, keyHeight);
                GUI.DrawTexture(keyRect, whiteTexture);
                GUI.DrawTexture(new Rect(keyRect.x, keyRect.y, keyRect.width, 2f), blackTexture);
                GUI.DrawTexture(new Rect(keyRect.x, keyRect.yMax - 2f, keyRect.width, 2f), blackTexture);
                GUI.DrawTexture(new Rect(keyRect.x, keyRect.y, 2f, keyRect.height), blackTexture);
                GUI.DrawTexture(new Rect(keyRect.xMax - 2f, keyRect.y, 2f, keyRect.height), blackTexture);
                if (index == demonstrationKeyIndex || (!isDemonstrating && index == selectedKeyIndex))
                {
                    bool isDemonstrationKey = index == demonstrationKeyIndex;
                    Texture2D highlightTexture = isDemonstrationKey ? whiteTexture : selectionTexture;
                    GUI.color = isDemonstrationKey ? DemonstrationColor : Color.white;
                    GUI.DrawTexture(new Rect(keyRect.x, keyRect.y, keyRect.width, 5f), highlightTexture);
                    GUI.DrawTexture(new Rect(keyRect.x, keyRect.yMax - 5f, keyRect.width, 5f), highlightTexture);
                    GUI.DrawTexture(new Rect(keyRect.x, keyRect.y, 5f, keyRect.height), highlightTexture);
                    GUI.DrawTexture(new Rect(keyRect.xMax - 5f, keyRect.y, 5f, keyRect.height), highlightTexture);
                    GUI.color = Color.white;
                }
            }
        }

        private bool HasSceneKeyboardArt()
        {
            if (keyVisuals == null || keyVisuals.Length < KeyCount)
            {
                return false;
            }

            for (int index = FirstIndex; index < KeyCount; index++)
            {
                if (keyVisuals[index] == null || keyVisuals[index].sprite == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshKeyVisuals()
        {
            if (keyVisuals == null)
            {
                return;
            }

            int visualCount = Mathf.Min(keyVisuals.Length, KeyCount);
            for (int index = FirstIndex; index < visualCount; index++)
            {
                SpriteRenderer keyVisual = keyVisuals[index];
                if (keyVisual != null)
                {
                    keyVisual.color = index == demonstrationKeyIndex
                        ? DemonstrationColor
                        : !isDemonstrating && index == selectedKeyIndex ? SelectionColor : PianoWhite;
                }
            }
        }

        private void EnsureGuiTextures()
        {
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                whiteTexture.SetPixel(0, 0, PianoWhite);
                whiteTexture.Apply();
            }
            if (blackTexture == null)
            {
                blackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                blackTexture.SetPixel(0, 0, OutlineColor);
                blackTexture.Apply();
            }
            if (selectionTexture == null)
            {
                selectionTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                selectionTexture.SetPixel(0, 0, SelectionColor);
                selectionTexture.Apply();
            }
        }
    }
}
