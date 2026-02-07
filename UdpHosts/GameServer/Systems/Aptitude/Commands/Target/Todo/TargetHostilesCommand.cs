using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetHostilesCommand : Command, ICommand
{
    private TargetHostilesCommandDef Params;

    public TargetHostilesCommand(TargetHostilesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        // Get initiator's faction for hostility comparison
        byte selfFaction = 0;
        if (context.Self is CharacterEntity selfChar)
        {
            selfFaction = selfChar.HostilityInfo.FactionId;
        }

        foreach (var target in previousTargets)
        {
            if (target == context.Self && Params.IncludeSelf == 0)
                continue;
            if (target == context.Initiator && Params.IncludeInitiator == 0)
                continue;
            if (target == context.Self.Owner && Params.IncludeOwner == 0)
                continue;

            // Filter to only hostile targets (different faction)
            if (target is CharacterEntity targetChar)
            {
                byte targetFaction = targetChar.HostilityInfo.FactionId;
                if (selfFaction != 0 && targetFaction != 0 && targetFaction == selfFaction)
                    continue; // Same faction = friendly, skip
            }

            newTargets.Push(target);
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
