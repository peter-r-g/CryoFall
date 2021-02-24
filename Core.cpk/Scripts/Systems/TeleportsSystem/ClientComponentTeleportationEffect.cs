﻿namespace AtomicTorch.CBND.CoreMod.Systems.TeleportsSystem
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Vehicles;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ClientComponentTeleportationEffect : ClientComponent
    {
        private static readonly SoundResource SoundResourceTeleportIn
            = new("Objects/Misc/Teleport/TeleportIn.ogg");

        private static readonly SoundResource SoundResourceTeleportOut
            = new("Objects/Misc/Teleport/TeleportOut.ogg");

        private readonly List<RenderingEffectTeleportation> currentRenderingEffects = new();

        private readonly List<IComponentRendererWithEffects> lastComponents = new();

        private double duration;

        private Vector2Ushort fallbackTilePosition;

        private bool isTeleportationOut;

        private double time;

        private IDynamicWorldObject worldObject;

        public static void CreateEffect(
            IDynamicWorldObject worldObject,
            Vector2Ushort fallbackTilePosition,
            double animationDuration,
            double teleportationDelay,
            bool isTeleportationOut)
        {
            var isCurrentCharacter
                = worldObject == ClientCurrentCharacterHelper.Character;

            var sceneObject = Client.Scene.CreateSceneObject("Temp teleportation effect holder");
            sceneObject.Destroy(isTeleportationOut
                                    ? teleportationDelay // teleportation delay includes the animation duration
                                    : animationDuration);

            sceneObject.AddComponent<ClientComponentTeleportationEffect>()
                       .Setup(worldObject, fallbackTilePosition, animationDuration, isTeleportationOut);

            var soundResource = isTeleportationOut
                                    ? SoundResourceTeleportOut
                                    : SoundResourceTeleportIn;
            if (isCurrentCharacter)
            {
                Client.Audio.PlayOneShot(soundResource);
            }
            else
            {
                Client.Audio.PlayOneShot(soundResource, worldObject);
            }

            if (isCurrentCharacter
                && isTeleportationOut)
            {
                worldObject.ClientSceneObject
                           .AddComponent<ClientComponentTeleportationEffectCurrentCharacter>()
                           .Setup(animationDuration,
                                  teleportationDelay);
            }
        }

        public override void Update(double deltaTime)
        {
            this.time += deltaTime;
            this.time = Math.Min(this.time, this.duration);

            if (this.worldObject?.IsInitialized ?? false)
            {
                this.SceneObject.Position = this.worldObject.Position;
            }
            else
            {
                this.SceneObject.Position = this.fallbackTilePosition.ToVector2D() + (0.5, 0.5);
            }

            var teleportationProgress = this.time / this.duration;
            if (this.isTeleportationOut)
            {
                teleportationProgress = 1 - teleportationProgress;
            }

            using var tempComponents = Api.Shared.GetTempList<IComponentRendererWithEffects>();
            if (GetSkeleton(this.worldObject) is { } skeleton)
            {
                tempComponents.Add(skeleton);
            }

            tempComponents.AddRange(GetSpriteRenderers(this.worldObject));

            if (!tempComponents.AsList()
                               .SequenceEqual(this.lastComponents))
            {
                this.Cleanup();
                this.lastComponents.AddRange(tempComponents.AsList());

                foreach (var component in this.lastComponents)
                {
                    this.currentRenderingEffects.Add(
                        component.AddEffect<RenderingEffectTeleportation>());
                }
            }

            foreach (var effect in this.currentRenderingEffects)
            {
                effect.Progress = teleportationProgress;
            }
        }

        protected override void OnDisable()
        {
            this.Cleanup();
        }

        private static IComponentSkeleton GetSkeleton(IDynamicWorldObject worldObject)
        {
            if (!worldObject.IsInitialized)
            {
                return null;
            }

            return worldObject.ProtoGameObject switch
            {
                IProtoCharacter => worldObject.GetClientState<BaseCharacterClientState>().SkeletonRenderer,
                IProtoVehicle   => worldObject.GetClientState<VehicleClientState>().SkeletonRenderer,
                _               => null
            };
        }

        private static IEnumerable<IComponentSpriteRenderer> GetSpriteRenderers(IDynamicWorldObject worldObject)
        {
            if (!worldObject.IsInitialized)
            {
                yield break;
            }

            switch (worldObject.ProtoGameObject)
            {
                case IProtoCharacter:
                {
                    var clientState = worldObject.GetClientState<BaseCharacterClientState>();
                    if (clientState.RendererShadow is { } rendererShadow)
                    {
                        yield return rendererShadow;
                    }

                    break;
                }

                case IProtoVehicle:
                {
                    var clientState = worldObject.GetClientState<VehicleClientState>();
                    if (clientState.SpriteRenderer is { } spriteRenderer)
                    {
                        yield return spriteRenderer; // perhaps a hoverboard
                    }

                    if (clientState.RendererShadow is { } rendererShadow)
                    {
                        yield return rendererShadow;
                    }

                    break;
                }
            }
        }

        private void Cleanup()
        {
            foreach (var component in this.lastComponents)
            {
                foreach (var effect in this.currentRenderingEffects)
                {
                    component.RemoveEffect(effect);
                }
            }

            this.lastComponents.Clear();
            this.currentRenderingEffects.Clear();
        }

        private void Setup(
            IDynamicWorldObject worldObject,
            Vector2Ushort fallbackTilePosition,
            double duration,
            bool isTeleportationOut)
        {
            this.worldObject = worldObject;
            this.fallbackTilePosition = fallbackTilePosition;
            this.time = 0;
            this.duration = duration;
            this.isTeleportationOut = isTeleportationOut;
            this.Update(0);
        }
    }
}