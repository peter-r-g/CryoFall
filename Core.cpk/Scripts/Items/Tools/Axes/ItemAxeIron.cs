﻿namespace AtomicTorch.CBND.CoreMod.Items.Tools.Axes
{
    public class ItemAxeIron : ProtoItemToolAxe
    {
        public override double DamageApplyDelay => 0.075;

        public override double DamageToNonTree => 16;

        public override double DamageToTree => 55;

        public override string Description
            => "Iron axe is great for chopping trees. Faster and more durable than stone axe.";

        public override uint DurabilityMax => 1000;

        public override double FireAnimationDuration => 0.6;

        public override string Name => "Iron axe";
    }
}