using GameServer.Data.SDB.Records.aptfs;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class SetWeaponDamageCommand : Command, ICommand
{
    private SetWeaponDamageCommandDef Params;

    public SetWeaponDamageCommand(SetWeaponDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float damage;

        if (Params.Set == 1)
        {
            damage = Params.Dmgminvalue;
        }
        else
        {
            // Lerp between min and max damage based on some factor
            damage = Params.Dmgminvalue;

            if (Params.Dmgmaxvalue > Params.Dmgminvalue)
            {
                // Default to max damage for now
                damage = Params.Dmgmaxvalue;
            }
        }

        if (Params.Multiply == 1)
        {
            context.Register *= damage;
        }
        else
        {
            context.Register = AbilitySystem.RegistryOp(context.Register, damage, (Operand)Params.DamageRegop);
        }

        return true;
    }
}
