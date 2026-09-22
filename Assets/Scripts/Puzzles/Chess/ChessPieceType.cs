using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Kinds of chess pieces used by the knight placement puzzle.</summary>
    public enum ChessPieceType
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    /// <summary>Placeholder look for pieces without art: a colored quad whose size hints at the piece rank.</summary>
    public static class ChessPiecePlaceholder
    {
        private const float PawnScale = 0.18f;
        private const float KnightScale = 0.26f;
        private const float BishopScale = 0.24f;
        private const float RookScale = 0.24f;
        private const float QueenScale = 0.28f;
        private const float KingScale = 0.30f;

        public static readonly Color WhitePieceColor = new Color(0.92f, 0.92f, 0.86f, 1f);
        public static readonly Color DarkPieceColor = new Color(0.28f, 0.28f, 0.28f, 1f);

        /// <summary>Returns the placeholder quad size (in world units) for the piece type.</summary>
        public static float GetPlaceholderScale(ChessPieceType pieceType)
        {
            switch (pieceType)
            {
                case ChessPieceType.Pawn: return PawnScale;
                case ChessPieceType.Knight: return KnightScale;
                case ChessPieceType.Bishop: return BishopScale;
                case ChessPieceType.Rook: return RookScale;
                case ChessPieceType.Queen: return QueenScale;
                case ChessPieceType.King: return KingScale;
                default: return KnightScale;
            }
        }

        /// <summary>Returns the placeholder tint for a white or dark piece.</summary>
        public static Color GetPlaceholderColor(bool isWhite)
        {
            return isWhite ? WhitePieceColor : DarkPieceColor;
        }
    }
}
