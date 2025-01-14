﻿namespace AtomicTorch.CBND.CoreMod.Quests.Tutorial
{
    using AtomicTorch.CBND.CoreMod.Items.Equipment;
    using AtomicTorch.CBND.CoreMod.PlayerTasks;
    using AtomicTorch.CBND.CoreMod.Technologies.Tier1.Defense;
    using AtomicTorch.CBND.GameApi.Scripting;

    public class QuestCraftAndEquipClothArmor : ProtoQuest
    {
        public override string Description =>
            "Running around without any protection would certainly lead to a quick death. Having even a basic armor is always a good idea. Research, craft and equip basic cloth armor.";

        public override string Hints =>
            @"[*] Cloth armor may not provide significant protection, but it's cheap to craft and certainly better than nothing.
              [*] There are [b]different damage types[/b], and different armor offers different protection values for each. You can see this information in your inventory screen.";

        public override string Name => "Craft and equip cloth armor";

        public override ushort RewardLearningPoints => QuestConstants.TutorialRewardStage2;

        protected override void PrepareQuest(QuestsList prerequisites, TasksList tasks, HintsList hints)
        {
            var protoItemClothHelmet = Api.GetProtoEntity<ItemClothHelmet>();
            var protoItemClothArmor = Api.GetProtoEntity<ItemClothArmor>();

            tasks
                .Add(TaskHaveTechNode.Require<TechNodeClothArmor>())
                // suggest cloth hat but require any head item
                .Add(TaskHaveItemEquipped.Require<IProtoItemEquipmentHead>(
                                             string.Format(TaskHaveItemEquipped.DescriptionFormat,
                                                           protoItemClothHelmet.Name))
                                         .WithIcon(protoItemClothHelmet.Icon))
                // suggest cloth armor but require any armor item
                .Add(TaskHaveItemEquipped.Require<IProtoItemEquipmentArmor>(
                                             string.Format(TaskHaveItemEquipped.DescriptionFormat,
                                                           protoItemClothArmor.Name))
                                         .WithIcon(protoItemClothArmor.Icon));

            prerequisites
                .Add<QuestUnlockAndBuildWorkbench>();
        }
    }
}