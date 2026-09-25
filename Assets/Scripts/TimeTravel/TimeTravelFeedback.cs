using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Compact 2D prototype HUD with controls and time-travel rejection feedback.</summary>
    public sealed class TimeTravelFeedback : MonoBehaviour
    {
        private const string PresentTitle = "PRESENTE / 32 BITS";
        private const string PastTitle = "PASADO / 8 BITS";
        private const string ControlsText = "WASD: mover\nR: cambiar época\nE: interactuar";
        private const string BlockedText = "Viaje bloqueado:\nel destino está ocupado.";
        private const string InteractPrefix = "E: ";
        private const string PrototypeText = "Prototipo 2D\nMuros con colisión";
        private const float FeedbackDuration = 2f;
        private const float PanelX = 8f;
        private const float PanelY = 8f;
        private const float PanelWidth = 174f;
        private const float PanelHeight = 170f;
        private const float Inset = 8f;
        private const float TitleHeight = 24f;
        private const float ControlsHeight = 58f;
        private const float MessageHeight = 68f;
        private const int FontSize = 12;
        private static readonly Color PastColor = new Color(1f, 0.85f, 0.45f, 1f);
        private static readonly Color PresentColor = new Color(0.5f, 0.87f, 1f, 1f);
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private VoidEventChannelSO transitionBlockedChannel;
        [SerializeField] private PlayerInteraction playerInteraction;
        private TimelineEra currentEra;
        private float blockedUntil = float.NegativeInfinity;
        private GUIStyle labelStyle;

        private void OnEnable()
        {
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged += HandleTimelineChanged;
            if (transitionBlockedChannel != null) transitionBlockedChannel.OnEventRaised += HandleTransitionBlocked;
        }

        private void OnDisable()
        {
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged -= HandleTimelineChanged;
            if (transitionBlockedChannel != null) transitionBlockedChannel.OnEventRaised -= HandleTransitionBlocked;
        }

        private void OnGUI()
        {
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, wordWrap = true };
            GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), GUIContent.none);
            float x = PanelX + Inset;
            float y = PanelY + Inset;
            float width = PanelWidth - Inset - Inset;
            labelStyle.normal.textColor = currentEra == TimelineEra.Past ? PastColor : PresentColor;
            GUI.Label(new Rect(x, y, width, TitleHeight), currentEra == TimelineEra.Past ? PastTitle : PresentTitle, labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + TitleHeight, width, ControlsHeight), ControlsText, labelStyle);
            bool blocked = Time.unscaledTime < blockedUntil;
            InteractableBase target = playerInteraction != null ? playerInteraction.CurrentInteractable : null;
            string message = blocked ? BlockedText : target != null && target.isActiveAndEnabled
                ? InteractPrefix + target.InteractionPrompt : PrototypeText;
            labelStyle.normal.textColor = blocked ? Color.yellow : Color.white;
            GUI.Label(new Rect(x, y + TitleHeight + ControlsHeight, width, MessageHeight), message, labelStyle);
        }

        private void HandleTimelineChanged(TimelineEra era) { currentEra = era; blockedUntil = float.NegativeInfinity; }
        private void HandleTransitionBlocked() { blockedUntil = Time.unscaledTime + FeedbackDuration; }
    }
}
