using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace KitchenFires
{
    public static class EatingAccidentUtility
    {
        private const float BASE_CHOKING_CHANCE = 0.00008f; // rare mishap by default

        public static void MaybeTriggerChoking(Pawn pawn, Thing food)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonist) return;
            if (pawn.Map == null) return;
            if (food == null || food.def?.ingestible == null) return;

            // First priority: execute queued eating accidents
            if (KitchenIncidentQueue.TryExecuteQueuedIncident(pawn, KitchenIncidentQueue.QueuedIncidentContext.Eating))
                return;

            // pure chance roll (no modifiers for now)
            if (!Rand.Chance(BASE_CHOKING_CHANCE))
                return;

            float roll = Rand.Value; // 0..1 severity random

            if (roll >= 0.995f)
            {
                ApplyCriticalChoking(pawn);
                return;
            }

            ApplyChoking(pawn, roll);
        }

        public static void TriggerImmediateChoking(Pawn pawn)
        {
            float roll = Rand.Value;
            if (roll >= 0.995f) { ApplyCriticalChoking(pawn); return; }
            ApplyChoking(pawn, roll);
        }

        private static void ApplyChoking(Pawn pawn, float roll)
        {
            // Map roll to severity 0.15 .. 0.6 roughly
            float sev = Mathf.Lerp(0.15f, 0.6f, roll);

            var def = DefDatabase<HediffDef>.GetNamed("Choking", false);
            if (def == null)
            {
                Messages.Message($"{pawn.NameShortColored} started choking but recovered quickly.", new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
                return;
            }
            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                existing.Severity = sev;
            }
            else
            {
                var hediff = HediffMaker.MakeHediff(def, pawn);
                hediff.Severity = sev;
                pawn.health.AddHediff(hediff);
            }

            // Interrupt eating
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptOptional);

            string desc = sev > 0.45f ? "severely" : sev > 0.25f ? "badly" : "briefly";
            Messages.Message($"{pawn.NameShortColored} {desc} choked while eating.", new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
        }

        private static void ApplyCriticalChoking(Pawn pawn)
        {
            var def = DefDatabase<HediffDef>.GetNamed("ChokingCritical", false);
            if (def != null)
            {
                float sev = Rand.Range(0.3f, 0.55f);
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null)
                {
                    existing.Severity = Mathf.Max(existing.Severity, sev);
                }
                else
                {
                    var hediff = HediffMaker.MakeHediff(def, pawn);
                    hediff.Severity = sev;
                    pawn.health.AddHediff(hediff);
                }
            }
            // Hard stop eating
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);

            Find.LetterStack.ReceiveLetter(
                "Choking - needs intervention",
                $"{pawn.NameShortColored} is choking and cannot breathe. Provide medical help immediately or they may die.",
                LetterDefOf.ThreatBig,
                new LookTargets(pawn)
            );
        }
    }

    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.ChewIngestible))]
    public static class Toils_Ingest_ChewIngestible_Patch
    {
        public static void Postfix(Toil __result, Pawn chewer)
        {
            try
            {
                var toil = __result;
                var original = toil.tickIntervalAction;
                bool triggered = false;
                toil.tickIntervalAction = delta =>
                {
                    original?.Invoke(delta);
                    if (!triggered)
                    {
                        triggered = true;
                        try
                        {
                            var thing = toil.actor?.CurJob?.GetTarget(TargetIndex.A).Thing;
                            EatingAccidentUtility.MaybeTriggerChoking(chewer, thing);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[KitchenFires] Error during choking check: {ex}");
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[KitchenFires] Failed patching chew toil for choking: {ex}");
            }
        }
    }
}
