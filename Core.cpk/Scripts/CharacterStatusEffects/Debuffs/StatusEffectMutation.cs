﻿namespace AtomicTorch.CBND.CoreMod.CharacterStatusEffects.Debuffs
{
    using AtomicTorch.CBND.CoreMod.Stats;

    public class StatusEffectMutation : ProtoStatusEffect
    {
        public override string Description =>
            "You have been exposed to extreme levels of radiation and cells in your body have started to mutate. This can't be good! Try to avoid entering irradiated areas again.";

        public override StatusEffectDisplayMode DisplayMode => StatusEffectDisplayMode.None;

        /// <summary>
        /// Permanent, should not decrease.
        /// </summary>
        public override double IntensityAutoDecreasePerSecondValue => 0;

        /// <summary>
        /// Not removed on respawn. Only with special medicine.
        /// </summary>
        public override bool IsRemovedOnRespawn => false;

        public override StatusEffectKind Kind => StatusEffectKind.Debuff;

        public override string Name => "Mutation";

        protected override void PrepareEffects(Effects effects)
        {
            // instant death from radiation is guaranteed
            effects.AddPercent(this, StatName.RadiationPoisoningEffectMultiplier, 1000)
                   .AddPercent(this, StatName.RadiationPoisoningIncreaseRateMultiplier, 10000);
        }
    }
}