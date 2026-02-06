namespace GameServer.Systems.Chat.Commands;

[ChatCommand("Heal your character", "heal [amount]", "heal")]
public class HealChatCommand : ChatCommand
{
    public override void Execute(string[] parameters, ChatCommandContext context)
    {
        if (context.SourcePlayer == null)
        {
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (character == null || !character.Alive)
        {
            SourceFeedback("You are dead.", context);
            return;
        }

        int amount = character.MaxHealth.Value - character.CurrentHealth;
        if (parameters.Length > 0 && int.TryParse(parameters[0], out int parsed))
        {
            amount = parsed;
        }

        character.ApplyHealing(amount);
        SourceFeedback($"Healed {amount}. HP: {character.CurrentHealth}/{character.MaxHealth.Value}", context);
    }
}
