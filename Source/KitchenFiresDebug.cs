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

        [DebugAction("Kitchen Fires", "Force food spill (select pawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ForceFoodSpill()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist)
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }

            bool ok = EatingAccidentUtility.TriggerImmediateSpill(pawn);
            if (!ok)
            {
                Messages.Message($"No ingestible found to spill for {pawn.NameShortColored}.", MessageTypeDefOf.RejectInput);
            }
        }
        [DebugAction("Kitchen Fires", "Queue work accident", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueWorkAccident()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            var incidentDef = DefDatabase<IncidentDef>.GetNamed("WorkAccident", false);
            if (incidentDef == null)
            {
                Messages.Message("WorkAccident def not found.", MessageTypeDefOf.RejectInput);
                return;
            }
            var parms = new IncidentParms { target = map };
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Work accident queued - perform mining/chopping/planting/harvesting to trigger it!", MessageTypeDefOf.NeutralEvent);
        }

        [DebugAction("Kitchen Fires", "Test immediate work accident (select pawn first)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateWorkAccident()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist)
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            Messages.Message($"Testing immediate work accident for {pawn.Name}", MessageTypeDefOf.NeutralEvent);
            WorkAccidentUtility.TriggerImmediateWorkAccident(pawn);
        }

        [DebugAction("Kitchen Fires", "Queue choking", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueChoking()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("EatingAccident_Choking");
            var parms = new IncidentParms();
            parms.target = map;
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Choking queued - start eating to trigger it!", MessageTypeDefOf.NeutralEvent);
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

        [DebugAction("Kitchen Fires", "Test immediate choking (select pawn first)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateChoking()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist) 
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Messages.Message($"Testing immediate choking for {pawn.Name}", MessageTypeDefOf.NeutralEvent);
            EatingAccidentUtility.TriggerImmediateChoking(pawn);
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

        [DebugAction("Kitchen Fires", "Queue nightmare", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void QueueNightmare()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            var incidentDef = DefDatabase<IncidentDef>.GetNamed("SleepAccident_Nightmare", false);
            if (incidentDef == null)
            {
                Messages.Message("SleepAccident_Nightmare def not found.", MessageTypeDefOf.RejectInput);
                return;
            }
            var parms = new IncidentParms { target = map };
            KitchenIncidentQueue.Add(incidentDef, parms);
            Messages.Message("Nightmare queued - pawn must be sleeping to trigger it!", MessageTypeDefOf.NeutralEvent);
        }

        [DebugAction("Kitchen Fires", "Test immediate nightmare (select pawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateNightmare()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist)
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            SleepAccidentUtility.TriggerImmediateNightmare(pawn);
        }
        
        [DebugAction("Kitchen Fires", "Make selected animal milk-full", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void MakeSelectedAnimalMilkFull()
        {
            Pawn animal = Find.Selector.SingleSelectedThing as Pawn;
            if (animal == null || animal.RaceProps == null || !animal.RaceProps.Animal)
            {
                Messages.Message("Please select an animal first!", MessageTypeDefOf.RejectInput);
                return;
            }
            var comp = animal.TryGetComp<CompMilkable>();
            if (comp == null)
            {
                Messages.Message($"{animal.LabelShortCap} is not milkable.", MessageTypeDefOf.RejectInput);
                return;
            }
            var baseType = typeof(CompHasGatherableBodyResource);
            var field = baseType.GetField("fullness", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            field?.SetValue(comp, 1f);
            Messages.Message($"Set milk fullness to 100% for {animal.NameShortColored}.", MessageTypeDefOf.NeutralEvent);
        }

        [DebugAction("Kitchen Fires", "Make selected animal wool-full", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void MakeSelectedAnimalWoolFull()
        {
            Pawn animal = Find.Selector.SingleSelectedThing as Pawn;
            if (animal == null || animal.RaceProps == null || !animal.RaceProps.Animal)
            {
                Messages.Message("Please select an animal first!", MessageTypeDefOf.RejectInput);
                return;
            }
            var comp = animal.TryGetComp<CompShearable>();
            if (comp == null)
            {
                Messages.Message($"{animal.LabelShortCap} is not shearable.", MessageTypeDefOf.RejectInput);
                return;
            }
            var baseType = typeof(CompHasGatherableBodyResource);
            var field = baseType.GetField("fullness", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            field?.SetValue(comp, 1f);
            Messages.Message($"Set wool growth to 100% for {animal.NameShortColored}.", MessageTypeDefOf.NeutralEvent);
        }

        [DebugAction("Kitchen Fires", "Test immediate milking kick (select pawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateMilkingKick()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist)
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            // Simulate a milking kick accident on the selected pawn
            Messages.Message($"Testing milking kick for {pawn.NameShortColored}", MessageTypeDefOf.NeutralEvent);
            var method = typeof(AnimalAccidentUtility).GetMethod("ApplyKickInjury", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { pawn, null });
        }

        [DebugAction("Kitchen Fires", "Test immediate shearing mishap (select pawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateShearingAccident()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist)
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            Messages.Message($"Testing shearing mishap for {pawn.NameShortColored}", MessageTypeDefOf.NeutralEvent);
            // Randomly injure pawn (self) to simulate mishap
            var method = typeof(AnimalAccidentUtility).GetMethod("ApplyShearCutSelf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { pawn });
        }

        [DebugAction("Kitchen Fires", "Test immediate training bite (select pawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestImmediateTrainingBite()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || !pawn.IsColonist)
            {
                Messages.Message("Please select a colonist first!", MessageTypeDefOf.RejectInput);
                return;
            }
            Messages.Message($"Testing training bite for {pawn.NameShortColored}", MessageTypeDefOf.NeutralEvent);
            var method = typeof(AnimalAccidentUtility).GetMethod("ApplyBiteInjury", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { pawn, null });
        }
    }
}
