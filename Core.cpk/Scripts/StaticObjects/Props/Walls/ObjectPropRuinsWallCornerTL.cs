﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Props.Walls
{
    public class ObjectPropRuinsWallCornerTL : ProtoObjectProp
    {
        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (0.85, 0.65), offset: (0.15, 0));
            AddFullHeightWallHitboxes(data, width: 0.85, offsetX: 0.15);
        }
    }
}