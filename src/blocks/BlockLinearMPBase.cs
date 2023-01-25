using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public abstract class BlockLinearMPBase : BlockMPBase, ILinearMechanicalPowerBlock
    {
        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return false;
        }

        public abstract bool HasLinearMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);

        public override void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
        {
            if (connectedOnFacing != null)
            {
                if (HasMechPowerConnectorAt(world, ownPos, connectedOnFacing))
                {
                    (world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorMPBase>())?.tryConnect(connectedOnFacing);
                }
                else if (HasLinearMechPowerConnectorAt(world, ownPos, connectedOnFacing))
                {
                    (world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorLinearMPBase>())?.linearTryConnect(connectedOnFacing);
                }
            }
        }

        public override bool tryConnect(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, BlockFacing face)
        {
            if (world.BlockAccessor.GetBlock(pos.AddCopy(face)) is ILinearMechanicalPowerBlock linearMechanicalBlock &&
                linearMechanicalBlock.HasLinearMechPowerConnectorAt(world, pos, face.Opposite))
            {
                linearMechanicalBlock.DidConnectAt(world, pos.AddCopy(face), face.Opposite);
                WasPlaced(world, pos, face);
                return true;
            }

            return false;
        }
    }
}