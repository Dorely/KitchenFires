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

    // Butchering Accident IncidentWorkers
    public class IncidentWorker_ButcheringCut : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Log.Message($"[KitchenFires] IncidentWorker_ButcheringCut.TryExecuteWorker called - forced: {parms.forced}");
            
            if (!parms.forced)
            {
                // Storyteller selected this incident - queue it for next butchering action
                Log.Message("[KitchenFires] Storyteller selected butchering cut - queueing for next butchering action");
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            // Execute the actual incident
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            // Create cut injury based on butchering accident logic
            var targetPart = GetButcheringBodyPart(triggeringPawn, false); // fingers/hands
            if (targetPart == null) return false;
            
            var injury = HediffMaker.MakeHediff(HediffDefOf.Cut, triggeringPawn, targetPart);
            injury.Severity = Rand.Range(0.1f, 0.4f);
            triggeringPawn.health.AddHediff(injury);
            
            SendStandardLetter(parms, new LookTargets(triggeringPawn), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Butchering cut created for {triggeringPawn.Name} on {targetPart.Label}");
            return true;
        }
        
        private BodyPartRecord GetButcheringBodyPart(Pawn pawn, bool preferHand)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            if (preferHand)
            {
                var hands = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("hand")).ToList();
                if (hands.Any()) return hands.RandomElement();
            }
            
            var fingers = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("finger")).ToList();
            if (fingers.Any()) return fingers.RandomElement();
            
            var hands2 = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("hand")).ToList();
            if (hands2.Any()) return hands2.RandomElement();
            
            return null;
        }
    }

    public class IncidentWorker_ButcheringAmputation : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Log.Message($"[KitchenFires] IncidentWorker_ButcheringAmputation.TryExecuteWorker called - forced: {parms.forced}");
            
            if (!parms.forced)
            {
                // Storyteller selected this incident - queue it for next butchering action
                Log.Message("[KitchenFires] Storyteller selected butchering amputation - queueing for next butchering action");
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            // Execute the actual incident
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            // Create amputation injury - more severe
            var targetPart = GetButcheringBodyPart(triggeringPawn, Rand.Bool); // random hand vs finger
            if (targetPart == null) return false;
            
            var injury = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, triggeringPawn, targetPart);
            injury.Severity = Rand.Range(0.3f, 0.8f);
            triggeringPawn.health.AddHediff(injury);
            
            SendStandardLetter(parms, new LookTargets(triggeringPawn), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Butchering amputation created for {triggeringPawn.Name} on {targetPart.Label}");
            return true;
        }
        
        private BodyPartRecord GetButcheringBodyPart(Pawn pawn, bool preferHand)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            if (preferHand)
            {
                var hands = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("hand")).ToList();
                if (hands.Any()) return hands.RandomElement();
            }
            
            var fingers = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("finger")).ToList();
            if (fingers.Any()) return fingers.RandomElement();
            
            var hands2 = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("hand")).ToList();
            if (hands2.Any()) return hands2.RandomElement();
            
            return null;
        }
    }

    // Ankle Sprain IncidentWorker
    public class IncidentWorker_AnkleSprain : IncidentWorker_KitchenBase
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Log.Message($"[KitchenFires] IncidentWorker_AnkleSprain.TryExecuteWorker called - forced: {parms.forced}");
            
            if (!parms.forced)
            {
                // Storyteller selected this incident - queue it for next movement over obstacles
                Log.Message("[KitchenFires] Storyteller selected ankle sprain - queueing for next obstacle climb");
                KitchenIncidentQueue.Add(def, parms);
                return true;
            }
            
            // Execute the actual incident
            Pawn triggeringPawn = GetTriggeringPawn(parms);
            if (triggeringPawn == null) return false;
            
            // Create ankle sprain injury
            var targetPart = GetAnkleBodyPart(triggeringPawn);
            if (targetPart == null) 
            {
                // Fallback to leg
                targetPart = GetLegBodyPart(triggeringPawn);
                if (targetPart == null) return false;
            }
            
            // Try to use custom AnkleSprain hediff, fallback to bruise
            var sprain = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("AnkleSprain", false), triggeringPawn, targetPart);
            if (sprain == null)
            {
                sprain = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise", false), triggeringPawn, targetPart);
                if (sprain == null) return false;
            }
            
            sprain.Severity = Rand.Range(0.15f, 0.6f);
            triggeringPawn.health.AddHediff(sprain);
            
            SendStandardLetter(parms, new LookTargets(triggeringPawn), triggeringPawn.NameShortColored);
            
            Log.Message($"[KitchenFires] Ankle sprain created for {triggeringPawn.Name} on {targetPart.Label}");
            return true;
        }
        
        private BodyPartRecord GetAnkleBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            var ankles = bodyParts.Where(p => p.def.defName.ToLower().Contains("ankle")).ToList();
            if (ankles.Any()) return ankles.RandomElement();
            
            var feet = bodyParts.Where(p => p.def.defName.ToLower().Contains("foot")).ToList();
            if (feet.Any()) return feet.RandomElement();
            
            return null;
        }
        
        private BodyPartRecord GetLegBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            var legs = bodyParts.Where(p => p.def.defName.ToLower().Contains("leg")).ToList();
            return legs.Any() ? legs.RandomElement() : null;
        }
    }
}