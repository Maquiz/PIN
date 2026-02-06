using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ModifyDamageByTargetHealthCommand : Command, ICommand
{
    private ModifyDamageByTargetHealthCommandDef Params;

    public ModifyDamageByTargetHealthCommand(ModifyDamageByTargetHealthCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Execute-style damage scaling based on target health percentage.
        // Without detailed CommandDef fields, pass through for now.
        return true;
    }
}
