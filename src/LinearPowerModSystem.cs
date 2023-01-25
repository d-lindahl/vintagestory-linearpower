using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent.Mechanics;

namespace sawmill
{
    public class LinearPowerModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            try
            {
                LinearPowerConfig linearPowerConfig = api.LoadModConfig<LinearPowerConfig>("linearpower.json");
                if (linearPowerConfig != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                    LinearPowerConfig.Current = linearPowerConfig;
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    LinearPowerConfig.Current = LinearPowerConfig.GetDefault();
                }
            }
            catch
            {
                LinearPowerConfig.Current = LinearPowerConfig.GetDefault();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(LinearPowerConfig.Current, "linearpower.json");
            }

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

    public class LinearPowerConfig
    {
        public static LinearPowerConfig Current { get; set; }

        public float sawDegradeRate;

        public static LinearPowerConfig GetDefault()
        {
            return new LinearPowerConfig()
            {
                sawDegradeRate = 10
            };
        }
    }
}
