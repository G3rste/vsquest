using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorHasQuests : EntityBehavior
    {
        public EntityBehaviorHasQuests(Entity entity) : base(entity)
        {
        }

        public List<Quest> questList { get; set; }

        public void updateAfterKill(Entity victim){
            // TODO
            entity.Api.Logger.Debug("Entity killed: {0}", victim?.Code?.Path);
        }

        public override string PropertyName()
        {
            return "hasquests";
        }
    }
}