using Content.Shared.Damage;
using Robust.Shared.Player;

namespace Content.Client._SSS.UserInterface;

public sealed partial class SSSStatusUISystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    public event Action<Entity<DamageableComponent>>? OnPlayerDamageChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        var (uid, damagable) = ent;

        if (uid == _playerManager.LocalEntity)
        {
            OnPlayerDamageChanged?.Invoke(ent);
        }
    }
}
