namespace KitchenFires
{
    public static class KitchenFiresSettings
    {
        // Global multipliers
        public static float GlobalChanceMultiplier = 1.0f;
        public static float GlobalSeverityMultiplier = 1.0f;

        // Cooking incidents
        public static float CookingIncidentBaseChance = 0.00002f;
        public static float CookingIncidentChanceMultiplier = 1.0f;
        public static float KitchenFireSizeMultiplier = 1.0f;
        public static float KitchenExplosionRadiusMultiplier = 1.0f;
        public static float KitchenExplosionDamageMultiplier = 1.0f;
        public static float KitchenBurnSeverityMultiplier = 1.0f;

        // Butchering accidents
        public static float ButcheringBaseChance = 0.00005f;
        public static float ButcheringChanceMultiplier = 1.0f;
        public static float ButcheringSeverityMultiplier = 1.0f;

        // Tripping accidents
        public static float TrippingBaseChance = 0.00005f;
        public static float TrippingChanceMultiplier = 1.0f;
        public static float TrippingSeverityMultiplier = 1.0f;

        // Eating accidents
        public static float EatingChokingBaseChance = 0.00008f;
        public static float EatingSpillBaseChance = 0.00012f;
        public static float EatingChokingChanceMultiplier = 1.0f;
        public static float EatingSpillChanceMultiplier = 1.0f;
        public static float EatingChokingSeverityMultiplier = 1.0f;

        // Work accidents
        public static float WorkAccidentBaseChance = 0.000001f;
        public static float WorkAccidentChanceMultiplier = 1.0f;
        public static float WorkAccidentSeverityMultiplier = 1.0f;        // Sleep accidents
        public static float SleepNightmareBaseChance = 0.00002f;
        public static float SleepNightmareChanceMultiplier = 1.0f;

        // Animal handling accidents
        public static float AnimalMilkingAccidentBaseChance = 0.00006f;
        public static float AnimalShearingAccidentBaseChance = 0.00008f;
        public static float AnimalTrainingAccidentBaseChance = 0.00005f;
        public static float AnimalMilkingAccidentChanceMultiplier = 1.0f;
        public static float AnimalShearingAccidentChanceMultiplier = 1.0f;
        public static float AnimalTrainingAccidentChanceMultiplier = 1.0f;
        public static float AnimalAccidentSeverityMultiplier = 1.0f;

        // Accident storm controls
        public static float AccidentStormHourlyQueueChance = 0.5f;
        public static bool AccidentStormHourlyForeshadow = false;
    }
}