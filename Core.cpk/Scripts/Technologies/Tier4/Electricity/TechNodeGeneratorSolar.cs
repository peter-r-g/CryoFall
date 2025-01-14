﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier4.Electricity
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Generators;

    public class TechNodeGeneratorSolar : TechNode<TechGroupElectricityT4>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddStructure<ObjectGeneratorSolar>();

            config.SetRequiredNode<TechNodeProjectorTower>();
        }
    }
}