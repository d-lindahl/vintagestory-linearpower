using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class SawmillRenderer : MechBlockRenderer
    {
        private readonly Dictionary<string, CustomMeshDataPartFloat> matrixAndLightFloatsSaw = new();
        private readonly Dictionary<string, MeshRef> sawMeshrefs = new();
        private readonly Dictionary<string, int> quantitySaws = new();
        private readonly int count = (16 + 4) * 200;
        //private readonly Dictionary<long, Dictionary<float, Tuple<float, float>>> currentLinearOffsetCache = new Dictionary<long, Dictionary<float, Tuple<float, float>>>();

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public SawmillRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPSawmill sawmillDev = dev as BEBehaviorMPSawmill;

            BlockFacing powerdFrom = sawmillDev.GetPropagationDirectionInput(); // TODO should decide the offset to keep the saw close to the power direction
            bool flip = sawmillDev.MirroredLinearMotion;
            int mul = 1;
            //if (powerdFrom == sawmillDev.facing)
            //{
            //    mul *= 1;
            //}
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
                string metal = sawmillDev.blockEntitySawmill.sawMetal;
                if (metal == null)
                {
                    // defensive check
                    return;
                }

                if (!quantitySaws.ContainsKey(metal))
                {
                    quantitySaws.Add(metal, 0);
                }

                // check if saw mesh exists for this metal
                if (!matrixAndLightFloatsSaw.ContainsKey(metal))
                {
                    CustomMeshDataPartFloat customFloats = CreateCustomFloats(count);
                    matrixAndLightFloatsSaw.Add(metal, customFloats);
                    MeshData sawMeshData = sawmillDev.blockEntitySawmill.GetOrCreateMesh(sawmillDev.blockEntitySawmill.SawItemStack);
                    sawMeshData.CustomFloats = customFloats;
                    sawMeshrefs.Add(metal, capi.Render.UploadMesh(sawMeshData));

                }

                float offset = -0.05f;
                Vec3f copy = distToCamera.Clone();
                copy.X += mul * dev.AxisSign[0] * (currentLinearOffset + offset) + dev.AxisSign[2] * -0.04f;
                copy.Z += mul * dev.AxisSign[2] * (currentLinearOffset + offset) + Math.Abs(dev.AxisSign[0]) * 0.04f;
                copy.Y += 0.12f;

                UpdateLightAndTransformMatrix(matrixAndLightFloatsSaw[metal].Values, quantitySaws[metal], copy, dev.LightRgba, GameMath.PIHALF, 0, dev.AxisSign[2] * GameMath.PIHALF);
                quantitySaws[metal]++;
            }
        }

        private static CustomMeshDataPartFloat CreateCustomFloats(int count)
        {
            CustomMeshDataPartFloat result = new(count)
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

            foreach (var entry in quantitySaws)
            {
                if (entry.Value > 0)
                {
                    matrixAndLightFloatsSaw[entry.Key].Count = entry.Value * 20;
                    updateMesh.CustomFloats = matrixAndLightFloatsSaw[entry.Key];
                    capi.Render.UpdateMesh(sawMeshrefs[entry.Key], updateMesh);
                    capi.Render.RenderMeshInstanced(sawMeshrefs[entry.Key], quantitySaws[entry.Key]);
                }
                quantitySaws[entry.Key] = 0;
            }
            //currentLinearOffsetCache.Clear();

        }

        public override void Dispose()
        {
            base.Dispose();

            foreach (var meshRef in sawMeshrefs.Values)
            {
                meshRef.Dispose();
            }
            matrixAndLightFloatsSaw.Clear();
            quantitySaws.Clear();
        }
    }
}