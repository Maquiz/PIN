using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ApplyClientStatusEffectCommand : Command, ICommand
{
    private ApplyClientStatusEffectCommandDef Params;

    public ApplyClientStatusEffectCommand(ApplyClientStatusEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.StatusEffectId == 0)
        {
            return true;
        }

        if (Params.ApplyToSelf == 1)
        {
            ApplyToTarget(context.Self, context);
        }
        else
        {
            foreach (IAptitudeTarget target in context.Targets)
            {
                ApplyToTarget(target, context);
            }
        }

        return true;
    }

    private void ApplyToTarget(IAptitudeTarget target, Context context)
    {
        var effect = context.Abilities.Factory.LoadEffect(Params.StatusEffectId);
        if (effect != null)
        {
            target.AddEffect(effect, context);
        }
    }
}
