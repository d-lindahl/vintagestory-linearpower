using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
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

    }
}