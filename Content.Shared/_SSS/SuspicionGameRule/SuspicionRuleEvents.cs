using Content.Shared._SSS.SuspicionGameRule.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._SSS.SuspicionGameRule;


[Serializable, NetSerializable]
public sealed class SuspicionRuleTimerUpdate(TimeSpan endTime) : EntityEventArgs
{
    public TimeSpan EndTime = endTime;
}

[Serializable, NetSerializable]
public sealed class SuspicionRulePreroundStarted(TimeSpan preroundEndTime) : EntityEventArgs
{
    public TimeSpan PreroundEndTime = preroundEndTime;
}

[Serializable, NetSerializable]
public sealed class SuspicionRuleUpdateRole(SuspicionRole newRole, SuspicionSubRole? newSubRole = null) : EntityEventArgs
{
    public readonly SuspicionRole NewRole = newRole;
    public readonly SuspicionSubRole? NewSubRole = newSubRole;
}

[Serializable, NetSerializable]
public sealed class SuspicionRulePlayerSpawn : EntityEventArgs
{
    public SuspicionGameState GameState;
    public TimeSpan EndTime;
}
