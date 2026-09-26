namespace ReturnToTheEigth.Interaction
{
    /// <summary>Defines the access condition required to open a permanent door.</summary>
    public enum DoorAccessMode
    {
        /// <summary>Allows opening only from the configured side of the door plane.</summary>
        OneSided,

        /// <summary>Allows opening by consuming one item with the configured identifier.</summary>
        RequiredItem
    }
}
