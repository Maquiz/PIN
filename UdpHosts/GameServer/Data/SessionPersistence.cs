using System;
using System.Numerics;
using GameServer.GRPC;

namespace GameServer.Data;

/// <summary>
/// Single place that computes and persists a player's session data
/// (zone, nearest outpost, time played). Called from clean logout and
/// from Shard.MigrateOut so disconnects save the same way.
/// </summary>
public static class SessionPersistence
{
    public static void SaveSessionData(INetworkPlayer player)
    {
        if (player?.CharacterEntity == null || player.CurrentZone == null)
        {
            return;
        }

        var zone = player.CurrentZone;
        if (!zone.IsOpenWorld)
        {
            return;
        }

        var playerPosition = player.CharacterEntity.Position;
        var minDistance = Vector3.DistanceSquared(playerPosition, zone.POIs["spawn"]);
        var closestOutpostId = zone.DefaultOutpostId;

        if (player.AssignedShard.Outposts.TryGetValue(zone.ID, out var outposts))
        {
            foreach (var outpost in outposts)
            {
                var distance = Vector3.DistanceSquared(playerPosition, outpost.Value.Outpost_ObserverView.PositionProp);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestOutpostId = outpost.Key;
                }
            }
        }

        // CharacterId is the entity id; +0xFE restores the character guid
        // type tag (see GUIDService.Character) used by webapi.Characters.
        _ = GRPCService.SaveCharacterSessionDataAsync(
            player.CharacterId + 0xFE,
            zone.ID,
            closestOutpostId,
            (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - player.ConnectedAt);
    }
}
