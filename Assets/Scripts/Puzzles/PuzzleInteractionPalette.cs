using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Shared warm interaction tint used by exploration and puzzle selection visuals.</summary>
    public static class PuzzleInteractionPalette
    {
        private const float HighlightBlend = 0.42f;
        private static readonly Color InteractionTargetColor = new Color(1f, 0.82f, 0.42f, 1f);

        /// <summary>Blends a visual's original color toward the shared interaction tint while preserving its alpha.</summary>
        public static Color GetHighlightedColor(Color originalColor)
        {
            Color targetColor = new Color(
                InteractionTargetColor.r,
                InteractionTargetColor.g,
                InteractionTargetColor.b,
                originalColor.a);
            return Color.Lerp(originalColor, targetColor, HighlightBlend);
        }
    }
}