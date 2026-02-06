using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class HealDamageCommand : Command, ICommand
{
    private HealDamageCommandDef Params;

    public HealDamageCommand(HealDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        int healAmount;

        if (Params.Weapondamage == 1 || Params.Usedmgdealt == 1)
        {
            healAmount = (int)AbilitySystem.RegistryOp(context.Register, Params.Healpoints, (Operand)Params.HealpointsRegop);
        }
        else
        {
            healAmount = Params.Healpoints;
        }

        if (healAmount <= 0)
        {
            return true;
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is CharacterEntity character)
            {
                character.ApplyHealing(healAmount);
            }
        }

        return true;
    }
}
