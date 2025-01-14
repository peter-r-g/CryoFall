﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Farms
{
    using System;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.GameEngine.Common.Primitives;

    public abstract class ProtoObjectFarm
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoObjectStructure
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoObjectFarm
        where TPrivateState : StructurePrivateState, new()
        where TPublicState : StaticObjectPublicState, new()
        where TClientState : StaticObjectClientState, new()
    {
        public override double ClientUpdateIntervalSeconds => double.MaxValue;

        public abstract bool IsDrawingPlantShadow { get; }

        public override bool IsRepeatPlacement => true;

        public override StaticObjectKind Kind => StaticObjectKind.Floor;

        public override double ObstacleBlockDamageCoef => 0;

        public virtual Vector2D PlacedPlantPositionOffset { get; } = Vector2D.Zero;

        public override double ServerUpdateIntervalSeconds => double.MaxValue;

        public override void ServerApplyDecay(IStaticWorldObject worldObject, double deltaTime)
        {
            // don't decay if there are any plants on this farm
            foreach (var tile in worldObject.OccupiedTiles)
            {
                foreach (var staticWorldObject in Api.Shared.WrapInTempList(tile.StaticObjects).EnumerateAndDispose())
                {
                    if (staticWorldObject.ProtoStaticWorldObject is IProtoObjectPlant)
                    {
                        return;
                    }
                }
            }

            base.ServerApplyDecay(worldObject, deltaTime);
        }

        public override void ServerOnDestroy(IStaticWorldObject gameObject)
        {
            // destroy all the plants growing there
            foreach (var tile in gameObject.OccupiedTiles)
            {
                foreach (var staticWorldObject in Api.Shared.WrapInTempList(tile.StaticObjects).EnumerateAndDispose())
                {
                    if (staticWorldObject.ProtoStaticWorldObject is IProtoObjectPlant)
                    {
                        Server.World.DestroyObject(staticWorldObject);
                    }
                }
            }

            base.ServerOnDestroy(gameObject);
        }

        protected sealed override void ClientUpdate(ClientUpdateData data)
        {
        }

        protected sealed override void PrepareConstructionConfig(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade,
            out ProtoStructureCategory category)
        {
            category = GetCategory<StructureCategoryFood>();
            this.PrepareFarmConstructionConfig(tileRequirements, build, repair);
        }

        protected abstract void PrepareFarmConstructionConfig(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair);

        protected virtual void PrepareProtoObjectFarmPlot()
        {
        }

        protected sealed override void PrepareProtoStaticWorldObject()
        {
            base.PrepareProtoStaticWorldObject();

            if (IsServer)
            {
                ConstructionRelocationSystem.ServerStructureRelocated += this.ServerStructureRelocatedHandler;
            }

            this.PrepareProtoObjectFarmPlot();
        }

        protected sealed override void ServerUpdate(ServerUpdateData data)
        {
        }

        protected override bool SharedIsAllowedObjectToInteractThrough(IWorldObject worldObject)
        {
            return true;
        }

        /// <summary>
        /// Relocate plant in farm plot when relocating the farm.
        /// </summary>
        private void ServerStructureRelocatedHandler(
            ICharacter character,
            Vector2Ushort fromPosition,
            IStaticWorldObject staticWorldObject)
        {
            if (!ReferenceEquals(staticWorldObject.ProtoGameObject, this))
            {
                return;
            }

            foreach (var otherObject in Server.World.GetTile(fromPosition).StaticObjects)
            {
                if (!(otherObject.ProtoGameObject is IProtoObjectPlant))
                {
                    continue;
                }

                Server.World.SetPosition(otherObject, staticWorldObject.TilePosition);

                try
                {
                    otherObject.ServerInitialize();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }

                break;
            }
        }
    }
}