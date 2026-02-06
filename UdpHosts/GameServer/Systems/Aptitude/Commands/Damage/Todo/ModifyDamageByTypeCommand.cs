using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ModifyDamageByTypeCommand : Command, ICommand
{
    private ModifyDamageByTypeCommandDef Params;

    public ModifyDamageByTypeCommand(ModifyDamageByTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Damage type resistances require target's resistance data from SDB.
        // Pass through for now - all damage types deal full damage.
        return true;
    }
}
