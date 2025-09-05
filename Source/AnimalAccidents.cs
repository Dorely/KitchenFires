using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace KitchenFires
{
    public static class AnimalAccidentUtility
    {
        private const float BASE_MILK_ACCIDENT_CHANCE = 0.00006f;   // rare mishap while milking
        private const float BASE_SHEAR_ACCIDENT_CHANCE = 0.00008f;  // shearing is a bit trickier
        private const float BASE_TRAIN_ACCIDENT_CHANCE = 0.00005f;  // minor chance during training

        public static void CheckForAnimalAccident(Pawn pawn, JobDriver driver)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.IsColonist) return;
            if (pawn.Map == null) return;
            var job = pawn.CurJob; if (job == null) return;
            var def = job.def; if (def == null) return;

            try
            {
                if (def == JobDefOf.Milk)
                {
                    MaybeMilkingKick(pawn, driver);
                }
                else if (def == JobDefOf.Shear)
                {
                    MaybeShearingCut(pawn, driver);
                }
                else if (def == JobDefOf.Train)
                {
                    MaybeTrainingBite(pawn, driver);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KitchenFires] Animal accident check failed: {ex}");
            }
        }

        private static float SkillRiskMultiplier(Pawn pawn)
        {
            try
            {
                var skill = pawn.skills?.GetSkill(SkillDefOf.Animals);
                int level = skill?.Level ?? 0;
                if (level >= 15) return 0.45f;
                if (level >= 10) return 0.7f;
                if (level >= 5) return 1.0f;
                return 1.5f;
            }
            catch
            {
                return 1.0f;
            }
        }

        private static Pawn GetTargetAnimal(JobDriver driver)
        {
            try
            {
                var t = driver?.job?.GetTarget(TargetIndex.A).Thing as Pawn;
                if (t != null && t.RaceProps?.Animal == true) return t;
            }
            catch { }
            return null;
        }

        private static void MaybeMilkingKick(Pawn pawn, JobDriver driver)
        {
            float chance = BASE_MILK_ACCIDENT_CHANCE * SkillRiskMultiplier(pawn) * AccidentStormUtility.ChanceMultiplierFor(pawn.Map);
            if (!Rand.Chance(chance)) return;

            var animal = GetTargetAnimal(driver);
            ApplyKickInjury(pawn, animal);
        }

        private static void ApplyKickInjury(Pawn pawn, Pawn animal)
        {
            var hediffDef = DefDatabase<HediffDef>.GetNamed("Bruise", false) ?? HediffDefOf.Cut;
            var part = ChooseBodyPart(pawn, prefer: p =>
            {
                string n = p.def.defName.ToLowerInvariant();
                return n.Contains("leg") || n.Contains("torso") || n.Contains("pelvis") || n.Contains("spine");
            });
            if (part == null) return;

            var injury = HediffMaker.MakeHediff(hediffDef, pawn, part);
            injury.Severity = Rand.Range(0.12f, 0.40f);
            pawn.health.AddHediff(injury);

            pawn.stances?.stagger?.StaggerFor(Rand.RangeInclusive(45, 120));
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptOptional);

            string animalName = animal != null ? animal.NameShortColored.ToString() : "the animal";
            Messages.Message($"{pawn.NameShortColored} was kicked while milking {animalName}.", new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
            Log.Message($"[KitchenFires] Milking kick: {pawn.Name} injured on {part.Label}");
        }

        private static void MaybeShearingCut(Pawn pawn, JobDriver driver)
        {
            float chance = BASE_SHEAR_ACCIDENT_CHANCE * SkillRiskMultiplier(pawn) * AccidentStormUtility.ChanceMultiplierFor(pawn.Map);
            if (!Rand.Chance(chance)) return;

            var animal = GetTargetAnimal(driver);
            bool cutAnimal = animal != null && Rand.Chance(0.4f);

            if (cutAnimal)
            {
                ApplyShearCutAnimal(pawn, animal);
            }
            else
            {
                ApplyShearCutSelf(pawn);
            }
        }

        private static void ApplyShearCutSelf(Pawn pawn)
        {
            var part = ChooseBodyPart(pawn, prefer: p =>
            {
                string n = p.def.defName.ToLowerInvariant();
                return n.Contains("finger") || n.Contains("hand") || n.Contains("arm") || n.Contains("shoulder");
            });
            if (part == null) return;

            var injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
            injury.Severity = Rand.Range(0.10f, 0.35f);
            pawn.health.AddHediff(injury);

            pawn.jobs?.EndCurrentJob(JobCondition.InterruptOptional);
            Messages.Message($"{pawn.NameShortColored} cut themselves while shearing.", new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
            Log.Message($"[KitchenFires] Shearing cut (self): {pawn.Name} on {part.Label}");
        }

        private static void ApplyShearCutAnimal(Pawn handler, Pawn animal)
        {
            if (animal?.health == null) return;
            var part = ChooseBodyPart(animal, prefer: p =>
            {
                string n = p.def.defName.ToLowerInvariant();
                return p.depth == BodyPartDepth.Outside && !p.def.conceptual && (n.Contains("skin") || n.Contains("neck") || n.Contains("leg") || n.Contains("torso"));
            });
            part = part ?? animal.health.hediffSet.GetNotMissingParts().Where(p => !p.def.conceptual).RandomElementWithFallback(null);
            if (part == null) return;

            var injury = HediffMaker.MakeHediff(HediffDefOf.Cut, animal, part);
            injury.Severity = Rand.Range(0.08f, 0.30f);
            animal.health.AddHediff(injury);

            handler.jobs?.EndCurrentJob(JobCondition.InterruptOptional);
            Messages.Message($"{handler.NameShortColored} accidentally cut {animal.NameShortColored} while shearing.", new LookTargets(animal), MessageTypeDefOf.NegativeEvent);
            Log.Message($"[KitchenFires] Shearing cut (animal): {animal.LabelShort} on {part.Label}");
        }

        private static void MaybeTrainingBite(Pawn pawn, JobDriver driver)
        {
            float chance = BASE_TRAIN_ACCIDENT_CHANCE * SkillRiskMultiplier(pawn) * AccidentStormUtility.ChanceMultiplierFor(pawn.Map);
            if (!Rand.Chance(chance)) return;

            var animal = GetTargetAnimal(driver);
            ApplyBiteInjury(pawn, animal);
        }

        private static void ApplyBiteInjury(Pawn pawn, Pawn animal)
        {
            var part = ChooseBodyPart(pawn, prefer: p =>
            {
                string n = p.def.defName.ToLowerInvariant();
                return n.Contains("hand") || n.Contains("arm") || n.Contains("finger") || n.Contains("shoulder");
            });
            if (part == null) return;

            var injury = HediffMaker.MakeHediff(HediffDefOf.Bite, pawn, part);
            injury.Severity = Rand.Range(0.12f, 0.42f);
            pawn.health.AddHediff(injury);

            pawn.stances?.stagger?.StaggerFor(Rand.RangeInclusive(45, 120));
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptOptional);

            string animalName = animal != null ? animal.NameShortColored.ToString() : "the animal";
            Messages.Message($"{pawn.NameShortColored} was bitten while training {animalName}.", new LookTargets(pawn), MessageTypeDefOf.NegativeEvent);
            Log.Message($"[KitchenFires] Training bite: {pawn.Name} on {part.Label}");
        }

        private static BodyPartRecord ChooseBodyPart(Pawn pawn, Func<BodyPartRecord, bool> prefer)
        {
            var all = pawn.health?.hediffSet?.GetNotMissingParts()?.Where(p => !p.def.conceptual).ToList();
            if (all == null || all.Count == 0) return null;
            var outer = all.Where(p => p.depth == BodyPartDepth.Outside).ToList();
            var pref = outer.Where(prefer).ToList();
            if (pref.Any()) return pref.RandomElement();
            if (outer.Any()) return outer.RandomElement();
            return all.RandomElement();
        }
    }
}

