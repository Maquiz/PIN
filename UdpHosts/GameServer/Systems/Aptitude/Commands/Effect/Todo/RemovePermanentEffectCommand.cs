using System.Linq;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class RemovePermanentEffectCommand : Command, ICommand
{
    private RemovePermanentEffectCommandDef Params;

    public RemovePermanentEffectCommand(RemovePermanentEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Remove all effects from self (permanent effects are cleared on certain triggers)
        var activeEffects = context.Self.GetActiveEffects();
        foreach (var effect in activeEffects)
        {
            if (effect != null)
            {
                context.Self.ClearEffect(effect);
            }
        }

        return true;
    }
}
