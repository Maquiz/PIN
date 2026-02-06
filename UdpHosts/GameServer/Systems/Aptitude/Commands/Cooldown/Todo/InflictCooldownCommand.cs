using System;
using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class InflictCooldownCommand : Command, ICommand
{
    private InflictCooldownCommandDef Params;

    public InflictCooldownCommand(InflictCooldownCommandDef par)
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

        // Local cooldown for this specific ability
        if (Params.LocalCooldown > 0)
        {
            uint duration = Params.LocalCooldown;
            if (Params.DurationRegop != 0)
            {
                duration = (uint)AbilitySystem.RegistryOp(context.Register, duration, (Operand)Params.DurationRegop);
            }

            character.SetCooldown(context.AbilityId, duration);
        }

        // Global cooldown
        if (Params.GlobalCooldown > 0)
        {
            character.SetCooldown(0, Params.GlobalCooldown);
        }

        // Category cooldown
        if (Params.CategoryCooldown > 0 && Params.Category != 0)
        {
            character.SetCooldown(Params.Category, Params.CategoryCooldown);
        }

        return true;
    }
}
