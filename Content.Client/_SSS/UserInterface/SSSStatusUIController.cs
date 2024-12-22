using Content.Client._SSS.UserInterface.Widgets;
using Content.Client.Gameplay;
using Content.Client.GameTicking.Managers;
using Content.Shared._SSS.SuspicionGameRule;
using Content.Shared._SSS.SuspicionGameRule.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._SSS.UserInterface;

public sealed class SSSStatusUIController : UIController, IOnSystemChanged<SSSStatusUISystem>, IOnStateChanged<GameplayState>
{
    private ISawmill _log = default!;

    [Dependency] private readonly IPlayerManager _playerManager = default!;

    [UISystemDependency] private readonly MobThresholdSystem? _mobThreshold = default!;
    [UISystemDependency] private readonly ClientGameTicker? _clientGameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        _log = Logger.GetSawmill("StatusUI");

        SubscribeNetworkEvent<SuspicionRuleTimerUpdate>(UpdateTimerEnd);
        SubscribeNetworkEvent<SuspicionRulePreroundStarted>(PreroundStarted);
        SubscribeNetworkEvent<SuspicionRuleUpdateRole>(UpdateRoleDisplay);
        SubscribeNetworkEvent<SuspicionRulePlayerSpawn>(UpdatePlayerSpawn);
    }

    private TimeSpan _lastEndTime;
    private TimeSpan _lastDrawnSeconds;

    private (string, Color)? _queuedRole = null;
    private (string, float, float)? _queuedHealth = null;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // I LOVE THIS HACK I LOVE THIS HACK I LOVE THIS HACK I LOVE THIS HACK
        SetRoleFromQueued();
        SetHealthBarFromQueued();

        // TODO: limit this to only update when the timer is not the same
        if (_clientGameTicker is null)
            return;

        if (_lastEndTime == TimeSpan.MinValue)
            return;

        var drawTime = _lastEndTime.Subtract(_clientGameTicker.RoundDuration());
        if ((int)drawTime.TotalSeconds != (int)_lastDrawnSeconds.TotalSeconds)
        {
            UpdateTimer(drawTime);
            _lastDrawnSeconds = drawTime;
        }
    }

    private SSSStatusGui? StatusUI => UIManager.GetActiveUIWidgetOrNull<SSSStatusGui>();

    public void UpdateHealth(Entity<DamageableComponent> ent)
    {

        if (!EntityManager.TryGetComponent<MobStateComponent>(ent, out var mobState))
            return;

        if (mobState.CurrentState == MobState.Dead)
        {
            SetHealthBarUI(Loc.GetString("suspicion-status-ui-health-dead"), 0, 100);
            return;
        }

        if (!EntityManager.TryGetComponent<MobThresholdsComponent>(ent, out var mobThresholds))
            return;

        if (_mobThreshold is null)
            return;

        var maxHp = _mobThreshold.GetThresholdForState(ent, MobState.Critical, mobThresholds);
        var hp = Math.Max(Math.Ceiling((maxHp - ent.Comp.TotalDamage).Double()), 0);

        SetHealthBar((float)hp, maxHp.Float());
    }

    private void SetHealthBarUI(string text, float value, float maxValue)
    {
        var ui = StatusUI;

        if (ui == null)
        {
            _queuedHealth = (text, value, maxValue);
            return;
        }

        ui.SetHealthBar(text, value, maxValue);
    }

    private bool SetHealthBarFromQueued()
    {
        if (_queuedHealth is null)
            return false;

        var ui = StatusUI;
        if (ui is null)
            return false;

        var (text, value, maxValue) = _queuedHealth.Value;
        ui.SetHealthBar(text, value, maxValue);
        _queuedHealth = null;

        return true;
    }

    private void SetHealthBar(float hp, float maxHp)
    {
        SetHealthBarUI($"\u2665 {hp}", hp, maxHp);
    }

    private void UpdateTimer(TimeSpan ts)
    {
        var ui = StatusUI;

        if (ui == null)
            return;

        if (ts < TimeSpan.Zero)
        {
            ts = TimeSpan.Zero;
        }

        // nice job c#, TimeSpan.ToString doesn't support having no leading zeros
        ui.TimerText.Text = $"{ts.Minutes}:{ts.Seconds:00}";
    }

    public void UpdateTimerEnd(SuspicionRuleTimerUpdate ev, EntitySessionEventArgs args)
    {
        _lastEndTime = ev.EndTime;
    }

    public void PreroundStarted(SuspicionRulePreroundStarted ev, EntitySessionEventArgs args)
    {
        _lastEndTime = ev.PreroundEndTime;
        SetRoleToPreround();
    }

    private void SetRoleUI(string role, Color color)
    {
        var ui = StatusUI;

        if (ui is null)
        {
            _queuedRole = (role, color);
            return;
        }

        ui.SetRole(role, color);
    }

    private bool SetRoleFromQueued()
    {
        if (_queuedRole is null)
            return false;

        var ui = StatusUI;
        if (ui is null)
            return false;

        var (role, color) = _queuedRole.Value;
        ui.SetRole(role, color);
        _queuedRole = null;

        return true;
    }

    private void SetRoleToPreround()
    {
        SetRoleUI(Loc.GetString("suspicion-status-ui-role-preround"), Color.Gray);
    }

    private void SetRoleToObserbing()
    {
        SetRoleUI(Loc.GetString("suspicion-status-ui-role-obserbing"), Color.Gray);
    }

    public void UpdateRoleDisplay(SuspicionRuleUpdateRole ev, EntitySessionEventArgs args)
    {
        var roleName = Loc.GetString(ev.NewRole switch
        {
            SuspicionRole.Traitor => "roles-antag-suspicion-traitor-name",
            SuspicionRole.Detective => "roles-antag-suspicion-detective-name",
            SuspicionRole.Innocent => "roles-antag-suspicion-innocent-name",
            SuspicionRole.Wildcard => ev.NewSubRole switch
            {
                SuspicionSubRole.Jester => "roles-antag-suspicion-jester-name",
                _ => "roles-antag-suspicion-wildcard-unknown",
            },
            _ => "roles-antag-suspicion-unknown",
        });
        SetRoleUI(roleName, Color.FromName(ev.NewRole.GetRoleColor()));
    }

    public void UpdatePlayerSpawn(SuspicionRulePlayerSpawn ev, EntitySessionEventArgs args)
    {
        if (ev.GameState == SuspicionGameState.Preparing)
        {
            SetRoleToPreround();

            if (EntityManager.TryGetComponent<DamageableComponent>(_playerManager.LocalEntity!.Value, out var damagable))
                UpdateHealth((_playerManager.LocalEntity!.Value, damagable));
        }
        else
        {
            SetRoleToObserbing();
            SetHealthBar(0, 100);
        }

        _lastEndTime = ev.EndTime;
    }

    public void OnSystemLoaded(SSSStatusUISystem system)
    {
        system.OnPlayerDamageChanged += UpdateHealth;
    }

    public void OnSystemUnloaded(SSSStatusUISystem system)
    {
        system.OnPlayerDamageChanged -= UpdateHealth;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (EntityManager.TryGetComponent<DamageableComponent>(_playerManager.LocalEntity!.Value, out var damagable))
            UpdateHealth((_playerManager.LocalEntity!.Value, damagable));
        else
            SetHealthBar(0, 100);

        UpdateTimer(TimeSpan.Zero);

        SetRoleUI("-", Color.Black);
    }

    public void OnStateExited(GameplayState state)
    {
        SetHealthBarUI("-", 0, 100);

        UpdateTimer(TimeSpan.Zero);

        SetRoleUI("-", Color.Black);
    }
}
