using HugsLib;
using HugsLib.Settings;
using System.Globalization;
using System.Collections.Generic;
using Verse;

namespace KitchenFires
{
    // HugsLib controller: defines user-configurable settings and applies them to KitchenFiresSettings
    public class KitchenFiresHugsController : ModBase
    {
        protected override bool HarmonyAutoPatch => false;
        public override string ModIdentifier => "Dorely.KitchenFires";

        private SettingHandle<float> _globalChanceMul;
        private SettingHandle<float> _globalSeverityMul;

        private SettingHandle<float> _cookBase;
        private SettingHandle<float> _cookChanceMul;
        private SettingHandle<float> _fireSizeMul;
        private SettingHandle<float> _explRadiusMul;
        private SettingHandle<float> _explDamageMul;
        private SettingHandle<float> _burnSevMul;

        private SettingHandle<float> _butcherBase;
        private SettingHandle<float> _butcherChanceMul;
        private SettingHandle<float> _butcherSevMul;

        private SettingHandle<float> _tripBase;
        private SettingHandle<float> _tripChanceMul;
        private SettingHandle<float> _tripSevMul;

        private SettingHandle<float> _eatChokeBase;
        private SettingHandle<float> _eatChokeChanceMul;
        private SettingHandle<float> _eatChokeSevMul;
        private SettingHandle<float> _eatSpillBase;
        private SettingHandle<float> _eatSpillChanceMul;

        private SettingHandle<float> _workBase;
        private SettingHandle<float> _workChanceMul;
        private SettingHandle<float> _workSevMul;

        private SettingHandle<float> _sleepBase;
        private SettingHandle<float> _sleepChanceMul;

        private SettingHandle<float> _milkBase;
        private SettingHandle<float> _shearBase;
        private SettingHandle<float> _trainBase;
        private SettingHandle<float> _milkChanceMul;
        private SettingHandle<float> _shearChanceMul;
        private SettingHandle<float> _trainChanceMul;
        private SettingHandle<float> _animalSevMul;

        private SettingHandle<float> _stormHourlyChance;
        private SettingHandle<bool> _stormHourlyForeshadow;

        private static string FormatFloat(float v)
        {
            return v.ToString("0.####################", CultureInfo.InvariantCulture);
        }

        private static readonly Dictionary<SettingHandle, string> _floatDisplay = new Dictionary<SettingHandle, string>();
        private static void WireFloatHandle(SettingHandle<float> h)
        {
            if (h == null) return;
            _floatDisplay[h] = FormatFloat(h.Value);
            h.CustomDrawer = rect =>
            {
                var key = h;
                if (!_floatDisplay.TryGetValue(key, out var text)) text = FormatFloat(h.Value);
                var newText = Widgets.TextField(rect, text);
                bool changed = newText != text;
                if (changed)
                {
                    _floatDisplay[key] = newText;
                    if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        h.StringValue = newText; // will validate via h.Validator if present
                        _floatDisplay[key] = FormatFloat(h.Value);
                    }
                }
                return changed;
            };
        }

        public override void DefsLoaded()
        {
            // Global
            _globalChanceMul = Settings.GetHandle("Global_ChanceMultiplier", "Global chance multiplier", "Scales the chance of all KitchenFires accidents/incidents.", 1.0f);
            _globalChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_globalChanceMul);
            _globalChanceMul.Value = Clamp01Plus(_globalChanceMul.Value);

            _globalSeverityMul = Settings.GetHandle("Global_SeverityMultiplier", "Global severity multiplier", "Scales the severity of injuries, fire sizes, and explosion magnitude.", 1.0f);
            _globalSeverityMul.Validator = ValidateNonNegative;
            WireFloatHandle(_globalSeverityMul);
            _globalSeverityMul.Value = ClampMin(_globalSeverityMul.Value, 0f);
            // Cooking
            _cookBase = Settings.GetHandle("Cooking_BaseChance", "Cooking: base trigger chance", "Base per-tick chance for kitchen incidents while cooking.", 0.00002f);
            _cookBase.Validator = ValidateNonNegative;
            WireFloatHandle(_cookBase);
            _cookChanceMul = Settings.GetHandle("Cooking_ChanceMultiplier", "Cooking: chance multiplier", "Scales kitchen incident chance from all other factors.", 1.0f);
            _cookChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_cookChanceMul);
            _fireSizeMul = Settings.GetHandle("Cooking_FireSizeMultiplier", "Cooking: fire size multiplier", "Scales the size of fires started by kitchen incidents.", 1.0f);
            _fireSizeMul.Validator = ValidateNonNegative;
            WireFloatHandle(_fireSizeMul);
            _explRadiusMul = Settings.GetHandle("Cooking_ExplosionRadiusMultiplier", "Cooking: explosion radius multiplier", "Scales the radius of kitchen explosions.", 1.0f);
            _explRadiusMul.Validator = ValidateNonNegative;
            WireFloatHandle(_explRadiusMul);
            _explDamageMul = Settings.GetHandle("Cooking_ExplosionDamageMultiplier", "Cooking: explosion damage multiplier", "Scales the damage of kitchen explosions.", 1.0f);
            _explDamageMul.Validator = ValidateNonNegative;
            WireFloatHandle(_explDamageMul);
            _burnSevMul = Settings.GetHandle("Cooking_BurnSeverityMultiplier", "Cooking: burn severity multiplier", "Scales severity of burn injuries from kitchen incidents.", 1.0f);
            _burnSevMul.Validator = ValidateNonNegative;
            WireFloatHandle(_burnSevMul);

            // Butchering
            _butcherBase = Settings.GetHandle("Butchering_BaseChance", "Butchering: base trigger chance", "Base chance per butchering action for accidents.", 0.00005f);
            _butcherBase.Validator = ValidateNonNegative;
            WireFloatHandle(_butcherBase);
            _butcherChanceMul = Settings.GetHandle("Butchering_ChanceMultiplier", "Butchering: chance multiplier", "Scales butchering accident chance.", 1.0f);
            _butcherChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_butcherChanceMul);
            _butcherSevMul = Settings.GetHandle("Butchering_SeverityMultiplier", "Butchering: severity multiplier", "Scales severity of butchering injuries.", 1.0f);
            _butcherSevMul.Validator = ValidateNonNegative;
            WireFloatHandle(_butcherSevMul);

            // Tripping
            _tripBase = Settings.GetHandle("Tripping_BaseChance", "Tripping: base trigger chance", "Base per-step chance over difficult terrain.", 0.00005f);
            _tripBase.Validator = ValidateNonNegative;
            WireFloatHandle(_tripBase);
            _tripChanceMul = Settings.GetHandle("Tripping_ChanceMultiplier", "Tripping: chance multiplier", "Scales tripping accident chance.", 1.0f);
            _tripChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_tripChanceMul);
            _tripSevMul = Settings.GetHandle("Tripping_SeverityMultiplier", "Tripping: severity multiplier", "Scales severity of sprains from tripping.", 1.0f);
            _tripSevMul.Validator = ValidateNonNegative;
            WireFloatHandle(_tripSevMul);

            // Eating
            _eatChokeBase = Settings.GetHandle("Eating_ChokingBaseChance", "Eating: choking base chance", "Base chance to choke during a chew cycle.", 0.00008f);
            _eatChokeBase.Validator = ValidateNonNegative;
            WireFloatHandle(_eatChokeBase);
            _eatChokeChanceMul = Settings.GetHandle("Eating_ChokingChanceMultiplier", "Eating: choking chance multiplier", "Scales choking chance.", 1.0f);
            _eatChokeChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_eatChokeChanceMul);
            _eatChokeSevMul = Settings.GetHandle("Eating_ChokingSeverityMultiplier", "Eating: choking severity multiplier", "Scales choking hediff severity.", 1.0f);
            _eatChokeSevMul.Validator = ValidateNonNegative;
            WireFloatHandle(_eatChokeSevMul);
            _eatSpillBase = Settings.GetHandle("Eating_SpillBaseChance", "Eating: spill base chance", "Base chance to spill food while eating.", 0.00012f);
            _eatSpillBase.Validator = ValidateNonNegative;
            WireFloatHandle(_eatSpillBase);
            _eatSpillChanceMul = Settings.GetHandle("Eating_SpillChanceMultiplier", "Eating: spill chance multiplier", "Scales spill chance.", 1.0f);
            _eatSpillChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_eatSpillChanceMul);

            // Work
            _workBase = Settings.GetHandle("Work_BaseChance", "Work: base trigger chance", "Base per-tick chance while doing work jobs.", 0.000001f);
            _workBase.Validator = ValidateNonNegative;
            WireFloatHandle(_workBase);
            _workChanceMul = Settings.GetHandle("Work_ChanceMultiplier", "Work: chance multiplier", "Scales generic work accident chance.", 1.0f);
            _workChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_workChanceMul);
            _workSevMul = Settings.GetHandle("Work_SeverityMultiplier", "Work: severity multiplier", "Scales severity of work injuries.", 1.0f);
            _workSevMul.Validator = ValidateNonNegative;
            WireFloatHandle(_workSevMul);

            // Sleep
            _sleepBase = Settings.GetHandle("Sleep_BaseChance", "Sleep: base trigger chance", "Base per-tick chance to trigger a nightmare while sleeping.", 0.00002f);
            _sleepBase.Validator = ValidateNonNegative;
            WireFloatHandle(_sleepBase);
            _sleepChanceMul = Settings.GetHandle("Sleep_ChanceMultiplier", "Sleep: chance multiplier", "Scales nightmare trigger chance.", 1.0f);
            _sleepChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_sleepChanceMul);

            // Animals
            _milkBase = Settings.GetHandle("Animals_MilkingBaseChance", "Animals: milking base chance", "Base per-tick chance of a kick while milking.", 0.00006f);
            _milkBase.Validator = ValidateNonNegative;
            WireFloatHandle(_milkBase);
            _shearBase = Settings.GetHandle("Animals_ShearingBaseChance", "Animals: shearing base chance", "Base per-tick chance of a mishap while shearing.", 0.00008f);
            _shearBase.Validator = ValidateNonNegative;
            WireFloatHandle(_shearBase);
            _trainBase = Settings.GetHandle("Animals_TrainingBaseChance", "Animals: training base chance", "Base per-tick chance of a bite while training.", 0.00005f);
            _trainBase.Validator = ValidateNonNegative;
            WireFloatHandle(_trainBase);
            _milkChanceMul = Settings.GetHandle("Animals_MilkingChanceMultiplier", "Animals: milking chance multiplier", "Scales milking accident chance.", 1.0f);
            _milkChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_milkChanceMul);
            _shearChanceMul = Settings.GetHandle("Animals_ShearingChanceMultiplier", "Animals: shearing chance multiplier", "Scales shearing accident chance.", 1.0f);
            _shearChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_shearChanceMul);
            _trainChanceMul = Settings.GetHandle("Animals_TrainingChanceMultiplier", "Animals: training chance multiplier", "Scales training accident chance.", 1.0f);
            _trainChanceMul.Validator = ValidateNonNegative;
            WireFloatHandle(_trainChanceMul);
            _animalSevMul = Settings.GetHandle("Animals_SeverityMultiplier", "Animals: severity multiplier", "Scales severity of animal-related injuries.", 1.0f);
            _animalSevMul.Validator = ValidateNonNegative;
            WireFloatHandle(_animalSevMul);

            // Accident storm
            _stormHourlyChance = Settings.GetHandle("AccidentStorm_HourlyQueueChance", "Accident storm: hourly queue chance", "Probability (0..1) to enqueue an incident each hour while the storm is active.", 0.5f);
            _stormHourlyChance.Validator = ValidateChance01;
            WireFloatHandle(_stormHourlyChance);
            _stormHourlyForeshadow = Settings.GetHandle("AccidentStorm_HourlyForeshadow", "Accident storm: show hourly foreshadow", "When enabled, show a neutral alert when the storm enqueues an hourly incident.", false);

            ApplyToStatics();
        }

        public override void SettingsChanged()
        {
            ApplyToStatics();
        }

        private void ApplyToStatics()
        {
            KitchenFiresSettings.GlobalChanceMultiplier = ClampMin(_globalChanceMul.Value, 0f);
            KitchenFiresSettings.GlobalSeverityMultiplier = ClampMin(_globalSeverityMul.Value, 0f);

            KitchenFiresSettings.CookingIncidentBaseChance = Clamp01Plus(_cookBase.Value);
            KitchenFiresSettings.CookingIncidentChanceMultiplier = ClampMin(_cookChanceMul.Value, 0f);
            KitchenFiresSettings.KitchenFireSizeMultiplier = ClampMin(_fireSizeMul.Value, 0f);
            KitchenFiresSettings.KitchenExplosionRadiusMultiplier = ClampMin(_explRadiusMul.Value, 0f);
            KitchenFiresSettings.KitchenExplosionDamageMultiplier = ClampMin(_explDamageMul.Value, 0f);
            KitchenFiresSettings.KitchenBurnSeverityMultiplier = ClampMin(_burnSevMul.Value, 0f);

            KitchenFiresSettings.ButcheringBaseChance = Clamp01Plus(_butcherBase.Value);
            KitchenFiresSettings.ButcheringChanceMultiplier = ClampMin(_butcherChanceMul.Value, 0f);
            KitchenFiresSettings.ButcheringSeverityMultiplier = ClampMin(_butcherSevMul.Value, 0f);

            KitchenFiresSettings.TrippingBaseChance = Clamp01Plus(_tripBase.Value);
            KitchenFiresSettings.TrippingChanceMultiplier = ClampMin(_tripChanceMul.Value, 0f);
            KitchenFiresSettings.TrippingSeverityMultiplier = ClampMin(_tripSevMul.Value, 0f);

            KitchenFiresSettings.EatingChokingBaseChance = Clamp01Plus(_eatChokeBase.Value);
            KitchenFiresSettings.EatingChokingChanceMultiplier = ClampMin(_eatChokeChanceMul.Value, 0f);
            KitchenFiresSettings.EatingChokingSeverityMultiplier = ClampMin(_eatChokeSevMul.Value, 0f);
            KitchenFiresSettings.EatingSpillBaseChance = Clamp01Plus(_eatSpillBase.Value);
            KitchenFiresSettings.EatingSpillChanceMultiplier = ClampMin(_eatSpillChanceMul.Value, 0f);

            KitchenFiresSettings.WorkAccidentBaseChance = Clamp01Plus(_workBase.Value);
            KitchenFiresSettings.WorkAccidentChanceMultiplier = ClampMin(_workChanceMul.Value, 0f);
            KitchenFiresSettings.WorkAccidentSeverityMultiplier = ClampMin(_workSevMul.Value, 0f);

            KitchenFiresSettings.SleepNightmareBaseChance = Clamp01Plus(_sleepBase.Value);
            KitchenFiresSettings.SleepNightmareChanceMultiplier = ClampMin(_sleepChanceMul.Value, 0f);

            KitchenFiresSettings.AnimalMilkingAccidentBaseChance = Clamp01Plus(_milkBase.Value);
            KitchenFiresSettings.AnimalShearingAccidentBaseChance = Clamp01Plus(_shearBase.Value);
            KitchenFiresSettings.AnimalTrainingAccidentBaseChance = Clamp01Plus(_trainBase.Value);
            KitchenFiresSettings.AnimalMilkingAccidentChanceMultiplier = ClampMin(_milkChanceMul.Value, 0f);
            KitchenFiresSettings.AnimalShearingAccidentChanceMultiplier = ClampMin(_shearChanceMul.Value, 0f);
            KitchenFiresSettings.AnimalTrainingAccidentChanceMultiplier = ClampMin(_trainChanceMul.Value, 0f);
            KitchenFiresSettings.AnimalAccidentSeverityMultiplier = ClampMin(_animalSevMul.Value, 0f);

            KitchenFiresSettings.AccidentStormHourlyQueueChance = _stormHourlyChance.Value < 0f ? 0f : (_stormHourlyChance.Value > 1f ? 1f : _stormHourlyChance.Value);
            KitchenFiresSettings.AccidentStormHourlyForeshadow = _stormHourlyForeshadow.Value;
        }

        private string ClampTextToFloat(string s)
        {
            return s;
        }

        private bool ValidateNonNegative(string s)
        {
            float v;
            return float.TryParse(s, out v) && v >= 0f;
        }

        private bool ValidateChance01(string s)
        {
            float v;
            if (!float.TryParse(s, out v)) return false;
            return v >= 0f && v <= 1f;
        }

        private float Clamp01Plus(float v)
        {
            return v < 0f ? 0f : v;
        }

        private float ClampMin(float v, float min)
        {
            return v < min ? min : v;
        }
    }
}