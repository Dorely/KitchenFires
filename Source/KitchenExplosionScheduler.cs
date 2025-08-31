using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace KitchenFires
{
    public static class KitchenExplosionScheduler
    {
        private class PendingExplosion
        {
            public Map map;
            public IntVec3 pos;
            public float radius;
            public Pawn instigator;
            public int damage;
            public int dueTick;
            public Sustainer sustainer;
            public int nextMoteTick;
        }

        private static readonly List<PendingExplosion> _pending = new List<PendingExplosion>();

        public static void Schedule(Map map, IntVec3 pos, float radius, Pawn instigator, int damage, int delayTicks)
        {
            Log.Message($"[KitchenFires] Scheduling delayed kitchen explosion at {pos} in {delayTicks} ticks (r={radius:F1}, dmg={damage})");
            var pending = new PendingExplosion
            {
                map = map,
                pos = pos,
                radius = radius,
                instigator = instigator,
                damage = damage,
                dueTick = Find.TickManager.TicksGame + delayTicks,
                nextMoteTick = Find.TickManager.TicksGame
            };
            // Start looping hiss at the position
            var info = SoundInfo.InMap(new TargetInfo(pos, map), MaintenanceType.PerTick);
            pending.sustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
            _pending.Add(pending);
        }

        public static void Tick()
        {
            if (_pending.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (p.map == null)
                {
                    p.sustainer?.End();
                    _pending.RemoveAt(i);
                    continue;
                }

                // Maintain sound and spawn visual motes during countdown
                p.sustainer?.Maintain();
                if (now >= p.nextMoteTick)
                {
                    p.nextMoteTick = now + Rand.RangeInclusive(10, 20);
                    // A bit of smoke and sparks to indicate danger
                    FleckMaker.ThrowSmoke(p.pos.ToVector3Shifted(), p.map, Rand.Range(0.6f, 1.2f));
                    if (Rand.Chance(0.35f))
                        FleckMaker.ThrowMicroSparks(p.pos.ToVector3Shifted(), p.map);
                    if (Rand.Chance(0.4f))
                        FleckMaker.ThrowHeatGlow(p.pos, p.map, Rand.Range(0.6f, 1.0f));
                }

                if (now >= p.dueTick)
                {
                    Log.Message($"[KitchenFires] Delayed kitchen explosion firing at {p.pos} (scheduled {p.dueTick}, now {now})");
                    p.sustainer?.End();
                    GenExplosion.DoExplosion(
                        center: p.pos,
                        map: p.map,
                        radius: p.radius,
                        damType: DamageDefOf.Flame,
                        instigator: p.instigator,
                        damAmount: p.damage,
                        armorPenetration: -1f,
                        explosionSound: null,
                        weapon: null,
                        projectile: null,
                        intendedTarget: null,
                        postExplosionSpawnThingDef: ThingDefOf.Filth_Ash,
                        postExplosionSpawnChance: 0.5f,
                        postExplosionSpawnThingCount: Rand.Range(1, 3),
                        applyDamageToExplosionCellsNeighbors: false,
                        preExplosionSpawnThingDef: null,
                        preExplosionSpawnChance: 0f,
                        preExplosionSpawnThingCount: 0,
                        chanceToStartFire: 0.8f,
                        damageFalloff: true
                    );
                    _pending.RemoveAt(i);
                }
            }
        }
    }
}