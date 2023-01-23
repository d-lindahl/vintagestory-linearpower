using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public abstract class BEBehaviorLinearMPBase : BEBehaviorMPBase, ILinearMechanicalPowerRenderable
    {
        protected float lastKnownLinearOffset = 0;
        protected float lastKnownLinearOffsetMirror = 0;
        protected readonly float crankRadius = 1.5f / 16;
        protected readonly float rodLength = 5f / 16;
        protected int direction = 1;
        
        protected BEBehaviorLinearMPBase(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        public virtual float LinearOffset(bool mirror = false)
        { 
            if (network == null)
            {
                return lastKnownLinearOffset;
            }
                
            if (direction == 1 && AngleRad < GameMath.PI/16)
            {
                direction = -1;
                OnClientSideChangeDirection(direction);
                OnClientSideFullCycle();
            }
            if (direction == -1 && AngleRad > GameMath.PI - GameMath.PI / 16 && AngleRad < GameMath.PI + GameMath.PI / 16)
            {
                direction = 1;
                OnClientSideChangeDirection(direction);
            }
            return lastKnownLinearOffset = GetCurrentLinearOffset(mirror);
        }

        public virtual void OnClientSideChangeDirection(int newDirection)
        {
        }

        public virtual void OnClientSideFullCycle()
        {
        }

        private float GetCurrentLinearOffset(bool mirror = false)
        {
            float rodAngle = mirror ? 
                (float)Math.Asin(crankRadius * Math.Sin(GameMath.TWOPI - AngleRad) / rodLength) : 
                (float)Math.Asin(crankRadius * Math.Sin(AngleRad) / rodLength);
            float rodCrankRad = GameMath.PI - AngleRad - rodAngle;
            return (float)(rodLength * Math.Sin(rodCrankRad) / Math.Sin(AngleRad)) - (rodLength - crankRadius);
        }

        protected override bool spreadTo(ICoreAPI api, MechanicalNetwork network, BlockPos exitPos, MechPowerPath propagatePath, out Vec3i missingChunkPos)
        {
            BEBehaviorMPBase beMechBase = api.World.BlockAccessor.GetBlockEntity(exitPos)?.GetBehavior<BEBehaviorMPBase>();
            IMechanicalPowerBlock mechBlock = beMechBase?.Block as IMechanicalPowerBlock;
            
            if (beMechBase != null && !(beMechBase is BEBehaviorLinearMPBase) && (Block as ILinearMechanicalPowerBlock).HasMechPowerConnectorAt(api.World, exitPos, propagatePath.OutFacing))
            {
                return base.spreadTo(api, network, exitPos, propagatePath, out missingChunkPos);
            }

            missingChunkPos = null;
            if (beMechBase == null && api.World.BlockAccessor.GetChunkAtBlockPos(exitPos) == null)
            {
                if (OutsideMap(api.World.BlockAccessor, exitPos)) return true;  //Network discovery should not fail if there cannot be a block in this position
                missingChunkPos = new Vec3i(exitPos.X / api.World.BlockAccessor.ChunkSize, exitPos.Y / api.World.BlockAccessor.ChunkSize, exitPos.Z / api.World.BlockAccessor.ChunkSize);
                return false;
            }

            if (beMechBase != null && mechBlock is ILinearMechanicalPowerBlock linearBlock && linearBlock.HasLinearMechPowerConnectorAt(api.World, exitPos, propagatePath.OutFacing.Opposite))
            {
                beMechBase.Api = api;
                if (!beMechBase.JoinAndSpreadNetworkToNeighbours(api, network, propagatePath, out missingChunkPos))
                {
                    return false;
                }
            }
            return true;
        }
        private bool OutsideMap(IBlockAccessor blockAccessor, BlockPos exitPos)
        {
            if (exitPos.X < 0 || exitPos.X >= blockAccessor.MapSizeX) return true;
            if (exitPos.Y < 0 || exitPos.Y >= blockAccessor.MapSizeY) return true;
            if (exitPos.Z < 0 || exitPos.Z >= blockAccessor.MapSizeZ) return true;
            return false;
        }

    }
}