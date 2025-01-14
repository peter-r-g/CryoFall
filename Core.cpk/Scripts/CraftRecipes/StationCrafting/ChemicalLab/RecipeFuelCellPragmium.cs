﻿namespace AtomicTorch.CBND.CoreMod.CraftRecipes
{
    using System;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.CraftingStations;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.Crafting;

    public class RecipeFuelCellPragmium : Recipe.RecipeForStationCrafting
    {
        protected override void SetupRecipe(
            StationsList stations,
            out TimeSpan duration,
            InputItems inputItems,
            OutputItems outputItems)
        {
            stations.Add<ObjectChemicalLab>();

            duration = CraftingDuration.Medium;

            inputItems.Add<ItemFuelCellEmpty>(count: 1);
            inputItems.Add<ItemOrePragmium>(count: 30);
            inputItems.Add<ItemOreLithium>(count: 30);
            inputItems.Add<ItemComponentsIndustrialChemicals>(count: 5);

            outputItems.Add<ItemFuelCellPragmium>(count: 1);
        }
    }
}