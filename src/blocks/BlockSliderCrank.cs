using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class BlockSliderCrank : BlockLinearMPBase
    {
        private BlockFacing orientation;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            orientation = BlockFacing.FromFirstLetter(Variant["side"][0]);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;
            if (!world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(BlockFacing.DOWN)).SideSolid[BlockFacing.UP.Index])
            {
                // we need solid support
                failureCode = "belowblockcannotsupport";
                return false;
            }
            // if there's an adjecent block with a mech connector (but not linear) pointing at us, face that and connect
            foreach (BlockFacing blockFacing in BlockFacing.HORIZONTALS)
            {
                BlockPos pos = blockSel.Position.AddCopy(blockFacing);
                if (world.BlockAccessor.GetBlock(pos) is IMechanicalPowerBlock block && !(world.BlockAccessor.GetBlock(pos) is ILinearMechanicalPowerBlock)
                    && block.HasMechPowerConnectorAt(world, pos, blockFacing.Opposite))
                {
                    AssetLocation blockCode = new AssetLocation("linearpower:" + FirstCodePart() + "-" + blockFacing);
                    if (world.GetBlock(blockCode).DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                    {
                        block.DidConnectAt(world, pos, blockFacing.Opposite);
                        WasPlaced(world, blockSel.Position, blockFacing);
                        return true;
                    }
                }
            }
            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            // if the block below now doesn't have a solid up-face we break
            Block block = world.BlockAccessor.GetBlock(neibpos);
            if (pos.FacingFrom(neibpos) == BlockFacing.UP && !block.SideSolid[BlockFacing.UP.Index])
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
            if (!(block is ILinearMechanicalPowerBlock))
            {
                BlockEntitySliderCrank entity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySliderCrank;
                if (neibpos.FacingFrom(pos) == orientation.GetCCW())
                {
                    entity.connectedCCW = false;
                }
                else if(neibpos.FacingFrom(pos) == orientation.GetCW())
                {
                    entity.connectedCW = false;
                }
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override bool MirroredLinearMotion(IWorldAccessor world, BlockPos pos, BlockFacing outgoingFace)
        {
            return outgoingFace == orientation.GetCW();
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == orientation;
        }

        public override void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
        {

            base.WasPlaced(world, ownPos, connectedOnFacing);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            bool blockExists = world.BlockAccessor.GetBlock(pos.AddCopy(face)) is ILinearMechanicalPowerBlock;
            BlockEntitySliderCrank entity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySliderCrank;
            if (face == orientation.GetCW())
            {
                entity.connectedCW = blockExists;
            }
            else if (face == orientation.GetCCW())
            {
                entity.connectedCCW = blockExists;
            }
        }

        public override bool HasLinearMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == orientation.GetCW() || face == orientation.GetCCW();
        }
    }
}
