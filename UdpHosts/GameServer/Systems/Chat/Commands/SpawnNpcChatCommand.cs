using System.Numerics;
using GameServer.Data.SDB;

namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Spawn an NPC by typeId near you", "spawn <typeId>", "spawn", "spawnnpc")]
public class SpawnNpcChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (context.SourcePlayer?.CharacterEntity == null)
        {
            SourceFeedback("No character available.", context);
            return;
        }

        if (parameters.Length < 1)
        {
            SourceFeedback("Usage: /spawn <typeId>", context);
            return;
        }

        if (!uint.TryParse(parameters[0], out uint typeId))
        {
            SourceFeedback("Invalid typeId.", context);
            return;
        }

        if (SDBInterface.GetMonster(typeId) == null)
        {
            SourceFeedback($"No monster data for typeId {typeId}.", context);
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        var aim = character.AimDirection;
        var forward = new Vector3(aim.X, aim.Y, 0f);
        if (forward.LengthSquared() > 0.01f)
        {
            forward = Vector3.Normalize(forward);
        }
        else
        {
            forward = new Vector3(1f, 0f, 0f);
        }

        // Spawn 30m in front of the player (beyond attack range so you can see them chase)
        var spawnPos = character.Position + (forward * 30f);
        var npc = context.Shard.EntityMan.SpawnCharacter(typeId, spawnPos);
        var monster = SDBInterface.GetMonster(typeId);
        SourceFeedback($"Spawned NPC {typeId} (faction:{monster.FactionId}) 30m ahead. Hostile={Data.HardcodedCharacterData.HostileFactionIds.Contains(monster.FactionId)}", context);
    }
}
