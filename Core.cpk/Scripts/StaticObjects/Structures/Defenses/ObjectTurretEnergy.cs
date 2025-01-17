﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Defenses
{
    using AtomicTorch.CBND.CoreMod.Characters.Turrets;
    using AtomicTorch.CBND.CoreMod.ItemContainers.Turrets;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;

    public class ObjectTurretEnergy : ProtoObjectTurret
    {
        public override BaseItemsContainerTurretAmmo ContainerAmmoType
            => null; // no ammo

        public override string Description =>
            "Fully automated sentry turret that uses coherent energy beam to damage its target.";

        public override double ElectricityConsumptionPerSecondWhenActive => 0.5;

        public override string Name => "Energy turret";

        public override double StructureExplosiveDefenseCoef => 0.4;

        public override float StructurePointsMax => 3000;

        protected override void PrepareConstructionConfigTurret(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade)
        {
            build.StagesCount = 5;
            build.StageDurationSeconds = BuildDuration.Short;
            build.AddStageRequiredItem<ItemIngotSteel>(count: 5);
            build.AddStageRequiredItem<ItemWire>(count: 5);
            build.AddStageRequiredItem<ItemComponentsHighTech>(count: 1);
            build.AddStageRequiredItem<ItemComponentsOptical>(count: 1);

            repair.StagesCount = 5;
            repair.StageDurationSeconds = BuildDuration.Short;
            repair.AddStageRequiredItem<ItemIngotSteel>(count: 5);
            repair.AddStageRequiredItem<ItemWire>(count: 5);
        }

        protected override void PrepareDefense(DefenseDescription defense)
        {
            defense.Set(ObjectDefensePresets.Tier3);
        }

        protected override void PrepareProtoTurretObject(out IProtoCharacterTurret protoCharacter)
        {
            protoCharacter = GetProtoEntity<CharacterTurretEnergy>();
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (0.9, 1.17), offset: (0.05, 0), group: CollisionGroups.ClickArea);
        }
    }
}