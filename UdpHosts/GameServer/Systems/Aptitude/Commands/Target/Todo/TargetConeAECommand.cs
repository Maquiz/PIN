using System;
using System.Linq;
using System.Numerics;
using GameServer.Data.SDB.Records.apt;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class TargetConeAECommand : Command, ICommand
{
    private TargetConeAECommandDef Params;

    public TargetConeAECommand(TargetConeAECommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.FormerTargets = new AptitudeTargets(context.Targets);

        float range = AbilitySystem.RegistryOp(context.Register, Params.Range, (Operand)Params.RangeRegop);
        float halfAngleRad = Params.Angle * MathF.PI / 180f;
        float cosHalfAngle = MathF.Cos(halfAngleRad);

        Vector3 origin = context.Self.Position;
        Vector3 aimDir = Vector3.UnitX;

        if (context.Self is CharacterEntity selfChar)
        {
            aimDir = selfChar.AimDirection;
            if (aimDir.LengthSquared() < 0.001f)
            {
                aimDir = Vector3.UnitX;
            }
            else
            {
                aimDir = Vector3.Normalize(aimDir);
            }
        }

        var matches = context.Shard.Entities
            .Where(pair =>
            {
                if (pair.Value is not BaseAptitudeEntity compatibleEntity)
                    return false;

                if (Params.IncludeInteractives == 0 && compatibleEntity is not CharacterEntity)
                    return false;

                if (compatibleEntity == context.Self)
                    return false;

                var toTarget = compatibleEntity.Position - origin;
                float dist = toTarget.Length();

                if (dist > range || dist < 0.01f)
                    return false;

                // Check cone angle
                var toTargetNorm = toTarget / dist;
                float dot = Vector3.Dot(aimDir, toTargetNorm);

                return dot >= cosHalfAngle;
            })
            .Select(pair => pair.Value as BaseAptitudeEntity)
            .ToList();

        // Sort by angle (closest to aim center first) if requested
        if (Params.SortByAngle == 1)
        {
            matches.Sort((a, b) =>
            {
                float dotA = Vector3.Dot(aimDir, Vector3.Normalize(a.Position - origin));
                float dotB = Vector3.Dot(aimDir, Vector3.Normalize(b.Position - origin));
                return dotB.CompareTo(dotA);
            });
        }

        // Enforce MaxTargets
        int maxTargets = Params.MaxTargets > 0 ? Params.MaxTargets : matches.Count;
        int added = 0;
        foreach (var match in matches)
        {
            if (added >= maxTargets)
                break;
            context.Targets.Push(match);
            added++;
        }

        if (context.Targets.Count < Params.MinTargets)
        {
            return false;
        }

        return true;
    }
}
