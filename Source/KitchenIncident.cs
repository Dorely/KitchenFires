using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace KitchenFires
{
    public static class KitchenIncidentUtility
    {
        private const float BASE_INCIDENT_CHANCE = 0.00002f; // 0.002% base chance for any incident

        public static void CheckForKitchenIncident(Pawn pawn)
        {
            if (!pawn.IsColonist || pawn.Dead || pawn.Downed) return;

            // First priority: Check for queued storyteller incidents
            if (KitchenIncidentQueue.TryExecuteQueuedIncident(pawn, KitchenIncidentQueue.QueuedIncidentContext.Cooking))
            {
                return; // Queued incident was executed
            }

            var cookingSkill = pawn.skills.GetSkill(SkillDefOf.Cooking);
            var riskAssessment = CalculateIncidentRisk(pawn, cookingSkill);

            // Reduced chance for immediate incidents (30% of original rate)
            // This maintains some spontaneity while letting storyteller drive most incidents
            float reducedRisk = riskAssessment.IncidentRisk * 0.3f * AccidentStormUtility.ChanceMultiplierFor(pawn.Map);

            if (Rand.Chance(reducedRisk))
            {
                Log.Message($"[KitchenFires] Immediate kitchen incident triggered for {pawn.Name}!");
                TriggerImmediateKitchenIncident(pawn, riskAssessment);
                return;
            }

            // Small chance for positive message for skilled cooks
            if (cookingSkill.Level >= 12 && Rand.Chance(0.0001f))
            {
                Messages.Message($"{pawn.NameShortColored} expertly handled the cooking equipment, avoiding any mishaps.", 
                    MessageTypeDefOf.PositiveEvent);
                pawn.skills.Learn(SkillDefOf.Cooking, 25f);
            }
        }

        private static KitchenRiskAssessment CalculateIncidentRisk(Pawn pawn, SkillRecord cookingSkill)
        {
            float skillMultiplier = CalculateSkillMultiplier(cookingSkill.Level);
            float passionMultiplier = CalculatePassionMultiplier(cookingSkill.passion);
            float moodMultiplier = CalculateMoodMultiplier(pawn);
            float traitMultiplier = CalculateTraitMultiplier(pawn);

            float totalMultiplier = skillMultiplier * passionMultiplier * moodMultiplier * traitMultiplier;

            // Calculate base incident risk
            float incidentRisk = BASE_INCIDENT_CHANCE * totalMultiplier;
            incidentRisk = Mathf.Clamp01(incidentRisk);

            return new KitchenRiskAssessment
            {
                IncidentRisk = incidentRisk,
                SeverityMultiplier = totalMultiplier,
                SkillLevel = cookingSkill.Level
            };
        }

        private static float CalculateSkillMultiplier(int skillLevel)
        {
            // Level 0-2: 3x risk, Level 3-5: 2x risk, Level 6-9: 1x risk, Level 10+: 0.5x risk
            if (skillLevel <= 2) return 3.0f;
            if (skillLevel <= 5) return 2.0f;
            if (skillLevel <= 9) return 1.0f;
            return 0.5f - (skillLevel - 10) * 0.03f; // Further reduction for higher levels
        }

        private static float CalculatePassionMultiplier(Passion passion)
        {
            switch (passion)
            {
                case Passion.None:
                    return 1.5f;      // Less careful when not passionate
                case Passion.Minor:
                    return 1.0f;     // Normal risk
                case Passion.Major:
                    return 0.7f;     // More careful when passionate
                default:
                    return 1.0f;
            }
        }

        private static float CalculateMoodMultiplier(Pawn pawn)
        {
            if (pawn.needs?.mood == null) return 1.0f;
            
            float moodLevel = pawn.needs.mood.CurLevelPercentage;
            // Bad mood increases risk: 0% mood = 2x risk, 100% mood = 0.8x risk
            return Mathf.Lerp(2.0f, 0.8f, moodLevel);
        }

        private static float CalculateTraitMultiplier(Pawn pawn)
        {
            float multiplier = 1.0f;
            
            if (pawn.story?.traits == null) return multiplier;

            // Check for traits that exist - using safer approach
            foreach (var trait in pawn.story.traits.allTraits)
            {
                if (trait.def.defName == "Careful")
                    multiplier *= 0.6f;
                else if (trait.def.defName == "Neurotic")
                    multiplier *= 1.2f;
                else if (trait.def == TraitDefOf.Pyromaniac)
                    multiplier *= 1.8f;
            }

            return multiplier;
        }

        private static void TriggerImmediateKitchenIncident(Pawn pawn, KitchenRiskAssessment riskAssessment)
        {
            // Select incident type based on severity roll
            float severityRoll = Rand.Value;
            IncidentDef incidentDef;
            
            if (severityRoll >= 0.95f)
            {
                incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenExplosion");
            }
            else if (severityRoll >= 0.80f)
            {
                incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenFire_Large");
            }
            else if (severityRoll >= 0.50f)
            {
                incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenFire_Small");
            }
            else
            {
                incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenBurn");
            }
            
            // Create incident parameters
            var parms = new IncidentParms();
            parms.target = pawn.Map;
            parms.forced = true; // Mark as immediate execution
            
            // Store triggering pawn info in custom text for now
            parms.customLetterText = $"triggeringPawn:{pawn.thingIDNumber}";
            
            // Execute through IncidentWorker for proper storyteller integration
            incidentDef.Worker.TryExecute(parms);
        }

        private static void TriggerKitchenIncident(Pawn pawn, KitchenRiskAssessment riskAssessment)
        {
            // Pure random roll for incident severity - skill only affects occurrence probability
            float severityRoll = Rand.Value;
            
            // Determine incident type based on fixed severity thresholds
            if (severityRoll >= 0.95f)
            {
                // 5% chance: Small explosion
                TriggerKitchenExplosion(pawn);
            }
            else if (severityRoll >= 0.80f)
            {
                // 15% chance: Large fire
                TriggerKitchenFire(pawn, Rand.Range(0.6f, 1.0f), true);
            }
            else if (severityRoll >= 0.50f)
            {
                // 30% chance: Small fire
                TriggerKitchenFire(pawn, Rand.Range(0.3f, 0.6f), false);
            }
            else
            {
                // 50% chance: Minor burn
                TriggerBurnInjury(pawn, CalculateBurnSeverity(riskAssessment.SkillLevel));
            }
        }
        
        private static float CalculateBurnSeverity(int skillLevel)
        {
            // Lower skill = potentially more severe burns
            float maxSeverity = Mathf.Lerp(0.4f, 0.15f, skillLevel / 20f);
            float minSeverity = 0.05f;
            return Rand.Range(minSeverity, maxSeverity);
        }

        private static void TriggerKitchenFire(Pawn pawn, float severity, bool isLarge = false)
        {
            Map map = pawn.Map;
            if (map == null) return;

            // Find suitable spots near the pawn for the fire
            IntVec3 firePos = pawn.Position;
            var fireCells = new List<IntVec3> { firePos };
            
            // Try to find nearby cooking stations or flammable objects
            var nearbyBuildings = GenRadial.RadialCellsAround(pawn.Position, 2, true)
                .Where(c => c.InBounds(map) && c.GetFirstBuilding(map) != null)
                .Where(c => IsCookingRelatedBuilding(c.GetFirstBuilding(map)))
                .ToList();

            if (nearbyBuildings.Any())
            {
                firePos = nearbyBuildings.RandomElement();
                fireCells[0] = firePos;
            }

            // For large fires, create multiple fire spots
            if (isLarge)
            {
                var additionalCells = GenRadial.RadialCellsAround(firePos, 1, true)
                    .Where(c => c.InBounds(map) && c.Standable(map))
                    .Take(Rand.Range(1, 3))
                    .ToList();
                fireCells.AddRange(additionalCells);
            }

            // Create the fire(s)
            Fire primaryFire = null;
            foreach (var cell in fireCells)
            {
                Fire fire = (Fire)GenSpawn.Spawn(ThingDefOf.Fire, cell, map);
                fire.fireSize = severity * Rand.Range(0.8f, 1.2f);
                if (primaryFire == null) primaryFire = fire;
            }

            // Send message
            string severityDesc = isLarge ? "large" : severity > 0.4f ? "moderate" : "small";
            Messages.Message($"A {severityDesc} kitchen fire started while {pawn.NameShortColored} was cooking!", 
                new LookTargets(primaryFire), MessageTypeDefOf.NegativeEvent);

            Log.Message($"[KitchenFires] Kitchen fire triggered for {pawn.Name} with severity {severity:F2}, large: {isLarge}");
        }

        private static void TriggerKitchenExplosion(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) return;

            // Find the best spot for the explosion (cooking station if available)
            IntVec3 explosionPos = pawn.Position;
            var nearbyBuildings = GenRadial.RadialCellsAround(pawn.Position, 2, true)
                .Where(c => c.InBounds(map) && c.GetFirstBuilding(map) != null)
                .Where(c => IsCookingRelatedBuilding(c.GetFirstBuilding(map)))
                .ToList();

            if (nearbyBuildings.Any())
            {
                explosionPos = nearbyBuildings.RandomElement();
            }

            // Create a small explosion - similar to a chemfuel explosion but smaller
            float explosionRadius = Rand.Range(1.5f, 2.5f);
            GenExplosion.DoExplosion(
                center: explosionPos,
                map: map,
                radius: explosionRadius,
                damType: DamageDefOf.Flame,
                instigator: pawn,
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

            Messages.Message($"A small explosion erupted from the cooking equipment while {pawn.NameShortColored} was working!", 
                new LookTargets(explosionPos, map), MessageTypeDefOf.NegativeEvent);

            Log.Message($"[KitchenFires] Kitchen explosion triggered for {pawn.Name} at {explosionPos}");
        }

        private static void TriggerBurnInjury(Pawn pawn, float severity)
        {
            // Choose a body part for the burn (hands are most likely)
            BodyPartRecord targetPart = GetLikelyBurnBodyPart(pawn);
            if (targetPart == null) return;

            // Find the correct burn hediff
            HediffDef burnDef = DefDatabase<HediffDef>.AllDefs.FirstOrDefault(h => h.defName == "Burn");
            if (burnDef == null) return; // Safety check

            // Create burn injury
            var injury = HediffMaker.MakeHediff(burnDef, pawn, targetPart);
            injury.Severity = severity;
            pawn.health.AddHediff(injury);

            // Send message
            string severityDesc = severity > 0.3f ? "serious" : severity > 0.15f ? "moderate" : "minor";
            Messages.Message($"{pawn.NameShortColored} suffered a {severityDesc} burn while cooking!", 
                new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);

            Log.Message($"[KitchenFires] Burn injury triggered for {pawn.Name} on {targetPart.Label} with severity {severity:F2}");
        }

        private static BodyPartRecord GetLikelyBurnBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            // Prefer hands, then arms, then other parts
            var hands = bodyParts.Where(p => p.def.defName == "Hand").ToList();
            if (hands.Any()) return hands.RandomElement();

            var arms = bodyParts.Where(p => p.def.defName == "Arm").ToList();
            if (arms.Any()) return arms.RandomElement();

            var face = bodyParts.Where(p => p.def.defName == "Head").ToList();
            if (face.Any() && Rand.Chance(0.2f)) return face.RandomElement(); // 20% chance for face burns

            return bodyParts.Where(p => !p.def.conceptual).RandomElement();
        }

        private static bool IsCookingRelatedBuilding(Building building)
        {
            if (building?.def == null) return false;
            
            string defName = building.def.defName.ToLower();
            return defName.Contains("stove") || defName.Contains("grill") || 
                   defName.Contains("kitchen") || defName.Contains("cook") ||
                   building.def.building?.isMealSource == true;
        }

        public static bool IsCookingJob(JobDef jobDef)
        {
            if (jobDef == null) return false;
            return jobDef.defName.ToLower().Contains("cook") ||
                   jobDef.defName.ToLower().Contains("meal");
        }

        public static bool IsCookingRecipe(RecipeDef recipe)
        {
            if (recipe == null) return false;
            return recipe.workSkill == SkillDefOf.Cooking ||
                   recipe.defName.ToLower().Contains("cook") ||
                   recipe.defName.ToLower().Contains("meal");
        }
    }


    public struct KitchenRiskAssessment
    {
        public float IncidentRisk;
        public float SeverityMultiplier;
        public int SkillLevel;
    }
}