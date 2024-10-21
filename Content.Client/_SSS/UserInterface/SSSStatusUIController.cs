using Content.Client._SSS.UserInterface.Widgets;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._SSS.UserInterface;

public sealed class SSSStatusUIController : UIController, IOnSystemChanged<DamageableSystem>
{
    private ISawmill _log = default!;
    [UISystemDependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        // SubscribeLocalEvent<DamageChangedEvent>(DoSomething);

        _log = Logger.GetSawmill("StatusUI");

        _log.Info($"{nameof(SSSStatusUIController)} loaded.");
    }

    private SSSStatusGui? StatusUI => UIManager.GetActiveUIWidgetOrNull<SSSStatusGui>();

    public void UpdateHealth(Entity<DamageableComponent> ent)
    {
        var ui = StatusUI;

        if (ui == null)
            return;

        if (!EntityManager.TryGetComponent<MobThresholdsComponent>(ent, out var mobThresholds))
            return;

        if (!EntityManager.TryGetComponent<MobStateComponent>(ent, out var mobState))
            return;

        if (mobState.CurrentState == MobState.Dead)
        {
            ui.HealthNumber.Text = "DEAD";
            ui.HealthBar.Value = 0;
            return;
        }

        var maxHp = _mobThreshold.GetThresholdForState(ent, MobState.Critical, mobThresholds);
        var hp = Math.Max(Math.Ceiling((maxHp - ent.Comp.TotalDamage).Float()), 0);

        ui.HealthNumber.Text = $"\u2665 {hp}";
        ui.HealthBar.MaxValue = maxHp.Float();
        ui.HealthBar.Value = (float)hp;
    }

    public void OnSystemLoaded(DamageableSystem system)
    {
        system.OnPlayerDamageChanged += UpdateHealth;
    }

    public void OnSystemUnloaded(DamageableSystem system)
    {
        system.OnPlayerDamageChanged -= UpdateHealth;
    }
}
