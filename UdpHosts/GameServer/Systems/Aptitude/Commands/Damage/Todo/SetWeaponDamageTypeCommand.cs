using GameServer.Data.SDB.Records.aptfs;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class SetWeaponDamageTypeCommand : Command, ICommand
{
    private SetWeaponDamageTypeCommandDef Params;

    public SetWeaponDamageTypeCommand(SetWeaponDamageTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Apply bonus damage amount via registry op
        if (Params.BonusAmt != 0)
        {
            context.Register = AbilitySystem.RegistryOp(context.Register, Params.BonusAmt, (Operand)Params.BonusAmtRegop);
        }

        return true;
    }
}
