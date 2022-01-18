using Vintagestory.API.Common;

namespace VsQuest
{
    public class VsQuest : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("updatekillquest", typeof(BehaviorUpdateKillquest));
            api.RegisterEntityBehaviorClass("hasquests", typeof(EntityBehaviorHasQuests));
        }
    }
}
