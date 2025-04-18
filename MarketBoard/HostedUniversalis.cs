using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CriticalCommonLib.Interfaces;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static CriticalCommonLib.MarketBoard.IUniversalis;

namespace CriticalCommonLib.MarketBoard;

/// <summary>
/// 负责与Universalis API交互的后台服务，管理市场板价格查询队列和异步请求处理。
/// </summary>
public class HostedUniversalis : IUniversalis
{
    private readonly ExcelSheet<World> _worldSheet;
    private readonly IFramework _framework;
    private readonly UniversalisConfiguration _universalisConfiguration;
    public IPluginLog pluginLog { get; }
    public HttpClient HttpClient { get; }
    public IBackgroundTaskQueue UniversalisQueue { get; }
    private Dictionary<uint, string> _worldNames = new();
    public uint QueueTime { get; } = 5;
    public uint MaxRetries { get; } = 3;
    public DateTime? LastFailure { get; private set; }
    public bool TooManyRequests { get; private set; }

    public int QueuedCount => _queuedCount;


    public HostedUniversalis(
        IPluginLog pluginLog, 
        HttpClient httpClient, 
        IBackgroundTaskQueue marketboardTaskQueue, 
        ExcelSheet<World> worldSheet, 
        IFramework framework, 
        UniversalisConfiguration universalisConfiguration)
    {
        _worldSheet = worldSheet;
        _framework = framework;
        _universalisConfiguration = universalisConfiguration;
        this.pluginLog = pluginLog;
        HttpClient = httpClient;
        UniversalisQueue = marketboardTaskQueue;

        _framework.Update += FrameworkOnUpdate;
    }

    private void FrameworkOnUpdate(IFramework framework)
    {
        foreach (var world in _worldItemQueue)
        {
            if (world.Value.Item1 < DateTime.Now)
            {
                _worldItemQueue.Remove(world.Key, out var fullList);
                _queuedCount += fullList.Item2.Count;
                UniversalisQueue.QueueBackgroundWorkItemAsync(token => RetrieveMarketBoardPrices(fullList.Item2, world.Key,token));
                break;
            }
        }
    }

    private Task? _executingTask;
    private readonly CancellationTokenSource _stoppingCts = new();

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _framework.Update += FrameworkOnUpdate;
        _executingTask = ExecuteAsync(_stoppingCts.Token);
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundProcessing(stoppingToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }

        try
        {
            _framework.Update -= FrameworkOnUpdate;
            _stoppingCts.Cancel();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem =
                await UniversalisQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex,
                    "Error occurred executing {WorkItem}.", nameof(workItem));
            }
        }
    }


    public event UniversalisResponseReceivedDelegate? UniversalisResponseReceived;

    public void SetSaleHistoryLimit(int limit)
    {
    }

    // 使用专用并发容器替代原始锁机制
    private ConcurrentDictionary<uint, (DateTime ExpiryTime, HashSet<uint> Items)> _worldItemQueue = new();
    private ConcurrentDictionary<uint, SemaphoreSlim> _worldLocks = new();

    public void QueuePriceCheck(uint itemId, uint worldId)
    {
        var queueEntry = _worldItemQueue.GetOrAdd(worldId, 
            _ => (DateTime.Now.AddSeconds(QueueTime), new HashSet<uint>()));

        var worldLock = _worldLocks.GetOrAdd(worldId, _ => new SemaphoreSlim(1, 1));
        
        worldLock.Wait();
        try {
            queueEntry.Items.Add(itemId);
            if (queueEntry.Items.Count == 50) {
                ProcessFullBatch(worldId);
            }
        }
        finally {
            worldLock.Release();
        }
    }

    private void ProcessFullBatch(uint worldId)
    {
        if (_worldItemQueue.TryRemove(worldId, out var fullList)) 
        {
            Interlocked.Add(ref _queuedCount, fullList.Items.Count);
            UniversalisQueue.QueueBackgroundWorkItemAsync(token => 
                RetrieveMarketBoardPrices(fullList.Items, worldId, token));
        }
    }

    private int _queuedCount;

    private readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(50, 50);
    private readonly TimeSpan _rateLimitInterval = TimeSpan.FromSeconds(1);

    private async Task RetrieveMarketBoardPrices(IEnumerable<uint> itemIds, uint worldId, CancellationToken token, uint attempt = 0)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }
        var itemIdList = itemIds.ToList();
        if (attempt == MaxRetries)
        {
            _queuedCount -= itemIdList.Count;
            pluginLog.Error($"Maximum retries for universalis has been reached, cancelling.");
            return;
        }

        // 使用速率限制器
        await _rateLimiter.WaitAsync(token);
        try
        {
            string worldName;
            if (!_worldNames.ContainsKey(worldId))
            {
                var world = GetWorldName(worldId);
                if (world == null)
                {
                    _queuedCount -= itemIdList.Count;
                    return;
                }
                _worldNames[worldId] = world;
            }
            worldName = _worldNames[worldId];

            var itemIdsString = String.Join(",", itemIdList.Select(c => c.ToString()).ToArray());
            pluginLog.Verbose("Sending request for items {ItemIds} to universalis API.", itemIdsString);

            // 构造API请求URL
            var apiRequestBuilder = new UniversalisApiBuilder(worldName)
                .AddItemIds(itemIds)
                .WithListings(_universalisConfiguration.ListingsCount)
                .WithEntries(_universalisConfiguration.EntrisCount)
                .WithStatsWithin(_universalisConfiguration.StatsWithin) // 7天 = 604800000ms
                .WithEntriesWithin(_universalisConfiguration.EntrisWithin)
                .WithUserAgent("MyCustomClient/1.0");
            var (requestUrl, _) = apiRequestBuilder.Build();

            try
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var response = await HttpClient.GetAsync(requestUrl, token);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    pluginLog.Warning("Too many requests to universalis, waiting a minute.");
                    TooManyRequests = true;
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    await RetrieveMarketBoardPrices(itemIdList, worldId, token, attempt + 1);
                    return;
                }

                TooManyRequests = false;

                var value = await response.Content.ReadAsStringAsync(token);

                if (value == "error code: 504")
                {
                    pluginLog.Warning("Gateway timeout to universalis, waiting 30 seconds.");
                    LastFailure = DateTime.Now;
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    await RetrieveMarketBoardPrices(itemIdList, worldId, token, attempt + 1);
                    return;
                }

                // 解析响应数据
                var itemsDict = ParseResponse(value, itemIdList.Count == 1);
                if (itemsDict != null)
                {
                    foreach (var apiResponse in itemsDict.Values)
                    {
                        await _framework.RunOnFrameworkThread(() =>
                            UniversalisResponseReceived?.Invoke(apiResponse));
                    }
                }
                else
                {
                    pluginLog.Error("Failed to parse universalis json data, backing off 30 seconds.");
                    LastFailure = DateTime.Now;
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
            catch (TaskCanceledException)
            {
                // 忽略取消异常
            }
            catch (JsonReaderException readerException)
            {
                // 记录解析错误并重试
                pluginLog.Error(readerException, "Failed to parse universalis data, backing off 30 seconds");
                LastFailure = DateTime.Now;
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            finally
            {
                // 确保计数器正确更新
                _queuedCount -= itemIdList.Count;
            }
        }
        finally
        {
            // 释放速率限制器
            _rateLimiter.Release();
            // 等待速率限制间隔
            await Task.Delay(_rateLimitInterval, token);
        }
    }

    private static Dictionary<string, UniversalisApiResponse> ParseResponse(string value, bool isSingleItem)
    {
        if (isSingleItem)
        {
            var apiListing = JsonConvert.DeserializeObject<UniversalisApiResponse>(value);
            return apiListing != null 
                ? new Dictionary<string, UniversalisApiResponse> { { apiListing.itemID.ToString(), apiListing } }
                : new Dictionary<string, UniversalisApiResponse>();
        }
        else
        {
            var multiRequest = JsonConvert.DeserializeObject<MultiRequest>(value);
            return multiRequest?.items ?? new Dictionary<string, UniversalisApiResponse>();
        }
    }

    private readonly ConcurrentDictionary<uint, string> _worldNameCache = new();

    private string GetWorldName(uint worldId)
    {
        return _worldNameCache.GetOrAdd(worldId, id => {
            var world = _worldSheet.GetRowOrDefault(id);
            return world?.Name.ExtractText() ?? string.Empty;
        });
    }
}