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
            // Tick scheduled kitchen explosions
            KitchenExplosionScheduler.Tick();
        }
    }
}