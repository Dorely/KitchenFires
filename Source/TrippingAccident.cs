using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace KitchenFires
{
    public static class TrippingAccidentUtility
    {
        private const float BASE_SPRAIN_CHANCE = 0.00005f; // 0.005% base chance per difficult cell
        //private const float BASE_SPRAIN_CHANCE = 0.50005f; // testing value

        // Backward-compatible entry that checks the pawn's current cell
        public static void CheckForTrippingAccident(Pawn pawn)
        {
            CheckForTrippingAccident(pawn, pawn.Position);
        }

        // Preferred entry that evaluates the target cell (e.g., the cell being entered)
        public static void CheckForTrippingAccident(Pawn pawn, IntVec3 cell)
        {
            if (!pawn.IsColonist || pawn.Dead || pawn.Downed) return;
            if (pawn.Map == null) return;

            var things = cell.GetThingList(pawn.Map);
            bool hasClimbableObjects = HasClimbableObjects(things);
            if (!hasClimbableObjects)
                return;

            if (KitchenIncidentQueue.TryExecuteQueuedIncident(pawn, KitchenIncidentQueue.QueuedIncidentContext.Movement))
                return;

            var riskAssessment = CalculateSprainRisks(pawn, things);
            float reducedRisk = riskAssessment.SprainRisk * 0.3f;

            if (Rand.Chance(reducedRisk))
            {
                Log.Message($"[KitchenFires] Immediate tripping accident triggered for {pawn.Name}!");
                HandleTripAccident(pawn, cell, riskAssessment);
            }
        }

        private static bool HasClimbableObjects(List<Thing> things)
        {
            foreach (Thing thing in things)
            {
                if (thing?.def == null) continue;
                if (thing.def.passability == Traversability.PassThroughOnly && thing.def.pathCost > 10)
                    return true;
            }
            return false;
        }

        private static AnkleRiskAssessment CalculateSprainRisks(Pawn pawn, List<Thing> things)
        {
            float climbingMultiplier = CalculateClimbingDifficultyMultiplier(things);
            float traitMultiplier = CalculateAnkleTraitMultiplier(pawn);
            float moodMultiplier = CalculateAnkleMoodMultiplier(pawn);
            float ageMultiplier = CalculateAgeMultiplier(pawn);

            float totalMultiplier = climbingMultiplier * traitMultiplier * moodMultiplier * ageMultiplier;

            float risk = BASE_SPRAIN_CHANCE * totalMultiplier;
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
            foreach (Thing thing in things)
            {
                if (thing?.def == null) continue;
                multiplier += thing.def.pathCost / 20f;
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
                if (trait.def == TraitDefOf.Brawler)
                    multiplier *= 0.9f;
                else if (trait.def.defName == "Nimble")
                    multiplier *= 0.75f;
            }

            float moving = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f;
            float moveCapMul = moving >= 1f
                ? Mathf.Lerp(1.0f, 0.9f, Mathf.Clamp01(moving - 1f))
                : Mathf.Lerp(1.6f, 1.0f, Mathf.Clamp01(moving));
            multiplier *= moveCapMul;

            return Mathf.Clamp(multiplier, 0.5f, 1.7f);
        }

        private static float CalculateAnkleMoodMultiplier(Pawn pawn)
        {
            if (pawn.needs?.mood == null) return 1.0f;
            float moodLevel = pawn.needs.mood.CurLevelPercentage;
            return Mathf.Lerp(1.5f, 0.8f, moodLevel);
        }

        private static float CalculateAgeMultiplier(Pawn pawn)
        {
            if (pawn.ageTracker == null) return 1.0f;
            long ageInTicks = pawn.ageTracker.AgeBiologicalTicks;
            float ageInYears = ageInTicks / (float)GenDate.TicksPerYear;
            if (ageInYears < 16) return 1.3f;
            if (ageInYears > 50) return 1.0f + (ageInYears - 50) * 0.02f;
            return 1.0f;
        }

        private static float CalculateAnkleSprainSeverity(Pawn pawn, float terrainMultiplier)
        {
            float baseSeverity = Rand.Range(0.15f, 0.4f);
            baseSeverity *= (1.0f + terrainMultiplier * 0.1f);

            float moving = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f;
            float movingFactor = moving >= 1f
                ? Mathf.Lerp(1.0f, 0.9f, Mathf.Clamp01(moving - 1f))
                : Mathf.Lerp(1.3f, 1.0f, Mathf.Clamp01(moving));
            baseSeverity *= movingFactor;

            int melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 5;
            float prowessFactor = Mathf.Lerp(1.1f, 0.85f, Mathf.Clamp01(melee / 20f));
            baseSeverity *= prowessFactor;

            if (pawn.ageTracker != null)
            {
                float ageInYears = pawn.ageTracker.AgeBiologicalTicks / (float)GenDate.TicksPerYear;
                if (ageInYears > 50)
                    baseSeverity *= 1.0f + (ageInYears - 50) * 0.01f;
            }

            return Mathf.Clamp(baseSeverity, 0.1f, 0.6f);
        }

        private static void HandleTripAccident(Pawn pawn, IntVec3 cell, AnkleRiskAssessment risk)
        {
            if (pawn?.Map == null) return;
            float severity = Mathf.Clamp01(risk.SprainSeverity);
            TripSpillCarriedItems(pawn, cell, severity);
            // Always apply sprain as part of the accident, using severity mitigation logic
            ApplySprainInjury(pawn, severity);
        }

        public static void TripSpillCarriedItems(Pawn pawn, IntVec3 cell, float severity)
        {
            if (pawn?.Map == null) return;
            var map = pawn.Map;
            severity = Mathf.Clamp01(severity);

            var carried = pawn.carryTracker?.CarriedThing;
            if (carried != null)
            {
                // Precompute scatter cells around the target cell
                var scatterCells = GenRadial.RadialCellsAround(cell, 2, true)
                    .Where(c => c.InBounds(map))
                    .Take(12)
                    .ToList();

                if (pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Near, out Thing dropped))
                {
                    if (dropped != null)
                    {
                        int total = dropped.stackCount;
                        int desiredPiles = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 6f, severity)), 2, 10);
                        int piles = Mathf.Clamp(desiredPiles, 2, Mathf.Max(2, total));

                        int remaining = total;
                        int scatterIndex = 0;
                        for (int i = 0; i < piles - 1 && remaining > 1; i++)
                        {
                            int maxForThis = Mathf.Max(1, remaining - (piles - 1 - i));
                            int amount = Rand.RangeInclusive(1, maxForThis);
                            if (amount >= remaining) break;
                            Thing piece = dropped.SplitOff(amount);
                            remaining -= amount;

                            // Choose a scatter cell cycling through candidates
                            IntVec3 target = (scatterCells.Count > 0) ? scatterCells[scatterIndex % scatterCells.Count] : cell;
                            scatterIndex++;

                            // Place piece directly if possible, otherwise near
                            if (target.Standable(map))
                                GenPlace.TryPlaceThing(piece, target, map, ThingPlaceMode.Direct);
                            else
                                GenPlace.TryPlaceThing(piece, target, map, ThingPlaceMode.Near);

                            MaybeDamageThing(piece, pawn, severity);
                            MaybeTriggerExplosion(piece, pawn, severity);
                        }
                        // Damage the main dropped remainder as well
                        MaybeDamageThing(dropped, pawn, severity);
                        MaybeTriggerExplosion(dropped, pawn, severity);

                        Messages.Message($"{pawn.NameShortColored} tripped and dropped {dropped.LabelNoCount} in a messy pile!",
                            new LookTargets(dropped), MessageTypeDefOf.NegativeEvent);

                        // Interrupt current job so pawn doesn't immediately re-pick the items
                        pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }

            // Stagger the pawn briefly to simulate being stunned
            pawn.stances?.stagger?.StaggerFor(Rand.RangeInclusive(60, 120));
        }

        private static void MaybeDamageThing(Thing thing, Pawn instigator, float severity)
        {
            if (thing == null || thing.Destroyed) return;
            if (thing is Pawn) return;
            if (!thing.def.useHitPoints) return;

            int baseDmg = Mathf.RoundToInt(Mathf.Lerp(1f, 15f, severity));
            int dmg = Mathf.Clamp(baseDmg, 1, Mathf.Max(1, thing.MaxHitPoints / 3));
            var dinfo = new DamageInfo(DamageDefOf.Blunt, dmg, 0f, -1f, instigator);
            thing.TakeDamage(dinfo);
        }

        private static void MaybeTriggerExplosion(Thing thing, Pawn instigator, float severity)
        {
            if (thing == null || thing.Destroyed) return;
            var comp = thing.TryGetComp<CompExplosive>();
            if (comp == null) return;
            if (Rand.Chance(Mathf.Lerp(0.02f, 0.25f, severity)))
            {
                comp.StartWick(instigator);
            }
        }

        private static void TriggerImmediateTrippingAccident(Pawn pawn, AnkleRiskAssessment riskAssessment)
        {
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("TrippingAccident");
            var parms = new IncidentParms();
            parms.target = pawn.Map;
            parms.forced = true;
            parms.customLetterText = $"triggeringPawn:{pawn.thingIDNumber}";
            incidentDef.Worker.TryExecute(parms);
        }

        private static void ApplySprainInjury(Pawn pawn, float severity)
        {
            BodyPartRecord targetPart = GetAnkleBodyPart(pawn) ?? GetLegBodyPart(pawn);
            if (targetPart == null) return;

            var sprain = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("AnkleSprain", false), pawn, targetPart)
                        ?? HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise", false), pawn, targetPart);
            if (sprain == null) return;

            sprain.Severity = severity;
            pawn.health.AddHediff(sprain);

            Messages.Message($"{pawn.NameShortColored} sprained their ankle while tripping over an obstacle!",
                new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
        }

        private static BodyPartRecord GetAnkleBodyPart(Pawn pawn)
        {
            var bodyParts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            var ankles = bodyParts.Where(p => p.def.defName.ToLower().Contains("ankle")).ToList();
            if (ankles.Any()) return ankles.RandomElement();
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