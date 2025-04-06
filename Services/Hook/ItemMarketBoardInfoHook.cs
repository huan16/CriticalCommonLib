using System;
using System.Collections.Generic;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Services;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace CriticalCommonLib.Services.Hook
{
    public class ItemMarketBoardInfoHook : IDisposable
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
        
        private readonly Dictionary<ulong, uint[]> _cachedPrices = new();

        public delegate void RetainerMarketPriceReceived(ulong retainerId, uint[] marketPrices);
        public event RetainerMarketPriceReceived? OnRetainerMarketPriceReceived;

        public ItemMarketBoardInfoHook(
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
                    var currentRetainerId = _characterMonitor.ActiveRetainerId;

                    if (currentRetainerId == 0)
                    {
                        return _itemMarketBoardInfoHook!.Original(seq, a3);
                    }

                    // 检测到新的数据序列时重置缓存
                    if (containerInfo.sequence != _currentSequenceId)
                    {
                        _pluginLog.Verbose("New sequence ID received, resetting cached prices.");
                        _currentSequenceId = containerInfo.sequence;
                        if (!_cachedPrices.ContainsKey(currentRetainerId))
                        {
                            _cachedPrices[currentRetainerId] = new uint[20];
                        }
                    }

                    // 仅处理市场板类型的数据（过滤其他容器类型）
                    if (containerInfo.containerId == (uint)InventoryType.RetainerMarket)
                    {
                        // 将数据存入对应槽位并记录日志
                        _cachedPrices[currentRetainerId][containerInfo.slot] = containerInfo.unitPrice;
                    }
                }
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Error in ItemMarketBoardInfoDetour");
            }

            return _itemMarketBoardInfoHook!.Original(seq, a3);
        }

        public uint[]? GetCachedMarketPrice(ulong retainerId)
        {
            return _cachedPrices.TryGetValue(retainerId, out var prices) ? prices : null;
        }

        public void Dispose()
        {
            _itemMarketBoardInfoHook?.Disable();
            _itemMarketBoardInfoHook?.Dispose();
        }
    }
}