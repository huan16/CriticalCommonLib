using System;
using System.Collections.Generic;
using CriticalCommonLib.Services.Mediator;
using Dalamud.Game.Network.Structures;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace CriticalCommonLib.Services.Hook;

public class RetainerMarketBoardItemRequest : IDisposable
{
    // 使用特征签名定位并挂钩物品请求开始数据包处理函数
    [Signature(
        "48 89 5C 24 08 57 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 48 8B FA E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 4A",
        DetourName = nameof(ItemRequestStartPacketDetour))]
    private readonly Hook<MarketBoardItemRequestStartPacketHandler>? itemRequestStartPacketDetourHook;
    private delegate nint MarketBoardItemRequestStartPacketHandler(nint a1, nint packetRef);

    private bool disposed;
    private readonly IPluginLog pluginLog;
    private readonly IGameInteropProvider gameInteropProvider;
    public RetainerMarketBoardItemRequest(
        IPluginLog pluginLog,
        IGameInteropProvider gameInteropProvider)
    {   
        this.pluginLog = pluginLog;
        this.gameInteropProvider = gameInteropProvider;

        this.gameInteropProvider.InitializeFromAttributes(this);
        itemRequestStartPacketDetourHook?.Enable();
    }

    // 定义市场板物品请求接收事件的委托
    public delegate void MarketBoardItemRequestReceivedDelegate(bool Valid, uint AmountToArrive);
    public event MarketBoardItemRequestReceivedDelegate MarketBoardItemRequestReceived;

    // 状态跟踪变量
    private uint expectedAmountToArrive;    // 预期接收的市场报价总数
    private bool itemRequestValid;
    private int? currentRequestId;           // 当前处理中的请求ID

    /// <summary>
    /// 市场板物品请求开始数据包拦截处理
    /// </summary>
    /// <remarks>
    /// 1. 当接收到新的市场板请求时触发事件
    /// 2. 调用原始游戏函数保持游戏正常运行
    /// </remarks>
    private unsafe nint ItemRequestStartPacketDetour(nint a1, nint packetRef)
    {
        try
        {
            var request = MarketBoardItemRequest.Read(packetRef);
            
            pluginLog.Verbose("Market board item request: AmountToArrive =  " + request.AmountToArrive + " Status = " + request.Status);

            if (request.Status == 0)
            {
                // 初始化新请求的状态
                itemRequestValid = true;
                // 设置预期接收数量
                expectedAmountToArrive = request.AmountToArrive;
            }
            else
            {
                itemRequestValid = false;
                expectedAmountToArrive = 0;
            }

            MarketBoardItemRequestReceived?.Invoke(itemRequestValid, expectedAmountToArrive);
        }
        catch (Exception ex)
        {
            pluginLog.Error(ex, "ItemRequestStartPacketDetour threw an exception");
        }

        // 调用原始游戏函数
        return itemRequestStartPacketDetourHook!.OriginalDisposeSafe(a1, packetRef);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        if (disposed) return;

        if (disposing)
        {
            itemRequestStartPacketDetourHook?.Dispose();
        }

        disposed = true;
    }
}