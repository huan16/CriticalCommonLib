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

namespace CriticalCommonLib.MarketBoard;

/// <summary>
/// 负责与Universalis API交互的后台服务，管理市场板价格查询队列和异步请求处理。
/// </summary>
public class HostedUniversalis : BackgroundService, IUniversalis
{
    private readonly ExcelSheet<World> _worldSheet;
    private readonly IFramework _framework;
    private readonly IHostedUniversalisConfiguration _hostedUniversalisConfiguration;
    public ILogger<HostedUniversalis> Logger { get; }
    public HttpClient HttpClient { get; }
    public IBackgroundTaskQueue UniversalisQueue { get; }
    private Dictionary<uint, string> _worldNames = new();
    public uint QueueTime { get; } = 5;
    public uint MaxRetries { get; } = 3;
    public DateTime? LastFailure { get; private set; }
    public bool TooManyRequests { get; private set; }

    public int QueuedCount => _queuedCount;


    public HostedUniversalis(
        ILogger<HostedUniversalis> logger, 
        HttpClient httpClient, 
        MarketboardTaskQueue marketboardTaskQueue, 
        ExcelSheet<World> worldSheet, 
        IFramework framework, 
        IHostedUniversalisConfiguration hostedUniversalisConfiguration)
    {
        _worldSheet = worldSheet;
        _framework = framework;
        _hostedUniversalisConfiguration = hostedUniversalisConfiguration;
        Logger = logger;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundProcessing(stoppingToken);
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
                Logger.LogError(ex,
                    "Error occurred executing {WorkItem}.", nameof(workItem));
            }
        }
    }

    public event Universalis.ItemPriceRetrievedDelegate? ItemPriceRetrieved;
    public void SetSaleHistoryLimit(int limit)
    {
    }

    public void Initialise()
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

    /// <summary>
    /// 执行实际的Universalis API请求，处理价格数据并触发回调。
    /// </summary>
    /// <param name="itemIds">需要查询的物品ID列表</param>
    /// <param name="worldId">目标服务器ID</param>
    /// <param name="token">取消标记</param>
    /// <param name="attempt">当前重试次数</param>
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
            Logger.LogError($"Maximum retries for universalis has been reached, cancelling.");
            return;
        }

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
        Logger.LogTrace("Sending request for items {ItemIds} to universalis API.", itemIdsString);
        // 构造API请求URL
        var apiRequestBuilder = new UniversalisApiBuilder(worldName)
            .AddItemIds(itemIds)
            .WithListings(30)
            .WithEntries(30)
            .WithStatsWithin(4 * 604800000L) // 7天 = 604800000ms
            .WithEntriesWithin(4 * 604800000L)
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
                Logger.LogWarning("Too many requests to universalis, waiting a minute.");
                TooManyRequests = true;
                await Task.Delay(TimeSpan.FromMinutes(1));
                await RetrieveMarketBoardPrices(itemIdList, worldId, token, attempt + 1);
                return;
            }

            TooManyRequests = false;

            var value = await response.Content.ReadAsStringAsync(token);

            if (value == "error code: 504")
            {
                Logger.LogWarning("Gateway timeout to universalis, waiting 30 seconds.");
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
                    var listing = new MarketPricing(apiResponse);
                    await _framework.RunOnFrameworkThread(() =>
                        ItemPriceRetrieved?.Invoke(apiResponse.itemID, worldId, listing));
                }
            }
            else
            {
                Logger.LogError("Failed to parse universalis json data, backing off 30 seconds.");
                LastFailure = DateTime.Now;
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }
        catch (TaskCanceledException)
        {

        }
        catch (JsonReaderException readerException)
        {
            // 记录解析错误并重试
            Logger.LogError(readerException, "Failed to parse universalis data, backing off 30 seconds");
            LastFailure = DateTime.Now;
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
        finally
        {
            // 确保计数器正确更新
            _queuedCount -= itemIdList.Count;
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

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _framework.Update -= FrameworkOnUpdate;
        }
    }

    public sealed override void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}