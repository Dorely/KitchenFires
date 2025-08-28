using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace KitchenFires
{
    [StaticConstructorOnStartup]
    public class KitchenFiresMod
    {
        static KitchenFiresMod()
        {
            var harmony = new Harmony("com.kitchenfires.mod");
            harmony.PatchAll();
            Log.Message("[KitchenFires] Mod initialized with Harmony patches.");
        }
    }

    public static class KitchenIncidentUtility
    {
        private const float BASE_FIRE_CHANCE = 0.00008f; // 0.008% base chance
        private const float BASE_BURN_CHANCE = 0.00012f; // 0.012% base chance
        //private const float BASE_FIRE_CHANCE = 0.10008f; // 0.008% base chance
        //private const float BASE_BURN_CHANCE = 0.10012f; // 0.012% base chance

        public static void CheckForKitchenIncident(Pawn pawn)
        {
            if (!pawn.IsColonist || pawn.Dead || pawn.Downed) return;

            var cookingSkill = pawn.skills.GetSkill(SkillDefOf.Cooking);
            var riskAssessment = CalculateRisks(pawn, cookingSkill);

            // Debug logging
            Log.Message($"[KitchenFires] Risk check for {pawn.Name}: Fire={riskAssessment.FireRisk:P}, Burn={riskAssessment.BurnRisk:P}, Skill={cookingSkill.Level}");

            // Check for kitchen fire first (more dramatic)
            if (Rand.Chance(riskAssessment.FireRisk))
            {
                Log.Message($"[KitchenFires] Fire incident triggered for {pawn.Name}!");
                TriggerKitchenFire(pawn, riskAssessment.FireSeverity);
                return;
            }

            // Check for burn injury
            if (Rand.Chance(riskAssessment.BurnRisk))
            {
                Log.Message($"[KitchenFires] Burn incident triggered for {pawn.Name}!");
                TriggerBurnInjury(pawn, riskAssessment.BurnSeverity);
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

        private static KitchenRiskAssessment CalculateRisks(Pawn pawn, SkillRecord cookingSkill)
        {
            float skillMultiplier = CalculateSkillMultiplier(cookingSkill.Level);
            float passionMultiplier = CalculatePassionMultiplier(cookingSkill.passion);
            float moodMultiplier = CalculateMoodMultiplier(pawn);
            float traitMultiplier = CalculateTraitMultiplier(pawn);

            float totalMultiplier = skillMultiplier * passionMultiplier * moodMultiplier * traitMultiplier;

            return new KitchenRiskAssessment
            {
                FireRisk = BASE_FIRE_CHANCE * totalMultiplier,
                BurnRisk = BASE_BURN_CHANCE * totalMultiplier,
                FireSeverity = CalculateFireSeverity(cookingSkill.Level),
                BurnSeverity = CalculateBurnSeverity(cookingSkill.Level)
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

        private static float CalculateFireSeverity(int skillLevel)
        {
            // Beginners: larger fires (0.3-0.8), experts: tiny fires (0.1-0.3)
            float maxSeverity = Mathf.Lerp(0.8f, 0.3f, skillLevel / 20f);
            float minSeverity = Mathf.Lerp(0.3f, 0.1f, skillLevel / 20f);
            return Rand.Range(minSeverity, maxSeverity);
        }

        private static float CalculateBurnSeverity(int skillLevel)
        {
            // Lower skill = potentially more severe burns
            float maxSeverity = Mathf.Lerp(0.4f, 0.15f, skillLevel / 20f);
            float minSeverity = 0.05f;
            return Rand.Range(minSeverity, maxSeverity);
        }

        private static void TriggerKitchenFire(Pawn pawn, float severity)
        {
            Map map = pawn.Map;
            if (map == null) return;

            // Find a suitable spot near the pawn for the fire
            IntVec3 firePos = pawn.Position;
            
            // Try to find a nearby cooking station or flammable object
            var nearbyBuildings = GenRadial.RadialCellsAround(pawn.Position, 2, true)
                .Where(c => c.InBounds(map) && c.GetFirstBuilding(map) != null)
                .Where(c => IsCookingRelatedBuilding(c.GetFirstBuilding(map)))
                .ToList();

            if (nearbyBuildings.Any())
            {
                firePos = nearbyBuildings.RandomElement();
            }

            // Create the fire
            Fire fire = (Fire)GenSpawn.Spawn(ThingDefOf.Fire, firePos, map);
            fire.fireSize = severity;

            // Send message
            string severityDesc = severity > 0.6f ? "large" : severity > 0.3f ? "moderate" : "small";
            Messages.Message($"A {severityDesc} kitchen fire started while {pawn.NameShortColored} was cooking!", 
                new LookTargets(fire), MessageTypeDefOf.NegativeEvent);

            Log.Message($"[KitchenFires] Kitchen fire triggered for {pawn.Name} with severity {severity:F2}");
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
        public float FireRisk;
        public float BurnRisk;
        public float FireSeverity;
        public float BurnSeverity;
    }

    // Harmony Patch - Patch the actual recipe work method that runs during cooking
    [HarmonyPatch(typeof(Toils_Recipe), "DoRecipeWork")]
    static class Toils_Recipe_Patch
    {
        static void Postfix(ref Toil __result)
        {
            try
            {
                // Store references to avoid ref parameter issues
                var toil = __result;
                var originalTickInterval = toil.tickIntervalAction;
                
                // Replace with our enhanced version
                toil.tickIntervalAction = delegate(int delta)
                {
                    try
                    {
                        // Call the original tick interval action first
                        originalTickInterval?.Invoke(delta);
                        
                        // Our incident check logic
                        Pawn actor = toil.actor;
                        if (actor != null && actor.IsColonist && actor.jobs.curDriver is JobDriver_DoBill doBillDriver)
                        {
                            Log.Message($"[KitchenFires] Recipe work tick for {actor.Name}");
                            
                            var bill = doBillDriver.job?.bill;
                            if (bill?.recipe != null && KitchenIncidentUtility.IsCookingRecipe(bill.recipe))
                            {
                                Log.Message($"[KitchenFires] Cooking recipe work: {bill.recipe.defName} for {actor.Name}");
                                KitchenIncidentUtility.CheckForKitchenIncident(actor);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[KitchenFires] Error in recipe work tick: {ex}");
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[KitchenFires] Error in Toils_Recipe patch: {ex}");
            }
        }
    }
}