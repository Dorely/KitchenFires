using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KitchenFires
{
    public class QueuedKitchenIncident : IExposable
    {
        public IncidentDef def;
        public IncidentParms parms;
        public int queuedTick;
        public int expirationTick;
        
        public QueuedKitchenIncident()
        {
        }
        
        public QueuedKitchenIncident(IncidentDef def, IncidentParms parms)
        {
            this.def = def;
            this.parms = parms;
            this.queuedTick = Find.TickManager.TicksGame;
            // Queue expires after 3 days if no cooking happens
            this.expirationTick = Find.TickManager.TicksGame + 180000;
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
        private static List<QueuedKitchenIncident> queuedIncidents = new List<QueuedKitchenIncident>();
        
        public static int Count => queuedIncidents.Count;
        
        public static void Add(IncidentDef def, IncidentParms parms)
        {
            var queued = new QueuedKitchenIncident(def, parms);
            queuedIncidents.Add(queued);
            
            Log.Message($"[KitchenFires] Queued kitchen incident: {def.defName}");
            
            // Send foreshadowing message
            string equipmentDesc = GetRandomEquipmentWarning();
            Messages.Message($"The {equipmentDesc} seems particularly temperamental today...", 
                MessageTypeDefOf.NeutralEvent);
        }
        
        public static bool TryExecuteQueuedIncident(Pawn cookingPawn)
        {
            CleanExpiredIncidents();
            
            if (!queuedIncidents.Any())
                return false;
                
            var incident = queuedIncidents.First();
            queuedIncidents.RemoveAt(0);
            
            // Update incident parameters with current context
            incident.parms.target = cookingPawn.Map;
            incident.parms.forced = true;
            
            // Store the triggering pawn for the incident worker
            incident.parms.customLetterText = $"triggeringPawn:{cookingPawn.thingIDNumber}";
            
            Log.Message($"[KitchenFires] Executing queued kitchen incident: {incident.def.defName} for {cookingPawn.Name}");
            
            // Execute through proper IncidentWorker for full storyteller integration
            bool result = incident.def.Worker.TryExecute(incident.parms);
            
            if (result)
            {
                Messages.Message("The kitchen mishap you were worried about has occurred!", 
                    MessageTypeDefOf.NegativeEvent);
            }
            
            return result;
        }
        
        private static void CleanExpiredIncidents()
        {
            int originalCount = queuedIncidents.Count;
            queuedIncidents.RemoveAll(qi => qi.IsExpired);
            
            if (queuedIncidents.Count < originalCount)
            {
                Messages.Message("The kitchen equipment seems to have settled down.", 
                    MessageTypeDefOf.NeutralEvent);
            }
        }
        
        private static string GetRandomEquipmentWarning()
        {
            var warnings = new[]
            {
                "cooking stove",
                "kitchen equipment", 
                "cooking fire",
                "stove burner",
                "cooking apparatus",
                "food preparation equipment"
            };
            return warnings.RandomElement();
        }
        
        public static void ExposeData()
        {
            Scribe_Collections.Look(ref queuedIncidents, "queuedIncidents", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && queuedIncidents == null)
            {
                queuedIncidents = new List<QueuedKitchenIncident>();
            }
        }
        
        // Debug method for dev mode
        public static string GetDebugInfo()
        {
            if (!queuedIncidents.Any())
                return "No queued kitchen incidents";
                
            return $"Queued kitchen incidents ({queuedIncidents.Count}):\n" + 
                   string.Join("\n", queuedIncidents.Select(qi => "- " + qi.ToString()));
        }
    }
}