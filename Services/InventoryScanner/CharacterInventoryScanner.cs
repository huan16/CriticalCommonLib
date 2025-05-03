using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AllaganLib.GameSheets.Sheets;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Common.Component.Excel;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using InventoryItem = FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;
using InventoryType = FFXIVClientStructs.FFXIV.Client.Game.InventoryType;

namespace CriticalCommonLib.Services.SubInventoryScanner
{
    public class CharacterInventoryScanner : IDisposable
    {
        private readonly IPluginLog _pluginLog;
        private readonly ItemSheet _itemSheet;
        private List<uint>? _currencyItemIds;
        private bool isDisposed = false;

        // 物品栏类型到对应数组的映射
        private readonly Dictionary<InventoryType, InventoryItem[]> _inventoryMap;

        // 角色物品栏容器
        public InventoryItem[] CharacterBag1 { get; } = new InventoryItem[35];
        public InventoryItem[] CharacterBag2 { get; } = new InventoryItem[35];
        public InventoryItem[] CharacterBag3 { get; } = new InventoryItem[35];
        public InventoryItem[] CharacterBag4 { get; } = new InventoryItem[35];
        public InventoryItem[] CharacterEquipped { get; } = new InventoryItem[14];
        public InventoryItem[] CharacterCrystals { get; } = new InventoryItem[18];
        public InventoryItem[] CharacterCurrency { get; } = new InventoryItem[100];

        public CharacterInventoryScanner(IPluginLog pluginLog, ItemSheet itemSheet)
        {
            _pluginLog = pluginLog;
            _itemSheet = itemSheet;

            _currencyItemIds = _itemSheet.Where(c => c.RowId is >= 20 and <= 60 && c.Base.FilterGroup == 16 || c.Base.ItemUICategory.RowId == 100 || c.RowId == 1)
                    .Select(c => c.RowId).ToList();

            // 初始化物品栏类型映射
            _inventoryMap = new Dictionary<InventoryType, InventoryItem[]>
            {
                { InventoryType.Inventory1, CharacterBag1 },
                { InventoryType.Inventory2, CharacterBag2 },
                { InventoryType.Inventory3, CharacterBag3 },
                { InventoryType.Inventory4, CharacterBag4 },
                { InventoryType.Crystals, CharacterCrystals },
                { InventoryType.Currency, CharacterCurrency }
            };
        }

        // 获取指定类型的物品栏容器
        private unsafe InventoryContainer* GetInventoryContainer(InventoryType type)
        {
            return InventoryManager.Instance()->GetInventoryContainer(type);
        }

        // 处理物品栏项目并记录变更
        private static unsafe void ProcessInventoryItems(
            InventoryContainer* container, InventoryItem[] targetArray, InventoryType type, BagChangeContainer changeSet)
        {
            for (var i = 0; i < container->Size; i++)
            {
                var item = container->Items[i];
                if (!item.IsSame(targetArray[i]))
                {
                    targetArray[i] = item;
                    changeSet.Add(new BagChange(item, type));
                }
            }
        }

        public unsafe void ParseCharacterBags(InventorySortOrder currentSortOrder, BagChangeContainer changeSet)
        {
            var bag0 = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory1);
            var bag1 = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory2);
            var bag2 = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory3);
            var bag3 = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory4);

            if (bag0 != null && bag1 != null && bag2 != null && bag3 != null)
            {
                var uiBag1 = new InventoryItem[35];
                var uiBag2 = new InventoryItem[35];
                var uiBag3 = new InventoryItem[35];
                var uiBag4 = new InventoryItem[35];
                var bagCount1 = 0;
                var bagCount2 = 0;
                var bagCount3 = 0;
                var bagCount4 = 0;

                //Sort ordering
                if (currentSortOrder.NormalInventories.ContainsKey("PlayerInventory"))
                {
                    var playerInventorySort = currentSortOrder.NormalInventories["PlayerInventory"];

                    for (var index = 0; index < playerInventorySort.Count; index++)
                    {
                        var sort = playerInventorySort[index];
                        InventoryContainer* currentBag;
                        switch (sort.containerIndex)
                        {
                            case 0:
                                currentBag = bag0;
                                break;
                            case 1:
                                currentBag = bag1;
                                break;
                            case 2:
                                currentBag = bag2;
                                break;
                            case 3:
                                currentBag = bag3;
                                break;
                            default:
                                continue;
                        }

                        if (sort.slotIndex >= currentBag->Size)
                        {
                            _pluginLog.Verbose("bag was too big UwU for player inventory");
                        }
                        else
                        {
                            var sortedBagIndex = index / 35;
                            switch (sortedBagIndex)
                            {
                                case 0:
                                    uiBag1[bagCount1] = currentBag->Items[sort.slotIndex];
                                    bagCount1++;
                                    break;
                                case 1:
                                    uiBag2[bagCount2] = currentBag->Items[sort.slotIndex];
                                    bagCount2++;
                                    break;
                                case 2:
                                    uiBag3[bagCount3] = currentBag->Items[sort.slotIndex];
                                    bagCount3++;
                                    break;
                                case 3:
                                    uiBag4[bagCount4] = currentBag->Items[sort.slotIndex];
                                    bagCount4++;
                                    break;
                                default:
                                    continue;
                            }
                        }
                    }

                    for (var index = 0; index < uiBag1.Length; index++)
                    {
                        var newBag = uiBag1[index];
                        newBag.Slot = (short)index;
                        newBag.Container = InventoryType.Inventory1;
                        if (!CharacterBag1[index].IsSame(newBag))
                        {
                            CharacterBag1[index] = newBag;
                            changeSet.Add(new BagChange(newBag, InventoryType.Inventory1));
                        }
                    }

                    for (var index = 0; index < uiBag2.Length; index++)
                    {
                        var newBag = uiBag2[index];
                        newBag.Slot = (short)index;
                        newBag.Container = InventoryType.Inventory2;
                        if (!CharacterBag2[index].IsSame(newBag))                        {
                            CharacterBag2[index] = newBag;
                            changeSet.Add(new BagChange(newBag, InventoryType.Inventory2));
                        }
                    }

                    for (var index = 0; index < uiBag3.Length; index++)
                    {
                        var newBag = uiBag3[index];
                        newBag.Slot = (short)index;
                        newBag.Container = InventoryType.Inventory3;
                        if (!CharacterBag3[index].IsSame(newBag))
                        {
                            CharacterBag3[index] = newBag;
                            changeSet.Add(new BagChange(newBag, InventoryType.Inventory3));
                        }
                    }

                    for (var index = 0; index < uiBag4.Length; index++)
                    {
                        var newBag = uiBag4[index];
                        newBag.Slot = (short)index;
                        newBag.Container = InventoryType.Inventory4;
                        if (!CharacterBag4[index].IsSame(newBag))
                        {
                            CharacterBag4[index] = newBag;
                            changeSet.Add(new BagChange(newBag, InventoryType.Inventory4));
                        }
                    }
                }
            }

            // 获取基础容器
            var crystals = GetInventoryContainer(InventoryType.Crystals);
            var gearSet0 = GetInventoryContainer(InventoryType.EquippedItems);

            // 处理水晶栏
            ProcessInventoryItems(crystals, CharacterCrystals, InventoryType.Crystals, changeSet);

            // 处理装备栏
            if (gearSet0 != null && gearSet0->Loaded != 0)
            {
                ProcessInventoryItems(gearSet0, CharacterEquipped, InventoryType.EquippedItems, changeSet);
            }

            // 处理货币栏（特殊处理）
            short slot = 0;
            if (_currencyItemIds != null)
            {
                foreach (var currencyItemId in _currencyItemIds)
                {
                    var itemCount = InventoryManager.Instance()->GetInventoryItemCount(currencyItemId, false, false, false);
                    if (itemCount == 0) continue;

                    var fakeItem = new InventoryItem()
                    {
                        ItemId = currencyItemId,
                        Slot = slot,
                        Quantity = itemCount,
                        Container = InventoryType.Currency,
                        Flags = InventoryItem.ItemFlags.None,
                        GlamourId = 0
                    };

                    if (!CharacterCurrency[slot].IsSame(fakeItem))
                    {
                        CharacterCurrency[slot] = fakeItem;
                        changeSet.Add(new BagChange(fakeItem, InventoryType.Currency));
                    }
                    slot++;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposed) return;
            isDisposed = true;
        }
    }
}