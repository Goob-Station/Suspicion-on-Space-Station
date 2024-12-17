using Robust.Shared.Configuration;
// ReSharper disable InconsistentNaming rawr

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Percentage of total players that will be a traitor.
    /// The number of players will be multiplied by this number, and then rounded down.
    /// If the result is less than 1 or more than the player count, it is clamped to those values.
    /// </summary>
    public static readonly CVarDef<float> SSSTraitorPercentage =
        CVarDef.Create("sss.traitor_percentage", 0.25f, CVar.SERVERONLY);

    /// <summary>
    /// Percentage of total players that will be a detective (detective innocent). Handled similar to traitor percentage (rounded down etc).
    /// </summary>
    public static readonly CVarDef<float> SSSDetectivePercentage =
        CVarDef.Create("sss.detective_percentage", 0.25f, CVar.SERVERONLY);

    /// <summary>
    /// How long to wait before the game starts after the round starts.
    /// </summary>
    public static readonly CVarDef<int> SSSPreparingDuration =
        CVarDef.Create("sss.preparing_duration", 30, CVar.SERVERONLY);

    /// <summary>
    /// How long the round lasts in seconds.
    /// </summary>
    public static readonly CVarDef<int> SSSRoundDuration =
        CVarDef.Create("sss.round_duration", 480, CVar.SERVERONLY);

    /// <summary>
    /// How long to add to the round time when a player is killed.
    /// </summary>
    public static readonly CVarDef<int> SSSTimeAddedPerKill =
        CVarDef.Create("sss.time_added_per_kill", 30, CVar.SERVERONLY);

    /// <summary>
    /// How long to wait before restarting the round after the summary is displayed.
    /// </summary>
    public static readonly CVarDef<int> SSSPostRoundDuration =
        CVarDef.Create("sss.post_round_duration", 30, CVar.SERVERONLY);
}
