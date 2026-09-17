using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Allows inactive obstacles to expose pending logical collision state.</summary>
    public interface ITimelineObstacle
    {
        /// <summary>Reports whether this collider will be solid when its era is enabled.</summary>
        bool WillBlockTimeline(Collider2D candidate);
    }
}
