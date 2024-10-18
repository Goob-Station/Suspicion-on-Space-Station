namespace Content.Server._SSS;

/// <summary>
/// Main component that marks a player "active" in a round of SSS.
/// </summary>
[RegisterComponent]
public sealed partial class SuspicionPlayerComponent : Component
{
    /// <summary>
    /// If true, the examine window will show the dead person's role.
    /// </summary>
    [ViewVariables]
    public bool Revealed = false;

    /// <summary>
    /// How many units (probably meters? idk what this game uses) a person must be from the station to be considered "off-station" and start taking damage.
    /// </summary>
    [ViewVariables]
    public float SpacewalkThreshold = 15;

    [ViewVariables]
    public DateTime LastTookSpacewalkDamage = DateTime.Now;
}
