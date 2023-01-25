using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class BEBehaviorMPSliderCrank : BEBehaviorLinearMPBase
    {
        private ICoreClientAPI capi;
        public BlockEntitySliderCrank entity;

        public BlockFacing Facing { get; private set; }

        public BEBehaviorMPSliderCrank(BlockEntity blockentity) : base(blockentity)
        {
            Facing = BlockFacing.FromCode(this.Block.Variant["side"]);
            if (Facing == null)
            {
                Facing = BlockFacing.NORTH;
            }
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            Facing = BlockFacing.FromCode(this.Block.Variant["side"]);
            if (Facing == null)
            {
                Facing = BlockFacing.NORTH;
            }

            base.Initialize(api, properties);
            entity = Blockentity as BlockEntitySliderCrank;
            switch (Facing.Index)
            {
                case 0:
                    AxisSign = new int[3] { 0, 0, 1 };
                    break;
                case 2:
                    AxisSign = new int[3] { 0, 0, -1 };
                    break;
                case 1:
                    AxisSign = new int[3] { 1, 0, 0 };
                    break;
                case 3:
                    AxisSign = new int[3] { -1, 0, 0 };
                    break;
            }
            if (api.Side == EnumAppSide.Client)
                capi = api as ICoreClientAPI;
        }

        public override bool IsMirroredLinearMotion(IWorldAccessor world, BlockPos pos, BlockFacing outgoingFace)
        {
            return outgoingFace == Facing.GetCCW();
        }

        protected override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            return new MechPowerPath[3]
            {
                entryDir,
                new MechPowerPath(entryDir.OutFacing.GetCW(), entryDir.gearingRatio, Position, !entryDir.invert),
                new MechPowerPath(entryDir.OutFacing.GetCCW(), entryDir.gearingRatio, Position, !entryDir.invert)
            };
        }

        public override float GetResistance() => 0.0005f;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // add main support
            Shape shape = capi.Assets.TryGet("linearpower:shapes/block/wood/mechanics/slidercrank-support.json").ToObject<Shape>();
            
            float rotateY;
            switch (Facing.Index)
            {
                case 0:
                    rotateY = 270f;
                    break;
                case 1:
                    rotateY = 180f;
                    break;
                case 2:
                    rotateY = 90f;
                    break;
                default:
                    rotateY = 0;
                    break;
            }

            capi.Tesselator.TesselateShape((CollectibleObject)this.Block, shape, out MeshData modeldata, new Vec3f(0.0f, rotateY, 0.0f));
            mesher.AddMeshData(modeldata);

            // add piston support if there's a connection
            if (entity.connectedCW || entity.connectedCCW)
            {
                shape = capi.Assets.TryGet("linearpower:shapes/block/wood/mechanics/slidercrank-slidersupport.json").ToObject<Shape>();

                if (entity.connectedCCW)
                {
                    capi.Tesselator.TesselateShape((CollectibleObject)this.Block, shape, out modeldata, new Vec3f(0.0f, rotateY, 0.0f));
                    mesher.AddMeshData(modeldata);
                }
                if (entity.connectedCW)
                {
                    capi.Tesselator.TesselateShape((CollectibleObject)this.Block, shape, out modeldata, new Vec3f(0, rotateY + 180, 180));
                    mesher.AddMeshData(modeldata);
                }
            }

            return base.OnTesselation(mesher, tesselator);
        }
    }
}
