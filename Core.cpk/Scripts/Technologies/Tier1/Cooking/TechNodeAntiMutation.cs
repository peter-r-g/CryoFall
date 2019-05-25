﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier1.Cooking
{
    using AtomicTorch.CBND.CoreMod.CraftRecipes;

    public class TechNodeAntiMutation : TechNode<TechGroupCooking>
    {
        public override FeatureAvailability AvailableIn => FeatureAvailability.OnlyPvE;

        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddRecipe<RecipeAntiMutation>();

            config.SetRequiredNode<TechNodeHerbalRemedy>();
        }
    }
}