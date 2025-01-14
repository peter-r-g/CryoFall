﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Props.Misc
{
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ObjectPropHelicopter : ProtoObjectProp
    {
        public override bool CanFlipSprite => false;

        public override ObjectMaterial ObjectMaterial
            => ObjectMaterial.Metal;

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);
            renderer.DrawOrderOffsetY = 0.8;
        }

        protected override void CreateLayout(StaticObjectLayout layout)
        {
            layout.Setup("####",
                         "####");
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (0.6, 0.6), offset: (0, 0.15))
                .AddShapeRectangle(size: (1.0, 0.8), offset: (0.6, 0.15))
                .AddShapeRectangle(size: (1.0, 0.8), offset: (1.6, 0.30))
                .AddShapeRectangle(size: (0.6, 0.7), offset: (2.6, 0.50))
                .AddShapeRectangle(size: (0.4, 0.6), offset: (3.2, 0.70))
                .AddShapeRectangle(size: (0.2, 0.3), offset: (3.6, 1.10));

            AddHalfHeightWallHitboxes(data, width: 1.1, offsetX: 0.7, offsetY: 0.15);
            AddHalfHeightWallHitboxes(data, width: 1,   offsetX: 1.6, offsetY: 0.5);
            AddHalfHeightWallHitboxes(data, width: 0.4,   offsetX: 2.6, offsetY: 0.65);
        }
    }
}