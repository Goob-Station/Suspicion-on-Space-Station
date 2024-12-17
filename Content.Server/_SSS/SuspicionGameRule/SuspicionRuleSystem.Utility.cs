using Content.Shared._SSS.SuspicionGameRule.Components;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Store.Components;
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

    private void AddTcToPlayer(EntityUid player, int amount, bool displayMessage = true)
    {
        var implantedComponent = CompOrNull<ImplantedComponent>(player);
        if (implantedComponent == null)
            return;

        foreach (var implant in implantedComponent.ImplantContainer.ContainedEntities)
        {
            var storeComp = CompOrNull<StoreComponent>(implant);
            if (storeComp == null)
                continue;

            _storeSystem.TryAddCurrency(new Dictionary<string, FixedPoint2>()
                {
                    { "Telecrystal", FixedPoint2.New(amount)},
                },
                implant,
                storeComp
            );
        }

        if (!_playerManager.TryGetSessionByEntity(player, out var session))
            return;

        if (!displayMessage)
            return;

        var message = Loc.GetString("tc-added-sus", ("tc", amount));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, message, EntityUid.Invalid, false, session.Channel);
    }

    private Entity<StoreComponent>? GetUplinkImplant(EntityUid player)
    {
        var implantedComponent = CompOrNull<ImplantedComponent>(player);
        if (implantedComponent == null)
            return null;

        foreach (var implant in implantedComponent.ImplantContainer.ContainedEntities)
        {
            var storeComp = CompOrNull<StoreComponent>(implant);
            if (storeComp == null)
                continue;

            return (implant, storeComp);
        }

        return null;
    }

    /// <summary>
    /// Finds all players with a specific role.
    /// </summary>
    private List<(EntityUid body, Entity<MindRoleComponent, SuspicionRoleComponent> sus)> FindAllOfType(SuspicionRole role, bool filterDead = true)
    {
        var allMinds = new  HashSet<Entity<MindComponent>>();
        if (filterDead)
        {
            allMinds = _mindSystem.GetAliveHumans(EntityUid.Invalid);
        }
        else
        {
            var query = EntityQueryEnumerator<MindComponent, HumanoidAppearanceComponent>();
            while (query.MoveNext(out var mind, out var mindComp, out _))
            {
                allMinds.Add(new Entity<MindComponent>(mind, mindComp));
            }
        }

        var result = new List<(EntityUid body, Entity<MindRoleComponent, SuspicionRoleComponent>)>();
        foreach (var mind in allMinds)
        {
            var nullableMind = new Entity<MindComponent?>(mind.Owner, mind.Comp); // I see your shitcode, and i raise you MORE shit code.

            if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(nullableMind, out var roleComp))
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

    public void DropAllItemsOnEntity(EntityUid entity)
    {
        if (!TryComp(entity, out InventoryComponent? inventory))
            return;

        var slots = _inventory.GetSlotEnumerator(new Entity<InventoryComponent?>(entity, inventory), SlotFlags.All);
        var targetPos = _transformSystem.GetWorldPosition(entity);

        while (slots.MoveNext(out var slot))
        {
            foreach (var rootContainerEnt in slot.ContainedEntities)
            {
                if (!TryComp(rootContainerEnt, out StorageComponent? storage))
                    continue;

                var dumpQueue = new Queue<EntityUid>(storage.Container.ContainedEntities);

                foreach (var item in dumpQueue)
                {
                    var transform = Transform(item);
                    _transformSystem.SetWorldPositionRotation(item, targetPos + _random.NextVector2Box() / 4, _random.NextAngle(), transform);
                }
            }
        }
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
