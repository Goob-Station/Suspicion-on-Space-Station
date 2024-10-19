using Content.Server._SSS.SuspicionGameRule.Components;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Server.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._SSS.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    /// <summary>
    /// This is DispatchServerAnnouncement but markdown is not escaped (why is it escaped in the first place on a server announcment???)
    /// </summary>
    private void SendAnnouncement(string message, Color? colorOverride = null)
    {
        _chatManager.ChatMessageToAll(
            ChatChannel.Server,
            message,
            message,
            EntityUid.Invalid,
            hideChat: false,
            recordReplay: true,
            colorOverride: colorOverride);
    }

    /// <summary>
    /// Finds all players with a specific role.
    /// </summary>
    private List<(EntityUid body, Entity<MindRoleComponent, SuspicionRoleComponent> sus)> FindAllOfType(SuspicionRole role, bool filterDead = true)
    {
        var allMinds = new List<EntityUid>();
        if (filterDead)
        {
            allMinds = _mindSystem.GetAliveHumansExcept(EntityUid.Invalid);
        }
        else
        {
            var query = EntityQueryEnumerator<MindContainerComponent, HumanoidAppearanceComponent>();
            while (query.MoveNext(out var _, out var mc, out _))
            {
                // the player needs to have a mind and not be the excluded one
                if (mc.Mind == null)
                    continue;

                allMinds.Add(mc.Mind.Value);
            }
        }

        var result = new List<(EntityUid body, Entity<MindRoleComponent, SuspicionRoleComponent>)>();
        foreach (var mind in allMinds)
        {
            if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(mind, out var roleComp))
                continue;

            if (roleComp.Value.Comp2.Role != role)
                continue;

            var entity = Comp<MindComponent>(mind).OwnedEntity;
            if (!entity.HasValue)
                continue;

            result.Add((entity.Value, roleComp.Value));
        }

        return result;
    }

    public void AddKeyToRadio(EntityUid entity, string keyProto)
    {
        if (!_inventory.TryGetSlotEntity(entity, "ears", out var headset))
            return;

        if (!_containerSystem.TryGetContainer(headset.Value, "key_slots", out var container))
            return;

        var key = Spawn(keyProto, MapCoordinates.Nullspace);
        var transform = Comp<TransformComponent>(key);
        var meta = Comp<MetaDataComponent>(key);
        var physics = Comp<PhysicsComponent>(key);

        _containerSystem.Insert((key, transform, meta, physics), container, null, true);
    }
}
