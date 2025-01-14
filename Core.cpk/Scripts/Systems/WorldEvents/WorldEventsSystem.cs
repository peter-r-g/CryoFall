﻿namespace AtomicTorch.CBND.CoreMod.Systems.WorldEvents
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Systems.Chat;
    using AtomicTorch.CBND.GameApi;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Logic;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.CBND.GameApi.ServicesServer;

    public class WorldEventsSystem : ProtoSystem<WorldEventsSystem>
    {
        private static readonly List<ILogicObject> ServerListActiveEvents
            = Api.IsServer ? new List<ILogicObject>() : null;

        private static readonly IWorldServerService ServerWorld
            = Api.IsServer ? Server.World : null;

        [NotLocalizable]
        public override string Name => "World events system";

        public static void ServerAddActiveEventToPlayerScope(ICharacter character, ILogicObject activeEvent)
        {
            if (character.ServerIsOnline)
            {
                ServerWorld.ForceEnterScope(character, activeEvent);
            }
        }

        public static void ServerRegisterEvent(ILogicObject activeEvent)
        {
            ServerListActiveEvents.Add(activeEvent);

            var onlinePlayers = Server.Characters.EnumerateAllPlayerCharacters(onlyOnline: true);
            foreach (var character in onlinePlayers)
            {
                ServerAddActiveEventToPlayerScope(character, activeEvent);
            }
        }

        public static void ServerUnregisterEvent(ILogicObject activeEvent)
        {
            ServerListActiveEvents.Remove(activeEvent);
            // the event will be automatically removed from players' scope as it's destroyed
        }

        [RemoteCallSettings(timeInterval: RemoteCallSettingsAttribute.MaxTimeInterval)]
        private void ServerRemote_RequestActiveEvents()
        {
            var character = ServerRemoteContext.Character;

            foreach (var activeEvent in ServerListActiveEvents)
            {
                ServerAddActiveEventToPlayerScope(character, activeEvent);
            }
        }

        private class Bootstrapper : BaseBootstrapper
        {
            public override void ClientInitialize()
            {
                ClientChatBlockList.Initialize();
                Client.Characters.CurrentPlayerCharacterChanged += Refresh;

                Refresh();

                static void Refresh()
                {
                    if (Api.Client.Characters.CurrentPlayerCharacter is not null)
                    {
                        Instance.CallServer(_ => _.ServerRemote_RequestActiveEvents());
                    }
                }
            }
        }
    }
}