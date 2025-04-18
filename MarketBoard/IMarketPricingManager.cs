using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;

namespace CriticalCommonLib.MarketBoard
{
    public interface IMarketPricingManager : IHostedService
    {
        void LoadDatabase();
        void ClearDatabase();
        void SaveDatabase(bool forceSave = false);

        bool GetRecommendedPrice(uint itemId, uint worldId, bool? isHq, out uint? recommendedUnitPrice);
        bool GetMBRecommendedPrice(uint itemId, uint worldId, bool? isHq, out uint? recommendedUnitPrice);
        DateTime GetLastUpdateTime(uint itemId, uint worldId);
        bool IsNeedUpdate(uint itemId, uint worldId);
        bool IsMarketBoardPriceNeedUpdate(uint itemId, uint worldID);
        bool IsMarketBoardPriceRecentlyUpdate(uint itemId, uint worldID);
        bool IsUniversalisPriceNeedUpdate(uint itemId, uint worldID);

        MarketPricing? GetPricing(uint itemId, uint worldId, bool forceCheck);
        MarketPricingGetResult GetPricing(
            uint itemId, uint worldId, bool ignoreCache, bool forceCheck, out MarketPricing? marketPricing);
        List<MarketPricing> GetPricing(uint itemId, List<uint> worldIds, bool forceCheck);
        List<MarketPricing> GetPricing(uint itemId, bool forceCheck);

        // 市场价格字典，键值为(物品Id, 世界Id)
        ConcurrentDictionary<(uint, uint), MarketPricing> marketPricingDict { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="worldId"></param>
        /// <returns>Was the request successful</returns>
        bool RequestCheck(uint itemId, uint worldId, bool forceCheck);
        void RequestCheck(List<uint> itemIds, List<uint> worldIds, bool forceCheck);
        void RequestCheck(List<uint> itemIds, uint worldId, bool forceCheck);
        void RequestCheck(uint itemId, List<uint> worldIds, bool forceCheck);
    }

    public enum MarketPricingGetResult
    {
        Untradable,
        Queued,
        AlreadyQueued,
        Successful,
        NoPricing,
        Disabled
    }
}