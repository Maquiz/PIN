using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class FireProjectileCommand : Command, ICommand
{
    private FireProjectileCommandDef Params;

    public FireProjectileCommand(FireProjectileCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float damage;

        if (Params.UseWeaponDamage == 1)
        {
            // Use damage from register (set by SetWeaponDamageCommand earlier in chain)
            damage = AbilitySystem.RegistryOp(context.Register, Params.Damage, (Operand)Params.DamageRegop);
        }
        else
        {
            damage = Params.Damage;
        }

        float range = AbilitySystem.RegistryOp(context.Register, Params.Range, (Operand)Params.RangeRegop);

        // Set the damage into the register for downstream commands
        context.Register = damage;

        if (Params.PassBonus == 1)
        {
            context.Bonus = (int)damage;
        }

        // The projectile firing/raycasting is handled by the existing ProjectileSim system
        // when the client sends FireWeaponProjectile. This command sets up the damage data
        // that will be used when the projectile hits.
        Console.WriteLine($"FireProjectileCommand {Params.Id}: damage={damage}, range={range}, ammo={Params.Ammotype}");

        return true;
    }
}
