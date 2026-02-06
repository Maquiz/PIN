namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Deal damage to yourself for testing", "damage <amount>", "damage", "hurt")]
public class DamageChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            return;
        }

        int amount = 1000;
        if (parameters.Length > 0 && int.TryParse(parameters[0], out int parsed))
        {
            amount = parsed;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (character != null && character.Alive)
        {
            character.ApplyDamage(amount, character, 0);
            SourceFeedback($"Dealt {amount} damage. HP: {character.CurrentHealth}/{character.MaxHealth.Value}", context);
        }
        else
        {
            SourceFeedback("You are dead.", context);
        }
    }
}
