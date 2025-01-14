﻿namespace AtomicTorch.CBND.CoreMod.Quests.Tutorial
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.CraftRecipes;
    using AtomicTorch.CBND.CoreMod.Items.Tools;
    using AtomicTorch.CBND.CoreMod.Items.Tools.Lights;
    using AtomicTorch.CBND.CoreMod.PlayerTasks;

    public class QuestCraftATorch : ProtoQuest
    {
        public override string Description =>
            "Too many people make the mistake of feeling their way around at night in complete darkness. It just makes no sense when you can conveniently use a torch or a lantern.";

        public override string Hints =>
            @"[*] To use a torch, simply place it in your [b]hotbar[/b] and activate it as you would any other item.
              [*] There are many other types of light sources you can unlock later, both handheld and stationary.";

        public override string Name => "Craft and use a torch";

        public override ushort RewardLearningPoints => QuestConstants.TutorialRewardStage1;

        protected override void PrepareQuest(QuestsList prerequisites, TasksList tasks, HintsList hints)
        {
            tasks
                .Add(TaskCraftRecipe.RequireHandRecipe<RecipeTorch>())
                .Add(TaskUseItem.Require<ItemTorch>());

            prerequisites
                .Add<QuestMineAnyMineral>();
        }
    }
}