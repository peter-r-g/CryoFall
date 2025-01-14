﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Loot
{
    using AtomicTorch.CBND.CoreMod.Systems.Droplists;
    using AtomicTorch.CBND.GameApi.Data.World;

    public interface IProtoObjectLoot : IProtoStaticWorldObject
    {
        IReadOnlyDropItemsList LootDroplist { get; }

        bool IsAvailableInCompletionist { get; }
    }
}