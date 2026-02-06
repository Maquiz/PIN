using System;
using AeroMessages.GSS.V66;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class InflictDamageCommand : Command, ICommand
{
    private InflictDamageCommandDef Params;

    public InflictDamageCommand(InflictDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        int damage;

        if (Params.Weapondamage == 1 || Params.Usedmgdealt == 1)
        {
            // Use the register value (set by SetWeaponDamageCommand or prior chain)
            damage = (int)AbilitySystem.RegistryOp(context.Register, Params.Damagepoints, (Operand)Params.DamagepointsRegop);
        }
        else
        {
            damage = Params.Damagepoints;
        }

        if (damage <= 0)
        {
            return true;
        }

        byte damageType = Params.DamageType;

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is CharacterEntity character)
            {
                character.ApplyDamage(damage, context.Initiator, damageType);
            }
        }

        return true;
    }
}
