using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ApplyPermanentEffectCommand : Command, ICommand
{
    private ApplyPermanentEffectCommandDef Params;

    public ApplyPermanentEffectCommand(ApplyPermanentEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.EffectId == 0)
        {
            return true;
        }

        var effect = context.Abilities.Factory.LoadEffect(Params.EffectId);
        if (effect != null)
        {
            context.Self.AddEffect(effect, context);
        }

        return true;
    }
}
