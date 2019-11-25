﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Props.Building
{
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ObjectPropRuinsBuildingTL : ProtoObjectProp
    {
        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);
            renderer.DrawOrder = DrawOrder.OverDefault;
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
        }
    }
}