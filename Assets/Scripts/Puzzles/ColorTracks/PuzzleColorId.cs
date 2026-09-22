using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Colors shared by sliding pieces and their targets in the color track puzzle.</summary>
    public enum PuzzleColorId
    {
        Red,
        Green,
        Blue,
        Yellow,
        Purple,
        Orange
    }

    /// <summary>Static palette and display names for <see cref="PuzzleColorId"/>.</summary>
    public static class PuzzleColorPalette
    {
        private const string RedName = "Rojo";
        private const string GreenName = "Verde";
        private const string BlueName = "Azul";
        private const string YellowName = "Amarillo";
        private const string PurpleName = "Morado";
        private const string OrangeName = "Naranja";
        private static readonly Color RedColor = new Color(0.62f, 0.13f, 0.08f, 1f);
        private static readonly Color GreenColor = new Color(0.30f, 0.62f, 0.20f, 1f);
        private static readonly Color BlueColor = new Color(0.10f, 0.20f, 0.80f, 1f);
        private static readonly Color YellowColor = new Color(0.92f, 0.82f, 0.25f, 1f);
        private static readonly Color PurpleColor = new Color(0.55f, 0.25f, 0.70f, 1f);
        private static readonly Color OrangeColor = new Color(0.90f, 0.50f, 0.15f, 1f);

        /// <summary>Returns the display color for the supplied puzzle color.</summary>
        public static Color GetColor(PuzzleColorId id)
        {
            switch (id)
            {
                case PuzzleColorId.Red: return RedColor;
                case PuzzleColorId.Green: return GreenColor;
                case PuzzleColorId.Blue: return BlueColor;
                case PuzzleColorId.Yellow: return YellowColor;
                case PuzzleColorId.Purple: return PurpleColor;
                default: return OrangeColor;
            }
        }

        /// <summary>Returns the Spanish name used by HUD texts.</summary>
        public static string GetSpanishName(PuzzleColorId id)
        {
            switch (id)
            {
                case PuzzleColorId.Red: return RedName;
                case PuzzleColorId.Green: return GreenName;
                case PuzzleColorId.Blue: return BlueName;
                case PuzzleColorId.Yellow: return YellowName;
                case PuzzleColorId.Purple: return PurpleName;
                default: return OrangeName;
            }
        }
    }
}
