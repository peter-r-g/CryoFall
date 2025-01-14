﻿namespace AtomicTorch.CBND.CoreMod.Systems.LandClaim
{
    using System;
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.LandClaim;
    using AtomicTorch.CBND.GameApi.Data.Logic;
    using AtomicTorch.CBND.GameApi.Data.Structures;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ClientLandClaimGroupRenderer
    {
        public static readonly TextureResource TextureResourceLandClaimAreaCell
            = new("FX/LandClaimAreaCell",
                  qualityOffset: -100);

        private readonly List<ILogicObject> areas
            = new();

        private readonly Func<IComponentSpriteRenderer> callbackGetRendererFromCache;

        private readonly Action<IComponentSpriteRenderer> callbackReturnRendererToCache;

        private readonly bool isGraceAreaRenderer;

        private readonly RenderingMaterial material;

        private readonly List<IComponentSpriteRenderer> renderers
            = new();

        private IProtoObjectLandClaim blueprintProtoObjectLandClaim;

        private Vector2Ushort? blueprintTilePosition;

        private RectangleInt? cachedGroupBounds;

        private bool isDisplayOverlays;

        private bool isVisible;

        public ClientLandClaimGroupRenderer(
            RenderingMaterial material,
            bool isGraceAreaRenderer,
            Func<IComponentSpriteRenderer> callbackGetRendererFromCache,
            Action<IComponentSpriteRenderer> callbackReturnRendererToCache)
        {
            this.material = material;
            this.isGraceAreaRenderer = isGraceAreaRenderer;
            this.callbackGetRendererFromCache = callbackGetRendererFromCache;
            this.callbackReturnRendererToCache = callbackReturnRendererToCache;
        }

        public RectangleInt Bounds
        {
            get
            {
                if (this.cachedGroupBounds.HasValue)
                {
                    return this.cachedGroupBounds.Value;
                }

                this.cachedGroupBounds = this.CalculateBounds();
                return this.cachedGroupBounds.Value;
            }
        }

        public bool IsDisplayOverlays
        {
            get => this.isDisplayOverlays;
            set
            {
                if (this.isDisplayOverlays == value)
                {
                    return;
                }

                this.isDisplayOverlays = value;
                if (!this.isVisible)
                {
                    return;
                }

                // recreate renderers
                this.DestroyRenderers();
                this.CreateRenderers();
            }
        }

        public bool IsEmpty => this.areas.Count == 0;

        public bool IsVisible
        {
            get => this.isVisible;
            set
            {
                if (this.isVisible == value)
                {
                    return;
                }

                this.isVisible = value;

                if (this.isVisible)
                {
                    this.CreateRenderers();
                }
                else
                {
                    this.DestroyRenderers();
                }
            }
        }

        public static RenderingMaterial CreateRenderingMaterial()
        {
            var material = RenderingMaterial.Create(new EffectResource("LandClaimArea"));
            material.EffectParameters.Set("SpriteTexture",
                                          TextureResourceLandClaimAreaCell);
            return material;
        }

        public void RegisterArea(ILogicObject area)
        {
            this.areas.Add(area);
            this.cachedGroupBounds = null;
            this.DestroyRenderers();
        }

        public void RegisterBlueprint(Vector2Ushort tilePosition, IProtoObjectLandClaim protoObjectLandClaim)
        {
            this.blueprintTilePosition = tilePosition;
            this.blueprintProtoObjectLandClaim = protoObjectLandClaim;
            this.DestroyRenderers();
        }

        public void UnregisterArea(ILogicObject area)
        {
            this.areas.Remove(area);
            this.cachedGroupBounds = null;
            this.DestroyRenderers();
        }

        public void UnregisterBlueprint()
        {
            this.blueprintTilePosition = null;
            this.blueprintProtoObjectLandClaim = null;
            this.DestroyRenderers();
        }

        private RectangleInt CalculateBounds()
        {
            // TODO: redo this completely as currently it will cause a lot of unnecessary quadtree nesting
            // and also break the optimization of rendering
            return new RectangleInt(0,
                                    0,
                                    ushort.MaxValue / 2 - 100,
                                    ushort.MaxValue / 2 - 100);

            var hasBounds = false;
            ushort minX = ushort.MaxValue,
                   minY = ushort.MaxValue,
                   maxX = 0,
                   maxY = 0;

            foreach (var area in this.areas)
            {
                var areaPublicState = LandClaimArea.GetPublicState(area);

                hasBounds = true;
                var bounds = LandClaimSystem.SharedGetLandClaimAreaBounds(area);
                bounds = bounds.Inflate(areaPublicState.ProtoObjectLandClaim
                                                       .LandClaimGraceAreaPaddingSizeOneDirection);

                minX = Math.Min(minX, (ushort)Math.Min(bounds.X,                 ushort.MaxValue));
                minY = Math.Min(minY, (ushort)Math.Min(bounds.Y,                 ushort.MaxValue));
                maxX = Math.Max(maxX, (ushort)Math.Min(bounds.X + bounds.Width,  ushort.MaxValue));
                maxY = Math.Max(maxY, (ushort)Math.Min(bounds.Y + bounds.Height, ushort.MaxValue));
            }

            if (!hasBounds)
            {
                return default;
            }

            return new RectangleInt(x: minX,
                                    y: minY,
                                    width: maxX - minX,
                                    height: maxY - minY);
        }

        private void CreateRenderers()
        {
            this.DestroyRenderers();

            this.isVisible = true;

            if (this.isDisplayOverlays)
            {
                // render each area as a separate single layer
                foreach (var area in this.areas)
                {
                    CreateSeparateAreaRenderer(area);
                }
            }
            else // unified layers mode
            {
                CreateUnifiedRenderers();
            }

            void CreateUnifiedRenderers()
            {
                // create and fill quad tree with all areas
                var groupBounds = this.Bounds;
                var quadTree = QuadTreeNodeFactory.Create(
                    position: new Vector2Ushort((ushort)groupBounds.X,
                                                (ushort)groupBounds.Y),
                    size: (ushort)Math.Max(groupBounds.Width + 2,
                                           groupBounds.Height + 2));

                foreach (var area in this.areas)
                {
                    var publicState = LandClaimArea.GetPublicState(area);
                    var protoObjectLandClaim = publicState.ProtoObjectLandClaim;
                    var areaBounds = LandClaimSystem.SharedGetLandClaimAreaBounds(area);
                    FillArea(areaBounds, protoObjectLandClaim);
                }

                if (this.blueprintTilePosition.HasValue)
                {
                    var areaBounds = LandClaimSystem.SharedCalculateLandClaimAreaBounds(
                        this.blueprintTilePosition.Value,
                        this.blueprintProtoObjectLandClaim.LandClaimSize);

                    FillArea(areaBounds, this.blueprintProtoObjectLandClaim);
                }

                if (this.isGraceAreaRenderer)
                {
                    // remove filled areas so only the grace area around the base is rendered
                    foreach (var area in this.areas)
                    {
                        var areaBounds = LandClaimSystem.SharedGetLandClaimAreaBounds(area);
                        ResetFilledPositions(areaBounds);
                    }

                    if (this.blueprintTilePosition.HasValue)
                    {
                        var areaBounds = LandClaimSystem.SharedCalculateLandClaimAreaBounds(
                            this.blueprintTilePosition.Value,
                            this.blueprintProtoObjectLandClaim.LandClaimSize);
                        ResetFilledPositions(areaBounds);
                    }

                    void ResetFilledPositions(RectangleInt rectangleInt)
                    {
                        var areaBoundsRight = (ushort)rectangleInt.Right;
                        var areaBoundsTop = (ushort)rectangleInt.Top;
                        for (var x = (ushort)rectangleInt.Left; x < areaBoundsRight; x++)
                        for (var y = (ushort)rectangleInt.Bottom; y < areaBoundsTop; y++)
                        {
                            quadTree.ResetFilledPosition((x, y));
                        }
                    }
                }

                // create renderers for filled quad tree nodes
                foreach (var node in quadTree.EnumerateFilledNodes())
                {
                    var renderer = this.callbackGetRendererFromCache();
                    renderer.RenderingMaterial = this.material;

                    // uncomment to visualize quad tree
                    //var mat = CreateRenderingMaterial();
                    //mat.EffectParameters.Set("Color",
                    //                         Color.FromArgb(0xFF,
                    //                                        (byte)RandomHelper.Next(0, 255),
                    //                                        (byte)RandomHelper.Next(0, 255),
                    //                                        (byte)RandomHelper.Next(0, 255)));
                    //renderer.RenderingMaterial = mat;

                    renderer.Scale = 1 << node.SizePowerOfTwo;
                    renderer.PositionOffset = node.Position.ToVector2D();
                    this.renderers.Add(renderer);
                    renderer.IsEnabled = true;
                }

                //Api.Logger.Dev("Preparing areas group for rendering is done! Used renderers number: " + this.renderers.Count);

                void FillArea(RectangleInt areaBounds, IProtoObjectLandClaim protoObjectLandClaim)
                {
                    if (this.isGraceAreaRenderer)
                    {
                        areaBounds = areaBounds.Inflate(protoObjectLandClaim
                                                            .LandClaimGraceAreaPaddingSizeOneDirection);
                    }

                    var areaBoundsRight = (ushort)areaBounds.Right;
                    var areaBoundsTop = (ushort)areaBounds.Top;
                    for (var x = (ushort)areaBounds.Left; x < areaBoundsRight; x++)
                    for (var y = (ushort)areaBounds.Bottom; y < areaBoundsTop; y++)
                    {
                        quadTree.SetFilledPosition((x, y));
                    }
                }
            }

            void CreateSeparateAreaRenderer(ILogicObject area)
            {
                var publicState = LandClaimArea.GetPublicState(area);
                CreateSeparateRenderer(publicState.ProtoObjectLandClaim,
                                       publicState.LandClaimCenterTilePosition);

                if (this.blueprintTilePosition.HasValue)
                {
                    CreateSeparateRenderer(this.blueprintProtoObjectLandClaim,
                                           this.blueprintTilePosition.Value);
                }

                void CreateSeparateRenderer(IProtoObjectLandClaim protoObjectLandClaim, Vector2Ushort position)
                {
                    int size = protoObjectLandClaim.LandClaimSize;
                    if (this.isGraceAreaRenderer)
                    {
                        var padding = protoObjectLandClaim.LandClaimGraceAreaPaddingSizeOneDirection;
                        size += 2 * padding;
                    }

                    position = new Vector2Ushort((ushort)Math.Max(0, position.X - size / 2),
                                                 (ushort)Math.Max(0, position.Y - size / 2));

                    var renderer = this.callbackGetRendererFromCache();
                    renderer.RenderingMaterial = this.material;
                    renderer.Scale = size;
                    renderer.PositionOffset = position.ToVector2D();
                    this.renderers.Add(renderer);
                    renderer.IsEnabled = true;
                }
            }
        }

        private void DestroyRenderers()
        {
            foreach (var renderer in this.renderers)
            {
                this.callbackReturnRendererToCache(renderer);
            }

            this.renderers.Clear();

            this.isVisible = false;
        }
    }
}