using System;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Cardinal ports exposed by one electricity puzzle tile.</summary>
    [Flags]
    public enum ElectricityPorts
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }
}
