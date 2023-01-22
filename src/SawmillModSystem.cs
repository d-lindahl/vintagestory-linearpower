using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class SawmillModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockSawmill", typeof(BlockSawmill));
            api.RegisterBlockEntityClass("BlockEntitySawmill", typeof(BlockEntitySawmill));
            api.RegisterBlockEntityBehaviorClass("BEBehaviorMPSawmill", typeof(BEBehaviorMPSawmill));
            api.RegisterBlockClass("BlockSliderCrank", typeof(BlockSliderCrank));
            api.RegisterBlockEntityClass("BlockEntitySliderCrank", typeof(BlockEntitySliderCrank));
            api.RegisterBlockEntityBehaviorClass("BEBehaviorMPSliderCrank", typeof(BEBehaviorMPSliderCrank));
            if (api is ICoreClientAPI && !MechNetworkRenderer.RendererByCode.ContainsKey("slidercrank"))
            {
                MechNetworkRenderer.RendererByCode.Add("slidercrank", typeof(SliderCrankRenderer));
            }
            if (api is ICoreClientAPI && !MechNetworkRenderer.RendererByCode.ContainsKey("sawmill"))
            {
                MechNetworkRenderer.RendererByCode.Add("sawmill", typeof(SawmillRenderer));
            }
        }
    }
}
