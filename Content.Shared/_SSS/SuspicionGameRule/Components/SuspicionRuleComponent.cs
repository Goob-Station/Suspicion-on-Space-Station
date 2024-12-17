using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._SSS.SuspicionGameRule.Components;

[RegisterComponent]
public sealed partial class SuspicionRuleComponent : Component
{
    #region State management

    public SuspicionGameState GameState = SuspicionGameState.Preparing;

    /// <summary>
    /// Defines when the round will end.
    /// </summary>
    public TimeSpan EndAt = TimeSpan.MinValue;

    public List<int> AnnouncedTimeLeft = new List<int>();

    #endregion


    [DataField]
    public int PostRoundDuration = 30;

    /// <summary>
    /// The gear all players spawn with.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<StartingGearPrototype>))]
    public string Gear = "SuspicionGear";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string UplinkImplant = "SusTraitorUplinkImplant";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string DetectiveImplant = "SusDetectiveUplinkImplant";


    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string TraitorRadio = "EncryptionKeySyndie";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<NpcFactionPrototype>))]
    public string TraitorFaction = "Syndicate";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string DetectiveRadio = "EncryptionKeySecurity";

    /// <summary>
    /// How much TC to give to traitors/detectives for their performance
    /// </summary>
    [DataField]
    public int AmountAddedPerKill = 1;

}

public enum SuspicionGameState
{
    /// <summary>
    /// The game is preparing to start. No roles have been assigned yet and new joining players will be spawned in.
    /// </summary>
    Preparing,

    /// <summary>
    /// The game is in progress. Roles have been assigned and players are hopefully killing each other. New joining players will be forced to spectate.
    /// </summary>
    InProgress,

    /// <summary>
    /// The game has ended. The summary is being displayed and players are waiting for the round to restart.
    /// </summary>
    PostRound
}
