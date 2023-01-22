using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class BlockSawmill : BlockLinearMPBase
    {
        private BlockFacing orientation;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            orientation = BlockFacing.FromFirstLetter(Variant["side"][0]);
        }
        public bool IsOrientedTo(BlockFacing facing) => facing == orientation;

        public override bool OnBlockInteractStart(
          IWorldAccessor world,
          IPlayer byPlayer,
          BlockSelection blockSel)
        {
            return world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntitySawmill blockEntity ? blockEntity.OnInteract(byPlayer) : base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool MirroredLinearMotion(IWorldAccessor world, BlockPos pos, BlockFacing facing)
        {
            return (world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySawmill).mirroredLinearMotion;
        }

        public override bool HasLinearMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return orientation == face || orientation == face.Opposite;
        }

        public override bool TryPlaceBlock(
            IWorldAccessor world,
            IPlayer byPlayer,
            ItemStack itemstack,
            BlockSelection blockSel,
            ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;
            foreach (BlockFacing blockFacing in BlockFacing.HORIZONTALS)
            {
                BlockPos pos = blockSel.Position.AddCopy(blockFacing);
                if (world.BlockAccessor.GetBlock(pos) is ILinearMechanicalPowerBlock block && block.HasLinearMechPowerConnectorAt(world, pos, blockFacing.Opposite))
                {
                    AssetLocation blockCode = new AssetLocation(Code.Clone().WithoutPathAppendix(Code.EndVariant()) + blockFacing.Code);
                    if (world.GetBlock(blockCode).DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                    {
                        block.DidConnectAt(world, pos, blockFacing.Opposite);
                        WasPlaced(world, blockSel.Position, blockFacing);
                        (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySawmill).mirroredLinearMotion =
                            block.MirroredLinearMotion(world, pos, blockFacing.Opposite);
                        return true;
                    }
                }
            }
            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (world.BlockAccessor.GetBlock(pos.AddCopy(face)) is ILinearMechanicalPowerBlock block)
            {
                (world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySawmill).mirroredLinearMotion =
                            block.MirroredLinearMotion(world, pos, face.Opposite);
            }
            else if (world.BlockAccessor.GetBlockEntity(pos.AddCopy(face)) is BlockEntity nonLinear)
            {
                BEBehaviorMPBase behavior = nonLinear.GetBehavior<BEBehaviorMPBase>();
                if (behavior != null)
                {
                }
            }
        }

        public override bool tryConnect(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, BlockFacing face)
        {
            return base.tryConnect(world, byPlayer, pos, face);
        }

        public override ItemStack[] GetDrops(
          IWorldAccessor world,
          BlockPos pos,
          IPlayer byPlayer,
          float dropQuantityMultiplier = 1f)
        {
            ItemStack sawmillFrame = new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("north")), 1);
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntitySawmill blockEntity)
                return blockEntity.GetDrops(sawmillFrame);
            return new ItemStack[1] { sawmillFrame };
        }
    }
}
