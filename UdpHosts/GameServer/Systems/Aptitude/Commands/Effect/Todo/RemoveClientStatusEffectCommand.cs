using System.Linq;
using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class RemoveClientStatusEffectCommand : Command, ICommand
{
    private RemoveClientStatusEffectCommandDef Params;

    public RemoveClientStatusEffectCommand(RemoveClientStatusEffectCommandDef par)
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
            RemoveFromTarget(context.Self);
        }
        else
        {
            foreach (IAptitudeTarget target in context.Targets)
            {
                RemoveFromTarget(target);
            }
        }

        return true;
    }

    private void RemoveFromTarget(IAptitudeTarget target)
    {
        var activeEffects = target.GetActiveEffects();
        var effectToRemove = activeEffects.FirstOrDefault(e => e?.Effect?.Id == Params.StatusEffectId);
        if (effectToRemove != null)
        {
            target.ClearEffect(effectToRemove);
        }
    }
}
