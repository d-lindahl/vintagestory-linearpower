using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public interface ILinearMechanicalPowerBlock : IMechanicalPowerBlock
    {
        bool HasLinearMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);

        bool MirroredLinearMotion(IWorldAccessor world, BlockPos pos, BlockFacing facing);
    }
}