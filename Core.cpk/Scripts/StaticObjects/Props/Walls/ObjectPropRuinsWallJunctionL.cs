﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Props.Walls
{
    public class ObjectPropRuinsWallJunctionL : ProtoObjectProp
    {
        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (0.7, 1), offset: (0.15, 0))
                .AddShapeRectangle(size: (0.15, 0.65));
            AddFullHeightWallHitboxes(data, width: 0.85, offsetX: 0);
        }
    }
}