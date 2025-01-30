using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class SliderCrankRenderer : MechBlockRenderer
    {
        private readonly Dictionary<string, CustomMeshDataPartFloat> matrixAndLightFloatsCrank = new();
        private readonly Dictionary<string, CustomMeshDataPartFloat> matrixAndLightFloatsRod = new();
        private readonly Dictionary<string, MeshRef> crankMeshRef = new();
        private readonly Dictionary<string, MeshRef> rodMeshRef = new();
        private readonly Dictionary<string, int> quantityCranks = new();
        private readonly Dictionary<string, int> quantityRods = new();
        private readonly Vec3f axisCenter = new Vec3f(0.5f, 0.5f, 0.5f);
        private readonly Vec3f globalRot;
        private readonly Shape sliderCrankShape;
        private readonly Shape sliderCrankRodShape;

        // cache calculations per orientation and network

        private readonly int count = (16 + 4) * 200;

        public SliderCrankRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            globalRot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            AssetLocation loc = new AssetLocation("linearpower:shapes/block/wood/mechanics/slidercrank.json");
            sliderCrankShape = Shape.TryGet(capi, loc);
            

            loc = new AssetLocation("linearpower:shapes/block/wood/mechanics/slidercrank-rod.json");
            sliderCrankRodShape = Shape.TryGet(capi, loc);
        }

        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotRad, IMechanicalPowerRenderable dev)
        {
            // check if material is instanciated
            var material = dev.Block.Variant["wood"];
            if (!crankMeshRef.ContainsKey(material))
            {
                capi.Tesselator.TesselateShape(dev.Block, sliderCrankShape, out MeshData crankMesh, globalRot);
                crankMesh.CustomFloats = createCustomFloats(count);
                matrixAndLightFloatsCrank.Add(material, crankMesh.CustomFloats);
                crankMeshRef.Add(material, capi.Render.UploadMesh(crankMesh));

                capi.Tesselator.TesselateShape(dev.Block, sliderCrankRodShape, out MeshData rodMesh, globalRot);
                rodMesh.CustomFloats = createCustomFloats(count);
                matrixAndLightFloatsRod.Add(material, rodMesh.CustomFloats);
                rodMeshRef.Add(material, capi.Render.UploadMesh(rodMesh));
            }

            if (!quantityCranks.ContainsKey(material))
            {
                quantityCranks.Add(material, 0);
                quantityRods.Add(material, 0);
            }

            // check connections

            int axX = dev.AxisSign[0];
            int axZ = dev.AxisSign[2];
            int axXAbs = Math.Abs(dev.AxisSign[0]);
            int axZAbs = Math.Abs(dev.AxisSign[2]);


            // Crank
            float rotX = dev.AngleRad * -axXAbs;
            float rotZ = dev.AngleRad * -axZAbs;
            UpdateLightAndTransformMatrix(matrixAndLightFloatsCrank[material].Values, quantityCranks[material], distToCamera, dev.LightRgba, rotX, rotZ, axisCenter, new Vec3f());
            quantityCranks[material]++;
            BEBehaviorMPSliderCrank devSliderCrank = dev as BEBehaviorMPSliderCrank;
            if (devSliderCrank.entity.NeedsPistonSupport())
            {
                Vec3f pinPosition = GetPinPosition(dev.AngleRad, axX, axZ);
                float rodRotation = GetRodRotation(dev.AngleRad);
                float rodRotationMirror = (float)(2 * Math.PI - GetRodRotation(dev.AngleRad));
                Vec3f pistonPosition = GetPistonPosition(dev.AngleRad, rodRotation, axX, axZ);
                Vec3f pistonPositionMirror = GetPistonPosition(dev.AngleRad, rodRotationMirror, axX, axZ);
                Vec3f pinPositionOffset = pinPosition.AddCopy(axX * -0.55f / 16, 0, axZ * 0.55f / 16);

                if (devSliderCrank.entity.connectedCCW)
                {
                    UpdateLightAndTransformMatrix(matrixAndLightFloatsRod[material].Values, quantityRods[material], distToCamera, dev.LightRgba, -axXAbs * rodRotation, -axZAbs * rodRotation, axisCenter, pinPosition);
                    quantityRods[material]++;
                    UpdateLightAndTransformMatrix(matrixAndLightFloatsRod[material].Values, quantityRods[material], distToCamera, dev.LightRgba, (float)(axZAbs * Math.PI), (float)(axXAbs * Math.PI), axisCenter, pistonPosition);
                    quantityRods[material]++;
                }
                if (devSliderCrank.entity.connectedCW)
                {
                    UpdateLightAndTransformMatrix(matrixAndLightFloatsRod[material].Values, quantityRods[material], distToCamera, dev.LightRgba, (float)(-axXAbs * rodRotationMirror - Math.PI), (float)(-axZAbs * rodRotation - Math.PI), axisCenter, pinPositionOffset);
                    quantityRods[material]++;
                    UpdateLightAndTransformMatrix(matrixAndLightFloatsRod[material].Values, quantityRods[material], distToCamera, dev.LightRgba, (float)(axZAbs * Math.PI + axXAbs * Math.PI), (float)(axXAbs * Math.PI + axZ * Math.PI), axisCenter, pistonPositionMirror * -1);
                    quantityRods[material]++;
                }
            }
        }

        private Vec3f GetPinPosition(float axleRad, int axX, int axZ)
        {
            float r = 1.5f / 16;
            float xPin = (float)(r * Math.Cos(axleRad));
            float yPin = (float)(r * Math.Sin(axleRad));
            return new Vec3f(axZ*xPin, (-axX + axZ) * -yPin, axX*xPin);
        }

        private Vec3f GetPistonPosition(float axleRad, float rodRad, int axX, int axZ)
        {
            float l = 5.0f / 16;

            float rodCrankRad = GameMath.PI - axleRad - (GameMath.PI - rodRad);

            float xSlider = (float)(l * Math.Sin(rodCrankRad) / Math.Sin(axleRad));
            return new Vec3f(axZ * xSlider, 0f, axX * xSlider);
        }

        private float GetRodRotation(float angleRad)
        {
            float r = 1.5f / 16;
            float l = 5.0f / 16;
            return (float)Math.Asin(r * Math.Sin(angleRad) / l);
        }

        protected void UpdateLightAndTransformMatrix(float[] values, int index, Vec3f distToCamera, Vec4f lightRgba, float rotX, float rotZ, Vec3f axis, Vec3f translate)
        {
            Mat4f.Identity(tmpMat);
            Mat4f.Translate(tmpMat, tmpMat, distToCamera.X + axis.X + translate.X, distToCamera.Y + axis.Y + translate.Y, distToCamera.Z + axis.Z + translate.Z);

            quat[0] = 0;
            quat[1] = 0;
            quat[2] = 0;
            quat[3] = 1;
            if (rotX != 0f) Quaterniond.RotateX(quat, quat, rotX);
            if (rotZ != 0f) Quaterniond.RotateZ(quat, quat, rotZ);

            for (int i = 0; i < quat.Length; i++) qf[i] = (float)quat[i];
            Mat4f.Mul(tmpMat, tmpMat, Mat4f.FromQuat(rotMat, qf));

            Mat4f.Translate(tmpMat, tmpMat, -axis.X, -axis.Y, -axis.Z);

            int j = index * 20;
            values[j] = lightRgba.R;
            values[++j] = lightRgba.G;
            values[++j] = lightRgba.B;
            values[++j] = lightRgba.A;

            for (int i = 0; i < 16; i++)
            {
                values[++j] = tmpMat[i];
            }
        }

        private CustomMeshDataPartFloat createCustomFloats(int count)
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

            // Cranks
            foreach (var entry in quantityCranks)
            {
                if (entry.Value > 0)
                {
                    matrixAndLightFloatsCrank[entry.Key].Count = entry.Value * 20;
                    updateMesh.CustomFloats = matrixAndLightFloatsCrank[entry.Key];
                    capi.Render.UpdateMesh(crankMeshRef[entry.Key], updateMesh);
                    capi.Render.RenderMeshInstanced(crankMeshRef[entry.Key], entry.Value);
                }
                quantityCranks[entry.Key] = 0;
            }

            // Rods and pistons
            foreach (var entry in quantityRods)
            {
                if (entry.Value > 0)
                {
                    matrixAndLightFloatsRod[entry.Key].Count = entry.Value * 20;
                    updateMesh.CustomFloats = matrixAndLightFloatsRod[entry.Key];
                    capi.Render.UpdateMesh(rodMeshRef[entry.Key], updateMesh);
                    capi.Render.RenderMeshInstanced(rodMeshRef[entry.Key], entry.Value);
                }
                quantityRods[entry.Key] = 0;
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            foreach (var meshRef in crankMeshRef.Values)
            {
                meshRef.Dispose();
            }
            foreach (var meshRef in rodMeshRef.Values)
            {
                meshRef.Dispose();
            }
        }
    }
}
