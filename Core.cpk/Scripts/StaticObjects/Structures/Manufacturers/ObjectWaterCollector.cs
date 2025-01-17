﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers
{
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    /// <summary>
    /// Water collector is a manufacturer: empty bottle -> bottle with water.
    /// </summary>
    public class ObjectWaterCollector : ProtoObjectWell
    {
        public override string Description =>
            "Simple water collector designed to gather small amounts of water. Doesn't really work in deserts and other rocky places, for some reason...";

        public override string Name => "Water collector";

        public override ObjectMaterial ObjectMaterial => ObjectMaterial.Wood;

        public override double ObstacleBlockDamageCoef => 1.0;

        public override float StructurePointsMax => 500;

        public override double WaterCapacity => 25;

        public override double WaterProductionAmountPerSecond => 0.1;

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);

            renderer.DrawOrderOffsetY = 0.45;

            renderer.SpritePivotPoint = (0.5, 0);
            renderer.PositionOffset = (1, 0);
        }

        protected override void CreateLayout(StaticObjectLayout layout)
        {
            layout.Setup("##");
        }

        protected override void PrepareConstructionConfigWell(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade,
            out ProtoStructureCategory category)
        {
            category = GetCategory<StructureCategoryIndustry>();

            build.StagesCount = 5;
            build.StageDurationSeconds = BuildDuration.Short;
            build.AddStageRequiredItem<ItemTwigs>(count: 5);
            build.AddStageRequiredItem<ItemFibers>(count: 5);
            build.AddStageRequiredItem<ItemStone>(count: 1);

            repair.StagesCount = 10;
            repair.StageDurationSeconds = BuildDuration.Short;
            repair.AddStageRequiredItem<ItemTwigs>(count: 1);
            repair.AddStageRequiredItem<ItemFibers>(count: 1);
        }

        protected override void PrepareDefense(DefenseDescription defense)
        {
            defense.Set(ObjectDefensePresets.Default);
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeCircle(radius: 0.375, center: (0.75, 0.5))
                .AddShapeCircle(radius: 0.375, center: (1.25, 0.5))
                .AddShapeRectangle((1.4, 0.7), offset: (0.3, 0.45), CollisionGroups.HitboxMelee)
                .AddShapeRectangle((1.4, 0.9), offset: (0.3, 0.25), CollisionGroups.ClickArea);
        }
    }
}