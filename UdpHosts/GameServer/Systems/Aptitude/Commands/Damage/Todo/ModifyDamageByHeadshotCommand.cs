using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ModifyDamageByHeadshotCommand : Command, ICommand
{
    private ModifyDamageByHeadshotCommandDef Params;

    public ModifyDamageByHeadshotCommand(ModifyDamageByHeadshotCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Headshot detection requires projectile hit zone data which isn't available yet.
        // When projectile sim provides hit zone info, this will apply a 2x multiplier.
        return true;
    }
}
