using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest{
    public class ItemDebugTool : Item {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if(byEntity.Api is ICoreClientAPI capi && byEntity is EntityPlayer playerEntity){
                (playerEntity.Player as IClientPlayer).ShowChatNotification("Selected entity id: " + entitySel?.Entity?.EntityId);
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}