using System;
using UnityEngine;

namespace ReturnToTheEigth.Events
{
    /// <summary>A shared signal that does not retain references to scene objects in assets.</summary>
    [CreateAssetMenu(menuName = MenuPath, fileName = DefaultFileName)]
    public sealed class VoidEventChannelSO : ScriptableObject
    {
        private const string MenuPath = "Return To The Eigth/Events/Void Channel";
        private const string DefaultFileName = "VoidChannel";

        public event Action OnEventRaised;

        /// <summary>Notifies the currently subscribed listeners.</summary>
        public void RaiseEvent()
        {
            OnEventRaised?.Invoke();
        }
    }
}
