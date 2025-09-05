using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

using Hediff_MissingPart = Verse.Hediff_MissingPart;

namespace KitchenFires
{
    public static class WorkAccidentUtility
    {
        private const float BASE_WORK_ACCIDENT_CHANCE = 0.000001f; // per tick while working (~60x smaller than 60-tick checks)

        public static bool IsWorkJob(JobDef jobDef)
        {
            if (jobDef == null) return false;
            string n = jobDef.defName.ToLowerInvariant();
            return n.Contains("mine") || n.Contains("cutplant") || n.Contains("plantcut") || n.Contains("chop") || n.Contains("harvest") || n.Contains("sow");
        }

        public static void CheckForWorkAccident(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonist) return;
            var job = pawn.CurJob;
            if (job == null || !IsWorkJob(job.def)) return;
            if (pawn.Map == null) return;

            // First priority: execute queued storyteller incidents
            if (KitchenIncidentQueue.TryExecuteQueuedIncident(pawn, KitchenIncidentQueue.QueuedIncidentContext.Work))
                return;

            // Small spontaneous chance besides storyteller (per tick while working)
            if (Rand.Chance(BASE_WORK_ACCIDENT_CHANCE * AccidentStormUtility.ChanceMultiplierFor(pawn.Map)))
            {
                TriggerImmediateWorkAccident(pawn);
            }
        }

        public static void TriggerImmediateWorkAccident(Pawn pawn)
        {
            try
            {
                var incidentDef = DefDatabase<IncidentDef>.GetNamed("WorkAccident", false);
                if (incidentDef == null) return;

                var parms = new IncidentParms
                {
                    target = pawn.Map,
                    forced = true,
                    customLetterText = $"triggeringPawn:{pawn.thingIDNumber}"
                };
                incidentDef.Worker.TryExecute(parms);
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] Failed to trigger immediate work accident: {ex}");
            }
        }
    }

    public class IncidentWorker_WorkAccident : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            try
            {
                Map map = (Map)parms.target;
                Pawn triggeringPawn = null;

                if (!string.IsNullOrEmpty(parms.customLetterText) && parms.customLetterText.StartsWith("triggeringPawn:"))
                {
                    string idString = parms.customLetterText.Substring("triggeringPawn:".Length);
                    if (int.TryParse(idString, out int pawnId))
                    {
                        triggeringPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == pawnId);
                        parms.customLetterText = string.Empty;
                    }
                }

                if (triggeringPawn == null)
                {
                    triggeringPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => WorkAccidentUtility.IsWorkJob(p.CurJob?.def))
                                     ?? map.mapPawns.FreeColonistsSpawned.RandomElementWithFallback();
                }
                if (triggeringPawn == null) return false;

                // Decide if self-injury or nearby worker injury
                Pawn targetPawn = triggeringPawn;
                if (Rand.Chance(0.7f))
                {
                    var nearby = GenRadial.RadialCellsAround(triggeringPawn.Position, 3, true)
                        .Where(c => c.InBounds(map))
                        .SelectMany(c => c.GetThingList(map))
                        .OfType<Pawn>()
                        .Where(p => p.IsColonist && p != triggeringPawn && WorkAccidentUtility.IsWorkJob(p.CurJob?.def))
                        .ToList();
                    if (nearby.Any()) targetPawn = nearby.RandomElement();
                }

                ApplyWorkInjury(triggeringPawn, targetPawn);

                SendStandardLetter(parms, new LookTargets(targetPawn), triggeringPawn.NameShortColored);
                Log.Message($"[KitchenFires] Work accident: {(targetPawn == triggeringPawn ? "self" : "nearby")} injury involving {triggeringPawn.Name} -> {targetPawn.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] Work accident worker failed: {ex}");
                return false;
            }
        }

        private void ApplyWorkInjury(Pawn instigator, Pawn victim)
        {
            if (victim?.health == null) return;
            var allParts = victim.health.hediffSet.GetNotMissingParts().Where(p => !p.def.conceptual).ToList();
            var outerParts = allParts.Where(p => p.depth == BodyPartDepth.Outside).ToList();
            var part = outerParts.RandomElementWithFallback(null) ?? allParts.FirstOrDefault();
            if (part == null) return;

            float severityRoll = Rand.Value; // 0..1
            if (severityRoll >= 0.90f)
            {
                // Very severe: missing part (prefer hand/finger/arm if available)
                var pref = outerParts.Where(p => p.def.defName.ToLowerInvariant().Contains("finger") || p.def.defName.ToLowerInvariant().Contains("hand") || p.def.defName.ToLowerInvariant().Contains("arm")).ToList();
                var mpart = pref.Any() ? pref.RandomElement() : part;
                var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, victim, mpart);
                missing.lastInjury = HediffDefOf.Cut;
                missing.IsFresh = true;
                victim.health.AddHediff(missing, mpart);
            }
            else
            {
                // Cut or bruise with severity scaling; high roll can still be quite severe
                bool cut = Rand.Chance(0.6f);
                var hediffDef = cut ? HediffDefOf.Cut : DefDatabase<HediffDef>.GetNamed("Bruise", false) ?? HediffDefOf.Cut;
                var injury = HediffMaker.MakeHediff(hediffDef, victim, part);
                float min = 0.12f;
                float max = (severityRoll >= 0.80f) ? 0.75f : 0.45f;
                injury.Severity = Rand.Range(min, max);
                victim.health.AddHediff(injury);
            }

            // Interrupt and stagger both pawns for realism
            instigator.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            instigator.stances?.stagger?.StaggerFor(Rand.RangeInclusive(60, 120));
            if (victim != instigator)
            {
                victim.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                victim.stances?.stagger?.StaggerFor(Rand.RangeInclusive(60, 120));
            }
        }
    }
}
