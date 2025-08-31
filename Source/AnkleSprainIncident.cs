using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KitchenFires
{
    public static class AnkleSprainIncidentUtility
    {
        private const float BASE_SPRAIN_CHANCE = 0.00005f; // 0.005% base chance per difficult cell
        //private const float BASE_SPRAIN_CHANCE = 0.50005f; // 0.005% base chance per difficult cell

        // Backward-compatible entry that checks the pawn's current cell
        public static void CheckForAnkleSprain(Pawn pawn)
        {
            CheckForAnkleSprain(pawn, pawn.Position);
        }

        // Preferred entry that evaluates the target cell (e.g., the cell being entered)
        public static void CheckForAnkleSprain(Pawn pawn, IntVec3 cell)
        {
            if (!pawn.IsColonist || pawn.Dead || pawn.Downed) return;
            if (pawn.Map == null) return;

            // Check if there are climbable objects in the cell
            var things = cell.GetThingList(pawn.Map);
            
            bool hasClimbableObjects = HasClimbableObjects(things);
            if (!hasClimbableObjects)
            {
                //Log.Message($"[KitchenFires] AnkleSprain: Skipping at {cell} - no climbable objects found");
                return;
            }

            // First priority: Check for queued storyteller incidents
            if (KitchenIncidentQueue.TryExecuteQueuedIncident(pawn))
            {
                return; // Queued incident was executed
            }

            var riskAssessment = CalculateSprainRisks(pawn, things);
            
            // Reduced chance for immediate incidents (30% of original rate)
            // This maintains some spontaneity while letting storyteller drive most incidents
            float reducedRisk = riskAssessment.SprainRisk * 0.3f;

            if (Rand.Chance(reducedRisk))
            {
                Log.Message($"[KitchenFires] Immediate ankle sprain triggered for {pawn.Name}!");
                TriggerImmediateAnkleSprain(pawn, riskAssessment);
            }
        }


        private static bool HasClimbableObjects(List<Thing> things)
        {
            foreach (Thing thing in things)
            {
                if (thing?.def == null) continue;
                
                // Check for climbable objects using passability and pathCost
                // PassThroughOnly means you can walk through but it slows movement (chunks, debris)
                if (thing.def.passability == Traversability.PassThroughOnly && thing.def.pathCost > 10)
                {
                    return true;
                }
            }
            return false;
        }

        private static AnkleRiskAssessment CalculateSprainRisks(Pawn pawn, List<Thing> things)
        {
            // Calculate base difficulty from climbable objects
            float climbingMultiplier = CalculateClimbingDifficultyMultiplier(things);
            
            // Pawn-specific factors (excluding skills)
            float traitMultiplier = CalculateAnkleTraitMultiplier(pawn);
            float moodMultiplier = CalculateAnkleMoodMultiplier(pawn);
            float ageMultiplier = CalculateAgeMultiplier(pawn);

            float totalMultiplier = climbingMultiplier * traitMultiplier * moodMultiplier * ageMultiplier;

            float risk = BASE_SPRAIN_CHANCE * totalMultiplier;
            // In DevMode, boost risk to aid testing
            if (Prefs.DevMode)
            {
                risk *= 100f;
            }
            risk = Mathf.Clamp01(risk);

            return new AnkleRiskAssessment
            {
                SprainRisk = risk,
                SprainSeverity = CalculateAnkleSprainSeverity(pawn, climbingMultiplier)
            };
        }

        private static float CalculateClimbingDifficultyMultiplier(List<Thing> things)
        {
            float multiplier = 1.0f;
            
            // Check each climbable object and add difficulty based on pathCost and passability
            foreach (Thing thing in things)
            {
                if (thing?.def == null) continue;

                // Higher pathCost = more difficult to climb over
                multiplier += thing.def.pathCost / 20f; // Scale factor for PassThroughOnly objects
                break;
            }

            return multiplier;
        }


        private static float CalculateAnkleTraitMultiplier(Pawn pawn)
        {
            float multiplier = 1.0f;
            
            if (pawn.story?.traits == null) return multiplier;

            foreach (var trait in pawn.story.traits.allTraits)
            {
                // Keep to stable, known trait defs in 1.6
                if (trait.def == TraitDefOf.Brawler)
                    multiplier *= 0.9f; // Brawlers tend to have decent footwork
                else if (trait.def.defName == "Nimble")
                    multiplier *= 0.75f; // Nimble reduces missteps notably
                // Avoid using removed/unstable trait names like CarefulShooter/Jogger/etc.
            }

            // Moving capacity (injuries, prosthetics) affects risk
            float moving = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f;
            float moveCapMul = moving >= 1f
                ? Mathf.Lerp(1.0f, 0.9f, Mathf.Clamp01(moving - 1f)) // bionic/supra-normal: tiny reduction
                : Mathf.Lerp(1.6f, 1.0f, Mathf.Clamp01(moving));     // impaired movement increases risk
            multiplier *= moveCapMul;

            return Mathf.Clamp(multiplier, 0.5f, 1.7f);
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

        private static void TriggerImmediateAnkleSprain(Pawn pawn, AnkleRiskAssessment riskAssessment)
        {
            // Only one type of ankle sprain incident
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("AnkleSprainAccident");
            
            // Create incident parameters
            var parms = new IncidentParms();
            parms.target = pawn.Map;
            parms.forced = true; // Mark as immediate execution
            
            // Store triggering pawn info
            parms.customLetterText = $"triggeringPawn:{pawn.thingIDNumber}";
            
            // Execute through IncidentWorker for proper storyteller integration
            incidentDef.Worker.TryExecute(parms);
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

            // Create ankle sprain hediff if available; otherwise fall back to generic injury
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
            Messages.Message($"{pawn.NameShortColored} sprained their ankle while climbing over an obstacle!", 
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
