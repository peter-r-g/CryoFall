﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier2.Decorations
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Decorations;

    public class TechNodeDecorationTable : TechNode<TechGroupDecorationsT2>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddStructure<ObjectDecorationTable>();

            config.SetRequiredNode<TechNodeDecorationDoorBell>();
        }
    }
}