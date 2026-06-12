using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Controller;
using AeroMessages.GSS.V66.Character.Event;
using AeroMessages.Matrix.V25;
using GameServer.Data;
using GameServer.Data.SDB.Records.customdata;
using GameServer.GRPC;
using GameServer.Test;
using GrpcGameServerAPIClient;
using Serilog;
using CharacterEntity = GameServer.Entities.Character.CharacterEntity;

namespace GameServer;

public class NetworkPlayer : NetworkClient, INetworkPlayer
{
    public NetworkPlayer(IPEndPoint endPoint, uint socketId, ILogger logger)
        : base(endPoint, socketId, logger)
    {
        CharacterEntity = null;
        Status = IPlayer.PlayerStatus.Connecting;
        ConnectedAt = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public ulong PlayerId { get; private set; }
    public ulong CharacterId { get; private set; }
    public CharacterEntity CharacterEntity { get; private set; }
    public IPlayer.PlayerStatus Status { get; private set; }
    public PlayerPreferences Preferences { get; private set; }
    public Zone CurrentZone { get; private set; }
    public uint CurrentOutpostId { get; private set; }
    public uint LastRequestedUpdate { get; set; }
    public uint RequestedClientTime { get; set; }
    public bool FirstUpdateRequested { get; set; }
    public ulong SteamUserId { get; set; }
    public CharacterInventory Inventory { get; set; }
    public uint ConnectedAt { get; }
    public bool CanReceiveGSS => (Status.Equals(IPlayer.PlayerStatus.Playing) || Status.Equals(IPlayer.PlayerStatus.Loading)) && NetClientStatus.Equals(ClientStatus.Connected);

    public void Init(IShard shard)
    {
        Init(this, shard, shard);
        Preferences = new PlayerPreferences();
    }

    public async void Login(ulong characterId)
    {
        PlayerId = 0x4658281c142e9f00ul;
        var guid = characterId & 0xffffffffffffff00;
        CharacterId = guid;

        // If this character is already zoned in (e.g. the previous client was
        // killed and never sent CloseConnection), evict the stale session and
        // let this login proceed.
        AssignedShard.Entities.TryGetValue(CharacterId, out var existing);
        if (existing != null)
        {
            Console.WriteLine($"Character {CharacterId:X16} is already zoned in, evicting the stale session");
            if (existing is CharacterEntity { Player: not null } staleCharacter && staleCharacter.Player != this)
            {
                AssignedShard.MigrateOut(staleCharacter.Player);
            }

            // MigrateOut is a no-op if the stale socket is already gone from Clients
            if (AssignedShard.Entities.ContainsKey(CharacterId))
            {
                AssignedShard.EntityMan.Remove(CharacterId);
            }
        }

        // Begin setting up player character
        CharacterEntity = new CharacterEntity(AssignedShard, guid);

        // Try to get remote character data
        CharacterAndBattleframeVisuals remoteData = null;
        try
        {
            remoteData = await GRPCService.GetCharacterAndBattleframeVisualsAsync((long)characterId);
        }
        catch
        {
            if (!AssignedShard.Settings.AllowHardcodedFallback)
            {
                Logger.Error("Refusing login for character 0x{0:X16}: RIN is unreachable over gRPC. Set AllowHardcodedFallback=true in App.config to allow the dev fallback character.", characterId);
                RefuseLogin();
                return;
            }

            Logger.Warning("Could not get character over gRPC, using the hardcoded DEV fallback (AllowHardcodedFallback=true)");
        }

        // Load inventory from the DB. An empty result means a new character (seed
        // below); a failed call means RIN broke mid-login and playing on would
        // diverge the player from their DB state.
        // Persistence must use the full character guid (low byte 0xFE type tag);
        // CharacterId has that byte stripped for entity registration and does
        // not exist in webapi.Characters.
        Inventory = new CharacterInventory(AssignedShard, this, CharacterEntity, characterId);

        bool loadedFromDb = false;
        bool inventoryFetchFailed = false;
        try
        {
            var invData = await GRPCService.GetCharacterInventoryAsync((long)characterId);
            loadedFromDb = Inventory.LoadFromDb(invData);
        }
        catch (Exception ex)
        {
            inventoryFetchFailed = true;
            Console.WriteLine($"Could not load inventory from DB: {ex.Message}");
        }

        if (inventoryFetchFailed && !AssignedShard.Settings.AllowHardcodedFallback)
        {
            Logger.Error("Refusing login for character 0x{0:X16}: inventory could not be loaded from RIN.", characterId);
            RefuseLogin();
            return;
        }

        if (!loadedFromDb)
        {
            // New characters are seeded by RIN at creation (webapi.seed_character_inventory),
            // so an empty inventory here means the character predates that, the
            // NewCharacterTemplate tables are empty, or seeding failed.
            if (!AssignedShard.Settings.AllowHardcodedFallback)
            {
                Logger.Error("Refusing login for character 0x{0:X16}: no inventory in the DB. Check the webapi.NewCharacterTemplate* tables are populated (sql/Data/NewCharacterTemplate.sql), or set AllowHardcodedFallback=true to seed legacy defaults.", characterId);
                RefuseLogin();
                return;
            }

            Logger.Warning("Character 0x{0:X16} has no inventory in the DB, seeding hardcoded DEV defaults (AllowHardcodedFallback=true)", characterId);
            Inventory.LoadHardcodedInventory();

            // Seed to DB for next login (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var seed = Inventory.BuildSeedData();
                    await GRPCService.SeedCharacterInventoryAsync(
                        characterId, seed.items, seed.resources, seed.loadouts, seed.slots);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to seed inventory to DB: {ex.Message}");
                }
            });
        }

        // Use remote data or fallback to setup character
        bool useRemoteData = true;
        int loadoutId;
        if (remoteData != null && useRemoteData)
        {
            CharacterEntity.LoadRemote(remoteData);
            loadoutId = Inventory.GetLoadoutIdForChassis(remoteData.CharacterInfo.CurrentBattleframeSDBId);
        }
        else
        {
            CharacterEntity.Load(HardcodedCharacterData.FallbackData);
            loadoutId = Inventory.GetLoadoutIdForChassis(76331);
        }

        var loadoutRefData = Inventory.GetLoadoutReferenceData(loadoutId);
        var loadout = new CharacterLoadout(loadoutRefData);
        CharacterEntity.ApplyLoadout(loadout);

        CharacterEntity.SetControllingPlayer(this);
        CharacterEntity.SetCharacterState(CharacterStateData.CharacterStatus.Spawning, AssignedShard.CurrentTime);
        Status = IPlayer.PlayerStatus.LoggedIn;

        // WelcomeToTheMatrix
        var wel = new WelcomeToTheMatrix { PlayerID = PlayerId, Unk1 = Array.Empty<byte>(), Unk2 = Array.Empty<byte>() };
        NetChannels[ChannelType.Matrix].SendMessage(wel);

        Zone zone;
        uint zoneId;
        uint outpostId;

        if (remoteData != null)
        {
            zoneId = AssignedShard.ZoneId;
            zone = DataUtils.GetZone(zoneId);
            outpostId = remoteData.CharacterInfo.LastZoneId == zoneId ? FindClosestAvailableOutpost(zone, remoteData.CharacterInfo.LastOutpostId) : 0;
        }
        else
        {
            zoneId = (uint)(characterId & 0x000000000000ffff);
            zone = DataUtils.GetZone(zoneId);
            outpostId = zone.DefaultOutpostId;
        }

        Logger.Verbose("Zone {0} Outpost {1}", zoneId, outpostId);

        EnterZone(zone, outpostId);
    }

    private void RefuseLogin()
    {
        CharacterEntity = null;
        var resp = new AeroMessages.Control.CloseConnection { Unk = new byte[] { 0, 0, 0, 0 } };
        NetChannels[ChannelType.Control].SendMessage(resp);
    }

    public void EnterZoneAck()
    {
        AssignedShard.EntityMan.Add(CharacterEntity.EntityId, CharacterEntity);
    }

    public void ExitZoneAck()
    {
        AssignedShard.EntityMan.Remove(CharacterEntity);
    }

    public void Respawn()
    {
        Console.WriteLine($"[Respawn] Starting respawn for {CharacterEntity?.EntityId}, Alive={CharacterEntity?.Alive}");

        if (CurrentZone == null)
        {
            Console.WriteLine("[Respawn] CurrentZone is null, cannot respawn");
            return;
        }

        try
        {
            RespawnInternal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Respawn] FAILED: {ex}");
            // Ensure the player can at least try again
            if (CharacterEntity != null)
            {
                CharacterEntity.Alive = false;
            }
        }
    }

    private void RespawnInternal()
    {
        // The client handles this message sequence fine at zone-in (state Living), but when
        // it has seen a death state it needs a full re-init of its own character afterwards,
        // mirroring zone-in — see ResyncOwnCharacter below.
        bool wasDead = CharacterEntity.CharacterState.State
            is CharacterStateData.CharacterStatus.Dead
            or CharacterStateData.CharacterStatus.Incapacitated;

        CharacterEntity.ForcedRespawnTime = 0;

        SpawnPoint spawnPoint;
        try
        {
            var outpostId = FindClosestAvailableOutpost(CurrentZone, CurrentOutpostId);
            spawnPoint = outpostId == 0
                ? new SpawnPoint { Position = CurrentZone.POIs["spawn"] }
                : AssignedShard.Outposts[CurrentZone.ID][outpostId].RandomSpawnPoint;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Respawn] Failed to find spawn point, falling back to current position: {ex.Message}");
            spawnPoint = new SpawnPoint { Position = CharacterEntity.Position };
        }

        // Clear stale effects from previous life
        CharacterEntity.ClearAllEffects(flush: false);

        CharacterEntity.PositionAtSpawnPoint(spawnPoint);
        CharacterEntity.SetSpawnTime(AssignedShard.CurrentTime);
        var forcedMove = new ForcedMovement
        {
            Data = new ForcedMovementData
            {
                Type = 1,
                Unk1 = 0,
                HaveUnk2 = 0,
                Params1 = new ForcedMovementType1Params { Position = spawnPoint.Position, Direction = CharacterEntity.AimDirection, Velocity = Vector3.Zero, Time = AssignedShard.CurrentTime + 1 }
            },
            ShortTime = AssignedShard.CurrentShortTime
        };
        NetChannels[ChannelType.ReliableGss].SendMessage(forcedMove, CharacterEntity.EntityId);

        var respawnMsg = new Respawned { ShortTime = AssignedShard.CurrentShortTime, Unk1 = 0, Unk2 = 0 };
        NetChannels[ChannelType.ReliableGss].SendMessage(respawnMsg, CharacterEntity.EntityId);

        var baseController = CharacterEntity.Character_BaseController;

        // Update 1
        CharacterEntity.SetSpawnTime(AssignedShard.CurrentTime);
        CharacterEntity.SetCharacterState(CharacterStateData.CharacterStatus.Respawning, AssignedShard.CurrentTime);
        CharacterEntity.SetSpawnPose();
        baseController.RespawnTimesProp = new RespawnTimesData();
        baseController.RespawnTimesProp = null; // Make the field dirty so we send clear because we probably should send clear. At some point investigaste if this is neccessary.
        baseController.TimedDailyRewardProp = new TimedDailyRewardData { State = TimedDailyRewardData.TimedDailyRewardState.ROLLED, MaxRolls = 1, CountdownToTime = AssignedShard.CurrentTime };
        NetChannels[ChannelType.ReliableGss].SendChanges(baseController, CharacterEntity.EntityId);

        // Update 2
        CharacterEntity.SetCharacterState(CharacterStateData.CharacterStatus.Living, AssignedShard.CurrentTime + 1);
        baseController.GibVisualsIdProp = new GibVisuals { Id = 0, Time = AssignedShard.CurrentTime + 1 };
        baseController.RespawnTimesProp = new RespawnTimesData(); // Shake it up
        baseController.RespawnTimesProp = null; // It's dirt
        baseController.CurrentHealthProp = HardcodedCharacterData.MaxHealth;
        baseController.MaxHealthProp = new MaxVital { Value = HardcodedCharacterData.MaxHealth, Time = AssignedShard.CurrentTime };
        baseController.CurrentShieldsProp = 0;
        baseController.ZoneUnlocksProp = 0xFFFFFFFFFFFFFFFFUL;
        baseController.RegionUnlocksProp = 0xFFFFFFFFFFFFFFFFUL;
        baseController.PersonalFactionStanceProp = new PersonalFactionStanceData
        {
            Unk1 = new PersonalFactionStanceBitfield { NumFactions = 50, Bitfield = new byte[] { 0x09, 0x0e, 0x5d, 0xff, 0x5f, 0x08, 0x00, 0x00 } },
            Unk2 = new PersonalFactionStanceBitfield { NumFactions = 50, Bitfield = new byte[] { 0xf2, 0x00, 0x20, 0x00, 0x00, 0xf2, 0x00, 0x00 } }
        };
        NetChannels[ChannelType.ReliableGss].SendChanges(baseController, CharacterEntity.EntityId);

        if (wasDead)
        {
            Console.WriteLine("[Respawn] Respawning from Dead state, resyncing own character keyframes");
            ResyncOwnCharacter();
        }

        // Re-apply jetpack fx (hack until we hook up item effects)
        CharacterEntity.AddEffect(AssignedShard.Abilities.Factory.LoadEffect(986), new Aptitude.Context(AssignedShard, CharacterEntity)
        {
            InitTime = AssignedShard.CurrentTime,
        });
        CharacterEntity.AddEffect(AssignedShard.Abilities.Factory.LoadEffect(472), new Aptitude.Context(AssignedShard, CharacterEntity)
        {
            InitTime = AssignedShard.CurrentTime,
        });

        // Clear respawn_input permission, restore normal combat state
        CharacterEntity.SetPermissionFlag(PermissionFlagsData.CharacterPermissionFlags.respawn_input, false);

        // Reset combat timer on the real controller
        CharacterEntity.Character_CombatController.CombatTimer_0Prop = AssignedShard.CurrentTime;

        // Send full CombatController keyframe to ensure client has complete, consistent state
        NetChannels[ChannelType.ReliableGss].SendControllerKeyframe(
            CharacterEntity.Character_CombatController, CharacterEntity.EntityId, PlayerId);

        // InventoryUpdate
        Inventory.SendFullInventory();
        Inventory.EnablePartialUpdates = true;

        AssignedShard.Physics.UpdateEntity(CharacterEntity);
        CharacterEntity.Alive = true; // Accept MovementInputs only after Respawn
    }

    /// <summary>
    ///     Re-sends the own character's controller and view keyframes plus CharacterLoaded,
    ///     the same sequence EntityManager.ScopeIn sends at zone-in. A client that has been
    ///     in the Dead state ignores the incremental respawn updates and stays on the death
    ///     screen; a full re-init is the sequence it is known to accept.
    /// </summary>
    private void ResyncOwnCharacter()
    {
        var character = CharacterEntity;
        var entityId = character.EntityId;
        var reliable = NetChannels[ChannelType.ReliableGss];

        if (character.Character_BaseController != null
            && character.Character_CombatController != null
            && character.Character_MissionAndMarkerController != null
            && character.Character_LocalEffectsController != null)
        {
            reliable.SendControllerKeyframe(character.Character_BaseController, entityId, PlayerId);
            reliable.SendControllerKeyframe(character.Character_CombatController, entityId, PlayerId);
            reliable.SendControllerKeyframe(character.Character_LocalEffectsController, entityId, PlayerId);
            reliable.SendControllerKeyframe(character.Character_MissionAndMarkerController, entityId, PlayerId);
            reliable.SendMessage(new CharacterLoaded(), entityId);
        }

        if (character.Character_ObserverView != null
            && character.Character_EquipmentView != null
            && character.Character_CombatView != null
            && character.Character_MovementView != null)
        {
            reliable.SendViewKeyframe(character.Character_ObserverView, entityId);
            reliable.SendViewKeyframe(character.Character_EquipmentView, entityId);
            reliable.SendViewKeyframe(character.Character_CombatView, entityId);
            reliable.SendViewKeyframe(character.Character_MovementView, entityId);
        }

        if (character.Character_TinyObjectView != null)
        {
            reliable.SendViewKeyframe(character.Character_TinyObjectView, entityId);
        }
    }

    public void Ready()
    {
        Status = IPlayer.PlayerStatus.Playing;
    }

    public void Jump()
    {
        CharacterEntity.TimeSinceLastJump = -1;
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        switch (Status)
        {
            // TODO: Implement FSM here to move player thru log in process to connecting to a shard to playing
            case IPlayer.PlayerStatus.Connected:
                Status = IPlayer.PlayerStatus.LoggingIn;
                break;
            case IPlayer.PlayerStatus.Loading:
                break;
            case IPlayer.PlayerStatus.Playing:
                {
                    break;
                }
        }
    }

    public uint FindClosestAvailableOutpost(Zone zone, uint targetOutpostId = 0)
    {
        bool haveOutposts = AssignedShard.Outposts.TryGetValue(zone.ID, out var outposts);
        if (!haveOutposts)
        {
            return 0;
        }
        else if (targetOutpostId == 0)
        {
            return zone.DefaultOutpostId;
        }

        var targetOutpost = outposts[targetOutpostId];

        if (!targetOutpost.IsCapturedByHostiles)
        {
            return targetOutpostId;
        }

        Vector3 sourcePosition = targetOutpost.Outpost_ObserverView.PositionProp;

        var minDistance = Vector3.DistanceSquared(sourcePosition, zone.POIs["spawn"]);
        var closestOutpostId = zone.DefaultOutpostId;

        foreach (var outpost in outposts)
        {
            if (outpost.Value.IsCapturedByHostiles)
            {
                continue;
            }

            var distance = Vector3.DistanceSquared(sourcePosition, outpost.Value.Outpost_ObserverView.PositionProp);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestOutpostId = outpost.Key;
            }
        }

        return closestOutpostId;
    }

    public void HandleFireWeaponProjectile(uint time, Vector3 aim)
    {
        AssignedShard.WeaponSim.OnFireWeaponProjectile(CharacterEntity, time, aim);
    }

    public void EnterZone(Zone z, uint outpostId = 0)
    {
        var spawnPoint = outpostId == 0
                             ? new SpawnPoint { Position = z.POIs["spawn"] }
                             : AssignedShard.Outposts[z.ID][outpostId].RandomSpawnPoint;

        // Ensure character entity is placed at the respawn point
        CharacterEntity.PositionAtSpawnPoint(spawnPoint);
        CharacterEntity.SetSpawnPose();

        CurrentZone = z;
        CurrentOutpostId = outpostId;

        var msg = new EnterZone
        {
            InstanceId = AssignedShard.InstanceId,
            ZoneId = CurrentZone.ID,
            ZoneTimestamp = (long)CurrentZone.Timestamp,
            ZoneFlags = 0,
            ZoneOwner = "r5_exec",
            StreamingProtocol = 0x4c5f,
            SvnRevision = 0x0c9f5,
            HotfixLevel = 0,
            MatchId = 0,
            Unk2 = 0,
            SimulationSeedMs = 0x63e2db5e,
            ZoneName = CurrentZone.Name,
            HaveDevZoneInfo = 0,
            ZoneTimeSyncInfo = new ZoneTimeSyncData { FictionDateTimeOffsetMicros = 0, DayLengthFactor = 12.0F, DayPhaseOffset = 0.896445870399F },
            GameClockInfo = new GameClockInfoData
            {
                MicroUnix_1 = 1478970208392232,
                MicroUnix_2 = 1478774752697322,
                Timescale = 1.0,
                Unk3 = 0,
                Unk4 = 0,
                Paused = 0
            },
            SpectatorModeFlag = 0
        };

        NetChannels[ChannelType.Matrix].SendMessage(msg);

        Status = IPlayer.PlayerStatus.Loading;
    }
}