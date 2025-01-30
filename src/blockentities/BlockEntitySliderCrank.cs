using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace sawmill
{
    public class BlockEntitySliderCrank : BlockEntity
    {
        private BlockFacing Facing;
        public bool connectedCW = false;
        public bool connectedCCW = false;
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Facing = BlockFacing.FromCode(Block.Variant["side"]);
            if (Facing == null)
                Facing = BlockFacing.NORTH;

            Block checkBlock = api.World.BlockAccessor.GetBlock(Pos.AddCopy(Facing.GetCW()));
            if (checkBlock is ILinearMechanicalPowerBlock && checkBlock is not BlockSliderCrank)
            {
                connectedCW = true;
            }
            checkBlock = api.World.BlockAccessor.GetBlock(Pos.AddCopy(Facing.GetCCW()));
            if (checkBlock is ILinearMechanicalPowerBlock && checkBlock is not BlockSliderCrank)
            {
                connectedCCW = true;
            }
        }

        public bool NeedsPistonSupport()
        {
            return connectedCW || connectedCCW;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            connectedCW = tree.GetBool("connectedCW", false);
            connectedCCW = tree.GetBool("connectedCCW", false);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("connectedCW", connectedCW);
            tree.SetBool("connectedCCW", connectedCCW);
        }
    }
}
