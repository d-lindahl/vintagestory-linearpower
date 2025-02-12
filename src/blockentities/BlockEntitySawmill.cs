using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Reflection;

namespace sawmill
{
    public class BlockEntitySawmill : BlockEntityDisplay
    {
        private readonly InventorySawmill inv;
        public int sawToolTier;
        public string sawMetal;
        private float progress;
        public float processingResistance;
        private static readonly int inputInventoryIndex = 0;
        private static readonly int sawInventoryIndex = 1;


        private List<GridRecipe> sawGridRecipes;
        private HashSet<GridRecipeIngredient> sawRecipeIngredients;
        private Dictionary<string, bool> validIngredientCodes;
        private BEBehaviorMPSawmill behSawmill;
        private readonly int updateProgressPackage = 1701;
        private readonly string sawRecipeCacheKey = "linearpower:sawRecipes";
        private readonly string sawRecipeIngredientsCacheKey = "linearpower:sawRecipeIngredients";
        private readonly string sawValidIngredientCodesKey = "linearpower:sawValidIngredientCodes";

        private float accumulatedDurabilityCost = 0;

        public BlockFacing Facing { get; protected set; } = BlockFacing.NORTH;

        public override InventoryBase Inventory => inv;

        public override int DisplayedItems => 1;

        public override string InventoryClassName => "sawmill";
        private ItemSlot InputInventory => inv[inputInventoryIndex];
        public ItemStack InputItemStack => InputInventory.Itemstack;
        public ItemSlot SawInventory => inv[sawInventoryIndex];
        public bool HasSaw => !SawInventory.Empty;

        public ItemStack SawItemStack
        {
            get
            {
                return SawInventory.Itemstack;
            }
            set
            {
                SawInventory.Itemstack = value;
            }
        }

        public Item GetSawItem => SawItemStack?.Item;

        public BlockEntitySawmill()
        {
            inv = new InventorySawmill(this, 2);
            inv.SlotModified += new Action<int>(Inv_SlotModified);
        }

        private void Inv_SlotModified(int slot) => updateMesh(slot);

        public override void Initialize(ICoreAPI api)
        {
            Facing = BlockFacing.FromCode(Block.Variant["side"]);
            base.Initialize(api);

            UpdateSawInfo();
            inv.LateInitialize(InventoryClassName + "-" + Pos?.ToString(), api);
            UpdateCache(api);
            behSawmill = GetBehavior<BEBehaviorMPSawmill>();
            if (api.Side == EnumAppSide.Server)
                RegisterGameTickListener(OnServerTick, 1000);
        }

        public MeshData GetOrCreateMesh(ItemStack stack)
        {
            return getOrCreateMesh(stack, -1);
        }

        private void UpdateCache(ICoreAPI api)
        {
            if (!api.ObjectCache.TryGetValue(sawRecipeCacheKey, out object value))
            {
                sawGridRecipes = FindApplicableSawRecipes(api.World);
                api.ObjectCache.Add(sawRecipeCacheKey, sawGridRecipes);

                sawRecipeIngredients = new HashSet<GridRecipeIngredient>();
                foreach (GridRecipe recipe in sawGridRecipes)
                {
                    sawRecipeIngredients.Add(recipe.resolvedIngredients.First(ingredient => !ingredient.Code.Path.StartsWith("saw-")));
                }
                api.ObjectCache.Add(sawRecipeIngredientsCacheKey, sawRecipeIngredients);
                validIngredientCodes = new Dictionary<string, bool>();
                api.ObjectCache.Add(sawValidIngredientCodesKey, validIngredientCodes);
            }
            else
            {
                sawGridRecipes = (List<GridRecipe>)value;
                sawRecipeIngredients = (HashSet<GridRecipeIngredient>)api.ObjectCache[sawRecipeIngredientsCacheKey];
                validIngredientCodes = (Dictionary<string, bool>)api.ObjectCache[sawValidIngredientCodesKey];
            }
        }

        private void OnServerTick(float dx)
        {
            float oldProgress = progress;
            if (processingResistance == 0 && !InputInventory.Empty)
            {
                UpdateItemResistance();
                progress = 0;
            }

            if (processingResistance > 0)
            {
                float num = behSawmill.Network?.Speed ?? 0f;
                num = Math.Abs(num) / 10 * behSawmill.GearedRatio;
                progress += num;
            }

            if (processingResistance > 0 && progress >= processingResistance)
            {
                this.progress = 0.0f;
                this.Cut();
            }
            if (oldProgress != progress)
            {
                (Api as ICoreServerAPI).Network.BroadcastBlockEntityPacket<float>(Pos, updateProgressPackage, progress);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == updateProgressPackage)
            {
                progress = SerializerUtil.Deserialize<float>(data);
            }
            else
            {
                base.OnReceivedServerPacket(packetid, data);
            }
        }

        private void Cut()
        {
            ItemStack outputStack = null;
            Object inDappledGrovesRecipe;
            if (UsesIndappledGroves() && (inDappledGrovesRecipe = GetInDappledGrovesRecipe(InputInventory)) != null)
            {
                // InDappledGroves compatibility
                outputStack = (ItemStack)inDappledGrovesRecipe.GetType().GetMethod("TryCraftNow").Invoke(inDappledGrovesRecipe, new object[] { Api, InputInventory });
            }
            else if (UsesImmersiveWoodSawing() && InputInventory?.Itemstack?.Block != null && GetImmersiveWoodSawingSawableBehavior(InputInventory.Itemstack.Block) != null)
            {
                // ImmersiveWoodSawing compatibility
                BlockBehavior sawableBehavior = GetImmersiveWoodSawingSawableBehavior(InputInventory.Itemstack.Block);
                Assembly immersiveWoodSawingAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("ImmersiveWoodSawing"), null);
                Type sawableBehaviorType = immersiveWoodSawingAssembly.GetType("ImmersiveWoodSawing.BlockBehaviorSawable");
                AssetLocation plankType = (AssetLocation)sawableBehaviorType.GetField("drop").GetValue(sawableBehavior);
                int plankCount = (int)sawableBehaviorType.GetField("dropAmount").GetValue(sawableBehavior);

                outputStack = new ItemStack(Api.World.GetItem(plankType), plankCount);
            }
            else
            {
                GridRecipe applicableRecipe = GetApplicableRecipe(InputInventory);
                if (applicableRecipe != null)
                {

                    applicableRecipe.Output.Resolve(Api.World, "sawmill");
                    outputStack = applicableRecipe.Output.ResolvedItemstack.Clone();
                    if (applicableRecipe.CopyAttributesFrom != null)
                    {
                        ItemStack inputStackForPatternCode = applicableRecipe.GetInputStackForPatternCode(applicableRecipe.CopyAttributesFrom, new ItemSlot[] { InputInventory });
                        if (inputStackForPatternCode != null)
                        {
                            ITreeAttribute treeAttribute = inputStackForPatternCode.Attributes.Clone();
                            treeAttribute.MergeTree(outputStack.Attributes);
                            outputStack.Attributes = treeAttribute;
                        }
                    }

                    if (LinearPowerConfig.Current.sawDegradeRate > 0)
                    {
                        float durabilityCost = applicableRecipe.resolvedIngredients.First(ingredient => ingredient.Code.Path.StartsWith("saw-")).ToolDurabilityCost * LinearPowerConfig.Current.sawDegradeRate;
                        if (durabilityCost > 0)
                        {
                            accumulatedDurabilityCost += durabilityCost;
                        }
                        if (accumulatedDurabilityCost > 1)
                        {
                            int damage = (int)Math.Floor(accumulatedDurabilityCost);
                            accumulatedDurabilityCost -= damage;
                            DamageSaw(damage);
                        }
                    }
                }
            }
            if (outputStack == null)
            {
                // No recipe found, drop the input item. This should not happen.
                outputStack = InputInventory.TakeOut(1);
            }
            else
            {
                // Recipe found, clear the input item
                InputInventory.TakeOut(1);
            }
            Api.World.SpawnItemEntity(outputStack, Pos.ToVec3d() + new Vec3d(0.5, 0.05, 0.5), new Vec3d(0, 0.05, 0));
            processingResistance = 0;
            MarkDirty(redrawOnClient: true);
        }

        private void DamageSaw(int amount)
        {
            ItemStack itemstack = SawItemStack;
            int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack);
            remainingDurability -= amount;
            itemstack.Attributes.SetInt("durability", remainingDurability);
            if (remainingDurability <= 0)
            {
                SawInventory.Itemstack = null;
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Pos.X, Pos.Y, Pos.Z, null, 1f, 16f);
                Api.World.SpawnCubeParticles(Pos.ToVec3d() + new Vec3d(0.5, 0.5, 0.5), itemstack, 0.25f, 30, 1f);
            }
        }

        public ItemStack[] GetDrops(ItemStack sawmillStack)
        {
            List<ItemStack> drops = new()
            {
                sawmillStack
            };
            if (!InputInventory.Empty)
            {
                drops.Add(InputInventory.TakeOutWhole());
            }
            if (HasSaw)
            {
                drops.Add(SawInventory.TakeOutWhole());
            }
            return drops.ToArray();
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemSlot itemSlot = InputInventory;
            if (activeHotbarSlot.Empty)
            {
                TryTake(itemSlot, byPlayer);
            }
            else
            {
                if (activeHotbarSlot.Itemstack.Collectible.Code.Path.StartsWith("saw-"))
                {
                    TryPut(byPlayer, activeHotbarSlot, SawInventory);
                    UpdateSawInfo();
                    return true;
                }
                if (HasSaw)
                {
                    if (IsValidForSawing(activeHotbarSlot))
                        TryPut(byPlayer, activeHotbarSlot, itemSlot);
                    else
                        if (Api is ICoreClientAPI capi)
                            capi.TriggerIngameError(this, "notValidForSawing", Lang.Get("linearpower:notValidForSawing"));
                }
                else if (Api is ICoreClientAPI api)
                {
                    api.TriggerIngameError(this, "requiresSaw", Lang.Get("linearpower:requiresSaw"));
                }
            }
            return true;
        }

        private bool UsesImmersiveWoodSawing()
        {
            return Api.ModLoader.IsModEnabled("immersivewoodsawing");
        }

        private static BlockBehavior GetImmersiveWoodSawingSawableBehavior(Block block)
        {
            return block.BlockBehaviors.FirstOrDefault(behavior => behavior.GetType().Name.Equals("BlockBehaviorSawable"), null);
        }

        private bool UsesIndappledGroves()
        {
            return Api.ModLoader.IsModEnabled("indappledgroves");
        }

        private Object GetInDappledGrovesRecipe(ItemSlot slot)
        {
            Assembly inDappledGrovesAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("InDappledGroves"), null);
            Type RecipeHandlerType = inDappledGrovesAssembly.GetType("InDappledGroves.Util.Handlers.RecipeHandler");
            Object handler = Activator.CreateInstance(RecipeHandlerType, Api, null);
            Object recipe = null;
            object[] args = new object[] { Api.World, slot, "sawing", "sawbuck", "basic", recipe };
            handler.GetType().GetMethod("GetMatchingRecipes").Invoke(handler, args);
            return args[5];
        }

        private bool IsValidForSawing(ItemSlot slot)
        {
            string code = slot.Itemstack.Collectible.Code.ToString();
            ItemStack stack = slot.Itemstack;
            if (UsesIndappledGroves() && GetInDappledGrovesRecipe(slot) != null)
            {
                // there's a recipe for the sawbuck in InDappledGroves for this input
                return true;
            }
            if (UsesImmersiveWoodSawing() && GetImmersiveWoodSawingSawableBehavior(stack.Block) != null)
            {
                // the block has an ImmersiveWoodSawing sawable behavior
                return true;
            }
            if (!validIngredientCodes.TryGetValue(code, out bool isValid))
            {
                foreach (GridRecipe recipe in sawGridRecipes)
                {
                    if (recipe.resolvedIngredients.Any(ingredient => ingredient.SatisfiesAsIngredient(stack)))
                    {
                        validIngredientCodes.Add(code, true);
                        return true;
                    }
                }
                validIngredientCodes.Add(code, false);
                return false;
            }
            return isValid;
        }

        private void UpdateItemResistance()
        {
            float itemResistance;
            Block invBlock;
            if ((invBlock = InputItemStack?.Block) != null)
            {
                itemResistance = invBlock.Resistance;
                if (SawItemStack.Collectible.MiningSpeed.TryGetValue(invBlock.BlockMaterial, out float value))
                {
                    itemResistance /= value;
                }
                else
                {
                    itemResistance /= sawToolTier;
                }
            }
            else if (InputItemStack?.Collectible != null)
            {
                itemResistance = 1.0f / sawToolTier;
            }
            else
            {
                itemResistance = 0;
            }
            processingResistance = itemResistance;
        }

        private void UpdateSawInfo()
        {
            if (SawItemStack == null)
            {
                sawToolTier = 0;
                sawMetal = null;
            }
            else
            {
                sawToolTier = SawItemStack.Collectible.ToolTier;
                sawMetal = SawItemStack.Collectible.Variant["metal"];
            }
        }

        private static List<GridRecipe> FindApplicableSawRecipes(IWorldAccessor world)
        {
            return world.GridRecipes.FindAll(recipe => recipe.Enabled && recipe.resolvedIngredients.Length == 2).FindAll(recipe =>
            {
                foreach (CraftingRecipeIngredient ingr in recipe.resolvedIngredients)
                {
                    if (ingr.IsTool && ingr.Code.Path.StartsWith("saw-"))
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        private GridRecipe GetApplicableRecipe(ItemSlot input)
        {
            if (sawGridRecipes != null)
                return sawGridRecipes.Find(recipe => RecipeSimpleMatch(recipe, input));
            return Api.World.GridRecipes.Find(recipe => RecipeSimpleMatch(recipe, input));
        }

        // already know that the recipe has 2 ingredients of which one is the saw
        private bool RecipeSimpleMatch(GridRecipe recipe, ItemSlot suppliedSlot)
        {
            foreach (CraftingRecipeIngredient ingredient in recipe.resolvedIngredients)
            {
                if (!ingredient.Code.Path.StartsWith("saw-"))
                {
                    if (ingredient.Quantity != 1)
                    {
                        return false;
                    }
                    if (ingredient.IsWildCard)
                    {
                        return WildcardUtil.Match(ingredient.Code, suppliedSlot.Itemstack.Collectible.Code, ingredient.AllowedVariants);
                    }
                    else if (ingredient.Resolve(Api.World, "sawmill"))
                    {
                        return ingredient.ResolvedItemstack.Equals(Api.World, suppliedSlot.Itemstack, GlobalConstants.IgnoredStackAttributes);
                    }
                }
            }
            return false;
        }

        private void TryPut(IPlayer byPlayer, ItemSlot from, ItemSlot to)
        {
            ItemStack taken;
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                taken = from.TakeOut(1);
                from.MarkDirty();

            }
            else
            {
                taken = from.Itemstack.Clone();
                taken.StackSize = 1;

            }
            if (!to.Empty && !byPlayer.InventoryManager.TryGiveItemstack(to.Itemstack, true))
                Api.World.SpawnItemEntity(to.Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            to.Itemstack = taken;
            to.MarkDirty();
            if (Api is ICoreClientAPI api)
            {
                Vec3d vec3d = Pos.ToVec3d().Add(0.5, 0.25, 0.5);
                AssetLocation sound;
                if (to.Itemstack?.Block != null)
                {
                    sound = to.Itemstack.Block.Sounds.Place;
                }
                else
                {
                    sound = Block.Sounds.Place;
                }
                api.World.PlaySoundAt(sound, vec3d.X + 0.5, vec3d.Y + 0.5, vec3d.Z + 0.5, byPlayer);
                api.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            }
            UpdateItemResistance();
            progress = 0;
            MarkDirty(redrawOnClient: true);
        }

        private void TryTake(ItemSlot fromSlot, IPlayer toPlayer)
        {
            ItemStack itemstack = fromSlot.TakeOut(1);
            if (itemstack != null)
            {
                if (!toPlayer.InventoryManager.TryGiveItemstack(itemstack))
                    Api.World.SpawnItemEntity(itemstack, Pos.ToVec3d().Add(0.5, 0.1, 0.5));
                UpdateItemResistance();
                MarkDirty(true);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            sb.AppendLine(Lang.Get("linearpower:sawInfo", (SawInventory.Empty ? Lang.Get("linearpower:generic-none") : SawInventory.GetStackName())));
            
            String sawingInfo;
            if (!InputInventory.Empty)
            {
                string info = InputInventory.GetStackName();
                if (progress > 0)
                    info += " (" + Math.Min(100, Math.Round(progress / processingResistance * 100)) + "%)";
                sawingInfo = info;
            }
            else
            {
                sawingInfo = Lang.Get("linearpower:generic-nothing");
            }
            sb.AppendLine(Lang.Get("linearpower:sawingInfo", sawingInfo));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("progress", progress);
            tree.SetFloat("processingResistance", processingResistance);
            tree.SetItemstack("sawStack", SawItemStack);
            tree.SetFloat("accumulatedDurabilityCost", accumulatedDurabilityCost);
            tree.SetString("sawMetal", sawMetal);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            progress = tree.GetFloat("progress");
            processingResistance = tree.GetFloat("processingResistance");
            ItemStack sawStack = tree.GetItemstack("sawStack");
            if (sawStack != null)
            {
                sawStack.ResolveBlockOrItem(worldForResolving);
                SawItemStack = sawStack;
                sawMetal = tree.GetString("sawMetal");
            }
            accumulatedDurabilityCost = tree.GetFloat("accumulatedDurabilityCost");
        }

        
        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[1][];

            tfMatrices[inputInventoryIndex] =
                    new Matrixf()
                    .Scale(0.5f, 0.5f, 0.5f)
                    .Translate(0.5f, 0f, 0.5f)
                    .Values;
            return tfMatrices;
        }
    }
}
