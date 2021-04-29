﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier5.ExoticWeapons
{
    using AtomicTorch.CBND.CoreMod.CraftRecipes;

    public class TechNodeToxinProliferator : TechNode<TechGroupExoticWeapons>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddRecipe<RecipeToxinProliferator>();

            config.SetRequiredNode<TechNodeAmmoKeinite>();
        }
    }
}