using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;

namespace CriticalCommonLib.MarketBoard
{
    public interface IMarketPricingReader : IHostedService
    {
        void LoadDatabase();

        MarketPricing? GetPricing(uint itemId, uint worldId);
        MarketPricingGetResult GetPricing(
            uint itemId, uint worldId, out MarketPricing? marketPricing);
        List<MarketPricing> GetPricing(uint itemId, List<uint> worldIds);
        List<MarketPricing> GetPricing(uint itemId);

        // 市场价格字典，键值为(物品Id, 世界Id)
        ConcurrentDictionary<(uint, uint), MarketPricing> marketPricingDict { get; }
    }
}