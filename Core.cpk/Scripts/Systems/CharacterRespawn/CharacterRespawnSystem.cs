﻿namespace AtomicTorch.CBND.CoreMod.Systems.CharacterRespawn
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects.Debuffs;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Beds;
    using AtomicTorch.CBND.CoreMod.Systems.CharacterCreation;
    using AtomicTorch.CBND.CoreMod.Systems.CharacterDamageTrackingSystem;
    using AtomicTorch.CBND.CoreMod.Systems.LandClaim;
    using AtomicTorch.CBND.CoreMod.Systems.NewbieProtection;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.CoreMod.Systems.PvE;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.Zones;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.Extensions;
    using AtomicTorch.GameEngine.Common.Helpers;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class CharacterRespawnSystem : ProtoSystem<CharacterRespawnSystem>
    {
        public const string DialogBedRespawnCooldown_Message =
            @"You can respawn in your bed in {0} sec.
              [br]Or you can respawn in the world right now.";

        public const string DialogBedRespawnCooldown_Title = "Bed respawn cooldown";

        public const string DialogCannotRespawnAtBed_Inaccessible =
            "It seems the bed is inaccessible (no free space around it).";

        public const string DialogCannotRespawnAtBed_NoBed = "You don't have a bed anymore.";

        public const string DialogCannotRespawnAtBed_Title = "Cannot respawn at bed";

        public const string DialogCannotRespawnNearBed_Message =
            "It seems there are no good places to respawn you.";

        public const string DialogCannotRespawnNearBed_Title = "Cannot respawn near bed";

        private static readonly Dictionary<string, double> ServerLastRespawnTime
            = IsServer
                  ? new Dictionary<string, double>()
                  : null;

        public override string Name => "Character respawn system";

        public static void ServerOnCharacterCreated(ICharacter character)
        {
            ServerPlayerSpawnManager.SpawnPlayer(character, isRespawn: false);
            ServerOnSuccessfulRespawn(character);
        }

        public static void ServerRemoveInvalidStatusEffects(ICharacter character)
        {
            foreach (var statusEffect in Api.Shared.WrapInTempList(
                character.ServerEnumerateCurrentStatusEffects()).EnumerateAndDispose())
            {
                var protoStatusEffect = (IProtoStatusEffect)statusEffect.ProtoLogicObject;
                if (protoStatusEffect.IsRemovedOnRespawn)
                {
                    character.ServerRemoveStatusEffect(protoStatusEffect);
                }
            }
        }

        /// <summary>
        /// Used when respawning near bed.
        /// Used to move character when using "unstuck" feature.
        /// </summary>
        public static bool ServerTryPlaceCharacterNearby(
            ICharacter character,
            Vector2D startPosition)
        {
            var attemptsRemains = 300;
            var radiuses = new[]
            {
                (min: 25, max: 50),
                (min: 50, max: 60),
                (min: 60, max: 70)
            };

            var restrictedZone = ZoneSpecialConstructionRestricted.Instance.ServerZoneInstance;
            var physicsSpace = Server.World.GetPhysicsSpace();

            foreach (var radius in radiuses)
            {
                do
                {
                    var offset = RandomHelper.Next(radius.min, radius.max);
                    var angle = RandomHelper.NextDouble() * MathConstants.DoublePI;
                    var spawnPosition = new Vector2D(startPosition.X + offset * Math.Cos(angle),
                                                     startPosition.Y + offset * Math.Sin(angle));

                    using (var objectsNearby = physicsSpace.TestCircle(
                        spawnPosition,
                        radius: 0.5,
                        collisionGroup: CollisionGroups.Default))
                    {
                        if (objectsNearby.Count > 0)
                        {
                            // invalid tile - obstacles
                            continue;
                        }
                    }

                    if (LandClaimSystem.SharedIsLandClaimedByAnyone(spawnPosition.ToVector2Ushort()))
                    {
                        // there is a land claim area - don't respawn there
                        continue;
                    }

                    // ensure that the tile is valid
                    var isValidTile = true;
                    var spawnTile = Server.World.GetTile(spawnPosition.ToVector2Ushort());
                    foreach (var neighborTile in spawnTile.EightNeighborTiles.ConcatOne(spawnTile))
                    {
                        if (!neighborTile.IsValidTile
                            || neighborTile.ProtoTile.Kind != TileKind.Solid
                            || restrictedZone.IsContainsPosition(neighborTile.Position))
                        {
                            isValidTile = false;
                            break;
                        }
                    }

                    if (!isValidTile)
                    {
                        continue;
                    }

                    // valid tile found - respawn here
                    Server.World.SetPosition(character, spawnPosition);
                    return true;
                }
                while (attemptsRemains-- > 0);
            }

            return false;
        }

        public Task<Tuple<bool, int>> ClientGetHasBedAsync()
        {
            return this.CallServer(_ => _.ServerRemote_GetHasBed());
        }

        public void ClientRequestRespawnAtBed()
        {
            this.CallServer(_ => _.ServerRemote_RespawnAtBed());
        }

        public void ClientRequestRespawnInWorld()
        {
            this.CallServer(_ => _.ServerRemote_RespawnInWorld());
        }

        public void ClientRequestRespawnNearBed()
        {
            this.CallServer(_ => _.ServerRemote_RespawnNearBed());
        }

        private static int ServerCalculateBedRespawnCooldownSecondsRemaining(
            ICharacter character,
            IStaticWorldObject bedObject)
        {
            if (PveSystem.ServerIsPvE)
            {
                // can respawn instantly on a PvE server
                return 0;
            }

            if (!ServerLastRespawnTime.TryGetValue(character.Name, out var lastRespawnTime))
            {
                return 0;
            }

            var cooldownDuration = ((IProtoObjectBed)bedObject.ProtoStaticWorldObject)
                .RespawnCooldownDurationSeconds;

            if (NewbieProtectionSystem.SharedIsNewbie(character))
            {
                // respawn cooldown is much shorter for newbies
                cooldownDuration /= 24.0;
            }

            var cooldownRemainsSeconds = lastRespawnTime + cooldownDuration - Server.Game.FrameTime;
            return Math.Max(0, (int)Math.Round(cooldownRemainsSeconds));
        }

        private static bool ServerCanRespawn(PlayerCharacterCurrentStats stats, ICharacter character)
        {
            if (stats.HealthCurrent <= 0
                || PlayerCharacter.GetPublicState(character).IsDead)
            {
                if (!CharacterCreationSystem.SharedIsCharacterCreated(character))
                {
                    Logger.Warning(
                        $"Cannot respawn {character} - character is not created");
                    return false;
                }

                return true;
            }

            Logger.Warning(
                $"Cannot respawn {character} - character is not dead or not valid: HP={stats.HealthCurrent:F1}");
            return false;
        }

        // also checks that the bed is inside the player land claim (or the land is free)
        private static bool ServerCheckIsHasBed(ICharacter character, out IStaticWorldObject bed)
        {
            bed = PlayerCharacter.GetPrivateState(character).CurrentBedObject;
            return bed is not null
                   && !bed.IsDestroyed
                   && LandClaimSystem.SharedIsObjectInsideOwnedOrFreeArea(bed,
                                                                          character,
                                                                          requireFactionPermission: false);
        }

        private static void ServerOnSuccessfulRespawn(ICharacter character)
        {
            var publicState = PlayerCharacter.GetPublicState(character);
            var privateState = PlayerCharacter.GetPrivateState(character);
            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;

            if (privateState.IsDespawned)
            {
                // character was despawned (PvE)
                privateState.IsDespawned = false;
                if (stats.HealthCurrent > 0)
                {
                    publicState.IsDead = false;
                }
                else
                {
                    ResetStatsOnRespawn();
                }
            }
            else
            {
                // if character was actually dead and not simply despawned
                ResetStatsOnRespawn();

                if (PveSystem.ServerIsPvE
                    || !NewbieProtectionSystem.SharedIsNewbie(character))
                {
                    // character is weakened after respawn for some time (except newbies in PvP)
                    // more intensity in PvE as otherwise death is not punishing enough on PvE.
                    character.ServerAddStatusEffect<StatusEffectWeakened>(intensity: PveSystem.ServerIsPvE ? 1.0 : 0.5);
                }
            }

            privateState.ServerLastActiveTime = Server.Game.FrameTime;

            // recreate physics (as dead character doesn't have any physics)
            character.ProtoCharacter.SharedCreatePhysics(character);

            Api.Server.Characters.SetViewScopeMode(character, isEnabled: true);

            CharacterDamageTrackingSystem.ServerClearStats(character);

            void ResetStatsOnRespawn()
            {
                stats.ServerSetHealthCurrent(stats.HealthMax / 2f, overrideDeath: true);
                stats.SharedSetStaminaCurrent(stats.StaminaMax / 2f);
                stats.ServerSetFoodCurrent(stats.FoodMax / 4f);
                stats.ServerSetWaterCurrent(stats.WaterMax / 4f);
                ServerRemoveInvalidStatusEffects(character);
            }
        }

        private static bool ServerTryRespawnAtBed(ICharacter character, bool respawnOnlyNearby)
        {
            if (!ServerCheckIsHasBed(character, out var bedObject))
            {
                Instance.CallClient(character,
                                    _ => _.ClientRemote_CannotRespawnDontHaveBed());
                return false;
            }

            var cooldownRemainsSeconds = ServerCalculateBedRespawnCooldownSecondsRemaining(
                character,
                bedObject);

            if (cooldownRemainsSeconds >= 1)
            {
                Instance.CallClient(character,
                                    _ => _.ClientRemote_CannotRespawnInBedCooldown(cooldownRemainsSeconds));
                return false;
            }

            var bedPosition = bedObject.TilePosition.ToVector2D()
                              + bedObject.ProtoStaticWorldObject.Layout.Center;
            return respawnOnlyNearby
                       ? RespawnNearbyBed()
                       : RespawnAtBed();

            bool RespawnAtBed()
            {
                var neighborTiles = bedObject.OccupiedTiles
                                             .SelectMany(t => t.EightNeighborTiles)
                                             .Concat(bedObject.OccupiedTiles)
                                             .Distinct()
                                             .ToList();
                neighborTiles.Shuffle();

                var bedTileHeight = bedObject.OccupiedTile.Height;

                neighborTiles.SortBy(t => t.Position.ToVector2D()
                                           .DistanceSquaredTo(bedPosition));
                var physicsSpace = Server.World.GetPhysicsSpace();

                foreach (var neighborTile in neighborTiles)
                {
                    if (neighborTile.Height != bedTileHeight)
                    {
                        continue;
                    }

                    var spawnPosition = neighborTile.Position.ToVector2D() + (0.5, 0.5);
                    using (var objectsNearby = physicsSpace.TestCircle(
                        spawnPosition,
                        radius: 0.5,
                        collisionGroup: CollisionGroups.Default))
                    {
                        if (objectsNearby.Count > 0)
                        {
                            // invalid tile - obstacles
                            continue;
                        }
                    }

                    if (!LandClaimSystem.SharedIsPositionInsideOwnedOrFreeArea(neighborTile.Position,
                                                                               character,
                                                                               requireFactionPermission: false))
                    {
                        // invalid tile - it's claimed by another player
                        continue;
                    }

                    // valid tile found - respawn here
                    Server.World.SetPosition(character, spawnPosition);
                    Logger.Important($"{character} respawned at bed {bedObject}");
                    ServerLastRespawnTime[character.Name] = Server.Game.FrameTime;
                    return true;
                }

                Logger.Warning($"{character} cannot be spawned at bed {bedObject}");
                Instance.CallClient(character,
                                    _ => _.ClientRemote_CannotRespawnInBedNoPlace());
                return false;
            }

            bool RespawnNearbyBed()
            {
                var respawnCenterPosition = bedPosition + (0.5, 0.5);

                if (ServerTryPlaceCharacterNearby(character, respawnCenterPosition))
                {
                    ServerLastRespawnTime[character.Name] = Server.Game.FrameTime;
                    Logger.Important($"{character} respawned near bed {bedObject}");
                    return true;
                }

                Logger.Warning($"{character} cannot be spawned near bed {bedObject}");
                Instance.CallClient(character,
                                    _ => _.ClientRemote_CannotRespawnNearBedNoPlace());
                return false;
            }
        }

        [RemoteCallSettings(DeliveryMode.ReliableSequenced)]
        private void ClientRemote_CannotRespawnDontHaveBed()
        {
            DialogWindow.ShowMessage(
                DialogCannotRespawnAtBed_Title,
                DialogCannotRespawnAtBed_NoBed,
                closeByEscapeKey: true);
        }

        [RemoteCallSettings(DeliveryMode.ReliableSequenced)]
        private void ClientRemote_CannotRespawnInBedCooldown(int cooldownRemainsSeconds)
        {
            DialogWindow.ShowMessage(
                DialogBedRespawnCooldown_Title,
                string.Format(DialogBedRespawnCooldown_Message,
                              cooldownRemainsSeconds),
                closeByEscapeKey: true);
        }

        [RemoteCallSettings(DeliveryMode.ReliableSequenced)]
        private void ClientRemote_CannotRespawnInBedNoPlace()
        {
            DialogWindow.ShowMessage(
                DialogCannotRespawnAtBed_Title,
                DialogCannotRespawnAtBed_Inaccessible,
                closeByEscapeKey: true);
        }

        [RemoteCallSettings(DeliveryMode.ReliableSequenced)]
        private void ClientRemote_CannotRespawnNearBedNoPlace()
        {
            DialogWindow.ShowMessage(
                DialogCannotRespawnNearBed_Title,
                DialogCannotRespawnNearBed_Message,
                closeByEscapeKey: true);
        }

        [RemoteCallSettings(timeInterval: 5)]
        private Tuple<bool, int> ServerRemote_GetHasBed()
        {
            var character = ServerRemoteContext.Character;
            var hasBed = ServerCheckIsHasBed(character, out var bedObject);
            if (!hasBed)
            {
                return new Tuple<bool, int>(false, 0);
            }

            var cooldownSecondsRemains = ServerCalculateBedRespawnCooldownSecondsRemaining(character, bedObject);
            return new Tuple<bool, int>(true, cooldownSecondsRemains);
        }

        private void ServerRemote_RespawnAtBed()
        {
            var character = ServerRemoteContext.Character;
            Logger.Important($"{character} requested respawn at bed");
            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;
            if (!ServerCanRespawn(stats, character))
            {
                return;
            }

            if (ServerTryRespawnAtBed(character, respawnOnlyNearby: false))
            {
                ServerOnSuccessfulRespawn(character);
            }
        }

        private void ServerRemote_RespawnInWorld()
        {
            var character = ServerRemoteContext.Character;
            Logger.Important($"{character} requested respawn in world");
            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;
            if (!ServerCanRespawn(stats, character))
            {
                return;
            }

            // spawn via spawn manager - will spawn like a new character
            ServerPlayerSpawnManager.SpawnPlayer(character, isRespawn: true);
            Logger.Important($"{character} respawned successfully in world at {character.TilePosition}");
            ServerOnSuccessfulRespawn(character);
        }

        private void ServerRemote_RespawnNearBed()
        {
            var character = ServerRemoteContext.Character;
            Logger.Important($"{character} requested respawn near bed");
            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;
            if (!ServerCanRespawn(stats, character))
            {
                return;
            }

            if (ServerTryRespawnAtBed(character, respawnOnlyNearby: true))
            {
                ServerOnSuccessfulRespawn(character);
            }
        }
    }
}