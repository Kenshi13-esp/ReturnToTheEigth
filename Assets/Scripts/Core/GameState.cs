namespace ReturnToTheEigth.Core
{
    /// <summary>The session states used to gate player input and simulation.</summary>
    public enum GameState
    {
        Exploration = 0,
        Puzzle = 1,
        Paused = 2,
        GameOver = 3
    }
}
