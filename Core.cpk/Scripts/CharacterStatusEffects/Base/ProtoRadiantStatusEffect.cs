﻿namespace AtomicTorch.CBND.CoreMod.CharacterStatusEffects
{
    using System;
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Stats;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.GameEngine.Common.Helpers;

    /// <summary>
    /// Special status effect which depends on nearby objects to increase/decrease.
    /// </summary>
    public abstract class ProtoRadiantStatusEffect : ProtoStatusEffect
    {
        /// <summary>
        /// How much a 100% armor would protect you from environmental effect.
        /// </summary>
        protected const double DefensePotentialMultiplier = 0.75;

        private static readonly List<IWorldObject> TempResult
            = new List<IWorldObject>(capacity: 512);

        public sealed override double IntensityAutoDecreasePerSecondFraction =>
            0; // doesn't decrease via base implementation

        public sealed override double IntensityAutoDecreasePerSecondValue =>
            0; // doesn't decrease via base implementation

        public override double ServerAutoAddRepeatIntervalSeconds => 0.5;

        public override double ServerUpdateIntervalSeconds => 0.5;

        protected abstract StatName DefenseStatName { get; }

        /// <summary>
        /// Time to remove full effect intensity back to zero in case the environmental intensity is 0.
        /// </summary>
        protected abstract double TimeToCoolDownToZeroSeconds { get; }

        /// <summary>
        /// Time to reach the full intensity in case the environmental intensity is 1.
        /// </summary>
        protected abstract double TimeToReachFullIntensitySeconds { get; }

        protected override IEnumerable<ICharacter> ServerAutoAddGetCharacterCandidates()
        {
            return Server.Characters.EnumerateAllPlayerCharacters(onlyOnline: true);
        }

        protected virtual double ServerCalculateEnvironmentalIntensityAroundCharacter(ICharacter character)
        {
            Server.World.GetWorldObjectsInView(character, TempResult, sortByDistance: false);

            var result = 0.0;
            foreach (var worldObject in TempResult)
            {
                var environmentalIntensity = this.ServerCalculateObjectEnvironmentalIntensity(character,
                                                                                              worldObject);

                if (result < environmentalIntensity)
                {
                    result = environmentalIntensity;
                }
            }

            return result;
        }

        protected abstract double ServerCalculateObjectEnvironmentalIntensity(
            ICharacter character,
            IWorldObject worldObject);

        // this method is called only for player characters and adding effect if it's absent
        protected override void ServerOnAutoAdd(ICharacter character)
        {
            var statusEffects = character.GetPrivateState<BaseCharacterPrivateState>()
                                         .StatusEffects;

            foreach (var statusEffect in statusEffects)
            {
                if (statusEffect.ProtoLogicObject == this)
                {
                    // status effect is already added
                    return;
                }
            }

            this.ServerUpdateRadiantStatusEffectIntensity(character,
                                                          deltaTime: this.ServerAutoAddRepeatIntervalSeconds);
        }

        protected override void ServerUpdate(StatusEffectData data)
        {
            base.ServerUpdate(data);
            this.ServerUpdateRadiantStatusEffectIntensity(data.Character, data.DeltaTime);
        }

        private void ServerUpdateRadiantStatusEffectIntensity(ICharacter character, double deltaTime)
        {
            var environmentalIntensity = character.IsNpc
                                             ? 0 // environmental intensity doesn't apply to NPCs
                                             : this.ServerCalculateEnvironmentalIntensityAroundCharacter(character);

            if (environmentalIntensity > 0)
            {
                // Armor/defense proportionally reduces the maximum environmental intensity.
                // i.e. 100% defense results in 75% reduced environmental intensity
                var defense = character.SharedGetFinalStatValue(this.DefenseStatName);
                defense = MathHelper.Clamp(defense, 0, 1);
                environmentalIntensity *= 1 - DefensePotentialMultiplier * defense;
            }

            var currentIntensity = character.SharedGetStatusEffectIntensity(this);

            var delta = environmentalIntensity - currentIntensity;
            if (delta > 0)
            {
                // need to add the intensity
                var speed = 1.0 / (environmentalIntensity * this.TimeToReachFullIntensitySeconds);
                delta = Math.Min(delta, speed * deltaTime);
            }
            else if (delta < 0)
            {
                // need to reduce the intensity
                var speed = 1.0 / ((environmentalIntensity - 1) * this.TimeToCoolDownToZeroSeconds);
                delta = Math.Max(delta, speed * deltaTime);
            }
            else
            {
                // no need to change the intensity
                return;
            }

            var newIntensity = currentIntensity + delta;
            character.ServerSetStatusEffectIntensity(this, intensity: newIntensity);
        }
    }
}