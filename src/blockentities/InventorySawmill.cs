using Vintagestory.API.Common;

namespace sawmill
{
    public class InventorySawmill : InventoryDisplayed
    {
        private BlockEntitySawmill beSawmill;

        public InventorySawmill(BlockEntity be, int size)
          : base(be, size, "sawmill-0", (ICoreAPI)null)
        {
            beSawmill = be as BlockEntitySawmill;
            for (int index = 0; index < size; ++index)
                slots[index].MaxSlotStackSize = 1;
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            return targetSlot == beSawmill.SawInventory ? 0.0f : base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }
    }
}
