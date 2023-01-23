using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public abstract class BlockLinearMPBase : BlockMPBase, ILinearMechanicalPowerBlock
    {
        public bool mirrored;

        public virtual bool MirroredLinearMotion(IWorldAccessor world, BlockPos pos, BlockFacing facing)
        {
            return false;
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return false;
        }

        public abstract bool HasLinearMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);

        public override bool tryConnect(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, BlockFacing face)
        {
            if (world.BlockAccessor.GetBlock(pos.AddCopy(face)) is ILinearMechanicalPowerBlock linearMechanicalBlock &&
                linearMechanicalBlock.HasLinearMechPowerConnectorAt(world, pos, face.Opposite))
            {
                linearMechanicalBlock.DidConnectAt(world, pos.AddCopy(face), face.Opposite);
                WasPlaced(world, pos, face);
                mirrored = linearMechanicalBlock.MirroredLinearMotion(world, pos, face.Opposite);
                return true;
            }

            return false;
        }
    }
}