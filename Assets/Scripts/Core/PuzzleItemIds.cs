namespace ReturnToTheEigth.Core
{
    /// <summary>Stable item identifiers shared by puzzle rewards, access checks, and session inventory.</summary>
    public static class PuzzleItemIds
    {
        /// <summary>Chess-piece item awarded by the color puzzle and consumed when entering Chess.</summary>
        public const string ChessKnightPiece = "ChessKnightPiece";

        /// <summary>Key item awarded by Chess and used by the left kitchen door.</summary>
        public const string KitchenLeftDoorKey = "KitchenLeftDoorKey";

        /// <summary>Painting fragment awarded by the ColorTrackPuzzle.</summary>
        public const string ColorTrackPuzzlePaintingFragment = "ColorTrackPuzzlePaintingFragment";

        /// <summary>Painting fragment awarded by the Chess puzzle.</summary>
        public const string ChessPuzzlePaintingFragment = "ChessPuzzlePaintingFragment";
    }
}
