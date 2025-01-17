﻿namespace AtomicTorch.CBND.CoreMod.Quests.Tutorial
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.PlayerTasks;
    using AtomicTorch.CBND.CoreMod.Tiles;

    public class QuestExploreBiomes1 : ProtoQuest
    {
        public override string Description =>
            "Each different biome has unique flora and fauna associated with it, in addition to other properties. It is a good idea to familiarize yourself with each of the basic biomes to extract maximum use out of them.";

        public override string Hints =>
            @"[*] Meadows have large numbers of bushes, herbs and other useful plants to forage.
              [*] You can find many aquatic animals near a shore.";

        public override string Name => "Explore biomes—part one";

        public override ushort RewardLearningPoints => QuestConstants.TutorialRewardStage2;

        protected override void PrepareQuest(QuestsList prerequisites, TasksList tasks, HintsList hints)
        {
            tasks
                .Add(TaskVisitTile.Require<TileForestTemperate>())
                .Add(TaskVisitTile.Require<TileForestTropical>())
                .Add(TaskVisitTile.Require<TileBeachTemperate>())
                .Add(TaskVisitTile.Require<TileLakeShore>())
                .Add(TaskVisitTile.Require<TileMeadows>());

            prerequisites
                .Add<QuestClaySandGlassBottlesWaterCollector>();
        }
    }
}