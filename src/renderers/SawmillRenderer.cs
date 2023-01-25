using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class SawmillRenderer : MechBlockRenderer, ITexPositionSource
    {
        public static string[] metals = new string[9] { "copper", "tinbronze", "bismuthbronze", "blackbronze", "gold", "silver", "iron", "meteoriciron", "steel" };
        private readonly CustomMeshDataPartFloat[] matrixAndLightFloatsSaw = new CustomMeshDataPartFloat[metals.Length];
        private readonly MeshRef[] sawMeshrefs = new MeshRef[metals.Length];
        private readonly int[] quantitySaws = new int[metals.Length];
        private readonly ITexPositionSource texSource;
        private readonly string metal;
        //private readonly Dictionary<long, Dictionary<float, Tuple<float, float>>> currentLinearOffsetCache = new Dictionary<long, Dictionary<float, Tuple<float, float>>>();

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "metal")
                    return texSource["saw-metal-" + metal];
                return texSource["saw-" + textureCode];
            }
        }

        public SawmillRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            int count = (16 + 4) * 200;

            AssetLocation loc = new AssetLocation("game:shapes/item/tool/saw.json");
            Shape shape = Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
            texSource = capi.Tesselator.GetTexSource(textureSoureBlock);
            for (int i = 0; i < metals.Length; i++)
            {
                metal = metals[i];
                matrixAndLightFloatsSaw[i] = CreateCustomFloats(count);
                capi.Tesselator.TesselateShape("sawmill-saw", shape, out MeshData sawMeshData, this, rot);
                sawMeshData.CustomFloats = matrixAndLightFloatsSaw[i];
                sawMeshrefs[i] = capi.Render.UploadMesh(sawMeshData);
            }
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPSawmill sawmillDev = dev as BEBehaviorMPSawmill;
            
            BlockFacing powerdFrom = sawmillDev.GetPropagationDirectionInput(); // TODO should decide the offset to keep the saw close to the power direction
            bool flip = sawmillDev.MirroredLinearMotion;
            int mul = 1;
            if (powerdFrom == sawmillDev.facing)
            {
                mul *= -1;
            }
            //currentLinearOffsetCache.TryGetValue(sawmillDev.NetworkId, out Dictionary <float, Tuple<float, float>> networkCache);
            //if (networkCache == null)
            //{
            //    networkCache = new Dictionary<float, Tuple<float, float>>();
            //    currentLinearOffsetCache[sawmillDev.NetworkId] = networkCache;
            //}
            //networkCache.TryGetValue(sawmillDev.GearedRatio, out Tuple<float, float> linearOffsetForGearedRatio);
            //if (linearOffsetForGearedRatio == null)
            //{
            //    linearOffsetForGearedRatio = Tuple.Create(sawmillDev.LinearOffset(), sawmillDev.LinearOffset(true));
            //    networkCache[sawmillDev.GearedRatio] = linearOffsetForGearedRatio;
            //}
            //float currentLinearOffset = flip ? linearOffsetForGearedRatio.Item1 : linearOffsetForGearedRatio.Item2;
            float currentLinearOffset = sawmillDev.LinearOffset(flip);

            if (sawmillDev.blockEntitySawmill.HasSaw)
            {
                int metalIndex = sawmillDev.blockEntitySawmill.sawMetalIndex;
                if (sawmillDev.blockEntitySawmill.HasSaw && metalIndex >= 0)
                {
                    
                    float offset = -0.05f;
                    Vec3f copy = distToCamera.Clone();
                    copy.X += mul * dev.AxisSign[0] * (currentLinearOffset + offset) + dev.AxisSign[2] * -0.04f;
                    copy.Z += mul * dev.AxisSign[2] * (currentLinearOffset + offset) + dev.AxisSign[0] * -0.04f;
                    copy.Y += 0.12f;
                    
                    UpdateLightAndTransformMatrix(matrixAndLightFloatsSaw[metalIndex].Values, quantitySaws[metalIndex], copy, dev.LightRgba, dev.AxisSign[2] * GameMath.PIHALF, dev.AxisSign[0] * GameMath.PIHALF, dev.AxisSign[2] * GameMath.PIHALF + dev.AxisSign[0] * GameMath.PIHALF);
                    quantitySaws[metalIndex]++;
                }
            }
        }

        private CustomMeshDataPartFloat CreateCustomFloats(int count)
        {
            CustomMeshDataPartFloat result = new CustomMeshDataPartFloat(count)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            result.SetAllocationSize(count);
            return result;
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            for (int i = 0; i < metals.Length; i++)
            {
                if (quantitySaws[i] > 0)
                {
                    matrixAndLightFloatsSaw[i].Count = quantitySaws[i] * 20;
                    updateMesh.CustomFloats = matrixAndLightFloatsSaw[i];
                    capi.Render.UpdateMesh(sawMeshrefs[i], updateMesh);
                    capi.Render.RenderMeshInstanced(sawMeshrefs[i], quantitySaws[i]);
                }
                quantitySaws[i] = 0;
            }
            //currentLinearOffsetCache.Clear();
        }
    }
}