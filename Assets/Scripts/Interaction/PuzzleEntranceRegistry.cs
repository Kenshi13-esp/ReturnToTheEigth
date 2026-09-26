using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>One world asset that acts as a puzzle entrance: which scene object, which puzzle scene and which progress id.</summary>
    [Serializable]
    public struct PuzzleEntranceDefinition
    {
        [Tooltip("Exact GameObject name of the asset in the exploration scene (for example: pastfirecookieng).")]
        public string assetName;
        [Tooltip("Scene loaded when the player interacts with the asset (must be in Build Settings).")]
        public string sceneToLoad;
        [Tooltip("Stable identifier used to remember that this puzzle is already solved.")]
        public string puzzleId;
        [Tooltip("Prompt shown while the player can interact with the asset.")]
        public string interactionPrompt;
    }

    /// <summary>Editable list of puzzle entrances; loaded from Resources and applied to every scene automatically.</summary>
    [CreateAssetMenu(menuName = MenuPath, fileName = ResourceName)]
    public sealed class PuzzleEntranceRegistry : ScriptableObject
    {
        public const string ResourceName = "PuzzleEntranceRegistry";
        private const string MenuPath = "Return To The Eigth/Puzzles/Puzzle Entrance Registry";

        [SerializeField] private List<PuzzleEntranceDefinition> entries = new List<PuzzleEntranceDefinition>();

        /// <summary>Gets every configured puzzle entrance.</summary>
        public IReadOnlyList<PuzzleEntranceDefinition> Entries => entries;

        /// <summary>Finds the entrance definition whose asset name matches exactly; returns false when none is registered.</summary>
        public bool TryGetEntry(string assetName, out PuzzleEntranceDefinition entry)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].assetName, assetName, StringComparison.Ordinal))
                {
                    entry = entries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
