﻿namespace AtomicTorch.CBND.CoreMod.Items.Ammo
{
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects.Debuffs;
    using AtomicTorch.CBND.CoreMod.Items.Weapons;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Weapons;

    public class ItemAmmo12gaSlugs : ProtoItemAmmo, IAmmoCaliber12g
    {
        public override string Description =>
            "Slugs are versatile ammo that can be used against most types of opponents. Due to their high mass, they're quite effective even against well-armored targets.";

        public override bool IsReferenceAmmo => false;

        public override string Name => "12-gauge slug ammo";

        public override void ServerOnCharacterHit(ICharacter damagedCharacter, double damage, ref bool isDamageStop)
        {
            if (damage < 1)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectDazed>(
                // add 0.4 seconds of dazed effect
                intensity: 0.4 / StatusEffectDazed.MaxDuration);
        }

        protected override void PrepareDamageDescription(
            out double damageValue,
            out double armorPiercingCoef,
            out double finalDamageMultiplier,
            out double rangeMax,
            DamageDistribution damageDistribution)
        {
            damageValue = 36;
            armorPiercingCoef = 0;
            finalDamageMultiplier = 1.1;
            rangeMax = 9;
            damageDistribution.Set(DamageType.Kinetic, 0.7)
                              .Set(DamageType.Impact, 0.3);
        }

        protected override WeaponFireTracePreset PrepareFireTracePreset()
        {
            return WeaponFireTracePresets.Heavy;
        }
    }
}