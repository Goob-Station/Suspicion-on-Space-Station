using Content.Shared.Rejuvenate;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;

namespace Content.Shared._SSS.Medical;

public sealed partial class SuspicionReviveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    private void OnDefibrillated(Entity<MobStateComponent> ent, ref TargetDefibrillatedEvent args)
    {
        RaiseLocalEvent(ent, new RejuvenateEvent());
    }
}
