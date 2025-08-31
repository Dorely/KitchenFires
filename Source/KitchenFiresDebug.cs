using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace KitchenFires
{
    public static class KitchenFiresDebug
    {
        [DebugAction("Kitchen Fires", "Queue small fire", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueSmallFire()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenFire_Small");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Small kitchen fire queued - cook something to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        [DebugAction("Kitchen Fires", "Queue large fire", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueLargeFire()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenFire_Large");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Large kitchen fire queued - cook something to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        [DebugAction("Kitchen Fires", "Queue explosion", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueExplosion()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenExplosion");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Kitchen explosion queued - cook something to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        [DebugAction("Kitchen Fires", "Queue burn", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueBurn()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("KitchenBurn");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Kitchen burn queued - cook something to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        [DebugAction("Kitchen Fires", "Show queue status", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ShowQueueStatus()
        {
            string info = KitchenIncidentQueue.GetDebugInfo();
            Messages.Message(info, MessageTypeDefOf.NeutralEvent);
            Log.Message($"[KitchenFires Debug] {info}");
        }
        
        [DebugAction("Kitchen Fires", "Test immediate incident (select pawn first)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateIncident()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist) 
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Messages.Message($"Testing immediate kitchen incident for {pawn.Name}", MessageTypeDefOf.NeutralEvent);
            
            // Force trigger the immediate incident system
            var cookingSkill = pawn.skills.GetSkill(SkillDefOf.Cooking);
            var riskAssessment = new KitchenRiskAssessment
            {
                IncidentRisk = 1.0f, // 100% chance for testing
                SeverityMultiplier = 1.0f,
                SkillLevel = cookingSkill.Level
            };
            
            // Use reflection to call the private method for testing
            var method = typeof(KitchenIncidentUtility).GetMethod("TriggerImmediateKitchenIncident", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { pawn, riskAssessment });
        }
        
        // Butchering Accident Debug Actions
        [DebugAction("Kitchen Fires", "Queue butchering cut", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueButcheringCut()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("ButcheringAccident_Cut");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Butchering cut queued - start butchering to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        [DebugAction("Kitchen Fires", "Queue butchering amputation", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueButcheringAmputation()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("ButcheringAccident_Amputation");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Butchering amputation queued - start butchering to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        // Ankle Sprain Debug Actions
        [DebugAction("Kitchen Fires", "Queue tripping accident", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueAnkleSprain()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("TrippingAccident");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Tripping accident queued - walk over obstacles to trigger it!", MessageTypeDefOf.NeutralEvent);
        }
        
        // Immediate testing for new accidents
        [DebugAction("Kitchen Fires", "Test immediate butchering accident (select pawn first)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateButcheringAccident()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist) 
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Messages.Message($"Testing immediate butchering accident for {pawn.Name}", MessageTypeDefOf.NeutralEvent);
            
            // Force trigger butchering accident
            var riskAssessment = new ButcheringRiskAssessment
            {
                AccidentRisk = 1.0f, // 100% chance for testing
                AccidentSeverity = 0.5f
            };
            
            // Use reflection to call the private method for testing
            var method = typeof(ButcheringAccidentUtility).GetMethod("TriggerImmediateButcheringAccident", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { pawn, riskAssessment });
        }
        
        [DebugAction("Kitchen Fires", "Test immediate tripping accident (select pawn first)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateAnkleSprain()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist) 
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Messages.Message($"Testing immediate tripping accident for {pawn.Name}", MessageTypeDefOf.NeutralEvent);
            
            // Force trigger ankle sprain
            var riskAssessment = new AnkleRiskAssessment
            {
                SprainRisk = 1.0f, // 100% chance for testing
                SprainSeverity = 0.4f
            };
            
            // Use reflection to call the private method for testing
            var method = typeof(TrippingAccidentUtility).GetMethod("TriggerImmediateTrippingAccident", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { pawn, riskAssessment });
        }
    }
}