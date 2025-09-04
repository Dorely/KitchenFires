using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace KitchenFires
{
    public class KitchenFiresMod : Mod
    {
        public KitchenFiresMod(ModContentPack content) : base(content)
        {
        }
    }

    [StaticConstructorOnStartup]
    public class KitchenFiresModStartup
    {
        static KitchenFiresModStartup()
        {
            var harmony = new Harmony("com.kitchenfires.mod");
            harmony.PatchAll();
            Log.Message("[KitchenFires] Mod initialized with Harmony patches and GameComponent.");
        }
    }

    // Harmony Patch - Patch the actual recipe work method that runs during cooking
    [HarmonyPatch(typeof(Toils_Recipe), "DoRecipeWork")]
    static class Toils_Recipe_Patch
    {
        static void Postfix(ref Toil __result)
        {
            try
            {
                var toil = __result;
                var originalTickInterval = toil.tickIntervalAction;
                toil.tickIntervalAction = delegate(int delta)
                {
                    try
                    {
                        originalTickInterval?.Invoke(delta);
                        Pawn actor = toil.actor;
                        if (actor != null && actor.IsColonist && actor.jobs.curDriver is JobDriver_DoBill doBillDriver)
                        {
                            var bill = doBillDriver.job?.bill;
                            if (bill?.recipe != null)
                            {
                                if (ButcheringAccidentUtility.IsButcheringRecipe(bill.recipe))
                                {
                                    ButcheringAccidentUtility.CheckForButcheringAccident(actor, bill.recipe);
                                }
                                else if (KitchenIncidentUtility.IsCookingRecipe(bill.recipe))
                                {
                                    KitchenIncidentUtility.CheckForKitchenIncident(actor);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[KitchenFires] Error in recipe work tick: {ex}");
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[KitchenFires] Error in Toils_Recipe patch: {ex}");
            }
        }
    }

    // Patch for ankle sprains when climbing over obstacles
    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Pawn_PathFollower_TryEnterNextPathCell_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_PathFollower __instance)
        {
            var pawnField = typeof(Pawn_PathFollower).GetField("pawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pawnField?.GetValue(__instance) is Pawn pawn)
            {
                var nextCell = __instance.nextCell;
                TrippingAccidentUtility.CheckForTrippingAccident(pawn, nextCell);
            }
        }
    }

    // Work accidents: check every job tick via JobDriver.DriverTick
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.DriverTick))]
    public static class JobDriver_DriverTick_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(JobDriver __instance)
        {
            try
            {
                var pawn = __instance?.pawn;
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonist) return;
                WorkAccidentUtility.CheckForWorkAccident(pawn);
                SleepAccidentUtility.CheckForSleepAccident(pawn, __instance);
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] Work/Sleep accident tick hook failed: {ex}");
            }
        }
    }
}
