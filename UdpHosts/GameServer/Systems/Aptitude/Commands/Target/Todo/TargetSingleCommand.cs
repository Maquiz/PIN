using System;
using System.Numerics;
using GameServer.Data.SDB.Records.apt;
using GameServer.Entities;

namespace GameServer.Aptitude;

public class TargetSingleCommand : Command, ICommand
{
    private TargetSingleCommandDef Params;

    public TargetSingleCommand(TargetSingleCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.FormerTargets = new AptitudeTargets(context.Targets);

        // TargetSingle picks the first valid target from the context's existing targets
        // (populated by the client's target list from ActivateAbility message)
        // If there are already targets, keep only the first one within range
        if (context.Targets.Count > 0)
        {
            var firstTarget = context.Targets.Peek();
            if (firstTarget != null)
            {
                float range = Params.Range;
                if (range > 0)
                {
                    float distance = Vector3.Distance(context.Self.Position, firstTarget.Position);
                    if (distance > range)
                    {
                        Console.WriteLine($"[Ability] TargetSingle: target out of range ({distance:F1} > {range:F1})");
                        return false;
                    }
                }

                // Trim to single target
                var singleTarget = new AptitudeTargets();
                singleTarget.Push(firstTarget);
                context.Targets = singleTarget;
                return true;
            }
        }

        // No targets available — for abilities that target the ground/position, still succeed
        if (Params.StaticOnly == 1)
        {
            return true;
        }

        Console.WriteLine($"[Ability] TargetSingle: no targets available");
        return false;
    }
}
