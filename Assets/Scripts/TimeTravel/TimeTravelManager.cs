using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Switches aligned XY eras after checking destination Collider2D geometry.</summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class TimeTravelManager : MonoBehaviour
    {
        private const int ExecutionOrder = -100;
        private const int OverlapCapacity = 64;
        private const int FirstIndex = 0;
        private const int AllLayers = -1;
        private const float TransitionCooldownSeconds = 0.5f;
        private const float ClearanceInset = 0.002f;
        private const float MinimumSize = 0.001f;
        private const float Zero = 0f;
        private const string MissingSetupError = "TimeTravelManager requires distinct XY era roots and a BoxCollider2D player outside them.";
        [SerializeField] private GameObject presentMansionRoot;
        [SerializeField] private GameObject pastMansionRoot;
        [SerializeField] private BoxCollider2D playerCollider;
        [SerializeField] private TimelineEra initialEra = TimelineEra.Present;
        [SerializeField] private LayerMask solidObstacleLayers = AllLayers;
        [SerializeField, Min(Zero)] private float transitionCooldownSeconds = TransitionCooldownSeconds;
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private VoidEventChannelSO timeShiftRequestedChannel;
        [SerializeField] private VoidEventChannelSO transitionBlockedChannel;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        private readonly Collider2D[] liveOverlaps = new Collider2D[OverlapCapacity];
        private TimelineCollisionWorld collisionWorld;
        private float nextTransitionTime;
        private bool isTransitioning;
        private bool isConfigured;
        public TimelineEra CurrentEra { get; private set; } = TimelineEra.Present;

        private void Awake()
        {
            isConfigured = presentMansionRoot != null && pastMansionRoot != null && playerCollider != null
                && presentMansionRoot != pastMansionRoot;
            if (isConfigured)
            {
                isConfigured = !playerCollider.transform.IsChildOf(presentMansionRoot.transform)
                    && !playerCollider.transform.IsChildOf(pastMansionRoot.transform)
                    && !transform.IsChildOf(presentMansionRoot.transform)
                    && !transform.IsChildOf(pastMansionRoot.transform)
                    && !pastMansionRoot.transform.IsChildOf(presentMansionRoot.transform)
                    && !presentMansionRoot.transform.IsChildOf(pastMansionRoot.transform);
            }
            if (!isConfigured)
            {
                Debug.LogError(MissingSetupError, this);
                enabled = false;
                return;
            }
            collisionWorld = new TimelineCollisionWorld();
            CurrentEra = initialEra;
            SetEraRootsActive(CurrentEra);
            Physics2D.SyncTransforms();
        }

        private void OnEnable()
        {
            if (timeShiftRequestedChannel != null)
                timeShiftRequestedChannel.OnEventRaised += HandleTimeShiftRequested;
        }

        private void Start() { timelineChangedChannel?.RaiseTimelineChanged(CurrentEra); }

        private void OnDisable()
        {
            if (timeShiftRequestedChannel != null)
                timeShiftRequestedChannel.OnEventRaised -= HandleTimeShiftRequested;
        }

        private void OnDestroy() { collisionWorld?.Dispose(); }

        /// <summary>Attempts a 2D era switch without changing XY position; blocked travel leaves both roots untouched.</summary>
        public bool TryShiftTime()
        {
            if (!isConfigured || !isActiveAndEnabled || isTransitioning || !playerCollider.enabled
                || Time.unscaledTime < nextTransitionTime
                || (gameStateChannel != null && gameStateChannel.CurrentState != GameState.Exploration)) return false;
            TimelineEra targetEra = CurrentEra == TimelineEra.Present ? TimelineEra.Past : TimelineEra.Present;
            isTransitioning = true;
            try
            {
                if (!CanTransitionToEra(targetEra))
                {
                    transitionBlockedChannel?.RaiseEvent();
                    return false;
                }
                CurrentEra = targetEra;
                SetEraRootsActive(targetEra);
                Physics2D.SyncTransforms();
                nextTransitionTime = Time.unscaledTime + transitionCooldownSeconds;
                timelineChangedChannel?.RaiseTimelineChanged(targetEra);
                return true;
            }
            finally { isTransitioning = false; }
        }

        private bool CanTransitionToEra(TimelineEra targetEra)
        {
            Physics2D.SyncTransforms();
            Transform player = playerCollider.transform;
            Vector2 center = player.TransformPoint(playerCollider.offset);
            Vector3 scale = player.lossyScale;
            Vector2 size = new Vector2(
                Mathf.Max(MinimumSize, playerCollider.size.x * Mathf.Abs(scale.x) - ClearanceInset),
                Mathf.Max(MinimumSize, playerCollider.size.y * Mathf.Abs(scale.y) - ClearanceInset));
            float angle = player.eulerAngles.z;
            GameObject targetRoot = targetEra == TimelineEra.Present ? presentMansionRoot : pastMansionRoot;
            GameObject sourceRoot = CurrentEra == TimelineEra.Present ? presentMansionRoot : pastMansionRoot;
            if (!collisionWorld.IsClear(targetRoot, center, size, angle, solidObstacleLayers)) return false;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(solidObstacleLayers);
            int hitCount = Physics2D.OverlapBox(center, size, angle, filter, liveOverlaps);
            if (hitCount == liveOverlaps.Length) return false;
            for (int index = FirstIndex; index < hitCount; index++)
            {
                Transform hit = liveOverlaps[index].transform;
                if (!hit.IsChildOf(player) && !hit.IsChildOf(sourceRoot.transform) && !hit.IsChildOf(targetRoot.transform))
                    return false;
            }
            return true;
        }

        private void SetEraRootsActive(TimelineEra era)
        {
            GameObject targetRoot = era == TimelineEra.Present ? presentMansionRoot : pastMansionRoot;
            GameObject sourceRoot = era == TimelineEra.Present ? pastMansionRoot : presentMansionRoot;
            sourceRoot.SetActive(false);
            targetRoot.SetActive(true);
        }

        private void HandleTimeShiftRequested() { TryShiftTime(); }
    }
}
