using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KitchenFires
{
    public static class AnkleSprainIncidentUtility
    {
        private const float BASE_SPRAIN_CHANCE = 1.00005f; // 0.005% base chance per difficult cell

        public static void CheckForAnkleSprain(Pawn pawn)
        {
            if (!pawn.IsColonist || pawn.Dead || pawn.Downed) return;

            // Check if the terrain or obstacles make movement difficult
            TerrainDef terrain = pawn.Position.GetTerrain(pawn.Map);
            var buildings = pawn.Position.GetThingList(pawn.Map);
            
            bool isDifficultTerrain = IsTerrainDifficult(terrain) || HasObstacles(buildings);
            if (!isDifficultTerrain) return;

            var riskAssessment = CalculateSprinRisks(pawn, terrain, buildings);
            
            // Debug logging
            Log.Message($"[KitchenFires] Ankle sprain risk check for {pawn.Name}: Risk={riskAssessment.SprainRisk:P}");

            if (Rand.Chance(riskAssessment.SprainRisk))
            {
                Log.Message($"[KitchenFires] Ankle sprain triggered for {pawn.Name}!");
                TriggerAnkleSprain(pawn, riskAssessment.SprainSeverity);
            }
        }

        private static bool IsTerrainDifficult(TerrainDef terrain)
        {
            // Check if terrain has movement penalties
            return terrain.extraDraftedPerceivedPathCost > 0 ||
                   terrain.extraNonDraftedPerceivedPathCost > 0 ||
                   terrain.HasTag("Rough") ||
                   terrain.HasTag("Sand") ||
                   terrain.HasTag("Mud");
        }

        private static bool HasObstacles(List<Thing> things)
        {
            foreach (Thing thing in things)
            {
                // Check for buildings that slow movement but are passable
                if (thing is Building building && building.def.passability == Traversability.Standable)
                {
                    if (building.def.defName.ToLower().Contains("chunk") ||
                        building.def.defName.ToLower().Contains("debris") ||
                        building.def.pathCost > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static AnkleRiskAssessment CalculateSprinRisks(Pawn pawn, TerrainDef terrain, List<Thing> buildings)
        {
            // Calculate base difficulty from terrain and obstacles
            float terrainMultiplier = CalculateTerrainDifficultyMultiplier(terrain, buildings);
            
            // Pawn-specific factors
            float skillMultiplier = CalculateMovementSkillMultiplier(pawn);
            float traitMultiplier = CalculateAnkleTraitMultiplier(pawn);
            float moodMultiplier = CalculateAnkleMoodMultiplier(pawn);
            float ageMultiplier = CalculateAgeMultiplier(pawn);

            float totalMultiplier = terrainMultiplier * skillMultiplier * traitMultiplier * moodMultiplier * ageMultiplier;

            return new AnkleRiskAssessment
            {
                SprainRisk = BASE_SPRAIN_CHANCE * totalMultiplier,
                SprainSeverity = CalculateAnkleSprainSeverity(pawn, terrainMultiplier)
            };
        }

        private static float CalculateTerrainDifficultyMultiplier(TerrainDef terrain, List<Thing> buildings)
        {
            float multiplier = 1.0f;
            
            // Terrain difficulty
            if (terrain.extraDraftedPerceivedPathCost > 0 || terrain.extraNonDraftedPerceivedPathCost > 0)
                multiplier += 2.0f;
            
            if (terrain.HasTag("Rough"))
                multiplier += 1.5f;
            
            if (terrain.HasTag("Sand") || terrain.HasTag("Mud"))
                multiplier += 1.0f;

            // Building obstacles
            foreach (Thing thing in buildings)
            {
                if (thing is Building building && building.def.pathCost > 0)
                {
                    multiplier += building.def.pathCost / 50f; // Scale path cost
                    break; // Only count one obstacle per cell
                }
            }

            return multiplier;
        }

        private static float CalculateMovementSkillMultiplier(Pawn pawn)
        {
            // Use mining skill as a proxy for physical coordination/sure-footedness
            var miningSkill = pawn.skills?.GetSkill(SkillDefOf.Mining);
            if (miningSkill == null) return 1.0f;

            // Higher skill = less risk
            if (miningSkill.Level >= 10) return 0.5f;
            if (miningSkill.Level >= 6) return 0.7f;
            if (miningSkill.Level >= 3) return 1.0f;
            return 1.5f; // Unskilled pawns are more clumsy
        }

        private static float CalculateAnkleTraitMultiplier(Pawn pawn)
        {
            float multiplier = 1.0f;
            
            if (pawn.story?.traits == null) return multiplier;

            foreach (var trait in pawn.story.traits.allTraits)
            {
                // Use traits that definitely exist in RimWorld
                if (trait.def == TraitDefOf.Brawler)
                    multiplier *= 0.8f; // Brawlers are physically coordinated
                else if (trait.def.defName == "Careful")
                    multiplier *= 0.7f; // Careful pawns watch their step
                else if (trait.def.defName == "Nimble")
                    multiplier *= 0.6f; // Nimble pawns less likely to trip
                else if (trait.def.defName == "SlowWalker")
                    multiplier *= 1.4f; // Slow walkers more prone to accidents
            }

            return multiplier;
        }

        private static float CalculateAnkleMoodMultiplier(Pawn pawn)
        {
            if (pawn.needs?.mood == null) return 1.0f;
            
            float moodLevel = pawn.needs.mood.CurLevelPercentage;
            // Bad mood = less careful: 0% mood = 1.5x risk, 100% mood = 0.8x risk
            return Mathf.Lerp(1.5f, 0.8f, moodLevel);
        }

        private static float CalculateAgeMultiplier(Pawn pawn)
        {
            if (pawn.ageTracker == null) return 1.0f;
            
            long ageInTicks = pawn.ageTracker.AgeBiologicalTicks;
            float ageInYears = ageInTicks / (float)GenDate.TicksPerYear;
            
            // Young adults (18-40) are most stable, children and elderly more prone to injury
            if (ageInYears < 16) return 1.3f; // Children less coordinated
            if (ageInYears > 50) return 1.0f + (ageInYears - 50) * 0.02f; // Increasing risk with age
            return 1.0f; // Prime age
        }

        private static float CalculateAnkleSprainSeverity(Pawn pawn, float terrainMultiplier)
        {
            // Base severity depends on terrain difficulty and age
            float baseSeverity = Rand.Range(0.15f, 0.4f);
            
            // Terrain makes it worse
            baseSeverity *= (1.0f + terrainMultiplier * 0.1f);
            
            // Age affects severity
            if (pawn.ageTracker != null)
            {
                float ageInYears = pawn.ageTracker.AgeBiologicalTicks / (float)GenDate.TicksPerYear;
                if (ageInYears > 50)
                    baseSeverity *= 1.0f + (ageInYears - 50) * 0.01f;
            }
            
            return Mathf.Clamp(baseSeverity, 0.1f, 0.6f);
        }

        private static void TriggerAnkleSprain(Pawn pawn, float severity)
        {
            // Find the ankle/foot body part
            BodyPartRecord targetPart = GetAnkleBodyPart(pawn);
            if (targetPart == null)
            {
                // Fall back to leg if no ankle found
                targetPart = GetLegBodyPart(pawn);
                if (targetPart == null) return;
            }

            // Create ankle sprain hediff (we'll create a custom one)
            var sprain = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("AnkleSprain", false), pawn, targetPart);
            if (sprain == null)
            {
                // Fallback to generic injury if custom hediff doesn't exist
                sprain = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise", false), pawn, targetPart);
                if (sprain == null) return;
            }

            sprain.Severity = severity;
            pawn.health.AddHediff(sprain);

            // Send message
            string severityDesc = severity > 0.4f ? "severe" : severity > 0.25f ? "moderate" : "mild";
            Messages.Message($"{pawn.NameShortColored} sprained their ankle while climbing over obstacles!", 
                new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);

            Log.Message($"[KitchenFires] Ankle sprain triggered for {pawn.Name} on {targetPart.Label} with severity {severity:F2}");
        }

        private static BodyPartRecord GetAnkleBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            
            // Look for ankle specifically
            var ankles = bodyParts.Where(p => p.def.defName.ToLower().Contains("ankle")).ToList();
            if (ankles.Any()) return ankles.RandomElement();
            
            // Look for foot as close alternative
            var feet = bodyParts.Where(p => p.def.defName.ToLower().Contains("foot")).ToList();
            if (feet.Any()) return feet.RandomElement();
            
            return null;
        }

        private static BodyPartRecord GetLegBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            var legs = bodyParts.Where(p => p.def.defName.ToLower().Contains("leg")).ToList();
            return legs.Any() ? legs.RandomElement() : null;
        }
    }

    public struct AnkleRiskAssessment
    {
        public float SprainRisk;
        public float SprainSeverity;
    }
}