using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KitchenFires
{
    public abstract class IncidentWorker_KitchenBase : IncidentWorker
    {
        protected Pawn GetTriggeringPawn(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            
            // Try to get the pawn that triggered this incident from parameters
            if (!string.IsNullOrEmpty(parms.customLetterText) && parms.customLetterText.StartsWith("triggeringPawn:"))
            {
                string idString = parms.customLetterText.Substring("triggeringPawn:".Length);
                if (int.TryParse(idString, out int pawnId))
                {
                    var pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == pawnId);
                    if (pawn != null)
                    {
                        // Clear the custom text so it doesn't interfere with letters
                        parms.customLetterText = "";
                        return pawn;
                    }
                }
            }
            
            // Fallback: find a random cooking pawn
            var cookingPawns = map.mapPawns.FreeColonistsSpawned
                .Where(p => IsCurrentlyCooking(p) || WasRecentlyCooking(p))
                .ToList();
                
            if (cookingPawns.Any())
                return cookingPawns.RandomElement();
                
            // Last fallback: any free colonist
            return map.mapPawns.FreeColonistsSpawned.RandomElementWithFallback();
        }
        
        private bool IsCurrentlyCooking(Pawn pawn)
        {
            return pawn.CurJob != null && KitchenIncidentUtility.IsCookingJob(pawn.CurJob.def);
        }
        
        private bool WasRecentlyCooking(Pawn pawn)
        {
            // Check if pawn was recently at a cooking station
            return GenRadial.RadialCellsAround(pawn.Position, 3, true)
                .Any(c => c.InBounds(pawn.Map) && 
                     c.GetFirstBuilding(pawn.Map) != null && 
                     IsCookingRelatedBuilding(c.GetFirstBuilding(pawn.Map)));
        }
        
        private bool IsCookingRelatedBuilding(Building building)
        {
            if (building?.def == null) return false;
            
            string defName = building.def.defName.ToLower();
            return defName.Contains("stove") || defName.Contains("grill") || 
                   defName.Contains("kitchen") || defName.Contains("cook") ||
                   building.def.building?.isMealSource == true;
        }
        
        protected IntVec3 FindBestIncidentLocation(Pawn triggeringPawn)
        {
            Map map = triggeringPawn.Map;
            
            // Try to find nearby cooking stations
            var nearbyBuildings = GenRadial.RadialCellsAround(triggeringPawn.Position, 2, true)
                .Where(c => c.InBounds(map) && c.GetFirstBuilding(map) != null)
                .Where(c => IsCookingRelatedBuilding(c.GetFirstBuilding(map)))
                .ToList();

            if (nearbyBuildings.Any())
            {
                return nearbyBuildings.RandomElement();
            }
            
            return triggeringPawn.Position;
        }
    }

    public class IncidentWorker_KitchenFire_Small : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Log.Message($"[KitchenFires] IncidentWorker_KitchenFire_Small.TryExecuteWorker called - forced: {parms.forced}");
            
            // Check if this is being queued by storyteller or executed immediately
            if (!parms.forced)
            {
                // Storyteller selected this incident - queue it for next cooking action
                Log.Message("[KitchenFires] Storyteller selected kitchen fire - queueing for next cooking action");
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            // Execute the actual incident
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            Map map = triggeringPawn.Map;
            IntVec3 firePos = FindBestIncidentLocation(triggeringPawn);
            
            // Create small fire
            Fire fire = (Fire)GenSpawn.Spawn(ThingDefOf.Fire, firePos, map);
            fire.fireSize = Rand.Range(0.3f, 0.6f);
            
            // Send letter using storyteller system
            SendStandardLetter(parms, new LookTargets(fire), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Small kitchen fire created for {triggeringPawn.Name} at {firePos}");
            return true;
        }
    }

    public class IncidentWorker_KitchenFire_Large : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!parms.forced)
            {
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            Map map = triggeringPawn.Map;
            IntVec3 firePos = FindBestIncidentLocation(triggeringPawn);
            
            // Create multiple fire spots for large fire
            var fireCells = new List<IntVec3> { firePos };
            var additionalCells = GenRadial.RadialCellsAround(firePos, 1, true)
                .Where(c => c.InBounds(map) && c.Standable(map))
                .Take(Rand.Range(2, 4))
                .ToList();
            fireCells.AddRange(additionalCells);
            
            Fire primaryFire = null;
            foreach (var cell in fireCells)
            {
                Fire fire = (Fire)GenSpawn.Spawn(ThingDefOf.Fire, cell, map);
                fire.fireSize = Rand.Range(0.6f, 1.0f);
                if (primaryFire == null) primaryFire = fire;
            }
            
            SendStandardLetter(parms, new LookTargets(primaryFire), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Large kitchen fire created for {triggeringPawn.Name} at {firePos}");
            return true;
        }
    }

    public class IncidentWorker_KitchenExplosion : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!parms.forced)
            {
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            Map map = triggeringPawn.Map;
            IntVec3 explosionPos = FindBestIncidentLocation(triggeringPawn);
            
            // Create small explosion
            float explosionRadius = Rand.Range(1.5f, 2.5f);
            GenExplosion.DoExplosion(
                center: explosionPos,
                map: map,
                radius: explosionRadius,
                damType: DamageDefOf.Flame,
                instigator: triggeringPawn,
                damAmount: Rand.Range(10, 25),
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
            
            SendStandardLetter(parms, new LookTargets(explosionPos, map), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Kitchen explosion created for {triggeringPawn.Name} at {explosionPos}");
            return true;
        }
    }

    public class IncidentWorker_KitchenBurn : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!parms.forced)
            {
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            // Calculate burn severity based on cooking skill
            var cookingSkill = triggeringPawn.skills.GetSkill(SkillDefOf.Cooking);
            float severity = CalculateBurnSeverity(cookingSkill.Level);
            
            // Choose body part for burn (hands most likely)
            BodyPartRecord targetPart = GetLikelyBurnBodyPart(triggeringPawn);
            if (targetPart == null) return false;
            
            // Find burn hediff definition
            HediffDef burnDef = DefDatabase<HediffDef>.AllDefs.FirstOrDefault(h => h.defName == "Burn");
            if (burnDef == null) return false;
            
            // Create burn injury
            var injury = HediffMaker.MakeHediff(burnDef, triggeringPawn, targetPart);
            injury.Severity = severity;
            triggeringPawn.health.AddHediff(injury);
            
            SendStandardLetter(parms, new LookTargets(triggeringPawn), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Kitchen burn created for {triggeringPawn.Name} on {targetPart.Label} with severity {severity:F2}");
            return true;
        }
        
        private float CalculateBurnSeverity(int skillLevel)
        {
            // Lower skill = potentially more severe burns
            float maxSeverity = Mathf.Lerp(0.4f, 0.15f, skillLevel / 20f);
            float minSeverity = 0.05f;
            return Rand.Range(minSeverity, maxSeverity);
        }
        
        private BodyPartRecord GetLikelyBurnBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            // Prefer hands, then arms, then other parts
            var hands = bodyParts.Where(p => p.def.defName == "Hand").ToList();
            if (hands.Any()) return hands.RandomElement();

            var arms = bodyParts.Where(p => p.def.defName == "Arm").ToList();
            if (arms.Any()) return arms.RandomElement();

            var face = bodyParts.Where(p => p.def.defName == "Head").ToList();
            if (face.Any() && Rand.Chance(0.2f)) return face.RandomElement();

            return bodyParts.Where(p => !p.def.conceptual).RandomElement();
        }
    }
}