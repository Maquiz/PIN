using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.V66;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.dbitems;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Physics;

namespace GameServer;

public class ProjectileSim
{
    private const float HitscanSpeedThreshold = 500f;

    private Shard _shard;
    private readonly List<InFlightProjectile> _inFlight = new();

    private struct InFlightProjectile
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Gravity;
        public float MaxRange;
        public float DistanceTraveled;
        public CharacterEntity Source;
        public Ammo Ammo;
        public int Damage;
        public byte DamageType;
        public uint Trace;
        public float HeadshotMult;
    }

    public ProjectileSim(Shard shard)
    {
        _shard = shard;
    }

    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo)
    {
        var weaponDetails = entity.GetActiveWeaponDetails();
        float weaponRange = weaponDetails?.Weapon.Range > 0 ? weaponDetails.Weapon.Range : 500f;
        int damage = weaponDetails?.Weapon.DamagePerRound ?? 100;
        float headshotMult = weaponDetails?.Weapon.HeadshotMult ?? 1f;
        byte damageType = ammo?.Damagetype ?? 0;

        // Determine if this is a slow projectile (needs travel time simulation)
        float projectileSpeed = ammo?.ProjectileSpeed ?? 0f;
        if (projectileSpeed > 0f && projectileSpeed < HitscanSpeedThreshold)
        {
            // Queue for ticked simulation
            _inFlight.Add(new InFlightProjectile
            {
                Position = origin,
                Velocity = direction * projectileSpeed,
                Gravity = ammo?.Gravity ?? 0f,
                MaxRange = weaponRange,
                DistanceTraveled = 0f,
                Source = entity,
                Ammo = ammo,
                Damage = damage,
                DamageType = damageType,
                Trace = trace,
                HeadshotMult = headshotMult
            });
            return;
        }

        // Instant hitscan
        var hit = _shard.Physics.ProjectileRayCast(origin, direction, entity, trace, weaponRange);

        if (hit.EntityId != 0 && hit.EntityId != entity.EntityId &&
            _shard.Entities.TryGetValue(hit.EntityId, out var hitEntity) &&
            hitEntity is CharacterEntity target && target.Alive)
        {
            bool headshot = IsHeadshot(hit.HitPosition, target);
            float hsMult = headshot ? Math.Max(headshotMult, 1f) : 1f;
            int finalDamage = (int)(damage * hsMult);
            DamageResponseFlags flags = headshot ? DamageResponseFlags.Critical : 0;

            target.ApplyDamage(finalDamage, entity, damageType, flags);
            Console.WriteLine($"[ProjectileSim] Hit entity {hit.EntityId} for {finalDamage} damage (type={damageType}{(headshot ? " HEADSHOT" : "")})");
        }

        // AoE splash
        if (ammo != null && ammo.ImpactRadius > 0)
        {
            Vector3 impactPos = hit.HitAnything
                ? hit.HitPosition
                : origin + direction * weaponRange;
            ApplySplashDamage(impactPos, ammo, damage, damageType, entity);
        }
    }

    public void ApplySplashDamage(Vector3 impactPos, Ammo ammo, int baseDamage,
        byte damageType, CharacterEntity source)
    {
        float radius = ammo.ImpactRadius;
        var nearby = _shard.Physics.FindEntitiesInRadius(impactPos, radius, source.EntityId);

        foreach (var (entityId, distance) in nearby)
        {
            if (!_shard.Entities.TryGetValue(entityId, out var ent) ||
                ent is not CharacterEntity target || !target.Alive)
                continue;

            float t = distance / radius;
            float minFrac = ammo.MinDamageFrac > 0 ? ammo.MinDamageFrac : 0.25f;
            float damageFrac = MathF.Max(minFrac, 1f - t);
            int splashDamage = (int)(baseDamage * damageFrac);

            target.ApplyDamage(splashDamage, source, damageType);
            Console.WriteLine($"[ProjectileSim] Splash hit entity {entityId} for {splashDamage} damage (dist={distance:F1}/{radius:F1})");
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (_inFlight.Count == 0) return;

        float dt = (float)(deltaTime / 1000.0);
        if (dt <= 0f) return;

        for (int i = _inFlight.Count - 1; i >= 0; i--)
        {
            var proj = _inFlight[i];
            var oldPos = proj.Position;

            // Apply gravity
            proj.Velocity.Z -= proj.Gravity * dt;

            var newPos = oldPos + proj.Velocity * dt;
            float segLen = Vector3.Distance(oldPos, newPos);
            proj.DistanceTraveled += segLen;

            // Raycast along movement segment
            var dir = segLen > 0.001f ? Vector3.Normalize(newPos - oldPos) : Vector3.Normalize(proj.Velocity);
            var hit = _shard.Physics.ProjectileRayCast(oldPos, dir, proj.Source, proj.Trace, segLen);

            bool impacted = false;
            Vector3 impactPos = newPos;

            if (hit.EntityId != 0 && hit.EntityId != proj.Source.EntityId)
            {
                // Hit an entity
                if (_shard.Entities.TryGetValue(hit.EntityId, out var hitEntity) &&
                    hitEntity is CharacterEntity target && target.Alive)
                {
                    bool headshot = IsHeadshot(hit.HitPosition, target);
                    float hsMult = headshot ? Math.Max(proj.HeadshotMult, 1f) : 1f;
                    int finalDamage = (int)(proj.Damage * hsMult);
                    DamageResponseFlags flags = headshot ? DamageResponseFlags.Critical : 0;

                    target.ApplyDamage(finalDamage, proj.Source, proj.DamageType, flags);
                }

                impactPos = hit.HitPosition;
                impacted = true;
            }
            else if (hit.HitAnything)
            {
                // Hit terrain/static
                impactPos = hit.HitPosition;
                impacted = true;
            }
            else if (proj.DistanceTraveled >= proj.MaxRange)
            {
                // Exceeded max range
                impactPos = newPos;
                impacted = true;
            }

            if (impacted)
            {
                // Apply AoE if applicable
                if (proj.Ammo != null && proj.Ammo.ImpactRadius > 0)
                {
                    ApplySplashDamage(impactPos, proj.Ammo, proj.Damage, proj.DamageType, proj.Source);
                }

                _inFlight.RemoveAt(i);
            }
            else
            {
                proj.Position = newPos;
                _inFlight[i] = proj;
            }
        }
    }

    private static bool IsHeadshot(Vector3 hitPos, CharacterEntity target)
    {
        float relativeZ = hitPos.Z - target.Position.Z;
        return relativeZ >= target.CollisionHeight * 0.85f;
    }
}
