namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Kill your character", "suicide", "suicide", "kill")]
public class SuicideChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (character != null && character.Alive)
        {
            character.Die(character);
            SourceFeedback("You died.", context);
        }
        else
        {
            SourceFeedback("You are already dead.", context);
        }
    }
}
