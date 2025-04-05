using System;
using System.Collections.Generic;
using System.Linq;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using InventoryItem = FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;

namespace CriticalCommonLib.Services.SubInventoryScanner;

/// <summary>
/// 雇员物品栏扫描器，用于扫描和管理游戏中雇员的物品栏内容
/// </summary>
public class RetainerInventoryScanner
{
    // 角色监视器，用于获取当前活动的雇员ID
    private readonly ICharacterMonitor _characterMonitor;
    // 出售商品顺序服务，用于处理雇员出售商品的顺序
    private readonly IMarketOrderService _marketOrderService;
    // 已加载的物品栏类型集合
    private readonly HashSet<InventoryType> _loadedInventories = new();
    // 插件日志服务
    private readonly IPluginLog _pluginLog;
    
    // 缓存雇员市场物品价格的字典，键为雇员ID，值为价格数组
    private readonly Dictionary<ulong,uint[]> _cachedRetainerMarketPrices = new Dictionary<ulong, uint[]>();

    // 内存中的雇员及其已加载的物品栏类型
    public Dictionary<ulong, HashSet<InventoryType>> InMemoryRetainers { get; } = new();
    // 雇员的各种物品栏容器，键为雇员ID，值为物品数组
    public Dictionary<ulong, InventoryItem[]> RetainerBag1 { get; } = new(); // 雇员物品栏1
    public Dictionary<ulong, InventoryItem[]> RetainerBag2 { get; } = new(); // 雇员物品栏2
    public Dictionary<ulong, InventoryItem[]> RetainerBag3 { get; } = new(); // 雇员物品栏3
    public Dictionary<ulong, InventoryItem[]> RetainerBag4 { get; } = new(); // 雇员物品栏4
    public Dictionary<ulong, InventoryItem[]> RetainerBag5 { get; } = new(); // 雇员物品栏5
    public Dictionary<ulong, InventoryItem[]> RetainerEquipped { get; } = new(); // 雇员装备栏
    public Dictionary<ulong, InventoryItem[]> RetainerMarket { get; } = new(); // 雇员市场出售物品栏
    public Dictionary<ulong, InventoryItem[]> RetainerCrystals { get; } = new(); // 雇员水晶栏
    public Dictionary<ulong, InventoryItem[]> RetainerGil { get; } = new(); // 雇员金币栏
    public Dictionary<ulong, uint[]> RetainerMarketPrices { get; } = new(); // 雇员市场物品价格

    /// <summary>
    /// 构造函数，初始化雇员物品栏扫描器
    /// </summary>
    public RetainerInventoryScanner(
        ICharacterMonitor characterMonitor, 
        IMarketOrderService marketOrderService, 
        IPluginLog pluginLog)
    {
        _characterMonitor = characterMonitor;
        _marketOrderService = marketOrderService;
        _pluginLog = pluginLog;
        
        // 初始化物品栏类型到对应字典的映射
        _inventoryMap = new Dictionary<InventoryType, Dictionary<ulong, InventoryItem[]>>()
        {
            { InventoryType.RetainerPage1, RetainerBag1 },
            { InventoryType.RetainerPage2, RetainerBag2 },
            { InventoryType.RetainerPage3, RetainerBag3 },
            { InventoryType.RetainerPage4, RetainerBag4 },
            { InventoryType.RetainerPage5, RetainerBag5 },
            { InventoryType.RetainerCrystals, RetainerCrystals },
            { InventoryType.RetainerGil, RetainerGil },
            { InventoryType.RetainerMarket, RetainerMarket },
            { InventoryType.RetainerEquippedItems, RetainerEquipped }
        };
    }

    /// <summary>
    /// 获取指定类型的物品栏容器
    /// </summary>
    private unsafe InventoryContainer* GetInventoryContainer(InventoryType type)
    {
        return InventoryManager.Instance()->GetInventoryContainer(type);
    }

    /// <summary>
    /// 为指定雇员初始化所有物品栏容器
    /// </summary>
    private void InitializeContainers(ulong retainerId)
    {
        // 定义各个容器及其大小
        var containers = new Dictionary<Dictionary<ulong, InventoryItem[]>, int>
        {
            { RetainerBag1, 35 },    // 雇员物品栏1，35格
            { RetainerBag2, 35 },    // 雇员物品栏2，35格
            { RetainerBag3, 35 },    // 雇员物品栏3，35格
            { RetainerBag4, 35 },    // 雇员物品栏4，35格
            { RetainerBag5, 35 },    // 雇员物品栏5，35格
            { RetainerEquipped, 14 }, // 雇员装备栏，14格
            { RetainerMarket, 20 },   // 雇员市场出售物品栏，20格
            { RetainerGil, 1 },       // 雇员金币栏，1格
            { RetainerCrystals, 18 }  // 雇员水晶栏，18格
        };
        
        // 为每个容器初始化对应雇员的物品数组
        foreach (var container in containers)
        {
            if (!container.Key.ContainsKey(retainerId))
                container.Key.Add(retainerId, new InventoryItem[container.Value]);
        }
    }

    // 物品栏类型到对应字典的映射
    private readonly Dictionary<InventoryType, Dictionary<ulong, InventoryItem[]>> _inventoryMap;

    /// <summary>
    /// 根据雇员ID和物品栏类型获取物品数组
    /// </summary>
    public InventoryItem[] GetInventoryByType(ulong retainerId, InventoryType type)
    {
        return _inventoryMap.TryGetValue(type, out var bag) && bag.TryGetValue(retainerId, out var items) 
            ? items 
            : Array.Empty<InventoryItem>();
    }

    /// <summary>
    /// 处理物品栏项目并记录变更
    /// </summary>
    private unsafe void ProcessInventoryItems(InventoryContainer* container, InventoryItem[] targetArray, 
        InventoryType inventoryType, BagChangeContainer changeSet)
    {
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->Items[i];
            item.Slot = (short)i; // 设置物品的槽位索引
            if (!item.IsSame(targetArray[i])) // 如果物品发生变化
            {
                targetArray[i] = item; // 更新目标数组中的物品
                changeSet.Add(new BagChange(item, inventoryType)); // 记录变更
            }
        }
    }

    /// <summary>
    /// 解析雇员的所有物品栏，这是核心方法
    /// </summary>
    public unsafe void ParseRetainerBags(InventorySortOrder currentSortOrder, BagChangeContainer changeSet)
    {
        // 获取当前活动的雇员ID
        var currentRetainer = _characterMonitor.ActiveRetainerId;
        // 需要加载的物品栏类型
        var requiredInventories = new[]
        {
            InventoryType.RetainerPage1,
            InventoryType.RetainerPage2,
            InventoryType.RetainerPage3,
            InventoryType.RetainerPage4,
            InventoryType.RetainerPage5,
            InventoryType.RetainerPage6,
            InventoryType.RetainerPage7,
            InventoryType.RetainerEquippedItems,
            InventoryType.RetainerGil,
            InventoryType.RetainerCrystals,
            InventoryType.RetainerMarket
        };
        
        // 检查是否有活动雇员
        bool noCurrentRetainer = currentRetainer == 0;
        if (noCurrentRetainer)
        {
            _pluginLog.Verbose("Parsed retainer bags failed: no active retainer");
            return;
        }

        // 检查所有必需的物品栏类型是否都已加载
        var notLoadedInventories = requiredInventories.Where(inv => !_loadedInventories.Contains(inv)).ToList();
        bool notAllInventoriesLoaded = notLoadedInventories.Any();
        if (notAllInventoriesLoaded)
        {
            _pluginLog.Verbose($"Parsed retainer bags failed: unloaded inventories {string.Join(", ", notLoadedInventories)}");
            return;
        }

        // 获取当前市场订单
        var marketOrder = _marketOrderService.GetCurrentOrder();

        // 如果雇员不在内存中，则添加
        if (!InMemoryRetainers.ContainsKey(currentRetainer))
        {
            InMemoryRetainers.Add(currentRetainer, new HashSet<InventoryType>());
        }
            
        // 将所有必需的物品栏类型添加到雇员的已加载类型集合中
        foreach (var type in requiredInventories)
        {
            InMemoryRetainers[currentRetainer].Add(type);
        }

        // 初始化雇员的所有物品栏容器
        InitializeContainers(currentRetainer);

        // 获取雇员的各种物品栏容器指针
        var retainerBag1 = GetInventoryContainer(InventoryType.RetainerPage1);
        var retainerBag2 = GetInventoryContainer(InventoryType.RetainerPage2);
        var retainerBag3 = GetInventoryContainer(InventoryType.RetainerPage3);
        var retainerBag4 = GetInventoryContainer(InventoryType.RetainerPage4);
        var retainerBag5 = GetInventoryContainer(InventoryType.RetainerPage5);
        var retainerBag6 = GetInventoryContainer(InventoryType.RetainerPage6);
        var retainerBag7 = GetInventoryContainer(InventoryType.RetainerPage7);
        var retainerEquippedItems = GetInventoryContainer(InventoryType.RetainerEquippedItems);
        var retainerMarketItems = GetInventoryContainer(InventoryType.RetainerMarket);
        var retainerGil = GetInventoryContainer(InventoryType.RetainerGil);
        var retainerCrystal = GetInventoryContainer(InventoryType.RetainerCrystals);

        // 获取雇员物品栏的排序顺序
        RetainerSortOrder retainerInventory;
        if (currentSortOrder.RetainerInventories.ContainsKey(currentRetainer))
        {
            retainerInventory = currentSortOrder.RetainerInventories[currentRetainer];
        }
        else
        {
            retainerInventory = RetainerSortOrder.NoOdrOrder; // 无排序顺序
        }

        // 处理雇员装备栏物品
        ProcessInventoryItems(retainerEquippedItems, RetainerEquipped[currentRetainer], 
            InventoryType.RetainerEquippedItems, changeSet);
        
        // 处理雇员金币栏
        var retainerGilItem = retainerGil->Items[0];
        retainerGilItem.Slot = 0;
        if (!retainerGilItem.IsSame(RetainerGil[currentRetainer][0]))
        {
            RetainerGil[currentRetainer][0] = retainerGilItem;
            changeSet.Add(new BagChange(retainerGilItem, InventoryType.RetainerGil));
        }
        
        // 处理雇员水晶栏物品
        ProcessInventoryItems(retainerCrystal, RetainerCrystals[currentRetainer], 
            InventoryType.RetainerCrystals, changeSet);

        // 处理雇员市场出售物品栏
        var retainerMarketCopy = new InventoryItem[20];

        // 根据市场订单排序市场物品
        if (marketOrder != null)
        {
            retainerMarketCopy = marketOrder
                .Where(kv => kv.Key < retainerMarketItems->Size)
                .OrderBy(kv => kv.Value)
                .Select(kv => retainerMarketItems->Items[kv.Key])
                .ToArray();
        }
        else
        {
            // 如果没有市场订单，则使用备份排序方法
            for (var i = 0; i < retainerMarketItems->Size; i++)
            {
                retainerMarketCopy[i] = retainerMarketItems->Items[i];
            }
            retainerMarketCopy = _marketOrderService.SortByBackupRetainerMarketOrder(retainerMarketCopy.ToList()).ToArray();
        }

        // 处理排序后的市场物品
        retainerMarketCopy = retainerMarketCopy.ToArray();
        for (var i = 0; i < retainerMarketCopy.Length; i++)
        {
            var retainerItem = retainerMarketCopy[i];
            if (_cachedRetainerMarketPrices.ContainsKey(currentRetainer))
            {
                var cachedPrice = _cachedRetainerMarketPrices[currentRetainer][retainerItem.Slot];
                retainerItem.Slot = (short)i;
                // 如果物品或价格发生变化，则更新并记录变更
                if (!retainerItem.IsSame(RetainerMarket[currentRetainer][i]) ||
                    cachedPrice != RetainerMarketPrices[currentRetainer][i])
                {
                    RetainerMarket[currentRetainer][i] = retainerItem;
                    RetainerMarketPrices[currentRetainer][i] = cachedPrice;
                    changeSet.Add(new BagChange(retainerItem, InventoryType.RetainerMarket));
                }
            }
        }

        // 处理雇员普通物品栏（按排序顺序）
        var newBags = new InventoryItem[7][];
        for (int i = 0; i < 7; i++)
        {
            newBags[i] = new InventoryItem[25];
        }

        // 根据排序顺序分组物品
        var groupedItems = retainerInventory.InventoryCoords
            .Select((sort, index) => new { sort, index })
            .GroupBy(x => x.index / 25)
            .Where(g => g.Key < 7);

        // 处理每个分组中的物品
        foreach (var group in groupedItems)
        {
            foreach (var item in group)
            {
                var sort = item.sort;
                // 根据容器索引获取对应的物品栏容器
                InventoryContainer* currentBag = sort.containerIndex switch
                {
                    0 => retainerBag1,
                    1 => retainerBag2,
                    2 => retainerBag3,
                    3 => retainerBag4,
                    4 => retainerBag5,
                    5 => retainerBag6,
                    6 => retainerBag7,
                    _ => null
                };

                // 检查容器和槽位是否有效
                if (currentBag == null || sort.slotIndex >= currentBag->Size)
                {
                    _pluginLog.Verbose("bag was too big UwU retainer");
                    continue;
                }

                // 将物品添加到新的排序后的物品栏中
                newBags[group.Key][group.ToList().IndexOf(item)] = currentBag->Items[sort.slotIndex];
            }
        }

        // 雇员的物品栏类型列表
        var retainerBags = new List<InventoryType>
        {
            InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3,
            InventoryType.RetainerPage4, InventoryType.RetainerPage5
        };

        // 处理排序后的物品并更新物品栏
        var absoluteIndex = 0;
        for (int bagIndex = 0; bagIndex < newBags.Length; bagIndex++)
        {
            for (int itemIndex = 0; itemIndex < newBags[bagIndex].Length; itemIndex++)
            {
                var item = newBags[bagIndex][itemIndex];

                var sortedBagIndex = absoluteIndex / 35;
                if (sortedBagIndex >= 0 && retainerBags.Count > sortedBagIndex)
                {
                    // 设置物品的槽位和容器
                    item.Slot = (short)(absoluteIndex - sortedBagIndex * 35);
                    if (retainerBags.Count > sortedBagIndex) item.Container = retainerBags[sortedBagIndex];

                    // 获取对应的物品栏并检查物品是否发生变化
                    var bag = GetInventoryByType(currentRetainer, retainerBags[sortedBagIndex]);
                    if (!bag[item.Slot].IsSame(item))
                    {
                        bag[item.Slot] = item;
                        changeSet.Add(new BagChange(item, retainerBags[sortedBagIndex]));
                    }
                }
                absoluteIndex++;
            }
        }
    }
}
