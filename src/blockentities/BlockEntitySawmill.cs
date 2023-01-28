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

namespace sawmill
{
    public class BlockEntitySawmill : BlockEntityDisplay
    {
        private readonly InventorySawmill inv;
        private CollectibleObject sawCollectible;
        public int sawToolTier;
        public int sawMetalIndex;
        private float progress;
        public float processingResistance;
        public static readonly int inputInventoryIndex = 0;
        public static readonly int sawInventoryIndex = 1;
        
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

        public override string InventoryClassName => "sawmill";

        public bool HasSaw => !inv[sawInventoryIndex].Empty;

        public CollectibleObject GetSawCollectible => inv[sawInventoryIndex].Itemstack?.Collectible;

        public BlockEntitySawmill()
        {
            inv = new InventorySawmill(this, 2);
            inv.SlotModified += new Action<int>(Inv_SlotModified);
            meshes = new MeshData[2];
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

        private void UpdateCache(ICoreAPI api)
        {
            if (!api.ObjectCache.ContainsKey(sawRecipeCacheKey))
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
                sawGridRecipes = (List<GridRecipe>)api.ObjectCache[sawRecipeCacheKey];
                sawRecipeIngredients = (HashSet<GridRecipeIngredient>)api.ObjectCache[sawRecipeIngredientsCacheKey];
                validIngredientCodes = (Dictionary<string, bool>)api.ObjectCache[sawValidIngredientCodesKey];
            }
        }

        private void OnServerTick(float dx)
        {
            float oldProgress = progress;
            if (processingResistance == 0 && !inv[inputInventoryIndex].Empty)
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
            GridRecipe applicableRecipe = GetApplicableRecipe(inv[inputInventoryIndex]);
            if (applicableRecipe == null)
            {
                Api.World.SpawnItemEntity(inv[inputInventoryIndex].TakeOut(1), Pos.ToVec3d());
                return;
            }
            
            applicableRecipe.Output.Resolve(Api.World, "sawmill");
            ItemStack outputStack = applicableRecipe.Output.ResolvedItemstack.Clone();
            if (applicableRecipe.CopyAttributesFrom != null)
            {
                ItemStack inputStackForPatternCode = applicableRecipe.GetInputStackForPatternCode(applicableRecipe.CopyAttributesFrom, new ItemSlot[] { inv[inputInventoryIndex] });
                if (inputStackForPatternCode != null)
                {
                    ITreeAttribute treeAttribute = inputStackForPatternCode.Attributes.Clone();
                    treeAttribute.MergeTree(outputStack.Attributes);
                    outputStack.Attributes = treeAttribute;
                }
            }
            inv[inputInventoryIndex].TakeOut(1);

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
            
            Api.World.SpawnItemEntity(outputStack, Pos.ToVec3d() + new Vec3d(0.5, 0.05, 0.5), new Vec3d(0, 0.05, 0));
            processingResistance = 0;
            MarkDirty(redrawOnClient: true);
        }

        private void DamageSaw(int amount)
        {
            ItemStack itemstack = inv[sawInventoryIndex].Itemstack;
            int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack);
            remainingDurability -= amount;
            itemstack.Attributes.SetInt("durability", remainingDurability);
            if (remainingDurability <= 0)
            {
                inv[sawInventoryIndex].Itemstack = null;
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Pos.X, Pos.Y, Pos.Z, null, 1f, 16f);
                Api.World.SpawnCubeParticles(Pos.ToVec3d() + new Vec3d(0.5, 0.5, 0.5), itemstack, 0.25f, 30, 1f);
            }

            inv[sawInventoryIndex].MarkDirty();
        }

        public ItemStack[] GetDrops(ItemStack sawmillStack)
        {
            List<ItemStack> drops = new List<ItemStack>
            {
                sawmillStack
            };
            if (inv[inputInventoryIndex] != null)
                drops.Add(inv[inputInventoryIndex].TakeOutWhole());
            if (inv[sawInventoryIndex] != null)
                drops.Add(inv[sawInventoryIndex].TakeOutWhole());
            return drops.ToArray();
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemSlot itemSlot = inv[inputInventoryIndex];
            if (activeHotbarSlot.Empty)
            {
                TryTake(itemSlot, byPlayer);
            }
            else
            {
                if (activeHotbarSlot.Itemstack.Collectible.Code.Path.StartsWith("saw-"))
                {
                    TryPut(byPlayer, activeHotbarSlot, inv[sawInventoryIndex]);
                    UpdateSawInfo();
                    return true;
                }
                if (HasSaw)
                {
                    if (IsValidForSawing(activeHotbarSlot.Itemstack))
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

        private bool IsValidForSawing(ItemStack stack)
        {
            string code = stack.Collectible.Code.ToString();
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

        protected override void updateMesh(int index)
        {
            if (index == inputInventoryIndex)
            {
                base.updateMesh(index);
            }
        }

        private void UpdateItemResistance()
        {
            float itemResistance;
            Block invBlock;
            if ((invBlock = inv[inputInventoryIndex].Itemstack?.Block) != null)
            {
                itemResistance = invBlock.Resistance;
                if (sawCollectible.MiningSpeed.ContainsKey(invBlock.BlockMaterial))
                {
                    itemResistance /= sawCollectible.MiningSpeed[invBlock.BlockMaterial];
                }
                else
                {
                    itemResistance /= sawToolTier;
                }
            }
            else if (inv[inputInventoryIndex].Itemstack?.Collectible != null)
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
            if (inv[sawInventoryIndex].Empty)
            {
                sawToolTier = 0;
                sawMetalIndex = 0;
            }
            else
            {
                sawCollectible = inv[sawInventoryIndex].Itemstack.Collectible;
                string key = inv[sawInventoryIndex].Itemstack.Collectible.Variant["metal"];
                sawToolTier = sawCollectible.ToolTier;
                sawMetalIndex = Math.Max(0, SawmillRenderer.metals.IndexOf<string>(key));
            }
        }

        private List<GridRecipe> FindApplicableSawRecipes(IWorldAccessor world)
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
            Vec3d vec3d = Pos.ToVec3d().Add(0.5, 0.25, 0.5);
            this.Api.World.PlaySoundAt(Block.Sounds.Place, vec3d.X + 0.5, vec3d.Y + 0.5, vec3d.Z + 0.5, byPlayer);
            if (Api is ICoreClientAPI api)
                api.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            UpdateItemResistance();
            progress = 0;
            MarkDirty(true);
            
        }


        private void TryTake(ItemSlot fromSlot, IPlayer toPlayer)
        {
            ItemStack itemstack = fromSlot.TakeOut(1);
            if (!toPlayer.InventoryManager.TryGiveItemstack(itemstack))
                Api.World.SpawnItemEntity(itemstack, Pos.ToVec3d().Add(0.5, 0.1, 0.5));
            UpdateItemResistance();
            MarkDirty(true);
        }

        public override void TranslateMesh(MeshData mesh, int index)
        {
            if (index == inputInventoryIndex)
            {
                mesh.Scale(new Vec3f(0.5f, 0f, 0.5f), 0.5f, 0.5f, 0.5f);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            sb.AppendLine("Sawing:");
            bool isSawing = false;
            if (!inv[inputInventoryIndex].Empty)
            {
                    isSawing = true;
                string info = "  " + inv[inputInventoryIndex].GetStackName();
                if (progress > 0)
                    info += " (" + Math.Min(100, Math.Round(progress / processingResistance * 100)) + "%)";
                sb.AppendLine(info);
            }
            if (isSawing)
                return;
            sb.AppendLine("  nothing");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("progress", progress);
            tree.SetFloat("processingResistance", processingResistance);
            tree.SetItemstack("sawStack", inv[sawInventoryIndex].Itemstack);
            tree.SetFloat("accumulatedDurabilityCost", accumulatedDurabilityCost);
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
                sawCollectible = sawStack.Collectible;
            }
            accumulatedDurabilityCost = tree.GetFloat("accumulatedDurabilityCost");
        }
    }
}
