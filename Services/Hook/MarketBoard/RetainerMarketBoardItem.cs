using System;
using System.Collections.Generic;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Services;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Dalamud.Game.Network.Internal;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace CriticalCommonLib.Services.Hook
{
    public class RetainerMarketBoardItem : IDisposable
    {
        private readonly ICharacterMonitor _characterMonitor;
        private readonly IPluginLog _pluginLog;
        private readonly IGameInteropProvider _gameInteropProvider;
        private uint _currentSequenceId;
        private unsafe delegate void* ItemMarketBoardInfoData(int a2, int* a3);
        
        [Signature(
            "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D6 8B CF E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 56",
            DetourName = nameof(ItemMarketBoardInfoDetour))]
        private Hook<ItemMarketBoardInfoData>? _itemMarketBoardInfoHook;
        
        private ItemMarketBoardInfo[] itemMarketBoardInfos = new ItemMarketBoardInfo[20];

        public delegate void RetainerMarketBoardItemReceived(ulong retainerId, uint[] marketPrices);
        public event RetainerMarketBoardItemReceived? retainerMarketBoardItemReceived;

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
                    var currentRetainerId = _characterMonitor.ActiveRetainerId;
                    if (currentRetainerId == 0)
                    {
                        return _itemMarketBoardInfoHook!.Original(seq, a3);
                    }

                    var ptr = (IntPtr)a3 + 16;
                    var containerInfo = NetworkDecoder.DecodeItemMarketBoardInfo(ptr);

                    // 检测到新的数据序列时重置缓存
                    if (containerInfo.sequence != _currentSequenceId)
                    {
                        _currentSequenceId = containerInfo.sequence;
                        itemMarketBoardInfos = new ItemMarketBoardInfo[20];
                    }

                    // 仅处理市场板类型的数据（过滤其他容器类型）
                    if (containerInfo.containerId == (uint)InventoryType.RetainerMarket)
                    {
                        // 将数据存入对应槽位并记录日志
                        itemMarketBoardInfos[containerInfo.slot] = containerInfo;
                    }
                }
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Error in ItemMarketBoardInfoDetour");
            }

            return _itemMarketBoardInfoHook!.Original(seq, a3);
        }

        public void Dispose()
        {
            _itemMarketBoardInfoHook?.Dispose();
        }
    }
}