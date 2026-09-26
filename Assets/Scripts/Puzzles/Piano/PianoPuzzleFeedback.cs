using ReturnToTheEigth.Core;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles.Piano
{
    /// <summary>Returns to Hall after the piano sequence is solved and starts persistent melody playback.</summary>
    [DisallowMultipleComponent]
    public sealed class PianoPuzzleFeedback : MonoBehaviour
    {
        private const string MissingControllerWarning = "PianoPuzzleFeedback could not find a PianoSequenceController.";
        private const string MissingManagerWarning = "Piano puzzle returned to Hall without a GameManager; movement could not be gated during the melody.";
        private const string MelodyRunnerObjectName = "PianoMelodyReturnPlayer";

        [SerializeField] private PianoSequenceController sequenceController;
        [SerializeField] private AudioClip melodyClip;
        private bool isReturningToHall;

        private void Awake()
        {
            if (sequenceController == null)
            {
                sequenceController = FindAnyObjectByType<PianoSequenceController>();
            }
            if (sequenceController == null)
            {
                Debug.LogWarning(MissingControllerWarning, this);
            }
        }

        private void OnEnable()
        {
            if (sequenceController != null)
            {
                sequenceController.Solved += HandleSequenceSolved;
            }
        }

        private void OnDisable()
        {
            if (sequenceController != null)
            {
                sequenceController.Solved -= HandleSequenceSolved;
            }
        }

        private void HandleSequenceSolved()
        {
            if (isReturningToHall)
            {
                return;
            }

            isReturningToHall = true;
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogWarning(MissingManagerWarning, this);
            }
            gameManager?.SetGameState(GameState.Puzzle);

            GameObject melodyRunnerObject = new GameObject(MelodyRunnerObjectName);
            PianoMelodyReturnPlayer melodyRunner = melodyRunnerObject.AddComponent<PianoMelodyReturnPlayer>();
            melodyRunner.ReturnToHallAndPlay(melodyClip, gameManager);
        }
    }
}
