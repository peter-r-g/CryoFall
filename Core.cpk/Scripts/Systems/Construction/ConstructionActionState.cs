﻿namespace AtomicTorch.CBND.CoreMod.Systems.Construction
{
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Items;
    using AtomicTorch.CBND.CoreMod.Items.Tools;
    using AtomicTorch.CBND.CoreMod.Skills;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.StaticObjects;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.ConstructionSite;
    using AtomicTorch.CBND.CoreMod.Stats;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.ItemDurability;
    using AtomicTorch.CBND.CoreMod.Systems.LandClaim;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.GameEngine.Common.Helpers;

    public class ConstructionActionState
        : BaseActionState<ConstructionActionState.PublicState>
    {
        public readonly IConstructionStageConfigReadOnly Config;

        public readonly IItem ItemConstructionTool;

        public readonly StaticObjectPublicState ObjectPublicState;

        public readonly IProtoItemToolToolbox ProtoItemConstructionTool;

        public readonly IStaticWorldObject WorldObject;

        private readonly double stageStructureAddValue;

        private readonly double structurePointsMax;

        private double currentStageDurationSeconds;

        private double currentStageTimeRemainsSeconds;

        public ConstructionActionState(ICharacter character, IStaticWorldObject worldObject, IItem itemConstructionTool)
            : base(character)
        {
            this.WorldObject = worldObject;
            this.ItemConstructionTool = itemConstructionTool;
            this.ProtoItemConstructionTool = (IProtoItemToolToolbox)itemConstructionTool.ProtoGameObject;
            var protoStructure = (IProtoObjectStructure)worldObject.ProtoWorldObject;

            this.Config = protoStructure.GetStructureActiveConfig(worldObject);

            this.currentStageTimeRemainsSeconds = this.currentStageDurationSeconds =
                                                      this.CalculateStageDurationSeconds(character, isFirstStage: true);
            this.ObjectPublicState = worldObject.GetPublicState<StaticObjectPublicState>();

            this.structurePointsMax = protoStructure.SharedGetStructurePointsMax(worldObject);
            this.stageStructureAddValue = this.structurePointsMax / this.Config.StagesCount;
        }

        public bool IsRepair => !(this.WorldObject.ProtoGameObject is ProtoObjectConstructionSite);

        public double StructurePointsMax => this.structurePointsMax;

        public override IWorldObject TargetWorldObject => this.WorldObject;

        public bool CheckIsAllowed()
        {
            return LandClaimSystem.SharedIsObjectInsideOwnedOrFreeArea(
                this.WorldObject,
                this.Character,
                // players can progress construction and repair structures without a faction permission
                requireFactionPermission: false);
        }

        public bool CheckIsNeeded()
        {
            return this.ObjectPublicState.StructurePointsCurrent < this.structurePointsMax;
        }

        public override void SharedUpdate(double deltaTime)
        {
            if (this.CharacterPublicState.SelectedItem != this.ItemConstructionTool)
            {
                this.AbortAction();
                return;
            }

            if (!ConstructionSystem.SharedCheckCanInteract(
                    this.Character,
                    this.WorldObject,
                    writeToLog: true))
            {
                this.AbortAction();
                return;
            }

            this.currentStageTimeRemainsSeconds -= deltaTime;
            if (this.currentStageTimeRemainsSeconds <= 0)
            {
                this.SharedOnStageCompleted();
            }
            else if (Api.IsClient)
            {
                if (this.ObjectPublicState.StructurePointsCurrent >= this.structurePointsMax)
                {
                    // apparently the building finished construction/repair before the client simulation was complete
                    this.AbortAction();
                }
            }

            this.UpdateProgress();
        }

        /// <summary>
        /// Verifies that the character has all the required items to use this action.
        /// </summary>
        public bool ValidateRequiredItemsAvailable()
        {
            if (this.Config.CheckStageCanBeBuilt(this.Character))
            {
                return true;
            }

            Api.Logger.Warning(
                $"Building/repairing is not possible - not all required items are available: {this.WorldObject} by {this.Character}",
                this.Character);

            ConstructionSystem.SharedOnNotEnoughItemsAvailable(this);

            return false;
        }

        protected override void AbortAction()
        {
            ConstructionSystem.SharedAbortAction(
                this.Character,
                this.WorldObject);
        }

        protected override void SetupPublicState(PublicState state)
        {
            base.SetupPublicState(state);
            state.ProtoItemConstructionTool = this.ProtoItemConstructionTool;
        }

        private double CalculateStageDurationSeconds(ICharacter character, bool isFirstStage)
        {
            if (CreativeModeSystem.SharedIsInCreativeMode(character))
            {
                // force instant construct
                return 0;
            }

            var durationSeconds = this.Config.StageDurationSeconds;
            durationSeconds /= this.ProtoItemConstructionTool.ConstructionSpeedMultiplier;
            durationSeconds /= character.SharedGetFinalStatMultiplier(StatName.BuildingSpeed);
            durationSeconds = Api.Shared.RoundDurationByServerFrameDuration(durationSeconds);

            if (isFirstStage && Api.IsClient)
            {
                // Add ping to all client action durations.
                // Otherwise the client will not see immediately the result of the action
                // - the client will receive it only after RTT (ping) time.
                // TODO: currently it's possible to cancel action earlier and start a new one, but completed action result will come from server - which looks like a bug to player
                durationSeconds += Api.Client.CurrentGame.PingGameSeconds;
            }

            return durationSeconds;
        }

        private void SharedOnStageCompleted()
        {
            if (!this.ValidateRequiredItemsAvailable())
            {
                // don't have required items - cannot do building/repairing action
                this.AbortAction();
                return;
            }

            if (Api.IsServer)
            {
                // items are removing only on the Server-side
                this.Config.ServerDestroyRequiredItems(this.Character);

                // notify tool was used
                ServerItemUseObserver.NotifyItemUsed(this.Character, this.ItemConstructionTool);

                // reduce tool durability
                ItemDurabilitySystem.ServerModifyDurability(this.ItemConstructionTool, delta: -1);
            }

            this.currentStageDurationSeconds = this.CalculateStageDurationSeconds(this.Character, isFirstStage: false);
            this.currentStageTimeRemainsSeconds += this.currentStageDurationSeconds;

            var currentStructurePoints = this.ObjectPublicState.StructurePointsCurrent;
            var newStructurePoints = currentStructurePoints
                                     + this.stageStructureAddValue;

            // Please note: as we're using floating number (StructurePointsCurrent) to track the construction progress
            // it might cause some inaccuracy. In order to avoid this inaccuracy we're adding some tolerance.
            // The tolerance is also needed to handle the case when the blueprint was damaged only slightly.
            var completionTolerance = this.stageStructureAddValue / 2.0;

            // Please note: client will simply always construct until finished
            // Server will keep building stages until the completion tolerance reached.
            if ((Api.IsClient && currentStructurePoints < this.structurePointsMax)
                || (Api.IsServer && newStructurePoints + completionTolerance < this.structurePointsMax))
            {
                // repairing/building is still possible - more stages are available
                if (Api.IsServer)
                {
                    this.ObjectPublicState.StructurePointsCurrent = (float)newStructurePoints;
                    Api.Logger.Important(
                        $"Building/repairing progressed: {this.WorldObject} structure points: {newStructurePoints}/{this.structurePointsMax}; by {this.Character}");

                    if (this.IsRepair)
                    {
                        ((IProtoObjectStructure)this.WorldObject.ProtoStaticWorldObject)
                            .ServerOnRepairStageFinished(this.WorldObject, this.Character);
                    }
                }

                this.UpdateProgress();

                if (!this.ValidateRequiredItemsAvailable())
                {
                    // don't have enough required items - cannot continue building/repairing action
                    this.AbortAction();
                    return;
                }

                if (Api.IsServer
                    && this.ItemConstructionTool.IsDestroyed)
                {
                    // tool was destroyed (durability 0)
                    this.AbortAction();
                    return;
                }

                return;
            }

            // repairing/building is completed
            if (Api.IsServer)
            {
                newStructurePoints = this.structurePointsMax;
                Api.Logger.Important(
                    $"Building/repairing completed: {this.WorldObject} structure points: {newStructurePoints}/{this.structurePointsMax}; by {this.Character}");
                this.ObjectPublicState.StructurePointsCurrent = (float)newStructurePoints;

                if (this.IsRepair)
                {
                    ((IProtoObjectStructure)this.WorldObject.ProtoStaticWorldObject)
                        .ServerOnRepairStageFinished(this.WorldObject, this.Character);
                }

                if (this.ObjectPublicState is ConstructionSitePublicState constructionSiteState)
                {
                    constructionSiteState.LastBuildActionDoneByCharacter = this.Character;

                    // add skill experience for building
                    this.CharacterPrivateState.Skills.ServerAddSkillExperience<SkillBuilding>(
                        SkillBuilding.ExperienceAddWhenBuildingFinished);
                }
                else
                {
                    // add skill experience for repair
                    this.CharacterPrivateState.Skills.ServerAddSkillExperience<SkillBuilding>(
                        SkillBuilding.ExperienceAddWhenRepairFinished);
                }
            }

            this.SetCompleted(isCancelled: false);
            ConstructionSystem.SharedActionCompleted(this.Character, this);
        }

        private void UpdateProgress()
        {
            var timeRemains = this.currentStageDurationSeconds - this.currentStageTimeRemainsSeconds;
            timeRemains = MathHelper.Clamp(timeRemains, 0, this.currentStageDurationSeconds);
            this.ProgressPercents = 100 * timeRemains / this.currentStageDurationSeconds;
        }

        public class PublicState : PublicActionStateWithTargetObjectSounds
        {
            [SyncToClient(receivers: SyncToClientReceivers.ScopePlayers)]
            public IProtoItemToolToolbox ProtoItemConstructionTool { get; set; }

            protected override void ClientOnCompleted()
            {
                // don't play base sounds
                this.DestroyProcessSoundEmitter();
            }

            protected override ReadOnlySoundPreset<ObjectSound> SharedGetObjectSoundPreset()
            {
                return this.ProtoItemConstructionTool.ObjectInteractionSoundsPreset;
            }
        }
    }
}