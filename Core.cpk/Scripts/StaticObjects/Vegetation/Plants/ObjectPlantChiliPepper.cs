﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants
{
    using System;
    using AtomicTorch.CBND.CoreMod.Items.Food;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.Skills;
    using AtomicTorch.CBND.CoreMod.Systems.Droplists;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ObjectPlantChiliPepper : ProtoObjectPlant
    {
        public override string Name => "Chili pepper";

        public override byte NumberOfHarvests => 2;

        public override double ObstacleBlockDamageCoef => 0.5;

        public override float StructurePointsMax => 100;

        protected override TimeSpan TimeToGiveHarvest { get; } = TimeSpan.FromHours(2);

        protected override TimeSpan TimeToMature { get; } = TimeSpan.FromHours(10);

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);
            renderer.DrawOrderOffsetY = 0.3;
        }

        protected override ITextureResource PrepareDefaultTexture(Type thisType)
        {
            return new TextureAtlasResource(
                base.PrepareDefaultTexture(thisType),
                columns: 6,
                rows: 1);
        }

        protected override void PrepareGatheringDroplist(DropItemsList droplist)
        {
            droplist.Add<ItemChiliPepper>(count: 3);

            // additional yield
            droplist.Add<ItemChiliPepper>(count: 2, condition: ItemFertilizer.ConditionExtraYield);
            droplist.Add<ItemChiliPepper>(count: 2, condition: SkillFarming.ConditionExtraYield, probability: 0.05f);
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeCircle(radius: 0.28, center: (0.5, 0.55))
                .AddShapeCircle(radius: 0.35, center: (0.5, 0.55), group: CollisionGroups.HitboxMelee)
                .AddShapeCircle(radius: 0.3,  center: (0.5, 0.55), group: CollisionGroups.HitboxRanged)
                .AddShapeCircle(radius: 0.45, center: (0.5, 0.5),  group: CollisionGroups.ClickArea);
        }
    }
}