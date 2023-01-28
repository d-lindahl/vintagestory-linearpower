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
        
        private bool _mirroredLinearMotion;
        public bool MirroredLinearMotion
        {
            get { return _mirroredLinearMotion; }
            set {
                _mirroredLinearMotion = value;
                if (Api.Side == EnumAppSide.Server)
                {
                    Blockentity.MarkDirty();
                }
            }
        }

        public virtual bool IsMirroredLinearMotion(IWorldAccessor world, BlockPos pos, BlockFacing facing)
        {
            return _mirroredLinearMotion;
            
        }

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

            if (beMechBase != null && mechBlock is BlockLinearMPBase linearBlock && linearBlock.HasLinearMechPowerConnectorAt(api.World, exitPos, propagatePath.OutFacing.Opposite))
            {
                BEBehaviorLinearMPBase beLinear = beMechBase as BEBehaviorLinearMPBase;
                beMechBase.Api = api;
                if (!beMechBase.JoinAndSpreadNetworkToNeighbours(api, network, propagatePath, out missingChunkPos))
                {
                    return false;
                }
                MirroredLinearMotion = beLinear.IsMirroredLinearMotion(api.World, exitPos, propagatePath.OutFacing.Opposite);
            }
            //UpdateMirroredLinearMotion();
            return true;
        }

        public override bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, MechanicalNetwork network, MechPowerPath exitTurnDir, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            if (this.network?.networkId == network?.networkId)
            {
                return true;
            }

            SetPropagationDirection(exitTurnDir);
            JoinNetwork(network);
            (Block as IMechanicalPowerBlock).DidConnectAt(api.World, Position, exitTurnDir.OutFacing.Opposite);
            MechPowerPath[] mechPowerExits = GetMechPowerExits(exitTurnDir);
            for (int i = 0; i < mechPowerExits.Length; i++)
            {
                BlockPos exitPos = Position.AddCopy(mechPowerExits[i].OutFacing);
                if (!spreadTo(api, network, exitPos, mechPowerExits[i], out missingChunkPos))
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

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
            Api.Logger.Notification("WasPlaced:" + Position + ":" + connectedOnFacing);
            if ((Api.Side != EnumAppSide.Client && OutFacingForNetworkDiscovery != null) || connectedOnFacing == null)
            {
                return;
            }
            linearTryConnect(connectedOnFacing);
        }

        public virtual bool linearTryConnect(BlockFacing toFacing)
        {
            if (Api == null)
            {
                return false;
            }

            BlockPos blockPos = Position.AddCopy(toFacing);
            IMechanicalPowerBlock mechanicalPowerBlock = Api.World.BlockAccessor.GetBlock(blockPos) as IMechanicalPowerBlock;

            if (mechanicalPowerBlock != null && (Block as IMechanicalPowerBlock).HasMechPowerConnectorAt(Api.World, blockPos.AddCopy(toFacing), toFacing) && mechanicalPowerBlock.HasMechPowerConnectorAt(Api.World, blockPos, toFacing.Opposite))
            {
                return base.tryConnect(toFacing);
            }

            if (!(mechanicalPowerBlock is ILinearMechanicalPowerBlock linearBlock) || !linearBlock.HasLinearMechPowerConnectorAt(Api.World, blockPos, toFacing.Opposite))
            {
                return false;
            }

            MechanicalNetwork mechanicalNetwork = mechanicalPowerBlock.GetNetwork(Api.World, blockPos);
            if (mechanicalNetwork != null)
            {
                IMechanicalPowerDevice behavior = Api.World.BlockAccessor.GetBlockEntity(blockPos).GetBehavior<BEBehaviorMPBase>();
                mechanicalPowerBlock.DidConnectAt(Api.World, blockPos, toFacing.Opposite);
                MechPowerPath mechPowerPath = new MechPowerPath(toFacing, behavior.GetGearedRatio(toFacing), blockPos, !behavior.IsPropagationDirection(Position, toFacing));
                SetPropagationDirection(mechPowerPath);
                MechPowerPath[] mechPowerExits = GetMechPowerExits(mechPowerPath);
                JoinNetwork(mechanicalNetwork);
                for (int i = 0; i < mechPowerExits.Length; i++)
                {
                    BlockPos exitPos = Position.AddCopy(mechPowerExits[i].OutFacing);
                    if (!spreadTo(Api, mechanicalNetwork, exitPos, mechPowerExits[i], out var _))
                    {
                        LeaveNetwork();
                        return true;
                    }
                }
                return true;
            }

            if (network != null)
            {
                BEBehaviorLinearMPBase bEBehaviorLinearMPBase = Api.World.BlockAccessor.GetBlockEntity(blockPos)?.GetBehavior<BEBehaviorLinearMPBase>();
                if (bEBehaviorLinearMPBase != null)
                {
                    return bEBehaviorLinearMPBase.linearTryConnect(toFacing.Opposite);
                }
            }

            return false;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("mirroredLinearMotion", _mirroredLinearMotion);
            tree.SetInt("direction", direction);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _mirroredLinearMotion = tree.GetBool("mirroredLinearMotion");
            direction = tree.GetInt("direction");
        }
    }
}