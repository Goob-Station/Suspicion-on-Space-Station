using System.Linq;
using Content.Server._SSS.GridMarker;
using Content.Server.Communications;
using Content.Server.Ghost;
using Content.Shared._SSS;
using Content.Shared._SSS.SuspicionGameRule;
using Content.Shared._SSS.SuspicionGameRule.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Content.Shared.Popups;

namespace Content.Server._SSS.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    private void OnGhost(GhostSpawnedEvent ghost)
    {
        var query = EntityQueryEnumerator<SuspicionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleId, out var _, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleId, gameRule))
                continue;

            // Only apply the overlay to ghosts when the gamemode is active.

            EnsureComp<ShowSyndicateIconsComponent>(ghost.Ghost);
            EnsureComp<ShowCriminalRecordIconsComponent>(ghost.Ghost);
            break;
        }
    }

    private void OnMobStateChanged(EntityUid uid, SuspicionPlayerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
        {
            var damageSpec = new DamageSpecifier(_prototypeManager.Index<DamageGroupPrototype>("Genetic"), 90000);
            _damageableSystem.TryChangeDamage(args.Target, damageSpec);
            Log.Debug("Player is critical, applying genetic damage.");
            return;
        }

        if (args.NewMobState != MobState.Dead) // Someone died.
            return;

        DropAllItemsOnEntity(args.Target);

        var query = EntityQueryEnumerator<SuspicionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleId, out var sus, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleId, gameRule))
                continue;

            if (sus.GameState != SuspicionGameState.InProgress)
                break;

            sus.EndAt += TimeSpan.FromSeconds(_timeAddedPerKill);
            sus.AnnouncedTimeLeft.Clear();

            RaiseNetworkEvent(new SuspicionRuleTimerUpdate(_gameTicker.RoundDuration() + sus.EndAt));

            var allTraitors = FindAllOfType(SuspicionRole.Traitor);
            // Ok this is fucking horrible
            foreach (var traitor in allTraitors)
            {
                AddTcToPlayer(traitor.body, sus.AmountAddedPerKill);
            }

            var allInnocents = FindAllOfType(SuspicionRole.Innocent);
            var allDetectives = FindAllOfType(SuspicionRole.Detective);

            if (allInnocents.Count == 0 && allDetectives.Count == 0)
            {
                _chatManager.DispatchServerAnnouncement("The traitors have won the round.");
                sus.GameState = SuspicionGameState.PostRound;
                _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.PostRoundDuration));
                return;
            }

            if (allTraitors.Count == 0)
            {
                _chatManager.DispatchServerAnnouncement("The innocents have won the round.");
                sus.GameState = SuspicionGameState.PostRound;
                _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.PostRoundDuration));
                return;
            }
            break;
        }
    }

    private void OnShuttleCall(ref CommunicationConsoleCallShuttleAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<SuspicionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var sus, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            ev.Cancelled = true;
        }
    }

    private void OnExamine(EntityUid uid, SuspicionPlayerComponent component, ref ExaminedEvent args)
    {
        if (!TryComp<MobStateComponent>(args.Examined, out var mobState))
            return;

        if (!_mobState.IsDead(args.Examined, mobState))
            return; // Not a dead body... *yet*.

        var isInRange = args.IsInDetailsRange || component.Revealed;
        // Always show the role if it was already announced in chat.

        if (!isInRange)
        {
            args.PushText("Get closer to examine the body.", -10);
            return;
        }

        var mind = _mindSystem.GetMind(args.Examined);
        var examinerMind = _mindSystem.GetMind(args.Examiner);

        if (mind == null)
            return;

        if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(mind.Value, out var role))
            return;

        if (role.Value.Comp2.Role == SuspicionRole.Pending)
            return;

        if (examinerMind.HasValue) // I apologize for this being so nested. Can't use early returns with the other stuff below.
        {
            // If the examined is a traitor and the examinor is a detective, we give the detective any TC the traitor had.
            if (role.Value.Comp2.Role == SuspicionRole.Traitor)
            {
                if (_roleSystem.MindHasRole<SuspicionRoleComponent>(examinerMind.Value, out var examinerRole))
                {
                    if (examinerRole.Value.Comp2.Role == SuspicionRole.Detective)
                    {
                        var implantT = GetUplinkImplant(args.Examined);
                        var implantD = GetUplinkImplant(args.Examiner);
                        if (implantT.HasValue && implantD.HasValue)
                        {
                            var tc = implantT.Value.Comp.Balance.Values.Sum(x => x.Int());
                            AddTcToPlayer(args.Examiner, tc);
                            implantT.Value.Comp.Balance.Clear();

                            if (tc >= 0)
                            {
                                if (_playerManager.TryGetSessionByEntity(args.Examiner, out var session))
                                {
                                    var msgFound = Loc.GetString("suspicion-found-tc", ("tc", tc));
                                    _chatManager.ChatMessageToOne(
                                        ChatChannel.Server,
                                        msgFound,
                                        msgFound,
                                        EntityUid.Invalid,
                                        false,
                                        client: session.Channel,
                                        recordReplay:true
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }

        args.PushMarkup(Loc.GetString(
                "suspicion-examination",
                ("ent", args.Examined),
                ("col", role.Value.Comp2.Role.GetRoleColor()),
                ("role", role.Value.Comp2.Role.ToString())),
            -10);

        if (!HasComp<HandsComponent>(args.Examiner))
            return;

        if (HasComp<GhostComponent>(args.Examiner))
            return; // Check for admin ghosts

        // Reveal the role in chat
        if (component.Revealed)
            return;

        if (role.Value.Comp2.Role == SuspicionRole.Traitor)
        {
            var allDetectives = FindAllOfType(SuspicionRole.Detective);
            foreach (var det in allDetectives) // Once a Traitor is found, all detectives get 1 TC.
            {
                AddTcToPlayer(det.body, 1, false);
            }
        }

        component.Revealed = true;
        var trans = Comp<TransformComponent>(args.Examined);
        var loc = _transformSystem.GetMapCoordinates(trans);

        var msg = Loc.GetString("suspicion-examination-chat",
            ("finder", args.Examiner),
            ("found", args.Examined),
            ("where", _navMapSystem.GetNearestBeaconString(loc)),
            ("col", role.Value.Comp2.Role.GetRoleColor()),
            ("role", role.Value.Comp2.Role.ToString()));
        SendAnnouncement(
            msg
        );
    }

    private void UpdateSpaceWalkDamage(ref SuspicionRuleComponent sus, float frameTime)
    {
        var query = EntityQueryEnumerator<SuspicionPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SpacewalkThreshold <= 0)
                continue;

            var coordinates = _transformSystem.GetMapCoordinates(uid);

            var entities = _entityLookupSystem.GetEntitiesInRange<SuspicionGridMarkerComponent>(coordinates,
                comp.SpacewalkThreshold,
                LookupFlags.Sundries);

            if (entities.Count > 0)
                continue;

            if (comp.LastTookSpacewalkDamage + TimeSpan.FromSeconds(1) > DateTime.Now)
                continue;

            var damage = new DamageSpecifier(_prototypeManager.Index<DamageGroupPrototype>("Toxin"), 5);
            _damageableSystem.TryChangeDamage(uid, damage);
            comp.LastTookSpacewalkDamage = DateTime.Now;
            _popupSystem.PopupEntity("You feel an outside force pressing in on you. Maybe try going back inside?",
                uid,
                uid,
                PopupType.LargeCaution);
        }
    }

    private void UpdateTimer(ref SuspicionRuleComponent sus, float frameTime)
    {
        sus.EndAt -= TimeSpan.FromSeconds(frameTime);

        var timeLeft = sus.EndAt.TotalSeconds;
        switch (timeLeft)
        {
            case <= 241 when !sus.AnnouncedTimeLeft.Contains(241):
                _chatManager.DispatchServerAnnouncement($"The round will end in {Math.Round(sus.EndAt.TotalMinutes)}:{sus.EndAt.Seconds}.");
                sus.AnnouncedTimeLeft.Add(241);
                break;
            case <= 181 when !sus.AnnouncedTimeLeft.Contains(181):
                _chatManager.DispatchServerAnnouncement($"The round will end in {Math.Round(sus.EndAt.TotalMinutes)}:{sus.EndAt.Seconds}.");
                sus.AnnouncedTimeLeft.Add(181);
                break;
            case <= 121 when !sus.AnnouncedTimeLeft.Contains(121):
                _chatManager.DispatchServerAnnouncement($"The round will end in {Math.Round(sus.EndAt.TotalMinutes)}:{sus.EndAt.Seconds}.");
                sus.AnnouncedTimeLeft.Add(121);
                break;
            case <= 61 when !sus.AnnouncedTimeLeft.Contains(61):
                _chatManager.DispatchServerAnnouncement($"The round will end in {Math.Round(sus.EndAt.TotalMinutes)}:{sus.EndAt.Seconds}.");
                sus.AnnouncedTimeLeft.Add(61);
                break;
            case <= 30 when !sus.AnnouncedTimeLeft.Contains(30):
                _chatManager.DispatchServerAnnouncement($"The round will end in 30 seconds.");
                sus.AnnouncedTimeLeft.Add(30);
                break;
            case <= 10 when !sus.AnnouncedTimeLeft.Contains(10):
                _chatManager.DispatchServerAnnouncement($"The round will end in 10 seconds.");
                sus.AnnouncedTimeLeft.Add(10);
                break;
            case <= 5 when !sus.AnnouncedTimeLeft.Contains(5):
                _chatManager.DispatchServerAnnouncement($"The round will end in 5 seconds.");
                sus.AnnouncedTimeLeft.Add(5);
                break;
        }

        if (sus.EndAt > TimeSpan.Zero)
            return;

        sus.GameState = SuspicionGameState.PostRound;
        _roundEndSystem.EndRound(TimeSpan.FromSeconds(sus.PostRoundDuration));
    }
}
