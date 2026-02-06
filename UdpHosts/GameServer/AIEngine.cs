using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data;
using GameServer.Data.SDB;
using GameServer.Entities;
using GameServer.Entities.Character;

namespace GameServer;

public enum NpcAIState
{
    Idle,
    Chase,
    Attack,
    Returning,
    Dead
}

public class AIEngine
{
    private const ulong AiTickIntervalMs = 500;
    private const ulong DespawnDelayMs = 5000;

    private IShard _shard;
    private ulong _lastAiTick;

    private ConcurrentDictionary<ulong, CharacterEntity> _npcs = new();
    private ConcurrentDictionary<ulong, NpcAIState> _npcStates = new();
    private ConcurrentDictionary<ulong, ulong> _npcTargets = new();

    // Dead NPCs pending respawn: (typeId, spawnPosition, respawnTime)
    private ConcurrentQueue<(uint TypeId, Vector3 Position, ulong RespawnTime, CharacterEntity Owner)> _pendingRespawns = new();

    // Dead NPCs pending removal: (entityId, removeTime)
    private ConcurrentQueue<(ulong EntityId, ulong RemoveTime)> _pendingRemovals = new();

    public void Init(IShard shard)
    {
        _shard = shard;
    }

    public void RegisterNpc(CharacterEntity npc)
    {
        if (npc.IsPlayerControlled)
        {
            return;
        }

        _npcs[npc.EntityId] = npc;
        _npcStates[npc.EntityId] = NpcAIState.Idle;
        Console.WriteLine($"[AI] Registered NPC {npc.EntityId} type={npc.MonsterTypeId} faction={npc.FactionId} hostile={IsHostileFaction(npc.FactionId)}");
    }

    public void UnregisterNpc(ulong entityId)
    {
        _npcs.TryRemove(entityId, out _);
        _npcStates.TryRemove(entityId, out _);
        _npcTargets.TryRemove(entityId, out _);
    }

    public void OnNpcDeath(CharacterEntity npc)
    {
        _npcStates[npc.EntityId] = NpcAIState.Dead;
        _npcTargets.TryRemove(npc.EntityId, out _);

        // Schedule entity removal after short delay (corpse time)
        _pendingRemovals.Enqueue((npc.EntityId, _shard.CurrentTimeLong + DespawnDelayMs));

        // Schedule respawn
        if (npc.MonsterTypeId != 0)
        {
            _pendingRespawns.Enqueue((npc.MonsterTypeId, npc.SpawnPosition, npc.NpcRespawnTime, npc.Owner as CharacterEntity));
        }

        Console.WriteLine($"[AI] NPC {npc.EntityId} died, respawn in {npc.NpcRespawnDelayMs}ms");
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (_shard == null)
        {
            return;
        }

        // Process pending removals
        ProcessRemovals(currentTime);

        // Process pending respawns
        ProcessRespawns(currentTime);

        // AI tick at fixed interval
        if (currentTime < _lastAiTick + AiTickIntervalMs)
        {
            return;
        }

        _lastAiTick = currentTime;

        foreach (var kvp in _npcs)
        {
            var npc = kvp.Value;
            if (!_npcStates.TryGetValue(npc.EntityId, out var state))
            {
                continue;
            }

            if (state == NpcAIState.Dead)
            {
                continue;
            }

            // Only hostile NPCs run AI
            if (!IsHostileFaction(npc.FactionId))
            {
                continue;
            }

            UpdateNpc(npc, state, currentTime);
        }
    }

    private static bool IsHostileFaction(uint factionId)
    {
        return HardcodedCharacterData.HostileFactionIds.Contains(factionId);
    }

    private void ProcessRemovals(ulong currentTime)
    {
        int count = _pendingRemovals.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_pendingRemovals.TryDequeue(out var removal))
            {
                break;
            }

            if (currentTime >= removal.RemoveTime)
            {
                UnregisterNpc(removal.EntityId);
                _shard.EntityMan.Remove(removal.EntityId);
            }
            else
            {
                _pendingRemovals.Enqueue(removal);
            }
        }
    }

    private void ProcessRespawns(ulong currentTime)
    {
        int count = _pendingRespawns.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_pendingRespawns.TryDequeue(out var respawn))
            {
                break;
            }

            if (currentTime >= respawn.RespawnTime)
            {
                _shard.EntityMan.SpawnCharacter(respawn.TypeId, respawn.Position, respawn.Owner);
                Console.WriteLine($"[AI] Respawned NPC type={respawn.TypeId}");
            }
            else
            {
                // Not ready yet, put it back
                _pendingRespawns.Enqueue(respawn);
            }
        }
    }

    private void UpdateNpc(CharacterEntity npc, NpcAIState state, ulong currentTime)
    {
        // Find nearest hostile player
        CharacterEntity target = FindNearestPlayer(npc);
        float distToTarget = target != null ? Vector3.Distance(npc.Position, target.Position) : float.MaxValue;
        float distToSpawn = Vector3.Distance(npc.Position, npc.SpawnPosition);

        switch (state)
        {
            case NpcAIState.Idle:
                if (target != null && distToTarget <= npc.AggroRadius)
                {
                    _npcStates[npc.EntityId] = NpcAIState.Chase;
                    _npcTargets[npc.EntityId] = target.EntityId;
                }

                break;

            case NpcAIState.Chase:
                if (target == null || !target.Alive || distToTarget > npc.LeashRadius || distToSpawn > npc.LeashRadius)
                {
                    // Lost target or too far from spawn, return
                    _npcStates[npc.EntityId] = NpcAIState.Returning;
                    _npcTargets.TryRemove(npc.EntityId, out _);
                    break;
                }

                if (distToTarget <= npc.AttackRange)
                {
                    _npcStates[npc.EntityId] = NpcAIState.Attack;
                }
                else
                {
                    // Move toward target
                    MoveNpc(npc, target.Position, npc.NpcFastSpeed);
                }

                break;

            case NpcAIState.Attack:
                if (target == null || !target.Alive)
                {
                    _npcStates[npc.EntityId] = NpcAIState.Idle;
                    _npcTargets.TryRemove(npc.EntityId, out _);
                    break;
                }

                if (distToTarget > npc.AttackRange * 1.5f)
                {
                    // Target moved out of range, chase again
                    _npcStates[npc.EntityId] = NpcAIState.Chase;
                    break;
                }

                if (distToSpawn > npc.LeashRadius)
                {
                    // Too far from spawn
                    _npcStates[npc.EntityId] = NpcAIState.Returning;
                    _npcTargets.TryRemove(npc.EntityId, out _);
                    break;
                }

                // Attack on interval
                if (currentTime >= npc.LastNpcAttackTime + npc.NpcAttackIntervalMs)
                {
                    npc.LastNpcAttackTime = currentTime;
                    target.ApplyDamage(npc.NpcDamage, npc, 0);
                }

                // Face target while attacking (stopped)
                npc.MovementState = (short)0x1000; // Standing
                FaceTarget(npc, target.Position);
                BroadcastNpcMovement(npc);
                break;

            case NpcAIState.Returning:
                if (distToSpawn < 3.0f)
                {
                    // Back at spawn
                    npc.SetPosition(npc.SpawnPosition);
                    npc.MovementState = (short)0x1000; // Standing
                    BroadcastNpcMovement(npc);
                    _npcStates[npc.EntityId] = NpcAIState.Idle;

                    // Heal to full
                    npc.CurrentHealth = npc.MaxHealth.Value;
                    npc.SetHealth(npc.CurrentHealth, _shard.CurrentTime);
                }
                else
                {
                    MoveNpc(npc, npc.SpawnPosition, npc.NpcNormalSpeed);

                    // Regen while returning
                    if (npc.CurrentHealth < npc.MaxHealth.Value)
                    {
                        npc.ApplyHealing(npc.MaxHealth.Value / 10);
                    }
                }

                break;
        }
    }

    private CharacterEntity FindNearestPlayer(CharacterEntity npc)
    {
        CharacterEntity nearest = null;
        float nearestDist = float.MaxValue;

        // If NPC already has a target, prefer it
        if (_npcTargets.TryGetValue(npc.EntityId, out var targetId))
        {
            if (_shard.Entities.TryGetValue(targetId, out var targetEntity) &&
                targetEntity is CharacterEntity tc &&
                tc.IsPlayerControlled &&
                tc.Alive)
            {
                return tc;
            }

            // Target lost, clear it
            _npcTargets.TryRemove(npc.EntityId, out _);
        }

        foreach (var client in _shard.Clients.Values)
        {
            if (client.CharacterEntity == null || !client.CharacterEntity.Alive)
            {
                continue;
            }

            float dist = Vector3.Distance(npc.Position, client.CharacterEntity.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = client.CharacterEntity;
            }
        }

        return nearest;
    }

    private void MoveNpc(CharacterEntity npc, Vector3 targetPos, float speed)
    {
        var direction = targetPos - npc.Position;
        if (direction.LengthSquared() < 0.01f)
        {
            return;
        }

        direction = Vector3.Normalize(direction);
        float moveDistance = speed * (AiTickIntervalMs / 1000.0f);
        float remainingDistance = Vector3.Distance(npc.Position, targetPos);
        moveDistance = Math.Min(moveDistance, remainingDistance);

        var newPos = npc.Position + (direction * moveDistance);
        npc.SetPosition(newPos);
        npc.MovementState = unchecked((short)0x2004); // Running + Movement flag
        FaceTarget(npc, targetPos);
        BroadcastNpcMovement(npc);
    }

    private void BroadcastNpcMovement(CharacterEntity npc)
    {
        // Player movement uses CurrentPoseUpdate events sent to all clients.
        // NPCs must use the same mechanism since MovementView is not flushed by EntityManager.
        npc.MovementShortTime = _shard.CurrentShortTime;

        var currentPose = new CurrentPoseUpdate
        {
            Data = new AeroMessages.GSS.V66.CurrentPoseUpdateData
            {
                Flags = 0x00,
                ShortTime = npc.MovementShortTime,
                UnkAlwaysPresent = 0x79,
                MovementState = (ushort)npc.MovementState,
                Position = npc.Position,
                Rotation = npc.Rotation,
                Aim = npc.AimDirection,
            }
        };

        foreach (var client in _shard.Clients.Values)
        {
            if (client.Status.Equals(IPlayer.PlayerStatus.Playing))
            {
                client.NetChannels[ChannelType.UnreliableGss].SendMessage(currentPose, npc.EntityId);
            }
        }

        // Flush other views (health bar, observer, etc.)
        _shard.EntityMan.FlushChanges(npc);
    }

    private static void FaceTarget(CharacterEntity npc, Vector3 targetPos)
    {
        var direction = targetPos - npc.Position;
        if (direction.LengthSquared() > 0.01f)
        {
            npc.AimDirection = Vector3.Normalize(direction);
        }
    }
}
