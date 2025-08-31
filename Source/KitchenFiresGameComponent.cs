using RimWorld;
using Verse;

namespace KitchenFires
{
    public class KitchenFiresGameComponent : GameComponent
    {
        public KitchenFiresGameComponent(Game game) : base()
        {
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            KitchenIncidentQueue.ExposeData();
        }
        
        public override void GameComponentTick()
        {
            // Clean up expired incidents periodically
            if (Find.TickManager.TicksGame % 2500 == 0) // Every game hour
            {
                // This will be handled internally by the queue when accessed
            }
        }
    }
}