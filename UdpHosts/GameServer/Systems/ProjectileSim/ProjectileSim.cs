using System;
using System.Numerics;
using GameServer.Data.SDB.Records.dbitems;
using GameServer.Entities;
using GameServer.Entities.Character;

namespace GameServer;

public class ProjectileSim
{
    private Shard _shard;

    public ProjectileSim(Shard shard)
    {
        _shard = shard;
    }

    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo)
    {
        ulong hitEntityId = _shard.Physics.ProjectileRayCast(origin, direction, entity, trace);

        if (hitEntityId != 0 && hitEntityId != entity.EntityId &&
            _shard.Entities.TryGetValue(hitEntityId, out var hitEntity) &&
            hitEntity is CharacterEntity target && target.Alive)
        {
            var weaponDetails = entity.GetActiveWeaponDetails();
            int damage = weaponDetails?.Weapon.DamagePerRound ?? 100;
            byte damageType = ammo?.Damagetype ?? 0;
            target.ApplyDamage(damage, entity, damageType);
            Console.WriteLine($"[ProjectileSim] Hit entity {hitEntityId} for {damage} damage (type={damageType})");
        }
    }

    /*
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
    }
    */
}