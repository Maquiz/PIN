using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AeroMessages.Common;
using AeroMessages.GSS.V66.AreaVisualData.View;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities;
using GameServer.Entities.Character;
using Shared.Udp;

namespace GameServer.Systems.Loot;

public enum LootDropType
{
    Resource,
    AmmoPowerup,
    HealthPowerup
}

public struct LootDropInfo
{
    public ulong EntityId;
    public LootDropType Type;
    public uint SdbId;
    public int Quantity;
    public Vector3 Position;
    public ulong SpawnTime;
}

public class LootService
{
    private const float PickupRadius = 3.0f;
    private const uint DespawnTimeMs = 30000;
    private const ulong TickIntervalMs = 500;

    // Hardcoded SDB IDs
    private const uint CrystiteSdbId = 10;
    private const uint HealthPickupSdbId = 77132;
    private const uint AmmoPickupSdbId = 82628;

    private static readonly Random _rng = new();

    private readonly IShard _shard;
    private readonly ConcurrentDictionary<ulong, LootDropInfo> _activeDrops = new();
    private ulong _lastTick;

    public LootService(IShard shard)
    {
        _shard = shard;
    }

    public void SpawnLootForNpc(CharacterEntity npc, CharacterEntity killer)
    {
        var drops = RollLootDrops(npc);
        var deathPos = npc.Position;

        foreach (var drop in drops)
        {
            // Scatter position around death location
            var offset = new Vector3(
                (float)(_rng.NextDouble() * 4.0 - 2.0),
                (float)(_rng.NextDouble() * 4.0 - 2.0),
                0.5f
            );
            var dropPos = deathPos + offset;

            // Ground clamp
            float? groundZ = _shard.Physics.SampleGroundHeight(dropPos);
            if (groundZ.HasValue)
            {
                dropPos.Z = groundZ.Value + 0.5f;
            }

            // Spawn AreaVisualData entity
            var avd = _shard.EntityMan.SpawnAreaVisualData(dropPos, new ScopingComponent { Range = 100f });

            // Initialize LootObjectView with drop data
            avd.AreaVisualData_LootObjectView = new LootObjectView();
            avd.AreaVisualData_LootObjectView.LootObjects_0Prop = new LootObjectData
            {
                Time = _shard.CurrentTime,
                HaveEntity = 0,
                HaveUnk3 = 0,
                Unk4 = Vector3.Zero,
                Position = dropPos,
                LootSdbId = drop.SdbId,
                Quantity = (byte)Math.Min(drop.Quantity, 255),
                Unk5 = 0,
                Unk6 = 0,
                Unk7 = new uint[] { 0, 0 }
            };

            // Auto-despawn after 30 seconds
            _shard.EntityMan.SetRemainingLifetime(avd, DespawnTimeMs);

            _activeDrops[avd.EntityId] = new LootDropInfo
            {
                EntityId = avd.EntityId,
                Type = drop.Type,
                SdbId = drop.SdbId,
                Quantity = drop.Quantity,
                Position = dropPos,
                SpawnTime = _shard.CurrentTimeLong
            };

            Console.WriteLine($"[Loot] Spawned {drop.Type} (sdbId={drop.SdbId} qty={drop.Quantity}) at ({dropPos.X:F1}, {dropPos.Y:F1}, {dropPos.Z:F1})");
        }
    }

    private List<(LootDropType Type, uint SdbId, int Quantity)> RollLootDrops(CharacterEntity npc)
    {
        var drops = new List<(LootDropType, uint, int)>();

        // Crystite - always drops
        drops.Add((LootDropType.Resource, CrystiteSdbId, _rng.Next(5, 26)));

        // Ammo powerup - 50% chance
        if (_rng.NextDouble() < 0.5)
        {
            drops.Add((LootDropType.AmmoPowerup, AmmoPickupSdbId, 1));
        }

        // Health powerup - 30% chance
        if (_rng.NextDouble() < 0.3)
        {
            drops.Add((LootDropType.HealthPowerup, HealthPickupSdbId, 1));
        }

        return drops;
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastTick + TickIntervalMs)
        {
            return;
        }

        _lastTick = currentTime;

        foreach (var kvp in _activeDrops)
        {
            // Clean up drops whose entity was already removed by lifetime system
            if (!_shard.Entities.ContainsKey(kvp.Key))
            {
                _activeDrops.TryRemove(kvp.Key, out _);
                continue;
            }

            // Check proximity to each playing client
            foreach (var client in _shard.Clients.Values)
            {
                if (!client.Status.Equals(IPlayer.PlayerStatus.Playing))
                {
                    continue;
                }

                if (client.CharacterEntity == null || !client.CharacterEntity.Alive)
                {
                    continue;
                }

                float dist = Vector3.Distance(client.CharacterEntity.Position, kvp.Value.Position);
                if (dist <= PickupRadius)
                {
                    if (_activeDrops.TryRemove(kvp.Key, out var dropInfo))
                    {
                        CollectLoot((INetworkPlayer)client, dropInfo);
                    }

                    break;
                }
            }
        }
    }

    public void TryCollectLoot(INetworkPlayer player, EntityId lootEntityId, uint lootSdbId)
    {
        if (_activeDrops.TryRemove(lootEntityId.Backing, out var drop))
        {
            CollectLoot(player, drop);
        }
    }

    private void CollectLoot(INetworkPlayer player, LootDropInfo drop)
    {
        // Apply effect based on drop type
        switch (drop.Type)
        {
            case LootDropType.Resource:
                player.Inventory.AddResource(drop.SdbId, (uint)drop.Quantity);
                break;

            case LootDropType.HealthPowerup:
                if (player.CharacterEntity != null && player.CharacterEntity.Alive)
                {
                    int healAmount = player.CharacterEntity.MaxHealth.Value / 5;
                    player.CharacterEntity.ApplyHealing(healAmount);
                }
                break;

            case LootDropType.AmmoPowerup:
                // Ammo system not server-tracked yet; give a small crystite bonus as placeholder
                player.Inventory.AddResource(CrystiteSdbId, 5);
                break;
        }

        // Send floating pickup notification to collecting player
        var pickupMsg = new SimulateLootPickup
        {
            Item = new()
            {
                SdbId = drop.SdbId,
                Quantity = (ushort)Math.Min(drop.Quantity, ushort.MaxValue),
            },
            RewardType = 1,
        };
        player.NetChannels[ChannelType.ReliableGss].SendMessage(pickupMsg, player.CharacterEntity.EntityId);

        // Broadcast LootObjectCollected to all clients so they remove the visual
        var collectedMsg = new AeroMessages.GSS.V66.AreaVisualData.Event.LootObjectCollected
        {
            Unk1 = 0,
            LootedByEntity = player.CharacterEntity.AeroEntityId,
            LootedToEntity = player.CharacterEntity.AeroEntityId,
        };

        foreach (var client in _shard.Clients.Values)
        {
            if (client.Status.Equals(IPlayer.PlayerStatus.Playing))
            {
                ((INetworkPlayer)client).NetChannels[ChannelType.ReliableGss]
                    .SendMessage(collectedMsg, drop.EntityId);
            }
        }

        // Remove the AVD entity
        if (_shard.Entities.ContainsKey(drop.EntityId))
        {
            _shard.EntityMan.Remove(drop.EntityId);
        }

        Console.WriteLine($"[Loot] Player collected {drop.Type} (sdbId={drop.SdbId} qty={drop.Quantity})");
    }
}
