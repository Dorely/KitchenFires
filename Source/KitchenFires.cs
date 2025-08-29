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
    [StaticConstructorOnStartup]
    public class KitchenFiresMod
    {
        static KitchenFiresMod()
        {
            var harmony = new Harmony("com.kitchenfires.mod");
            harmony.PatchAll();
            Log.Message("[KitchenFires] Mod initialized with Harmony patches.");
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
                // Store references to avoid ref parameter issues
                var toil = __result;
                var originalTickInterval = toil.tickIntervalAction;
                
                // Replace with our enhanced version
                toil.tickIntervalAction = delegate(int delta)
                {
                    try
                    {
                        // Call the original tick interval action first
                        originalTickInterval?.Invoke(delta);
                        
                        // Our incident check logic
                        Pawn actor = toil.actor;
                        if (actor != null && actor.IsColonist && actor.jobs.curDriver is JobDriver_DoBill doBillDriver)
                        {
                            Log.Message($"[KitchenFires] Recipe work tick for {actor.Name}");
                            
                            var bill = doBillDriver.job?.bill;
                            if (bill?.recipe != null && KitchenIncidentUtility.IsCookingRecipe(bill.recipe))
                            {
                                Log.Message($"[KitchenFires] Cooking recipe work: {bill.recipe.defName} for {actor.Name}");
                                KitchenIncidentUtility.CheckForKitchenIncident(actor);
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
    // Patch for ankle sprains when climbing over obstacles
    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Pawn_PathFollower_TryEnterNextPathCell_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_PathFollower __instance)
        {
            // Access the pawn through the private field
            var pawnField = typeof(Pawn_PathFollower).GetField("pawn", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (pawnField?.GetValue(__instance) is Pawn pawn)
            {
                AnkleSprainIncidentUtility.CheckForAnkleSprain(pawn);
            }
        }
    }
}