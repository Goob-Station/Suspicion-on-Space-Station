﻿using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Administration.Systems;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.Communications;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost;
using Content.Server.Implants;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Shared._SSS;
using Content.Shared._SSS.SuspicionGameRule;
using Content.Shared._SSS.SuspicionGameRule.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.KillTracking;

namespace Content.Server._SSS.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem : GameRuleSystem<SuspicionRuleComponent>
{
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelectionSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly SubdermalImplantSystem _subdermalImplant = default!;
    [Dependency] private readonly AccessSystem _accessSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StoreSystem _storeSystem = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly NavMapSystem _navMapSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly SoundSpecifier _traitorStartSound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");


    public override void Initialize()
    {
        base.Initialize();

        InitializeCVars();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<SuspicionRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<SuspicionPlayerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<SuspicionPlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CommunicationConsoleCallShuttleAttemptEvent>(OnShuttleCall);
        SubscribeLocalEvent<GhostSpawnedEvent>(OnGhost); // Map init just doesn't work??
        SubscribeLocalEvent<ApcComponent, MapInitEvent>(OnApcInit);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
    }


    protected override void AppendRoundEndText(EntityUid uid,
        SuspicionRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var traitors = FindAllOfType(SuspicionRole.Traitor, false);
        var innocents = FindAllOfType(SuspicionRole.Innocent, false);
        var detectives = FindAllOfType(SuspicionRole.Detective, false);
        var wildcards = FindAllOfType(SuspicionRole.Wildcard, false);
        var jesters = FindAllOfType(SuspicionSubRole.Jester, false);

        var traitorsOnlyAlive = FindAllOfType(SuspicionRole.Traitor);
        var innocentsOnlyAlive = FindAllOfType(SuspicionRole.Innocent);
        var detectivesOnlyAlive = FindAllOfType(SuspicionRole.Detective);
        var wildcardsOnlyAlive = FindAllOfType(SuspicionRole.Wildcard);
        var jestersOnlyAlive = FindAllOfType(SuspicionSubRole.Jester);

        void Append(List<EntityUid> people, ref RoundEndTextAppendEvent args)
        {
            foreach (var person in people)
            {
                var name = MetaData(person).EntityName;
                var isDead = _mobState.IsDead(person);
                if (isDead)
                    args.AddLine($"[bullet/] {name} (Dead)");
                else
                    args.AddLine($"[bullet/] {name}");
            }
        }

        string victory;

        switch (true)
        {
            case var _ when jestersOnlyAlive.Count != jesters.Count:
                victory = "Jesters";
                break;

            case var _ when innocentsOnlyAlive.Count + detectivesOnlyAlive.Count == 0:
                victory = "Traitors";
                break;

            default:
                victory = "Innocents";
                break;
        }

        args.AddLine($"[bold]{victory}[/bold] have won the round.");

        args.AddLine($"[color=red][bold]Traitors[/bold][/color]: {traitors.Count}");
        Append(traitors.Select(t => t.body).ToList(), ref args);
        args.AddLine($"[color=green][bold]Innocents[/bold][/color]: {innocents.Count}");
        Append(innocents.Select(t => t.body).ToList(), ref args);
        args.AddLine($"[color=blue][bold]Detectives[/bold][/color]: {detectives.Count}");
        Append(detectives.Select(t => t.body).ToList(), ref args);
        args.AddLine($"[color=pink][bold]Wildcards[/bold][/color]: {wildcards.Count}");
        Append(wildcards.Select(t => t.body).ToList(), ref args);
    }


    protected override void Started(EntityUid uid, SuspicionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.GameState = SuspicionGameState.Preparing;

        Timer.Spawn(TimeSpan.FromSeconds(_preparingDuration - 5), () => _chatManager.DispatchServerAnnouncement("The round will start in 5 seconds."));
        Timer.Spawn(TimeSpan.FromSeconds(_preparingDuration), () => StartRound(uid, component, gameRule));
        Log.Debug("Starting a game of Suspicion.");

        RaiseNetworkEvent(new SuspicionRulePreroundStarted(TimeSpan.FromSeconds(_preparingDuration)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SuspicionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var sus, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (sus.GameState != SuspicionGameState.InProgress)
                continue;

            UpdateTimer(ref sus, frameTime);
            UpdateSpaceWalkDamage(ref sus, frameTime);
        }
    }
}
