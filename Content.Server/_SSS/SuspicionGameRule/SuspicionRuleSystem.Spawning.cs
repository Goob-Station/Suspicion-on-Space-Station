using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Atmos.Components;
using Content.Server.GameTicking;
using Content.Server.KillTracking;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Server.Roles;
using Content.Server.Temperature.Components;
using Content.Server.Traits.Assorted;
using Content.Shared._SSS;
using Content.Shared._SSS.SuspicionGameRule;
using Content.Shared._SSS.SuspicionGameRule.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.NukeOps;
using Content.Shared.Nutrition.Components;
using Content.Shared.Overlays;
using Content.Shared.Players;
using Content.Shared.Security.Components;
using Robust.Shared.Prototypes;
using Content.Shared.CombatMode.Pacification;
using Robust.Shared.Audio;

namespace Content.Server._SSS.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    [ValidatePrototypeId<EntityPrototype>]
    private const string MarkerPrototype = "SSSGridMarker";

    private void OnGetBriefing(Entity<SuspicionRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Briefing = role.Comp.Role switch
        {
            SuspicionRole.Traitor => Loc.GetString("roles-antag-suspicion-traitor-objective"),
            SuspicionRole.Detective => Loc.GetString("roles-antag-suspicion-detective-objective"),
            SuspicionRole.Innocent => Loc.GetString("roles-antag-suspicion-innocent-objective"),
            SuspicionRole.Wildcard => role.Comp.SubRole switch
            {
                SuspicionSubRole.Jester => Loc.GetString("roles-antag-suspicion-jester-objective"),
                _ => "roles-antag-suspicion-pending-objective",
            },
            _ => Loc.GetString("roles-antag-suspicion-pending-objective")
        };
    }

    private void StartRound(EntityUid uid, SuspicionRuleComponent component, GameRuleComponent gameRule)
    {
        component.GameState = SuspicionGameState.InProgress;
        component.EndAt = TimeSpan.FromSeconds(_roundDuration);

        var allPlayerData = _playerManager.GetAllPlayerData().ToList();
        var participatingPlayers = new List<(EntityUid mind, SuspicionRoleComponent comp)>();
        foreach (var sessionData in allPlayerData)
        {
            var contentData = sessionData.ContentData();
            if (contentData == null)
                continue;

            if (!contentData.Mind.HasValue)
                continue;

            if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(contentData.Mind.Value, out var role))
                continue; // Player is not participating in the game.

            participatingPlayers.Add((contentData.Mind.Value, role));
        }

        if (participatingPlayers.Count == 0)
        {
            _chatManager.DispatchServerAnnouncement("The round has started but there are no players participating. Restarting", Color.Red);
            _roundEndSystem.EndRound(TimeSpan.FromSeconds(5));
            return;
        }

        foreach (var participatingPlayer in participatingPlayers)
        {
            var ent = Comp<MindComponent>(participatingPlayer.mind).OwnedEntity;
            if (ent.HasValue)
                _rejuvenate.PerformRejuvenate(ent.Value);
        }

        var traitorCount = MathHelper.Clamp((int)(participatingPlayers.Count * _traitorPercentage), 1, allPlayerData.Count);
        var detectiveCount = MathHelper.Clamp((int)(participatingPlayers.Count * _detectivePercentage), 1, allPlayerData.Count);
        var wildcardCount = 0; // Zero by default, we roll to see if wildcards will be in the next round in the next line.
        if (RobustRandom.NextFloat() <= _wildcardChance)
            wildcardCount = MathHelper.Clamp((int)(participatingPlayers.Count * _wildcardPercentage), 1, allPlayerData.Count);

        if (traitorCount + detectiveCount + wildcardCount > participatingPlayers.Count)
        {
            // we somehow have more picked players than valid

            // what the fuck

            traitorCount = participatingPlayers.Count;
            detectiveCount = 0;
            wildcardCount = 0;
        }

        /* Simyon, I think the issue is you didnt make it random enough, so I made it more random for you!
        RobustRandom.Shuffle(participatingPlayers); // Shuffle the list so we can just take the first N players
        RobustRandom.Shuffle(participatingPlayers);
        RobustRandom.Shuffle(participatingPlayers); // I don't trust the shuffle.
        RobustRandom.Shuffle(participatingPlayers);
        RobustRandom.Shuffle(participatingPlayers); // I really don't trust the shuffle.
        */

        var seed1 = (int)(Math.Sin(RobustRandom.Next()) * 10000);
        var seed2 = (int)(Math.Cos(RobustRandom.Next()) * 10000);
        var combinedSeed = seed1 ^ seed2;

        RobustRandom.SetSeed(combinedSeed);

        var depth = 5;
        while (depth > 0)
        {
            var chaoticSeed = (int)Math.Pow(RobustRandom.Next(), 3) ^ (RobustRandom.Next() % 10000);
            RobustRandom.SetSeed(chaoticSeed);

            var halfCount = participatingPlayers.Count / 2;
            var shuffledSubset = participatingPlayers.Take(halfCount).ToList();
            RobustRandom.Shuffle(shuffledSubset);
            participatingPlayers.RemoveRange(0, halfCount);
            participatingPlayers.AddRange(shuffledSubset);

            depth--;
        }

        RobustRandom.SetSeed(RobustRandom.Next());
        RobustRandom.Shuffle(participatingPlayers);

        RobustRandom.SetSeed(RobustRandom.Next());
        RobustRandom.Shuffle(participatingPlayers);

        var shuffledIndices = participatingPlayers
            .Select((_, i) => (Index: i, Value: RobustRandom.Next()))
            .OrderBy(tuple => tuple.Value)
            .Select(tuple => tuple.Index)
            .ToList();

        var reorderedPlayers = shuffledIndices.Select(i => participatingPlayers[i]).ToList();
        participatingPlayers.Clear();
        participatingPlayers.AddRange(reorderedPlayers);

        for (var i = 0; i < 3; i++)
        {
            RobustRandom.SetSeed(RobustRandom.Next());
            RobustRandom.Shuffle(participatingPlayers);
        }

        // I hope thats random enough!

        for (var i = 0; i < traitorCount; i++)
        {
            var role = participatingPlayers[RobustRandom.Next(participatingPlayers.Count)];
            role.comp.Role = SuspicionRole.Traitor;
            var ownedEntity = Comp<MindComponent>(role.mind).OwnedEntity;
            if (!ownedEntity.HasValue)
            {
                Log.Error("Player mind has no entity.");
                continue;
            }

            // Hijacking the nuke op systems to show fellow traitors. Don't have to reinvent the wheel.
            EnsureComp<NukeOperativeComponent>(ownedEntity.Value);
            EnsureComp<ShowSyndicateIconsComponent>(ownedEntity.Value);

            AddKeyToRadio(ownedEntity.Value, component.TraitorRadio);

            _npcFactionSystem.AddFaction(ownedEntity.Value, component.TraitorFaction);

            _subdermalImplant.AddImplants(ownedEntity.Value, new List<string> { component.UplinkImplant }); // Why does this method only take in a list???

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                Loc.GetString("traitor-briefing"),
                Color.Red,
                _traitorStartSound);

            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Traitor), ownedEntity.Value);
            participatingPlayers.Remove(role);
        }

        for (var i = 0; i < detectiveCount; i++)
        {
            var role = participatingPlayers[RobustRandom.Next(participatingPlayers.Count)];
            role.comp.Role = SuspicionRole.Detective;
            var ownedEntity = Comp<MindComponent>(role.mind).OwnedEntity;
            if (!ownedEntity.HasValue)
            {
                Log.Error("Player mind has no entity.");
                continue;
            }

            EnsureComp<CriminalRecordComponent>(ownedEntity.Value).StatusIcon = "SecurityIconDischarged";

            AddKeyToRadio(ownedEntity.Value, component.DetectiveRadio);

            _subdermalImplant.AddImplants(ownedEntity.Value, new List<string> { component.DetectiveImplant });

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                Loc.GetString("detective-briefing"),
                Color.LightBlue,
                briefingSound: null);
            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Detective), ownedEntity.Value);
            participatingPlayers.Remove(role);
        }

        var wildcardRoles = new List<SuspicionSubRole>
        {
            SuspicionSubRole.Jester
        };

        for (var i = 0; i < wildcardCount; i++)
        {
            var role = participatingPlayers[RobustRandom.Next(participatingPlayers.Count)];

            var selectedSubRole = wildcardRoles[RobustRandom.Next(wildcardRoles.Count)];

            role.comp.Role = SuspicionRole.Wildcard;
            role.comp.SubRole = selectedSubRole;

            string briefingText = selectedSubRole switch
            {
                SuspicionSubRole.Jester => Loc.GetString("jester-briefing"),
                _ => Loc.GetString("wildcard-briefing")
            };

            SoundPathSpecifier? briefingSound = selectedSubRole switch
            {
                SuspicionSubRole.Jester => new SoundPathSpecifier("/Audio/Voice/Cluwne/cluwnelaugh1.ogg"),
                _ => null
            };

            var ownedEntity = Comp<MindComponent>(role.mind).OwnedEntity;
            if (!ownedEntity.HasValue)
            {
                Log.Error("Player mind has no entity.");
                continue;
            }

            switch (selectedSubRole)
            {
                case SuspicionSubRole.Jester:
                    {
                        EnsureComp<PacifiedComponent>(ownedEntity.Value);
                        break;
                    }
            }

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                briefingText,
                Color.LightPink,
                briefingSound: briefingSound);

            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Wildcard, selectedSubRole), ownedEntity.Value);

            participatingPlayers.Remove(role);
        }

        // Anyone who isn't a traitor will get the innocent role.
        foreach (var (mind, role) in participatingPlayers)
        {
            if (role.Role != SuspicionRole.Pending)
                continue;

            role.Role = SuspicionRole.Innocent;
            var ownedEntity = Comp<MindComponent>(mind).OwnedEntity;
            if (!ownedEntity.HasValue)
                continue;

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                Loc.GetString("innocent-briefing"),
                briefingColor: Color.Green,
                briefingSound: null);

            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Innocent), ownedEntity.Value);
        }

        _chatManager.DispatchServerAnnouncement($"The round has started. There are {traitorCount} traitors among us.");

        // SIMYON WHY
        RaiseNetworkEvent(new SuspicionRuleTimerUpdate(_gameTicker.RoundDuration() + component.EndAt));
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var allAccess = _prototypeManager
            .EnumeratePrototypes<AccessLevelPrototype>()
            .Select(p => new ProtoId<AccessLevelPrototype>(p.ID))
            .ToArray();


        var query = EntityQueryEnumerator<SuspicionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var sus, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (sus.GameState != SuspicionGameState.Preparing)
            {
                Log.Debug("Player tried to join a game of Suspicion but the game is not in the preparing state.");
                _chatManager.DispatchServerMessage(ev.Player, "Sorry, the game has already started. You have been made an observer.");
                GameTicker.SpawnObserver(ev.Player); // Players can't join mid-round.
                ev.Handled = true;
                if (sus.GameState == SuspicionGameState.InProgress)
                {
                    RaiseNetworkEvent(new SuspicionRulePlayerSpawn()
                    {
                        GameState = SuspicionGameState.InProgress,
                        EndTime = _gameTicker.RoundDuration() + sus.EndAt,
                    }, ev.Player);
                }
                else if (sus.GameState == SuspicionGameState.PostRound)
                {
                    RaiseNetworkEvent(new SuspicionRulePlayerSpawn()
                    {
                        GameState = SuspicionGameState.PostRound,
                        EndTime = TimeSpan.MinValue,
                    }, ev.Player);
                }
                return;
            }

            RaiseNetworkEvent(new SuspicionRulePlayerSpawn()
            {
                GameState = SuspicionGameState.Preparing,
                EndTime = TimeSpan.FromSeconds(_preparingDuration),
            }, ev.Player);

            var newMind = _mindSystem.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mindSystem.SetUserId(newMind, ev.Player.UserId);

            var mobMaybe = _stationSpawningSystem.SpawnPlayerCharacterOnStation(ev.Station, null, ev.Profile);
            var mob = mobMaybe!.Value;

            _mindSystem.TransferTo(newMind, mob);
            SetOutfitCommand.SetOutfit(mob, sus.Gear, EntityManager);
            _roleSystem.MindAddRole(newMind, "MindRoleSuspicion");

            // Rounds only last like 5 minutes, so players shouldn't need to eat or drink.
            RemComp<ThirstComponent>(mob);
            RemComp<HungerComponent>(mob);

            EnsureComp<ShowCriminalRecordIconsComponent>(mob); // Hijacking criminal records for the blue "D" symbol.

            // Because of the limited tools available to crew, we need to make sure that spacings are not lethal.
            EnsureComp<BarotraumaComponent>(mob).MaxDamage = 90;
            EnsureComp<TemperatureComponent>(mob).ColdDamageThreshold = float.MinValue;

            EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
            _accessSystem.TrySetTags(mob, allAccess, EnsureComp<AccessComponent>(mob));

            EnsureComp<SuspicionPlayerComponent>(mob);

            RemComp<PerishableComponent>(mob);
            RemComp<RottingComponent>(mob); // No rotting bodies in this mode.

            EnsureComp<KillTrackerComponent>(mob);
            EnsureComp<BodyComponent>(mob).CanGib = false; // Examination is important.

            if (_idCardSystem.TryFindIdCard(mob, out var idCard))
            {
                idCard.Comp.FullName = MetaData(mob).EntityName;
                idCard.Comp.LocalizedJobTitle = Loc.GetString("job-name-psychologist");
            }

            ev.Handled = true;
            break;
        }
    }

    private void OnApcInit(EntityUid uid, ApcComponent component, MapInitEvent args)
    {
        var query = EntityQueryEnumerator<SuspicionRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var susUid, out var sus, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(susUid, gameRule))
                continue;

            if (sus.GameState != SuspicionGameState.Preparing)
                continue;

            var cords = _transformSystem.GetMapCoordinates(uid);
            Spawn(MarkerPrototype, cords);
            break;
        }
    }
}
