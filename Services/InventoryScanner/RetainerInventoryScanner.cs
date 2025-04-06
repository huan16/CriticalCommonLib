using System;
using System.Collections.Generic;
using System.Linq;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Models;
using CriticalCommonLib.Services.Hook;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using InventoryItem = FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;

namespace CriticalCommonLib.Services.SubInventoryScanner;

/// <summary>
/// ��Ա��Ʒ��ɨ����������ɨ��͹�����Ϸ�й�Ա����Ʒ������
/// </summary>
public class RetainerInventoryScanner : IDisposable
{
    // ��ɫ�����������ڻ�ȡ��ǰ��Ĺ�ԱID
    private readonly ICharacterMonitor _characterMonitor;
    // ������Ʒ˳��������ڴ����Ա������Ʒ��˳��
    private readonly IMarketOrderService _marketOrderService;
    private readonly ContainerInfoHook _containerInfoHook;

    // �Ѽ��ص���Ʒ�����ͼ���
    private readonly HashSet<InventoryType> _loadedInventories = new();
    // �����־����
    private readonly IPluginLog _pluginLog;
    
    // �����Ա�г���Ʒ�۸���ֵ䣬��Ϊ��ԱID��ֵΪ�۸�����
    private readonly Dictionary<ulong,uint[]> _cachedRetainerMarketPrices = new Dictionary<ulong, uint[]>();

    private bool isDisposed = false;

    // �ڴ��еĹ�Ա�����Ѽ��ص���Ʒ������
    public Dictionary<ulong, HashSet<InventoryType>> InMemoryRetainers { get; } = new();
    // ��Ա�ĸ�����Ʒ����������Ϊ��ԱID��ֵΪ��Ʒ����
    public Dictionary<ulong, InventoryItem[]> RetainerBag1 { get; } = new(); // ��Ա��Ʒ��1
    public Dictionary<ulong, InventoryItem[]> RetainerBag2 { get; } = new(); // ��Ա��Ʒ��2
    public Dictionary<ulong, InventoryItem[]> RetainerBag3 { get; } = new(); // ��Ա��Ʒ��3
    public Dictionary<ulong, InventoryItem[]> RetainerBag4 { get; } = new(); // ��Ա��Ʒ��4
    public Dictionary<ulong, InventoryItem[]> RetainerBag5 { get; } = new(); // ��Ա��Ʒ��5
    public Dictionary<ulong, InventoryItem[]> RetainerEquipped { get; } = new(); // ��Աװ����
    public Dictionary<ulong, InventoryItem[]> RetainerMarket { get; } = new(); // ��Ա�г�������Ʒ��
    public Dictionary<ulong, InventoryItem[]> RetainerCrystals { get; } = new(); // ��Աˮ����
    public Dictionary<ulong, InventoryItem[]> RetainerGil { get; } = new(); // ��Ա�����
    public Dictionary<ulong, uint[]> RetainerMarketPrices { get; } = new(); // ��Ա�г���Ʒ�۸�

    /// <summary>
    /// ���캯������ʼ����Ա��Ʒ��ɨ����
    /// </summary>
    public RetainerInventoryScanner(
        ICharacterMonitor characterMonitor, 
        IMarketOrderService marketOrderService, 
        IPluginLog pluginLog,
        ContainerInfoHook containerInfoHook)
    {
        _characterMonitor = characterMonitor;
        _marketOrderService = marketOrderService;
        _pluginLog = pluginLog;
        _containerInfoHook = containerInfoHook;

        // ע���¼�
        _containerInfoHook.ContainerInfoReceived += OnContainerInfoReceived;
        _characterMonitor.OnActiveRetainerChanged += CharacterMonitorOnOnActiveRetainerChanged;

        // ��ʼ����Ʒ�����͵���Ӧ�ֵ��ӳ��
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

    private void OnContainerInfoReceived(ContainerInfo containerInfo, InventoryType inventoryType)
    {
        _loadedInventories.Add(inventoryType);
    }
    
    private void CharacterMonitorOnOnActiveRetainerChanged(ulong retainerid)
    {
        if (retainerid == 0)
        {
            _loadedInventories.Clear();
        }
    }
    
    /// <summary>
    /// ��ȡָ�����͵���Ʒ������
    /// </summary>
    private unsafe InventoryContainer* GetInventoryContainer(InventoryType type)
    {
        return InventoryManager.Instance()->GetInventoryContainer(type);
    }

    /// <summary>
    /// Ϊָ����Ա��ʼ��������Ʒ������
    /// </summary>
    private void InitializeContainers(ulong retainerId)
    {
        // ����������������С
        var containers = new Dictionary<Dictionary<ulong, InventoryItem[]>, int>
        {
            { RetainerBag1, 35 },    // ��Ա��Ʒ��1��35��
            { RetainerBag2, 35 },    // ��Ա��Ʒ��2��35��
            { RetainerBag3, 35 },    // ��Ա��Ʒ��3��35��
            { RetainerBag4, 35 },    // ��Ա��Ʒ��4��35��
            { RetainerBag5, 35 },    // ��Ա��Ʒ��5��35��
            { RetainerEquipped, 14 }, // ��Աװ������14��
            { RetainerMarket, 20 },   // ��Ա�г�������Ʒ����20��
            { RetainerGil, 1 },       // ��Ա�������1��
            { RetainerCrystals, 18 }  // ��Աˮ������18��
        };
        
        // Ϊÿ��������ʼ����Ӧ��Ա����Ʒ����
        foreach (var container in containers)
        {
            if (!container.Key.ContainsKey(retainerId))
                container.Key.Add(retainerId, new InventoryItem[container.Value]);
        }
    }

    // ��Ʒ�����͵���Ӧ�ֵ��ӳ��
    private readonly Dictionary<InventoryType, Dictionary<ulong, InventoryItem[]>> _inventoryMap;

    /// <summary>
    /// ���ݹ�ԱID����Ʒ�����ͻ�ȡ��Ʒ����
    /// </summary>
    public InventoryItem[] GetInventoryByType(ulong retainerId, InventoryType type)
    {
        return _inventoryMap.TryGetValue(type, out var bag) && bag.TryGetValue(retainerId, out var items) 
            ? items 
            : Array.Empty<InventoryItem>();
    }

    /// <summary>
    /// ������Ʒ����Ŀ����¼���
    /// </summary>
    private static unsafe void ProcessInventoryItems(InventoryContainer* container, InventoryItem[] targetArray, 
        InventoryType inventoryType, BagChangeContainer changeSet)
    {
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->Items[i];
            
            item.Slot = (short)i; // ������Ʒ�Ĳ�λ����
            if (!item.IsSame(targetArray[i])) // �����Ʒ�����仯
            {
                targetArray[i] = item; // ����Ŀ�������е���Ʒ
                changeSet.Add(new BagChange(item, inventoryType)); // ��¼���
            }
        }
    }

    /// <summary>
    /// ������Ա��������Ʒ�������Ǻ��ķ���
    /// </summary>
    public unsafe void ParseRetainerBags(InventorySortOrder currentSortOrder, BagChangeContainer changeSet)
    {
        // ��ȡ��ǰ��Ĺ�ԱID
        var activeRetainerId = _characterMonitor.ActiveRetainerId;
        // ��Ҫ���ص���Ʒ������
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
        
        // ����Ƿ��л��Ա
        bool noCurrentRetainer = activeRetainerId == 0;
        if (noCurrentRetainer)
        {
            _pluginLog.Verbose("Parsed retainer bags failed: no active retainer");
            return;
        }

        // ������б������Ʒ�������Ƿ��Ѽ���
        // var notLoadedInventories = requiredInventories.Where(inv => !_loadedInventories.Contains(inv)).ToList();
        // bool notAllInventoriesLoaded = notLoadedInventories.Any();
        // if (notAllInventoriesLoaded)
        // {
        //     _pluginLog.Verbose($"Parsed retainer bags failed: unloaded inventories {string.Join(", ", notLoadedInventories)}");
        //     return;
        // }

        // �����Ա�����ڴ��У������
        if (!InMemoryRetainers.ContainsKey(activeRetainerId))
        {
            InMemoryRetainers.Add(activeRetainerId, new HashSet<InventoryType>());
        }
            
        // �����б������Ʒ��������ӵ���Ա���Ѽ������ͼ�����
        foreach (var type in requiredInventories)
        {
            InMemoryRetainers[activeRetainerId].Add(type);
        }

        // ��ʼ����Ա��������Ʒ������
        InitializeContainers(activeRetainerId);

        // ��ȡ��Ա�ĸ�����Ʒ������ָ��
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

        // �����Աװ������Ʒ
        ProcessInventoryItems(retainerEquippedItems, RetainerEquipped[activeRetainerId], 
            InventoryType.RetainerEquippedItems, changeSet);
        
        // �����Ա�����
        var retainerGilItem = retainerGil->Items[0];
        retainerGilItem.Slot = 0;
        if (!retainerGilItem.IsSame(RetainerGil[activeRetainerId][0]))
        {
            RetainerGil[activeRetainerId][0] = retainerGilItem;
            changeSet.Add(new BagChange(retainerGilItem, InventoryType.RetainerGil));
        }
        
        // �����Աˮ������Ʒ
        ProcessInventoryItems(retainerCrystal, RetainerCrystals[activeRetainerId], 
            InventoryType.RetainerCrystals, changeSet);

        // ��ȡ��ǰ������Ʒ˳��
        var marketOrder = _marketOrderService.GetCurrentOrder();
        // �����Ա�г�������Ʒ��
        if (marketOrder != null)
        {
            // �����г����������г���Ʒ
            var sortedItems = marketOrder
                .Where(kv => kv.Key < retainerMarketItems->Size)
                .OrderBy(kv => kv.Value)
                .Select(kv => 
                {
                    var item = retainerMarketItems->Items[kv.Key];
                    item.Slot = (short)kv.Key;
                    return item;
                });
                
            // �����������г���Ʒ
            int i = 0;
            foreach (var retainerItem in sortedItems)
            {
                if (!retainerItem.IsSame(RetainerMarket[activeRetainerId][i]))
                {
                    RetainerMarket[activeRetainerId][i] = retainerItem;
                    changeSet.Add(new BagChange(retainerItem, InventoryType.RetainerMarket));
                }
                i++;
            }
        }

        // �����Ա��ͨ��Ʒ����������˳��
        var newBags = new InventoryItem[7][];
        for (int i = 0; i < 7; i++)
        {
            newBags[i] = new InventoryItem[25];
        }

        // ��ȡ��Ա��Ʒ��������˳��
        RetainerSortOrder retainerInventory;
        if (currentSortOrder.RetainerInventories.ContainsKey(activeRetainerId))
        {
            retainerInventory = currentSortOrder.RetainerInventories[activeRetainerId];
        }
        else
        {
            retainerInventory = RetainerSortOrder.NoOdrOrder; // ������˳��
        }

        // ��������˳�������Ʒ
        var groupedItems = retainerInventory.InventoryCoords
            .Select((sort, index) => new { sort, index })
            .GroupBy(x => x.index / 25)
            .Where(g => g.Key < 7);

        // ����ÿ�������е���Ʒ
        foreach (var group in groupedItems)
        {
            foreach (var item in group)
            {
                var sort = item.sort;
                // ��������������ȡ��Ӧ����Ʒ������
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

                // ��������Ͳ�λ�Ƿ���Ч
                if (currentBag == null || sort.slotIndex >= currentBag->Size)
                {
                    _pluginLog.Verbose("bag was too big UwU retainer");
                    continue;
                }

                // ����Ʒ��ӵ��µ���������Ʒ����
                newBags[group.Key][group.ToList().IndexOf(item)] = currentBag->Items[sort.slotIndex];
            }
        }

        // ��Ա����Ʒ�������б�
        var retainerBags = new List<InventoryType>
        {
            InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3,
            InventoryType.RetainerPage4, InventoryType.RetainerPage5
        };

        // ������������Ʒ��������Ʒ��
        var absoluteIndex = 0;
        for (int bagIndex = 0; bagIndex < newBags.Length; bagIndex++)
        {
            for (int itemIndex = 0; itemIndex < newBags[bagIndex].Length; itemIndex++)
            {
                var item = newBags[bagIndex][itemIndex];

                var sortedBagIndex = absoluteIndex / 35;
                if (sortedBagIndex >= 0 && retainerBags.Count > sortedBagIndex)
                {
                    // ������Ʒ�Ĳ�λ������
                    item.Slot = (short)(absoluteIndex - sortedBagIndex * 35);
                    if (retainerBags.Count > sortedBagIndex) item.Container = retainerBags[sortedBagIndex];

                    // ��ȡ��Ӧ����Ʒ���������Ʒ�Ƿ����仯
                    var bag = GetInventoryByType(activeRetainerId, retainerBags[sortedBagIndex]);
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(true);
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (isDisposed) return;

        if (isDisposing)
        {
            _containerInfoHook.ContainerInfoReceived -= OnContainerInfoReceived;
            _characterMonitor.OnActiveRetainerChanged -= CharacterMonitorOnOnActiveRetainerChanged;
        }

        isDisposed = true;
    }
}
