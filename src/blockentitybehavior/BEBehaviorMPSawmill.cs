using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace sawmill
{
    public class BEBehaviorMPSawmill : BEBehaviorLinearMPBase
    {
        public float prevProgress;
        public BlockEntitySawmill blockEntitySawmill;
        public float baseResistance = 3.5f;
        public BlockFacing facing;

        private readonly AssetLocation sawSound = new AssetLocation("linearpower:sounds/effect/saw");
        private readonly AssetLocation sawRevSound = new AssetLocation("linearpower:sounds/effect/saw-rev");

        private static readonly SimpleParticleProperties bitsParticles;

        static BEBehaviorMPSawmill()
        {
            bitsParticles = new SimpleParticleProperties(50f, 60f, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1f, 1f, 0.1f, 0.4f, EnumParticleModel.Quad);
            bitsParticles.AddQuantity = 0f;
            bitsParticles.MinVelocity.Set(-1f, 0f, -1f);
            bitsParticles.AddVelocity.Set(2f, 2f, 2f);
            bitsParticles.WithTerrainCollision = false;
            bitsParticles.ParticleModel = EnumParticleModel.Cube;
            bitsParticles.LifeLength = 1.5f;
            bitsParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.4f);
            bitsParticles.MinQuantity = 50f;
            bitsParticles.AddQuantity = 10f;
            
        }

        public BEBehaviorMPSawmill(BlockEntity blockentity): base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            blockEntitySawmill = Blockentity as BlockEntitySawmill;
            facing = BlockFacing.FromCode(Block.Variant["side"]);
            switch (facing.Index)
            {
                case 0:
                    AxisSign = new int[3] { 0, 0, 1 };
                    break;
                case 2:
                    AxisSign = new int[3] { 0, 0, -1 };
                    break;
                case 1:
                    AxisSign = new int[3] { -1, 0, 0 };
                    break;
                case 3:
                    AxisSign = new int[3] { 1, 0, 0 };
                    break;
            }
        }

        public override void OnClientSideChangeDirection(int newDirection)
        {
            ItemStack itemstack = blockEntitySawmill.Inventory[BlockEntitySawmill.inputInventoryIndex].Itemstack;
            if (itemstack != null)
            {
                AssetLocation sound = newDirection == 1 ? sawSound : sawRevSound;
                Api.World.PlaySoundAt(sound, Position.X + 0.5, Position.Y + 0.5, Position.Z + 0.5,null, range: 8f, pitch: 0.3f + 1 * network.Speed * GearedRatio);
                // TODO better sawing particles
                if (facing.IsAxisNS)
                {
                    bitsParticles.MinPos.Set(Position.X + 0.4, Position.Y + 0.5, Position.Z + 0.2);
                    bitsParticles.AddPos = new Vec3d(0.2, 0, 0.6);
                }
                else
                {
                    bitsParticles.MinPos.Set(Position.X + 0.2, Position.Y + 0.5, Position.Z + 0.3);
                    bitsParticles.AddPos = new Vec3d(0.6, 0, 0.2);
                }
                bitsParticles.Color = itemstack.Collectible.GetRandomColor(Api as ICoreClientAPI, itemstack);
                
                Api.World.SpawnParticles(bitsParticles);
            }
        }

        // TODO maybe take item being cut into account
        public override float GetResistance() => blockEntitySawmill.HasSaw ? 0.085f : 0.0005f;
    }
}
