using System;
using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TimeCooldownCommand : Command, ICommand
{
    private TimeCooldownCommandDef Params;

    public TimeCooldownCommand(TimeCooldownCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            return true;
        }

        // Check if the ability is on local cooldown
        if (Params.CheckLocal == 1 && character.IsOnCooldown(context.AbilityId))
        {
            Console.WriteLine($"[Ability] TimeCooldown: ability {context.AbilityId} is on local cooldown");
            return false;
        }

        // Check global cooldown (ability id 0)
        if (Params.CheckGlobal == 1 && character.IsOnCooldown(0))
        {
            Console.WriteLine($"[Ability] TimeCooldown: global cooldown active");
            return false;
        }

        // Check category cooldown
        if (Params.CheckCategory == 1 && Params.Category != 0 && character.IsOnCooldown(Params.Category))
        {
            Console.WriteLine($"[Ability] TimeCooldown: category {Params.Category} cooldown active");
            return false;
        }

        return true;
    }
}
