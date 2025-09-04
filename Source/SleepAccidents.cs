using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace KitchenFires
{
    public static class SleepAccidentUtility
    {
        private const float BASE_NIGHTMARE_CHANCE = 0.00002f; // per tick while sleeping

        public static void CheckForSleepAccident(Pawn pawn, JobDriver driver)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonist) return;
            if (pawn.Map == null) return;
            var lay = driver as JobDriver_LayDown; if (lay == null) return;
            // Prefer when actually asleep; fallback to any laydown if forceSleep
            bool asleep = driver.asleep || pawn.jobs?.curDriver?.job?.forceSleep == true;
            if (!asleep) return;

            // Storyteller-queued sleep incidents first
            if (KitchenIncidentQueue.TryExecuteQueuedIncident(pawn, KitchenIncidentQueue.QueuedIncidentContext.Sleep))
                return;

            if (Rand.Chance(BASE_NIGHTMARE_CHANCE))
            {
                TriggerImmediateNightmare(pawn);
            }
        }

        public static void TriggerImmediateNightmare(Pawn pawn)
        {
            var def = DefDatabase<IncidentDef>.GetNamed("SleepAccident_Nightmare", false);
            if (def == null || pawn.Map == null) return;
            var parms = new IncidentParms
            {
                target = pawn.Map,
                forced = true,
                customLetterText = $"triggeringPawn:{pawn.thingIDNumber}"
            };
            def.Worker.TryExecute(parms);
        }
    }

    public class IncidentWorker_SleepNightmare : IncidentWorker
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
                    triggeringPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.jobs?.curDriver is JobDriver_LayDown)
                                     ?? map.mapPawns.FreeColonistsSpawned.RandomElementWithFallback();
                }
                if (triggeringPawn == null) return false;

                // Force wake and start a brief panic flee mental state to simulate terror
                try
                {
                    var ms = DefDatabase<MentalStateDef>.GetNamed("NightTerror", false);
                    var handler = triggeringPawn.mindState?.mentalStateHandler;

                    // If already in our custom state, end it so we can restart cleanly
                    if (triggeringPawn.InMentalState && triggeringPawn.MentalStateDef == ms)
                    {
                        triggeringPawn.MentalState?.RecoverFromState();
                    }

                    // Interrupt any current job (e.g., being tended) and wake
                    triggeringPawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                    if (ms == null)
                    {
                        Log.Warning("[KitchenFires] NightTerror MentalStateDef not found. Skipping nightmare start.");
                        return false;
                    }

                    // Forced + forceWake; allow transition (will end any current state)
                    handler?.TryStartMentalState(ms, null, forced: true, forceWake: true, causedByMood: true, otherPawn: null, transitionSilently: false);
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[KitchenFires] Failed to start nightmare mental state: {ex}");
                }

                Log.Message($"[KitchenFires] Nightmare sleep accident triggered for {triggeringPawn.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] Sleep nightmare worker failed: {ex}");
                return false;
            }
        }
    }
}
