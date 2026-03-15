using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest{
    public class ItemDebugTool : Item {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if(byEntity.Api is ICoreClientAPI && byEntity is EntityPlayer playerEntity && playerEntity is IClientPlayer player)
            {
                player.ShowChatNotification("Selected entity id: " + entitySel.Entity.EntityId);
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}