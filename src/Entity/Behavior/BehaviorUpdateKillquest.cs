using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class BehaviorUpdateKillquest : EntityBehavior
    {
        public BehaviorUpdateKillquest(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "updateKillQuest";
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            if (damageSourceForDeath.SourceEntity is EntityPlayer)
            {
                damageSourceForDeath.SourceEntity.GetBehavior<EntityBehaviorHasQuests>().updateAfterKill(entity);
            }
        }
    }
}