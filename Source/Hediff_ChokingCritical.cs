using RimWorld;
using UnityEngine;
using Verse;

namespace KitchenFires
{
    public class Hediff_ChokingCritical : HediffWithComps
    {
        private float _intervalFactor;
        private const int SeverityChangeInterval = 4000;
        private const float TendSuccessChanceFactor = 0.6f;
        private const float TendSeverityReduction = 0.25f;

        public override void PostMake()
        {
            base.PostMake();
            _intervalFactor = Rand.Range(0.8f, 1.6f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _intervalFactor, "intervalFactor", 1f);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick((int)(SeverityChangeInterval * _intervalFactor), delta))
            {
                Severity += Rand.Range(-0.08f, 0.12f);
                Severity = Mathf.Clamp01(Severity);
            }
        }

        public override void Tended(float quality, float maxQuality, int batchPosition = 0)
        {
            base.Tended(quality, maxQuality, batchPosition);
            float chance = TendSuccessChanceFactor * quality;
            if (Rand.Value < chance)
            {
                if (batchPosition == 0 && pawn.Spawned)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "TextMote_TreatSuccess".Translate(chance.ToStringPercent()), 6.5f);
                }
                Severity = Mathf.Max(0f, Severity - TendSeverityReduction);
            }
            else if (batchPosition == 0 && pawn.Spawned)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "TextMote_TreatFailed".Translate(chance.ToStringPercent()), 6.5f);
            }
        }
    }
}
