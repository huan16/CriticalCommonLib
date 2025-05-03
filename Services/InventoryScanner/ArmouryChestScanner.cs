using System;
using System.Collections.Generic;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Models;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using InventoryItem = FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;

namespace CriticalCommonLib.Services.SubInventoryScanner
{
    public class ArmouryChestScanner : IDisposable
    {
        private readonly IPluginLog _pluginLog;
        private bool isDisposed = false;

        // 物品栏类型到对应数组的映射
        private readonly Dictionary<InventoryType, InventoryItem[]> _inventoryMap;

        // 角色物品栏容器
        public InventoryItem[] ArmouryMainHand { get; } = new InventoryItem[50];
        public InventoryItem[] ArmouryHead { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryBody { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryHands { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryLegs { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryFeet { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryOffHand { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryEars { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryNeck { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryWrists { get; } = new InventoryItem[35];
        public InventoryItem[] ArmouryRings { get; } = new InventoryItem[50];
        public InventoryItem[] ArmourySoulCrystals { get; } = new InventoryItem[25];

        public ArmouryChestScanner(IPluginLog pluginLog)
        {
            _pluginLog = pluginLog;

            // 初始化物品栏类型映射
            _inventoryMap = new Dictionary<InventoryType, InventoryItem[]>
            {
                { InventoryType.ArmoryMainHand, ArmouryMainHand },
                { InventoryType.ArmoryHead, ArmouryHead },
                { InventoryType.ArmoryBody, ArmouryBody },
                { InventoryType.ArmoryHands, ArmouryHands },
                { InventoryType.ArmoryLegs, ArmouryLegs },
                { InventoryType.ArmoryFeets, ArmouryFeet },
                { InventoryType.ArmoryOffHand, ArmouryOffHand },
                { InventoryType.ArmoryEar, ArmouryEars },
                { InventoryType.ArmoryNeck, ArmouryNeck },
                { InventoryType.ArmoryWrist, ArmouryWrists },
                { InventoryType.ArmoryRings, ArmouryRings },
                { InventoryType.ArmorySoulCrystal, ArmourySoulCrystals }
            };
        }

        // 获取指定类型的物品栏容器
        private unsafe InventoryContainer* GetInventoryContainer(InventoryType type)
        {
            return InventoryManager.Instance()->GetInventoryContainer(type);
        }

        public unsafe void ParseArmouryChest(InventorySortOrder currentSortOrder, BagChangeContainer changeSet)
        {
            foreach (var armoryChest in _inventoryMap)
            {
                if (currentSortOrder.NormalInventories.ContainsKey(armoryChest.Key.ToString()))
                {
                    var odrOrdering = currentSortOrder.NormalInventories[armoryChest.Key.ToString()];
                    var chestBag = GetInventoryContainer(armoryChest.Key);
                    var targetBag = armoryChest.Value;

                    if (chestBag != null && chestBag->Loaded != 0 && targetBag != null)
                    {
                        for (var index = 0; index < odrOrdering.Count; index++)
                        {
                            var sort = odrOrdering[index];

                            if (sort.slotIndex >= chestBag->Size)
                            {
                                _pluginLog.Verbose("bag was too big UwU for " + armoryChest.Key);
                            }
                            else
                            {
                                var item = chestBag->Items[sort.slotIndex];
                                if (!targetBag[index].IsSame(item))
                                {
                                    targetBag[index] = item;
                                    changeSet.Add(new BagChange(item, armoryChest.Key));
                                }
                            }
                        }
                    }
                    else
                    {
                        _pluginLog.Warning("Could generate data for " + armoryChest.Key);
                    }
                }
                else
                {
                    _pluginLog.Warning("Could not find sort order for " + armoryChest.Key);
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