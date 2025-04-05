using System;
using System.Collections.Generic;
using CriticalCommonLib.GameStructs;
using CriticalCommonLib.Services;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace CriticalCommonLib.Services.Hook;

public class ContainerInfoHook : IDisposable
{
    private readonly IFramework _framework;
    private readonly IPluginLog _pluginLog;
    private readonly IGameInteropProvider _gameInteropProvider;
    private bool disposed = false;
    
    public delegate void ContainerInfoReceivedDelegate(ContainerInfo containerInfo, InventoryType inventoryType);

    public event ContainerInfoReceivedDelegate? ContainerInfoReceived;

    private unsafe delegate void* ContainerInfoNetworkData(int a2, int* a3);

    //If the signature for these are ever lost, find the ProcessZonePacketDown signature in Dalamud and then find the relevant function based on the opcode.
    [Signature(
        "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B D3 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 53 10 ",
        DetourName = nameof(ContainerInfoDetour), 
        UseFlags = SignatureUseFlags.Hook)]
    private Hook<ContainerInfoNetworkData>? _containerInfoNetworkHook = null;

    private unsafe void* ContainerInfoDetour(int seq, int* a3)
    {
        try
        {
            if (a3 != null)
            {
                var ptr = (IntPtr)a3 + 16;
                var containerInfo = NetworkDecoder.DecodeContainerInfo(ptr);
                if (Enum.IsDefined(typeof(InventoryType), containerInfo.containerId))
                {
                    var inventoryType = (InventoryType)containerInfo.containerId;
                    _framework.RunOnFrameworkThread(() =>
                    {
                        _pluginLog.Verbose($"Container update received for {inventoryType}");
                        ContainerInfoReceived?.Invoke(containerInfo, inventoryType);
                    });
                }
            }
        }
        catch (Exception e)
        {
            _framework.RunOnFrameworkThread(() =>
            {
                _pluginLog.Error("ContainerInfoDetour error: ", e);
            });
        }

        return _containerInfoNetworkHook!.Original(seq, a3);
    }

    public ContainerInfoHook(
        IFramework framework,
        IPluginLog pluginLog,
        IGameInteropProvider gameInteropProvider)
    {
        _framework = framework;
        _pluginLog = pluginLog;
        _gameInteropProvider = gameInteropProvider;

        _gameInteropProvider.InitializeFromAttributes(this);
        _containerInfoNetworkHook?.Enable();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);

    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

        if (disposing)
        {
            _containerInfoNetworkHook?.Dispose();
            _containerInfoNetworkHook = null;
        }

        disposed = true;
    }
}