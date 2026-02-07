using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetByHostilityCommand : Command, ICommand
{
    private TargetByHostilityCommandDef Params;

    public TargetByHostilityCommand(TargetByHostilityCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        // Determine reference entity for comparison
        IAptitudeTarget reference = Params.CompareFromInitiator == 1 ? context.Initiator : context.Self;
        byte refFaction = 0;
        if (reference is CharacterEntity refChar)
        {
            refFaction = refChar.HostilityInfo.FactionId;
        }

        foreach (var target in previousTargets)
        {
            if (target == context.Self && Params.IncludeSelf == 0)
                continue;
            if (target == context.Initiator && Params.IncludeInitiator == 0)
                continue;
            if (target == reference?.Owner && Params.IncludeOwner == 0)
                continue;

            bool isHostile = false;
            if (target is CharacterEntity targetChar)
            {
                byte targetFaction = targetChar.HostilityInfo.FactionId;
                // Different non-zero factions are hostile
                isHostile = refFaction != 0 && targetFaction != 0 && targetFaction != refFaction;
            }

            // FilterType: 0 = keep hostiles, 1 = keep friendlies
            // ExcludeMode: 0 = include matching, 1 = exclude matching
            bool keepTarget;
            if (Params.FilterType == 0)
            {
                // Hostile filter
                keepTarget = Params.ExcludeMode == 0 ? isHostile : !isHostile;
            }
            else
            {
                // Friendly filter
                keepTarget = Params.ExcludeMode == 0 ? !isHostile : isHostile;
            }

            if (keepTarget)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        return true;
    }
}
