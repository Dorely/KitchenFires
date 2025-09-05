using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KitchenFires
{
    public static class AccidentStormUtility
    {
        private static GameConditionDef _accidentStormDef;
        private static GameConditionDef AccidentStormDef { get { if (_accidentStormDef == null) { _accidentStormDef = DefDatabase<GameConditionDef>.GetNamed("AccidentStormCondition", false); } return _accidentStormDef; } }

        public static bool IsActive(Map map)
        {
            if (map == null || map.gameConditionManager == null || AccidentStormDef == null) return false;
            return map.gameConditionManager.ConditionIsActive(AccidentStormDef);
        }

        public static float ChanceMultiplierFor(Map map)
        {
            return IsActive(map) ? 10f : 1f;
        }

        public static void EnqueueHourlyAccident(Map map)
        {
            if (map == null) return;
            // 50% chance to actually queue on each hourly check
            if (!Rand.Chance(KitchenFiresSettings.AccidentStormHourlyQueueChance)) return;
            // Candidate incident defs that integrate with our queue contexts
            // Keep a mix so different activities can trigger during the storm
            var candidateDefNames = new []
            {
                "KitchenFire_Small",
                "KitchenFire_Large",
                "KitchenExplosion",
                "KitchenBurn",
                "ButcheringAccident_Cut",
                "ButcheringAccident_Amputation",
                "TrippingAccident",
                "EatingAccident_Choking",
                "WorkAccident",
                "SleepAccident_Nightmare"
            };

            // Filter to only those that exist (def names may change between versions)
            var candidates = candidateDefNames
                .Select(n => DefDatabase<IncidentDef>.GetNamed(n, false))
                .Where(d => d != null)
                .ToList();

            if (candidates.Count == 0) return;

            var def = candidates.RandomElement();
            var parms = new IncidentParms
            {
                target = map,
                forced = false
            };
            KitchenFires.KitchenIncidentQueue.Add(def, parms, !KitchenFiresSettings.AccidentStormHourlyForeshadow);
        }
    }

    public class GameCondition_AccidentStorm : GameCondition
    {
        private readonly Dictionary<int, int> _lastHourQueuedByMap = new Dictionary<int, int>();
        private const int TicksPerHour = 2500;

        public override void Init()
        {
            base.Init();
            Messages.Message("An ominous streak of accidents begins to loom over the colony...", MessageTypeDefOf.NegativeEvent);
        }

        public override void GameConditionTick()
        {
            try
            {
                int currentHour = TicksPassed / TicksPerHour;
                var maps = AffectedMaps;
                for (int i = 0; i < maps.Count; i++)
                {
                    var map = maps[i];
                    if (map == null) continue;

                    int key = map.uniqueID;
                    if (!_lastHourQueuedByMap.TryGetValue(key, out var lastHour))
                    {
                        lastHour = -1;
                    }

                    if (currentHour > lastHour)
                    {
                        _lastHourQueuedByMap[key] = currentHour;
                        // Guarantee one queued accident per hour while active
                        AccidentStormUtility.EnqueueHourlyAccident(map);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] AccidentStorm tick failed: {ex}");
            }
        }

        public override void End()
        {
            base.End();
            Messages.Message("The colony's streak of bad luck has passed.", MessageTypeDefOf.NeutralEvent);
        }
    }
}
