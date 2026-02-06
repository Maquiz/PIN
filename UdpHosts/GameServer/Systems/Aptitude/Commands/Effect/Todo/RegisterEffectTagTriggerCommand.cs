using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class RegisterEffectTagTriggerCommand : Command, ICommand
{
    private RegisterEffectTagTriggerCommandDef Params;

    public RegisterEffectTagTriggerCommand(RegisterEffectTagTriggerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Effect tag triggers require an effect tag system to be implemented.
        // Pass through for now - effects are applied/removed directly.
        return true;
    }
}
