using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace KitchenFires
{
    public class QueuedKitchenIncident : IExposable
    {
        public IncidentDef def;
        public IncidentParms parms;
        public int queuedTick;
        public int expirationTick;

        public QueuedKitchenIncident() { }

        public QueuedKitchenIncident(IncidentDef def, IncidentParms parms)
        {
            this.def = def;
            this.parms = parms;
            this.queuedTick = Find.TickManager.TicksGame;
            this.expirationTick = Find.TickManager.TicksGame + 180000; // ~3 days
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Deep.Look(ref parms, "parms");
            Scribe_Values.Look(ref queuedTick, "queuedTick");
            Scribe_Values.Look(ref expirationTick, "expirationTick");
        }

        public bool IsExpired => Find.TickManager.TicksGame > expirationTick;

        public override string ToString()
        {
            return $"{def?.defName} (queued {(Find.TickManager.TicksGame - queuedTick) / 2500f:F1} hrs ago)";
        }
    }

    public static class KitchenIncidentQueue
    {
        public enum QueuedIncidentContext
        {
            Cooking,
            Butchering,
            Movement,
            Eating,
            Work
        }

        private static List<QueuedKitchenIncident> queuedIncidents = new List<QueuedKitchenIncident>();
        public static int Count => queuedIncidents.Count;

        private class JobProgress
        {
            public string lastJobKey;
            public int startTick;
            public Job lastJob;
        }

        private static readonly Dictionary<int, JobProgress> _jobProgressByPawn = new Dictionary<int, JobProgress>();

        private const int PARTIAL_TICKS_COOKING = 300;
        private const int PARTIAL_TICKS_BUTCHERING = 300;
        private const int PARTIAL_TICKS_EATING = 180;
        private const int PARTIAL_TICKS_WORK = 200;

        public static void Add(IncidentDef def, IncidentParms parms)
        {
            var queued = new QueuedKitchenIncident(def, parms);
            queuedIncidents.Add(queued);

            Log.Message($"[KitchenFires] Queued incident: {def.defName}");
            string foreshadowingMessage = GetForeshadowingMessage(def);
            Messages.Message(foreshadowingMessage, MessageTypeDefOf.NeutralEvent);
        }

        public static bool TryExecuteQueuedIncident(Pawn pawn, QueuedIncidentContext context)
        {
            CleanExpiredIncidents();
            if (!queuedIncidents.Any())
                return false;

            int index = queuedIncidents.FindIndex(qi => IsIncidentAllowedInContext(qi.def, context));
            if (index < 0)
                return false;

            if (!HasSufficientProgress(pawn, context))
                return false;

            // Reset gating so subsequent incidents wait again in this job context
            var incident = queuedIncidents[index];

            // Require enough time since queueing or job start (whichever is later)
            int requiredTicks = 0;
            switch (context)
            {
                case QueuedIncidentContext.Cooking: requiredTicks = PARTIAL_TICKS_COOKING; break;
                case QueuedIncidentContext.Butchering: requiredTicks = PARTIAL_TICKS_BUTCHERING; break;
                case QueuedIncidentContext.Eating: requiredTicks = PARTIAL_TICKS_EATING; break;
                case QueuedIncidentContext.Work: requiredTicks = PARTIAL_TICKS_WORK; break;
                case QueuedIncidentContext.Movement: requiredTicks = 0; break;
            }
            if (_jobProgressByPawn.TryGetValue(pawn.thingIDNumber, out var progCheck))
            {
                int now = Find.TickManager.TicksGame;
                int effectiveStart = System.Math.Max(progCheck.startTick, incident.queuedTick);
                if (now - effectiveStart < requiredTicks)
                    return false;
            }
            else
            {
                return false;
            }

            // Remove from queue and reset gating so subsequent incidents wait again
            queuedIncidents.RemoveAt(index);
            if (_jobProgressByPawn.TryGetValue(pawn.thingIDNumber, out var prog))
            {
                prog.startTick = Find.TickManager.TicksGame;
                prog.lastJob = pawn.CurJob;
            }

            incident.parms.target = pawn.Map;
            incident.parms.forced = true;
            incident.parms.customLetterText = $"triggeringPawn:{pawn.thingIDNumber}";

            Log.Message($"[KitchenFires] Executing queued kitchen incident: {incident.def.defName} for {pawn.Name}");

            bool result = incident.def.Worker.TryExecute(incident.parms);
            if (result)
            {
                string completionMessage = GetCompletionMessage(incident.def);
                Messages.Message(completionMessage, MessageTypeDefOf.NegativeEvent);
            }
            return result;
        }

        private static bool IsIncidentAllowedInContext(IncidentDef def, QueuedIncidentContext context)
        {
            if (def == null) return false;
            string name = def.defName ?? string.Empty;
            switch (context)
            {
                case QueuedIncidentContext.Cooking:
                    return name.StartsWith("KitchenFire_") || name == "KitchenExplosion" || name == "KitchenBurn";
                case QueuedIncidentContext.Butchering:
                    return name.StartsWith("ButcheringAccident_");
                case QueuedIncidentContext.Movement:
                    return name == "TrippingAccident";
                case QueuedIncidentContext.Eating:
                    return name.StartsWith("EatingAccident_") || name == "EatingAccident_Choking";
                case QueuedIncidentContext.Work:
                    return name.StartsWith("WorkAccident");
                default:
                    return false;
            }
        }

        private static bool HasSufficientProgress(Pawn pawn, QueuedIncidentContext context)
        {
            if (pawn == null) return false;
            var job = pawn.CurJob;
            var jobDefName = job?.def?.defName ?? string.Empty;

            string expectedKey = null;
            int requiredTicks = 0;
            switch (context)
            {
                case QueuedIncidentContext.Cooking:
                case QueuedIncidentContext.Butchering:
                    if (!jobDefName.Contains("DoBill")) return false;
                    expectedKey = "DoBill";
                    requiredTicks = (context == QueuedIncidentContext.Cooking) ? PARTIAL_TICKS_COOKING : PARTIAL_TICKS_BUTCHERING;
                    break;
                case QueuedIncidentContext.Eating:
                    if (!jobDefName.ToLowerInvariant().Contains("ingest")) return false;
                    expectedKey = "Ingest";
                    requiredTicks = PARTIAL_TICKS_EATING;
                    break;
                case QueuedIncidentContext.Movement:
                    return true;
                case QueuedIncidentContext.Work:
                    {
                        string jn = jobDefName.ToLowerInvariant();
                        string key = null;
                        if (jn.Contains("mine")) key = "Mine";
                        else if (jn.Contains("cutplant") || jn.Contains("plantcut") || jn.Contains("chop")) key = "CutPlant";
                        else if (jn.Contains("harvest")) key = "Harvest";
                        else if (jn.Contains("sow")) key = "Sow";
                        else return false;
                        expectedKey = key;
                        requiredTicks = PARTIAL_TICKS_WORK;
                    }
                    break;
            }

            int now = Find.TickManager.TicksGame;
            int id = pawn.thingIDNumber;
            if (!_jobProgressByPawn.TryGetValue(id, out var prog))
            {
                _jobProgressByPawn[id] = new JobProgress { lastJobKey = expectedKey, startTick = now, lastJob = job };
                return false;
            }

            if (!ReferenceEquals(prog.lastJob, job) || prog.lastJobKey != expectedKey)
            {
                prog.lastJob = job;
                prog.lastJobKey = expectedKey;
                prog.startTick = now;
                return false;
            }

            return (now - prog.startTick) >= requiredTicks;
        }

        private static void CleanExpiredIncidents()
        {
            int originalCount = queuedIncidents.Count;
            queuedIncidents.RemoveAll(qi => qi.IsExpired);
            if (queuedIncidents.Count < originalCount)
            {
                Messages.Message("The ominous feeling has passed...", MessageTypeDefOf.NeutralEvent);
            }
        }

        private static string GetForeshadowingMessage(IncidentDef def)
        {
            var genericWarnings = new[]
            {
                "There's an ominous feeling in the air today...",
                "Something feels off about the colony today...",
                "The colonists seem unusually accident-prone today...",
                "There's a sense of impending misfortune...",
                "An unsettling atmosphere hangs over the settlement...",
                "The day feels particularly unlucky...",
                "There's an eerie tension in the colony...",
                "Something doesn't feel quite right today..."
            };
            return genericWarnings.RandomElement();
        }

        private static string GetCompletionMessage(IncidentDef def)
        {
            var genericCompletions = new[]
            {
                "The misfortune you sensed has come to pass...",
                "That ominous feeling was justified...",
                "The colony's bad luck has manifested...",
                "The sense of impending trouble was accurate...",
                "The unsettling atmosphere has led to incident...",
                "That eerie tension has culminated in mishap...",
                "The day's unlucky feeling has proven true..."
            };
            return genericCompletions.RandomElement();
        }

        public static void ExposeData()
        {
            Scribe_Collections.Look(ref queuedIncidents, "queuedIncidents", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && queuedIncidents == null)
            {
                queuedIncidents = new List<QueuedKitchenIncident>();
            }
        }

        public static string GetDebugInfo()
        {
            if (!queuedIncidents.Any())
                return "No queued kitchen incidents";
            return $"Queued kitchen incidents ({queuedIncidents.Count}):\n" + string.Join("\n", queuedIncidents.Select(qi => "- " + qi.ToString()));
        }
    }
}
