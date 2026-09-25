using ReturnToTheEigth.Events;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Player
{
    /// <summary>Plays era-specific Aseprite animation states from player movement and facing direction.</summary>
    [RequireComponent(typeof(Animator), typeof(TopDownCharacterController), typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private const string PresentIdleBackState = "Idle back";
        private const string PresentIdleFrontState = "Idle front";
        private const string PresentIdleLeftState = "Idle left";
        private const string PresentIdleRightState = "Idle right";
        private const string PresentWalkBackState = "walk_back";
        private const string PresentWalkFrontState = "walk_front";
        private const string PresentWalkLeftState = "walk_left";
        private const string PresentWalkRightState = "walk_right";
        private const string PastWalkBackState = "walk_back";
        private const string PastWalkFrontState = "walk front";
        private const string PastWalkLeftState = "walk_left";
        private const string PastWalkRightState = "walk right";
        private const float MovementThreshold = 0.01f;
        private const float MovementThresholdSquared = MovementThreshold * MovementThreshold;
        private const float UnitMagnitude = 1f;
        private const float IdlePlaybackSpeed = 0f;
        private const float WalkPlaybackSpeed = 0.3f;
        private const string BaseLayerName = "Base Layer";
        private const int BaseLayerIndex = 0;
        private const float StartAtBeginning = 0f;

        [SerializeField] private TopDownCharacterController characterController;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private RuntimeAnimatorController presentIdleController;
        [SerializeField] private RuntimeAnimatorController presentWalkController;
        [SerializeField] private RuntimeAnimatorController pastIdleController;
        [SerializeField] private RuntimeAnimatorController pastWalkController;

        private TimelineEra currentEra = TimelineEra.Present;
        private Vector2 lastFacingDirection = Vector2.down;
        private bool isMoving;
        private RuntimeAnimatorController lastController;
        private int lastStateHash;
        private bool lastIsMoving;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<TopDownCharacterController>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (characterController == null || animator == null || body == null)
            {
                Debug.LogWarning("PlayerAnimationController requires an Animator, TopDownCharacterController, and Rigidbody2D.", this);
                enabled = false;
                return;
            }

            lastFacingDirection = characterController.FacingDirection.normalized;
            if (lastFacingDirection.sqrMagnitude <= MovementThresholdSquared)
                lastFacingDirection = Vector2.down;
        }

        private void OnEnable()
        {
            if (timelineChangedChannel != null)
                timelineChangedChannel.OnTimelineChanged += HandleTimelineChanged;

            ApplyAnimationState(true);
        }

        private void OnDisable()
        {
            if (timelineChangedChannel != null)
                timelineChangedChannel.OnTimelineChanged -= HandleTimelineChanged;
        }

        private void Update()
        {
            if (animator == null || body == null)
                return;

            Vector2 velocity = body.linearVelocity;
            isMoving = velocity.sqrMagnitude > MovementThresholdSquared;
            if (isMoving)
            {
                Vector2 facingDirection = characterController != null
                    ? characterController.FacingDirection
                    : velocity.normalized;
                if (facingDirection.sqrMagnitude > MovementThresholdSquared)
                    lastFacingDirection = facingDirection.normalized;
            }

            ApplyAnimationState(false);
        }

        /// <summary>Sets the direction retained by the player's idle animation.</summary>
        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= MovementThresholdSquared)
                return;

            lastFacingDirection = Vector2.ClampMagnitude(direction, UnitMagnitude);
            ApplyAnimationState(false);
        }

        private void HandleTimelineChanged(TimelineEra era)
        {
            currentEra = era;
            isMoving = false;
            ApplyAnimationState(true);
        }

        private void ApplyAnimationState(bool forceRestart)
        {
            if (animator == null)
                return;

            RuntimeAnimatorController controller = GetControllerForCurrentState();
            if (controller == null)
            {
                animator.enabled = false;
                lastController = null;
                lastStateHash = 0;
                lastIsMoving = false;
                return;
            }

            string stateName = GetStateNameForCurrentDirection();
            int stateHash = Animator.StringToHash(BaseLayerName + "." + stateName);
            bool controllerChanged = animator.runtimeAnimatorController != controller;
            if (controllerChanged)
                animator.runtimeAnimatorController = controller;

            animator.enabled = true;
            animator.speed = isMoving ? WalkPlaybackSpeed : IdlePlaybackSpeed;
            if (forceRestart || controllerChanged || lastController != controller || lastStateHash != stateHash
                || lastIsMoving != isMoving)
                animator.Play(stateHash, BaseLayerIndex, StartAtBeginning);

            lastController = controller;
            lastStateHash = stateHash;
            lastIsMoving = isMoving;
        }

        private RuntimeAnimatorController GetControllerForCurrentState()
        {
            if (currentEra == TimelineEra.Present)
                return isMoving ? presentWalkController : presentIdleController;

            return pastWalkController;
        }

        private string GetStateNameForCurrentDirection()
        {
            if (currentEra == TimelineEra.Past)
            {
                return lastFacingDirection.y > Mathf.Abs(lastFacingDirection.x)
                    ? PastWalkBackState
                    : lastFacingDirection.y < -Mathf.Abs(lastFacingDirection.x)
                        ? PastWalkFrontState
                        : lastFacingDirection.x < 0f ? PastWalkLeftState : PastWalkRightState;
            }

            if (!isMoving)
            {
                return lastFacingDirection.y > Mathf.Abs(lastFacingDirection.x)
                    ? PresentIdleBackState
                    : lastFacingDirection.y < -Mathf.Abs(lastFacingDirection.x)
                        ? PresentIdleFrontState
                        : lastFacingDirection.x < 0f ? PresentIdleLeftState : PresentIdleRightState;
            }

            return lastFacingDirection.y > Mathf.Abs(lastFacingDirection.x)
                ? PresentWalkBackState
                : lastFacingDirection.y < -Mathf.Abs(lastFacingDirection.x)
                    ? PresentWalkFrontState
                    : lastFacingDirection.x < 0f ? PresentWalkLeftState : PresentWalkRightState;
        }
    }
}
