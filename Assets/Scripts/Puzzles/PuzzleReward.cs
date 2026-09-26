using System;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Defines one stable item identifier and positive quantity granted by a solved puzzle.</summary>
    [Serializable]
    public sealed class PuzzleReward
    {
        [SerializeField] private string itemId;
        [SerializeField, Min(1)] private int amount = 1;

        /// <summary>Creates an empty reward for Unity serialization.</summary>
        public PuzzleReward()
        {
        }

        /// <summary>Creates a reward with the supplied stable item identifier and quantity.</summary>
        public PuzzleReward(string itemId, int amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }

        /// <summary>Gets the stable identifier of the rewarded item.</summary>
        public string ItemId => itemId;

        /// <summary>Gets the quantity granted for this reward.</summary>
        public int Amount => amount;
    }
}
