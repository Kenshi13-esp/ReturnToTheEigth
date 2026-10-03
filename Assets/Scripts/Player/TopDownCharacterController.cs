using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Player
{
    /// <summary>Moves a gravity-free 2D body on XY and publishes time-shift requests.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class TopDownCharacterController : MonoBehaviour
    {
        private const string MoveActionPath = "Player/Move";
        private const string TimeShiftActionPath = "Player/TimeShift";
        private const string HallSceneName = "Hall";
        private const string MissingInputError = "The 2D player requires Player/Move and Player/TimeShift actions.";
        private const string FootstepSoundId = "wood-footstep-a";
        private const float DefaultMovementSpeed = 0.3f;
        private const float FootstepDistance = 0.42f;
        private const float FootstepPlaybackSpeed = 1.25f;
        private const float InputThreshold = 0.0001f;
        private const float UnitMagnitude = 1f;
        private const float Zero = 0f;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private VoidEventChannelSO timeShiftRequestedChannel;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField, Min(Zero)] private float movementSpeed = DefaultMovementSpeed;
        private Rigidbody2D body;
        private Vector2 previousFootstepPosition;
        private float distanceSinceLastFootstep;
        private InputAction moveAction;
        private InputAction timeShiftAction;
        private bool movementEnabled = true;
        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        private bool AllowsGameplay => gameStateChannel == null || gameStateChannel.CurrentState == GameState.Exploration;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = Zero;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            previousFootstepPosition = body.position;
            InputAction sourceMove = inputActions != null ? inputActions.FindAction(MoveActionPath) : null;
            InputAction sourceShift = inputActions != null ? inputActions.FindAction(TimeShiftActionPath) : null;
            if (sourceMove == null || sourceShift == null)
            {
                Debug.LogError(MissingInputError, this);
                enabled = false;
                return;
            }
            moveAction = sourceMove.Clone();
            timeShiftAction = sourceShift.Clone();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            moveAction?.Enable();
            if (timeShiftAction != null)
            {
                timeShiftAction.performed += HandleTimeShift;
                timeShiftAction.Enable();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            moveAction?.Disable();
            if (timeShiftAction != null)
            {
                timeShiftAction.performed -= HandleTimeShift;
                timeShiftAction.Disable();
            }
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                ResetFootstepDistanceTracking();
            }
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            timeShiftAction?.Dispose();
        }

        private void Start()
        {
            RestorePendingPlayerReturnPosition();
            ResetFootstepDistanceTracking();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == HallSceneName)
            {
                RestorePendingPlayerReturnPosition();
            }
        }

        private void RestorePendingPlayerReturnPosition()
        {
            if (SceneManager.GetActiveScene().name != HallSceneName
                || body == null
                || GameManager.Instance == null
                || !GameManager.Instance.TryConsumePendingPlayerReturnPosition(out Vector3 returnPosition))
            {
                return;
            }

            body.position = new Vector2(returnPosition.x, returnPosition.y);
            body.linearVelocity = Vector2.zero;
            transform.position = new Vector3(returnPosition.x, returnPosition.y, returnPosition.z);
            Physics2D.SyncTransforms();
            ResetFootstepDistanceTracking();

            TopDownCameraFollow cameraFollow = FindAnyObjectByType<TopDownCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
            }
        }

        private void FixedUpdate()
        {
            UpdateFootstepAudio();
            Vector2 direction = movementEnabled && AllowsGameplay && moveAction != null
                ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), UnitMagnitude) : Vector2.zero;
            if (direction.sqrMagnitude > InputThreshold)
            {
                FacingDirection = direction.normalized;
            }
            body.linearVelocity = direction * movementSpeed;
        }

        private void UpdateFootstepAudio()
        {
            Vector2 currentPosition = body.position;
            distanceSinceLastFootstep += Vector2.Distance(previousFootstepPosition, currentPosition);
            previousFootstepPosition = currentPosition;

            while (distanceSinceLastFootstep >= FootstepDistance)
            {
                distanceSinceLastFootstep -= FootstepDistance;
                SoundManager.Play(FootstepSoundId, FootstepPlaybackSpeed);
            }
        }

        private void ResetFootstepDistanceTracking()
        {
            if (body == null)
            {
                return;
            }

            previousFootstepPosition = body.position;
            distanceSinceLastFootstep = Zero;
        }

        /// <summary>Sets the direction retained by idle animation and interaction systems.</summary>
        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= InputThreshold) return;
            FacingDirection = direction.normalized;
        }

        /// <summary>Enables or stops movement without changing the independent interaction/time-travel bindings.</summary>
        public void SetMovementEnabled(bool isEnabled)
        {
            movementEnabled = isEnabled;
            if (!isEnabled && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void HandleTimeShift(InputAction.CallbackContext context)
        {
            if (AllowsGameplay)
            {
                timeShiftRequestedChannel?.RaiseEvent();
            }
        }
    }
}
