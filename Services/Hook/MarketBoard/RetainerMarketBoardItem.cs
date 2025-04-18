using System;
using CriticalCommonLib.GameStructs;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace CriticalCommonLib.Services.Hook
{
    public class RetainerMarketBoardItem : IDisposable
    {
        private readonly ICharacterMonitor _characterMonitor;
        private readonly IPluginLog _pluginLog;
        private readonly IGameInteropProvider _gameInteropProvider;
        private uint _currentSequenceId;
        private bool disposed = false;
        private unsafe delegate void* ItemMarketBoardInfoData(int a2, int* a3);
        
        [Signature(
            "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D6 8B CF E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 56",
            DetourName = nameof(ItemMarketBoardInfoDetour))]
        private Hook<ItemMarketBoardInfoData>? _itemMarketBoardInfoHook;
        
        private ItemMarketBoardInfo[] itemMarketBoardInfos = new ItemMarketBoardInfo[20];
        public ItemMarketBoardInfo[] ItemMarketBoardInfos => itemMarketBoardInfos;

        public RetainerMarketBoardItem(
            ICharacterMonitor characterMonitor,
            IPluginLog pluginLog,
            IGameInteropProvider gameInteropProvider)
        {
            _characterMonitor = characterMonitor;
            _pluginLog = pluginLog;
            _gameInteropProvider = gameInteropProvider;
            
            _gameInteropProvider.InitializeFromAttributes(this);
            _itemMarketBoardInfoHook?.Enable();
        }

        private unsafe void* ItemMarketBoardInfoDetour(int seq, int* a3)
        {
            try
            {
                if (a3 != null)
                {
                    var ptr = (IntPtr)a3 + 16;
                    var containerInfo = NetworkDecoder.DecodeItemMarketBoardInfo(ptr);
                    // _pluginLog.Debug($"ItemMarketBoardInfo: {containerInfo.Sequence} {containerInfo.ContainerId} {containerInfo.Slot} {containerInfo.UnitPrice}");

                    // 检测到新的数据序列时重置缓存
                    if (containerInfo.Sequence != _currentSequenceId)
                    {
                        _currentSequenceId = containerInfo.Sequence;
                        itemMarketBoardInfos = new ItemMarketBoardInfo[20];
                    }

                    // 仅处理市场板类型的数据（过滤其他容器类型）
                    if (containerInfo.ContainerId == (uint)InventoryType.RetainerMarket)
                    {
                        // 将数据存入对应槽位并记录日志
                        itemMarketBoardInfos[containerInfo.Slot] = containerInfo;
                    }
                }
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Error in ItemMarketBoardInfoDetour");
            }

            return _itemMarketBoardInfoHook!.Original(seq, a3);
        }

        ~RetainerMarketBoardItem()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _itemMarketBoardInfoHook?.Dispose();
                }
                
                // 这里可以添加非托管资源释放代码（如果有的话）

                disposed = true;
            }
        }
    }
}