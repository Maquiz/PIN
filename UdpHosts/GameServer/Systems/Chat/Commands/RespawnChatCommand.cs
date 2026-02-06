namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Respawn your character", "respawn", "respawn", "revive")]
public class RespawnChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (character != null && !character.Alive)
        {
            character.Respawn();
            SourceFeedback("You have been respawned.", context);
        }
        else
        {
            SourceFeedback("You are not dead.", context);
        }
    }
}
