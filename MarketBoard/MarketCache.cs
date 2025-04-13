using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AllaganLib.GameSheets.Sheets;
using CriticalCommonLib.Interfaces;
using CriticalCommonLib.Services.Mediator;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using Microsoft.Extensions.Hosting;

namespace CriticalCommonLib.MarketBoard
{
    using CriticalCommonLib.SQLite;
    using Dalamud.Plugin;

    public class MarketCacheConfiguration
    {
        public bool AutoRequest { get; set; }
        public int CacheMaxAgeHours { get; set; } = 24;
    }

    public class MarketCache : BackgroundService, IMarketCache
    {
        private readonly IUniversalis _universalis;
        private readonly MediatorService? _mediator;
        private readonly MarketCacheConfiguration _marketCacheConfiguration;
        private readonly ExcelSheet<World> _worldSheet;
        private readonly IPluginLog _pluginLog;
        private readonly GameData _gameData;
        private readonly ExcelSheet<Item> _itemSheet;
        private readonly ConcurrentDictionary<(uint,uint), byte> _requestedItems = new();
        private ConcurrentDictionary<(uint, uint), MarketPricing> _marketBoardCache = new();
        private readonly Stopwatch _automaticSaveTimer = new();
        private readonly IBackgroundTaskQueue _saveQueue;
        private readonly MarketPricingDatabase _marketPricingDatabase;

        public int AutomaticSaveTime { get; set; } = 120;

        public MarketCache(
            IUniversalis universalis, 
            MediatorService? mediator, 
            IDalamudPluginInterface pluginInterfaceService, 
            MarketCacheConfiguration marketCacheConfiguration, 
            IBackgroundTaskQueue saveQueue, 
            ExcelSheet<World> worldSheet, 
            IPluginLog pluginLog, 
            GameData gameData, 
            ExcelSheet<Item> itemSheet,
            MarketPricingDatabase marketPricingDatabase)
        {
            _saveQueue = saveQueue;
            _universalis = universalis;
            _mediator = mediator;
            _marketCacheConfiguration = marketCacheConfiguration;
            _worldSheet = worldSheet;
            _pluginLog = pluginLog;
            _gameData = gameData;
            _itemSheet = itemSheet;
            _marketPricingDatabase = marketPricingDatabase;

            LoadExistingCache();
            _universalis.ItemPriceRetrieved += UniversalisOnItemPriceRetrieved;
        }

        private bool _disposed;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem =
                    await _saveQueue.DequeueAsync(stoppingToken);

                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    _pluginLog.Error(ex,
                        "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            base.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!_disposed && disposing)
            {
                _universalis.ItemPriceRetrieved -= UniversalisOnItemPriceRetrieved;
            }
            _disposed = true;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _pluginLog.Verbose("Market cache save queue is ending.");
            SaveCacheFile();
            await base.StopAsync(cancellationToken);
        }


        private void UniversalisOnItemPriceRetrieved(uint itemId, uint worldId, MarketPricing marketPricing)
        {
            UpdateEntry(itemId, worldId, marketPricing);
        }

        public void LoadExistingCache()
        {
            try
            {
                var marketPricings = _marketPricingDatabase.GetAllMarketPricings();
                foreach (var pricing in marketPricings)
                {
                    // 提取键：(ItemId, WorldId)
                    var key = (pricing.itemID, pricing.worldID);
                    
                    // 将键值对存入缓存字典
                    _marketBoardCache[key] = pricing;
                }
            }
            catch (Exception e)
            {
                _pluginLog.Error("Error while parsing saved universalis data: " + e.Message);
            }
        }

        public void ClearCache()
        {
            _marketBoardCache = new ConcurrentDictionary<(uint, uint), MarketPricing>();
        }

        private void SaveAsync()
        {
            _saveQueue.QueueBackgroundWorkItemAsync(token => Task.Run(SaveCacheFile, token));
        }

        public void SaveCache(bool forceSave = false)
        {
            if (!forceSave && 
                _automaticSaveTimer.IsRunning && 
                _automaticSaveTimer.Elapsed < TimeSpan.FromSeconds(AutomaticSaveTime))
            {
                return; // 自动保存间隔未到，跳过
            }

            if (!_automaticSaveTimer.IsRunning)
            {
                _automaticSaveTimer.Start();
            }

            _pluginLog.Verbose("Saving MarketCache to database");
            SaveAsync(); // 触发异步保存任务

            _automaticSaveTimer.Restart(); // 重置计时器
        }

        private void SaveCacheFile()
        {
            try
            {
                // 获取所有缓存的 MarketPricing 对象
                var pricingsToSave = _marketBoardCache.Values.ToList();

                // 批量保存到数据库
                _marketPricingDatabase.SaveMarketPricings(pricingsToSave);

                _pluginLog.Verbose("MarketCache saved successfully");
            }
            catch (Exception e)
            {
                _pluginLog.Error($"Failed to save MarketCache to database: {e.Message}");
            }
        }
        
        public MarketPricing? GetPricing(uint itemId, uint worldId, bool forceCheck)
        {
            GetPricing(itemId, worldId, false, forceCheck, out var pricing);
            return pricing;
        }

        public List<MarketPricing> GetPricing(uint itemId, List<uint> worldIds, bool forceCheck)
        {
            var keys = worldIds.Select(c => (itemId, c)).ToList();
            var prices = new List<MarketPricing>();
            foreach (var key in keys)
            {
                if (_marketBoardCache.ContainsKey(key))
                {
                    prices.Add(_marketBoardCache[key]);
                }
            }

            return prices;
        }

        private List<uint>? _worldIds;

        public List<MarketPricing> GetPricing(uint itemId, bool forceCheck)
        {
            if (_worldIds == null)
            {
                _worldIds = _worldSheet.Where(c => c.IsPublic).Select(c => c.RowId).ToList();
            }

            return GetPricing(itemId, _worldIds, forceCheck);
        }

        public ConcurrentDictionary<(uint, uint), MarketPricing> CachedPricing => _marketBoardCache;

        public MarketCachePricingResult GetPricing(uint itemId, uint worldId, bool ignoreCache, bool forceCheck, out MarketPricing? marketPricing)
        {
            //Untradable
            if (_itemSheet.GetRowOrDefault(itemId)?.IsUntradable ?? true)
            {
                marketPricing = new MarketPricing();
                return MarketCachePricingResult.Untradable;
            }

            //Pricing available
            if (!ignoreCache && _marketBoardCache.ContainsKey((itemId,worldId)) && !forceCheck)
            {
                marketPricing = _marketBoardCache[(itemId, worldId)];
                return MarketCachePricingResult.Successful;
            }

            //No pricing available
            if (!_marketCacheConfiguration.AutoRequest && !forceCheck)
            {
                marketPricing = new MarketPricing();
                return MarketCachePricingResult.NoPricing;
            }

            marketPricing = null;

            //Pricing queued
            if (forceCheck || !_requestedItems.ContainsKey((itemId, worldId)))
            {
                _requestedItems.TryAdd((itemId, worldId), default);
                _universalis.QueuePriceCheck(itemId, worldId);
                return MarketCachePricingResult.Queued;
            }

            if (_requestedItems.ContainsKey((itemId, worldId)))
            {
                return MarketCachePricingResult.AlreadyQueued;
            }

            return MarketCachePricingResult.Disabled;
        }

        public bool RequestCheck(uint itemId, uint worldId, bool forceCheck)
        {
            //Allow the check if a force check is requested, or if we haven't requested in the item since we last retrieved it
            if(!_requestedItems.ContainsKey((itemId, worldId)) || forceCheck)
            {
                if (!_marketBoardCache.ContainsKey((itemId, worldId)) || _marketBoardCache[(itemId, worldId)].listings == null || forceCheck)
                {
                    _requestedItems.TryAdd((itemId, worldId), default);
                    _universalis.QueuePriceCheck(itemId, worldId);
                }
                return true;

            }
            return false;
        }

        public void RequestCheck(List<uint> itemIds, List<uint> worldIds, bool forceCheck)
        {
            foreach (var itemId in itemIds)
            {
                foreach (var worldId in worldIds)
                {
                    RequestCheck(itemId, worldId, forceCheck);
                }
            }
        }

        public void RequestCheck(List<uint> itemIds, uint worldId, bool forceCheck)
        {
            foreach (var itemId in itemIds)
            {
                RequestCheck(itemId, worldId, forceCheck);
            }
        }

        public void RequestCheck(uint itemId, List<uint> worldIds, bool forceCheck)
        {
            foreach (var worldId in worldIds)
            {
                RequestCheck(itemId, worldId, forceCheck);
            }
        }

        internal void UpdateEntry(uint itemId, uint worldId, MarketPricing pricingResponse)
        {
            _marketBoardCache[(itemId, worldId)] = pricingResponse;
            _requestedItems.TryRemove((itemId, worldId), out _);
            SaveCache();
            _mediator?.Publish(new MarketCacheUpdatedMessage(itemId, worldId));
        }
    }
}
