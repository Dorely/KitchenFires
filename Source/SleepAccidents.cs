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

            // Trauma-aware multiplier: recent negative memories, pain, traits, low mood
            float mult = ComputeNightTerrorChanceMultiplier(pawn);
            float chance = BASE_NIGHTMARE_CHANCE * mult;
            if (Rand.Chance(chance))
            {
                TriggerImmediateNightmare(pawn);
            }
        }

        private static float ComputeNightTerrorChanceMultiplier(Pawn pawn)
        {
            float mult = 1f;

            try
            {
                // 1) Recent negative memories boost
                var thoughts = pawn.needs?.mood?.thoughts?.memories?.Memories;
                if (thoughts != null)
                {
                    int recentWindowTicks = GenDate.TicksPerDay * 10; // last ~10 days
                    float negSum = 0f;
                    int traumaHits = 0;
                    foreach (var mem in thoughts)
                    {
                        // skip expired or very old
                        if (mem == null) continue;
                        if (mem.permanent || mem.age <= recentWindowTicks)
                        {
                            float mood = 0f;
                            try { mood = mem.MoodOffset(); } catch { }
                            if (mood < -1f)
                            {
                                negSum += Mathf.Min(-mood, 12f); // cap individual impact
                            }

                            // Key trauma memories get extra weight
                            var d = mem.def;
                            if (d == ThoughtDefOf.WitnessedDeathFamily || d == ThoughtDefOf.WitnessedDeathAlly || d == ThoughtDefOf.WitnessedDeathNonAlly || d == ThoughtDefOf.KnowColonistDied || d == ThoughtDefOf.PawnWithGoodOpinionDied || d == ThoughtDefOf.ColonistLost)
                            {
                                traumaHits++;
                            }
                        }
                    }
                    // General negativity: up to +1.0x
                    mult += Mathf.Clamp(negSum * 0.03f, 0f, 1.0f);
                    // Specific traumatic events: +0.3x each up to +1.5x
                    mult += Mathf.Clamp(traumaHits * 0.3f, 0f, 1.5f);
                }

                // 2) Physical pain increases risk
                float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
                if (pain > 0.2f)
                {
                    mult += Mathf.Clamp01(pain) * 0.5f; // up to +0.5x at 100% pain
                }

                // 3) Low mood increases risk
                float curMood = pawn.needs?.mood?.CurLevel ?? 0.5f;
                if (curMood < 0.35f) mult += 0.2f;
                if (curMood < 0.20f) mult += 0.2f;

                // 4) Traits: Wimp more susceptible; Psychopath less
                var traits = pawn.story?.traits;
                if (traits != null)
                {
                    if (traits.HasTrait(TraitDefOf.Wimp)) mult += 0.3f;
                    if (traits.HasTrait(TraitDefOf.Psychopath)) mult *= 0.7f;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] Night terror multiplier calc failed: {ex.Message}");
            }

            return Mathf.Clamp(mult, 0.5f, 5f);
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
