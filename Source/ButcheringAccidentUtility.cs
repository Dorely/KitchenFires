using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KitchenFires
{
    public static class ButcheringAccidentUtility
    {
        private const float BASE_ACCIDENT_CHANCE = 0.00005f; // 0.005% base chance per butchering action

        public static void CheckForButcheringAccident(Pawn pawn, RecipeDef recipe)
        {
            if (!pawn.IsColonist || pawn.Dead || pawn.Downed) return;
            if (recipe == null) return;

            // Only trigger on butchering recipes
            if (!IsButcheringRecipe(recipe)) return;

            var riskAssessment = CalculateAccidentRisk(pawn, recipe);

            // Debug logging
            Log.Message($"[KitchenFires] Butchering accident risk check for {pawn.Name}: Risk={riskAssessment.AccidentRisk:P}");

            if (Rand.Chance(riskAssessment.AccidentRisk))
            {
                Log.Message($"[KitchenFires] Butchering accident triggered for {pawn.Name}!");
                TriggerButcheringAccident(pawn, riskAssessment.AccidentSeverity);
            }
        }

        public static bool IsButcheringRecipe(RecipeDef recipe)
        {
            // Check if recipe has butchery special products
            if (recipe.specialProducts != null && recipe.specialProducts.Contains(SpecialProductType.Butchery))
                return true;

            // Check recipe def name patterns
            string defName = recipe.defName?.ToLowerInvariant() ?? "";
            return defName.Contains("butcher");
        }

        private static ButcheringRiskAssessment CalculateAccidentRisk(Pawn pawn, RecipeDef recipe)
        {
            // Calculate base risk factors
            float skillMultiplier = CalculateSkillMultiplier(pawn);
            float traitMultiplier = CalculateTraitMultiplier(pawn);
            float healthMultiplier = CalculateHealthMultiplier(pawn);
            float moodMultiplier = CalculateMoodMultiplier(pawn);
            float recipeMultiplier = CalculateRecipeMultiplier(recipe);

            float totalMultiplier = skillMultiplier * traitMultiplier * healthMultiplier * moodMultiplier * recipeMultiplier;

            float risk = BASE_ACCIDENT_CHANCE * totalMultiplier;
            
            // In DevMode, boost risk to aid testing
            if (Prefs.DevMode)
            {
                risk *= 100f;
            }
            
            risk = Mathf.Clamp01(risk);

            return new ButcheringRiskAssessment
            {
                AccidentRisk = risk,
                AccidentSeverity = CalculateAccidentSeverity(pawn, totalMultiplier)
            };
        }

        private static float CalculateSkillMultiplier(Pawn pawn)
        {
            if (pawn.skills == null) return 1.5f; // Higher risk if no skills

            var cookingSkill = pawn.skills.GetSkill(SkillDefOf.Cooking);
            if (cookingSkill == null) return 1.5f;

            int level = cookingSkill.Level;
            // Higher cooking skill = lower accident risk
            if (level >= 15) return 0.3f;      // Expert
            if (level >= 10) return 0.5f;      // Good
            if (level >= 5) return 0.8f;       // Average  
            return 1.8f;                       // Poor
        }

        private static float CalculateTraitMultiplier(Pawn pawn)
        {
            float multiplier = 1.0f;
            
            if (pawn.story?.traits == null) return multiplier;

            foreach (var trait in pawn.story.traits.allTraits)
            {
                // Clumsy/careless traits increase risk
                if (trait.def.defName == "Neurotic")
                    multiplier *= 1.3f;
                else if (trait.def == TraitDefOf.Brawler)
                    multiplier *= 0.9f; // Better with hands/weapons
                // Could add more trait checks here
            }

            return Mathf.Clamp(multiplier, 0.5f, 2.0f);
        }

        private static float CalculateHealthMultiplier(Pawn pawn)
        {
            float multiplier = 1.0f;

            if (pawn.health?.capacities == null) return multiplier;

            // Manipulation affects butchering precision
            float manipulation = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
            if (manipulation < 0.8f)
                multiplier *= Mathf.Lerp(2.0f, 1.0f, manipulation / 0.8f);
            else if (manipulation > 1.0f)
                multiplier *= Mathf.Lerp(1.0f, 0.8f, Mathf.Clamp01((manipulation - 1.0f) / 0.5f));

            // Consciousness affects general coordination
            float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            if (consciousness < 1.0f)
                multiplier *= Mathf.Lerp(1.8f, 1.0f, consciousness);

            return Mathf.Clamp(multiplier, 0.5f, 3.0f);
        }

        private static float CalculateMoodMultiplier(Pawn pawn)
        {
            if (pawn.needs?.mood == null) return 1.0f;
            
            float moodLevel = pawn.needs.mood.CurLevelPercentage;
            // Bad mood = less careful: 0% mood = 1.4x risk, 100% mood = 0.9x risk
            return Mathf.Lerp(1.4f, 0.9f, moodLevel);
        }

        private static float CalculateRecipeMultiplier(RecipeDef recipe)
        {
            // Larger animals are harder to butcher safely
            // We can't easily get the corpse size, so use work amount as proxy
            float workAmount = recipe.workAmount;
            
            if (workAmount > 1000f) return 1.3f;      // Large animals
            if (workAmount > 500f) return 1.1f;       // Medium animals  
            return 1.0f;                               // Small animals
        }

        private static float CalculateAccidentSeverity(Pawn pawn, float riskMultiplier)
        {
            // Base severity 
            float severity = Rand.Range(0.1f, 0.4f);
            
            // Risk factors make injuries worse
            severity *= (1.0f + riskMultiplier * 0.1f);
            
            return Mathf.Clamp(severity, 0.05f, 0.8f);
        }

        private static void TriggerButcheringAccident(Pawn pawn, float severity)
        {
            // Determine which type of injury based on severity
            bool isHandInjury = severity > 0.4f;
            
            BodyPartRecord targetPart = GetTargetBodyPart(pawn, isHandInjury);
            if (targetPart == null) return;

            // Create appropriate injury
            HediffDef injuryDef = isHandInjury ? HediffDefOf.Cut : HediffDefOf.Cut; // Could be MissingBodyPart for severe
            var injury = HediffMaker.MakeHediff(injuryDef, pawn, targetPart);
            
            if (severity > 0.6f && isHandInjury)
            {
                // Very severe - amputation
                injury = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, targetPart);
            }
            
            injury.Severity = severity;
            pawn.health.AddHediff(injury);

            // Send message
            string bodyPartName = isHandInjury ? "hand" : "finger";
            string injuryType = injury.def == HediffDefOf.MissingBodyPart ? "severed" : "cut";
            
            Messages.Message($"{pawn.NameShortColored} {injuryType} their {bodyPartName} while butchering!",
                new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);

            Log.Message($"[KitchenFires] Butchering accident for {pawn.Name}: {injuryType} {targetPart.Label} with severity {severity:F2}");
        }

        private static BodyPartRecord GetTargetBodyPart(Pawn pawn, bool preferHand)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            if (preferHand)
            {
                // Look for hands first
                var hands = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("hand")).ToList();
                if (hands.Any()) return hands.RandomElement();
            }
            
            // Look for fingers
            var fingers = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("finger")).ToList();
            if (fingers.Any()) return fingers.RandomElement();
            
            // Fallback to any hand if no fingers found
            var hands2 = bodyParts.Where(p => p.def.defName.ToLowerInvariant().Contains("hand")).ToList();
            if (hands2.Any()) return hands2.RandomElement();
            
            return null;
        }
    }

    public struct ButcheringRiskAssessment
    {
        public float AccidentRisk;
        public float AccidentSeverity;
    }
}