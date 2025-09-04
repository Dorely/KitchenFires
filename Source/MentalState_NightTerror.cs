using Verse;
using Verse.AI;

namespace KitchenFires
{
    // Short, panic-flee style mental state for nightmares
    public class MentalState_NightTerror : MentalState_PanicFlee
    {
        protected override bool CanEndBeforeMaxDurationNow => false;
        public override bool AllowRestingInBed => false;
    }
}
