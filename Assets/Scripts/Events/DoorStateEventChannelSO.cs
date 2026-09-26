using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReturnToTheEigth.Events
{
    /// <summary>Routes door commands by stable identifier, including to inactive era doors.</summary>
    [CreateAssetMenu(menuName = MenuPath, fileName = DefaultFileName)]
    public sealed class DoorStateEventChannelSO : ScriptableObject
    {
        private const string MenuPath = "Return To The Eigth/Events/Door State Channel";
        private const string DefaultFileName = "DoorStateChannel";
        private const string InvalidIdentifierWarning = "Door state requests require a non-empty door identifier.";
        private readonly Dictionary<string, bool> requestedStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly HashSet<string> permanentlyOpenDoorIdentifiers = new HashSet<string>(StringComparer.Ordinal);

        public event Action<string, bool> OnDoorStateRequested;

        /// <summary>Retains and broadcasts a door command; permanently opened doors reject later close requests.</summary>
        public void RequestDoorState(string doorIdentifier, bool shouldOpen)
        {
            if (string.IsNullOrWhiteSpace(doorIdentifier))
            {
                Debug.LogWarning(InvalidIdentifierWarning, this);
                return;
            }

            if (permanentlyOpenDoorIdentifiers.Contains(doorIdentifier))
            {
                shouldOpen = true;
            }

            requestedStates[doorIdentifier] = shouldOpen;
            OnDoorStateRequested?.Invoke(doorIdentifier, shouldOpen);
        }

        /// <summary>Records a permanent open state before broadcasting it to active and inactive door listeners.</summary>
        public void RequestPermanentDoorOpen(string doorIdentifier)
        {
            if (string.IsNullOrWhiteSpace(doorIdentifier))
            {
                Debug.LogWarning(InvalidIdentifierWarning, this);
                return;
            }

            permanentlyOpenDoorIdentifiers.Add(doorIdentifier);
            requestedStates[doorIdentifier] = true;
            OnDoorStateRequested?.Invoke(doorIdentifier, true);
        }

        /// <summary>Returns whether the supplied door identifier has been permanently opened this session.</summary>
        public bool IsDoorPermanentlyOpen(string doorIdentifier)
        {
            return !string.IsNullOrWhiteSpace(doorIdentifier) &&
                permanentlyOpenDoorIdentifiers.Contains(doorIdentifier);
        }

        /// <summary>Gets the latest session command for a door, if one has been issued.</summary>
        public bool TryGetDoorState(string doorIdentifier, out bool shouldOpen)
        {
            shouldOpen = false;
            return !string.IsNullOrWhiteSpace(doorIdentifier)
                && requestedStates.TryGetValue(doorIdentifier, out shouldOpen);
        }

        /// <summary>Clears transient door commands when a new play session starts.</summary>
        public void ResetSession()
        {
            requestedStates.Clear();
            permanentlyOpenDoorIdentifiers.Clear();
        }
    }
}
