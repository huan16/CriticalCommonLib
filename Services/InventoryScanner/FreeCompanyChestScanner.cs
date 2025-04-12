using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Services.Hook;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using InventoryItem = FFXIVClientStructs.FFXIV.Client.Game.InventoryItem;

namespace CriticalCommonLib.Services.SubInventoryScanner
{
    public class FreeCompanyChestScanner : IDisposable
    {
        private readonly ICharacterMonitor _characterMonitor;
        private readonly ContainerInfoHook _containerInfoHook;
        private bool _isDisposed;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        
        public delegate void FreeCompanyChestRefreshedDelegate(InventoryType inventoryType);
        public event FreeCompanyChestRefreshedDelegate? FreeCompanyChestRefreshed;

        // 存储不同类型的物品栏数据
        public Dictionary<InventoryType, InventoryItem[]> FreeCompanyBags { get; } = new()
        {
            { InventoryType.FreeCompanyPage1, new InventoryItem[50] },
            { InventoryType.FreeCompanyPage2, new InventoryItem[50] },
            { InventoryType.FreeCompanyPage3, new InventoryItem[50] },
            { InventoryType.FreeCompanyPage4, new InventoryItem[50] },
            { InventoryType.FreeCompanyPage5, new InventoryItem[50] },
            { InventoryType.FreeCompanyGil, new InventoryItem[1] },
            { InventoryType.FreeCompanyCrystals, new InventoryItem[18] }
        };

        private readonly HashSet<InventoryType> _loadedInventories = new();
        private readonly InventoryType[] _requiredInventories =
        [
            InventoryType.FreeCompanyPage1,
            InventoryType.FreeCompanyPage2,
            InventoryType.FreeCompanyPage3,
            InventoryType.FreeCompanyPage4,
            InventoryType.FreeCompanyPage5,
            InventoryType.FreeCompanyGil,
            InventoryType.FreeCompanyCrystals
        ];

        public FreeCompanyChestScanner(
            ICharacterMonitor characterMonitor,
            ContainerInfoHook containerInfoHook)
        {
            _characterMonitor = characterMonitor;
            _containerInfoHook = containerInfoHook;

            // 注册事件
            _containerInfoHook.ContainerInfoReceived += OnContainerInfoReceived;
        }

        private void OnContainerInfoReceived(ContainerInfo containerInfo, InventoryType inventoryType)
        {
            if (!_requiredInventories.Contains(inventoryType))
                return;

            _loadedInventories.Add(inventoryType);

            // 取消之前的延迟任务
            _cts.Cancel();
            _cts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, _cts.Token); // 可调整延迟时间
                    if (!_cts.IsCancellationRequested)
                    {
                        FreeCompanyChestRefreshed?.Invoke(inventoryType);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 忽略取消异常
                }
            });
        }


        public unsafe void ParseFreeCompanyBags(InventoryType inventoryType, BagChangeContainer changeSet)
        {
            var fcId = _characterMonitor.ActiveFreeCompanyId;
            if (fcId == 0) return;

            // 获取所有物品栏容器
            var bag = GetInventoryContainer(inventoryType);

            // 处理各个物品栏（示例）
            ProcessInventoryItems(bag, FreeCompanyBags[InventoryType.FreeCompanyPage1], changeSet);
        }

        private unsafe InventoryContainer* GetInventoryContainer(InventoryType type)
            => InventoryManager.Instance()->GetInventoryContainer(type);

        private static unsafe void ProcessInventoryItems(
            InventoryContainer* container, 
            InventoryItem[] targetArray, 
            BagChangeContainer changeSet)
        {
            for (var i = 0; i < container->Size; i++)
            {
                var item = container->Items[i];
                item.Slot = (short)i;
                if (!item.IsSame(targetArray[i]))
                {
                    targetArray[i] = item;
                    changeSet.Add(new BagChange(item, container->Type));
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _containerInfoHook.ContainerInfoReceived -= OnContainerInfoReceived;
                _cts?.Cancel();
                _cts?.Dispose();
            }


            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~FreeCompanyChestScanner()
        {
            Dispose(disposing: false);
        }
    }
}