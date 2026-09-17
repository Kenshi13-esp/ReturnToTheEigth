using System;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Events
{
    /// <summary>Broadcasts the active era after a successful transition.</summary>
    [CreateAssetMenu(menuName = MenuPath, fileName = DefaultFileName)]
    public sealed class TimelineEventChannelSO : ScriptableObject
    {
        private const string MenuPath = "Return To The Eigth/Events/Timeline Channel";
        private const string DefaultFileName = "TimelineChangedChannel";

        public event Action<TimelineEra> OnTimelineChanged;

        /// <summary>Notifies listeners of the newly active timeline.</summary>
        public void RaiseTimelineChanged(TimelineEra newTimeline)
        {
            OnTimelineChanged?.Invoke(newTimeline);
        }
    }
}
