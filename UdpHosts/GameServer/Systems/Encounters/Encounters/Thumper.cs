using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServer.Data;
using GameServer.Data.SDB;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Thumper;
using GameServer.Enums;

namespace GameServer.Systems.Encounters.Encounters;

public class Thumper : BaseEncounter, IInteractionHandler
{
    private static readonly uint _updateFrequency = ThumperState.THUMPING.CountdownTime() / 100;
    private ulong _lastUpdate;

    private ThumperEntity thumper;

    // Wave spawning
    private const uint WaveIntervalMs = 30_000;      // Spawn a new wave every 30 seconds
    private const int BaseWaveSize = 3;               // Starting wave size
    private const int MaxWaveSize = 8;                // Maximum NPCs per wave
    private const float SpawnRadius = 25.0f;          // Distance from thumper to spawn NPCs
    private const int MaxAliveNpcs = 12;              // Cap on simultaneously alive encounter NPCs

    private ulong _lastWaveTime;
    private int _waveNumber;
    private readonly List<CharacterEntity> _spawnedNpcs = new();
    private static uint[] _hostileMonsterTypes;

    public Thumper(IShard shard, ulong entityId, HashSet<INetworkPlayer> participants, ThumperEntity thumperEntity)
        : base(shard, entityId, participants)
    {
        thumper = thumperEntity;

        EnsureHostileMonsterTypes();

        Shard.EncounterMan.StartUpdatingEncounter(this);
    }

    private static void EnsureHostileMonsterTypes()
    {
        if (_hostileMonsterTypes != null)
        {
            return;
        }

        var allMonsters = SDBInterface.GetAllMonsters();
        if (allMonsters == null || allMonsters.Count == 0)
        {
            _hostileMonsterTypes = new uint[] { 356 }; // Fallback to Aero
            return;
        }

        var hostileIds = HardcodedCharacterData.HostileFactionIds;
        var hostiles = allMonsters
            .Where(m => hostileIds.Contains(m.Value.FactionId) && m.Value.ChassisId != 0)
            .Select(m => m.Key)
            .ToArray();

        _hostileMonsterTypes = hostiles.Length > 0 ? hostiles : new uint[] { 356 };
        Console.WriteLine($"[Thumper] Loaded {_hostileMonsterTypes.Length} hostile monster types for wave spawning");
    }

    public void OnInteraction(BaseEntity actingEntity, BaseEntity target)
    {
        switch ((ThumperState)thumper.StateInfo.State)
        {
            case ThumperState.THUMPING:
                Shard.Abilities.HandleActivateAbility(Shard, thumper, thumper.CompletedAbility);

                thumper.TransitionToState(ThumperState.LEAVING);
                break;
            case ThumperState.COMPLETED:
                thumper.StateInfo = thumper.StateInfo with { CountdownTime = Shard.CurrentTime };
                break;
        }
    }

    public override void OnUpdate(ulong currentTime)
    {
        if (Shard.CurrentTime >= thumper.StateInfo.CountdownTime)
        {
            switch ((ThumperState)thumper.StateInfo.State)
            {
                case ThumperState.LANDING:
                    Shard.Abilities.HandleActivateAbility(Shard, thumper, thumper.LandedAbility);
                    break;
                case ThumperState.WARMINGUP:
                    Shard.Abilities.HandleActivateAbility(Shard, thumper, 34579);
                    break;
                case ThumperState.THUMPING:
                    thumper.SetProgress(1);
                    CleanupNpcs();
                    Shard.Abilities.HandleActivateAbility(Shard, thumper, 34215);
                    break;
                case ThumperState.CLOSING:
                    break;
                case ThumperState.COMPLETED:
                    Shard.Abilities.HandleActivateAbility(Shard, thumper, 34216);
                    break;
                case ThumperState.LEAVING:
                    OnSuccess();
                    break;
            }

            if (thumper.StateInfo.State < (byte)ThumperState.LEAVING)
            {
                thumper.TransitionToState((ThumperState)(thumper.StateInfo.State + 1));
            }
        }
        else if (thumper.StateInfo.State == (byte)ThumperState.THUMPING)
        {
            // Progress bar update
            if (currentTime > _lastUpdate + _updateFrequency)
            {
                thumper.SetProgress((float)(Shard.CurrentTime - thumper.StateInfo.Time)
                                    / (thumper.StateInfo.CountdownTime - thumper.StateInfo.Time));
                _lastUpdate = currentTime;
            }

            // Wave spawning
            if (currentTime >= _lastWaveTime + WaveIntervalMs)
            {
                SpawnWave();
                _lastWaveTime = currentTime;
            }
        }
    }

    private void SpawnWave()
    {
        // Prune dead NPCs from tracking list
        _spawnedNpcs.RemoveAll(npc => !npc.Alive);

        // Don't exceed max alive NPCs
        int aliveCount = _spawnedNpcs.Count;
        if (aliveCount >= MaxAliveNpcs)
        {
            return;
        }

        _waveNumber++;
        int waveSize = Math.Min(BaseWaveSize + _waveNumber, MaxWaveSize);
        int toSpawn = Math.Min(waveSize, MaxAliveNpcs - aliveCount);

        Console.WriteLine($"[Thumper] Spawning wave {_waveNumber}: {toSpawn} NPCs around thumper at {thumper.Position}");

        for (int i = 0; i < toSpawn; i++)
        {
            var typeId = _hostileMonsterTypes[Rng.Next(_hostileMonsterTypes.Length)];
            var spawnPos = GetSpawnPosition(i, toSpawn);

            try
            {
                var npc = Shard.EntityMan.SpawnCharacter(typeId, spawnPos);

                // Prevent auto-respawn: clear MonsterTypeId so AIEngine.OnNpcDeath won't queue a respawn
                npc.MonsterTypeId = 0;

                // Tighten leash to keep NPCs near the thumper
                npc.SpawnPosition = thumper.Position;
                npc.LeashRadius = SpawnRadius * 2.5f;
                npc.AggroRadius = SpawnRadius * 2.0f;

                _spawnedNpcs.Add(npc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Thumper] Failed to spawn NPC type {typeId}: {ex.Message}");
            }
        }
    }

    private Vector3 GetSpawnPosition(int index, int total)
    {
        // Distribute NPCs in a ring around the thumper
        float angle = (MathF.PI * 2.0f / total * index) + (float)(Rng.NextDouble() * 0.5);
        float distance = SpawnRadius + (float)(Rng.NextDouble() * 5.0);
        return new Vector3(
            thumper.Position.X + MathF.Cos(angle) * distance,
            thumper.Position.Y + MathF.Sin(angle) * distance,
            thumper.Position.Z
        );
    }

    private void CleanupNpcs()
    {
        foreach (var npc in _spawnedNpcs)
        {
            if (npc.Alive)
            {
                Shard.AI.UnregisterNpc(npc.EntityId);
                Shard.EntityMan.Remove(npc);
            }
        }

        _spawnedNpcs.Clear();
    }

    public override void OnSuccess()
    {
        CleanupNpcs();

        Shard.EncounterMan.StopUpdatingEncounter(this);

        Shard.EntityMan.Remove(thumper);

        base.OnSuccess();
    }
}