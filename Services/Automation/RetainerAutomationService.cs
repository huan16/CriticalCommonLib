using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using CriticalCommonLib.AddonHelper;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.GameFunctions;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using ECommons.UIHelpers.AtkReaderImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Hosting;

using static ECommons.GenericHelpers;
using static ECommons.UIHelpers.AtkReaderImplementations.ReaderContextMenu;

namespace CriticalCommonLib.Services.Automation;

public unsafe class RetainerAutomationService(
    IAddonLifecycle addonLifecycle,
    IPluginLog pluginLog
) : IHostedService
{
    private AddonMaster.RetainerList? retainerList;
    private AtkUnitBase* retainerSellList;    // 在ECommons中没有封装，所以这里使用IntPtr
    private AddonMaster.RetainerSell? retainerSell;
    private AtkUnitBase* itemSearchResult;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Stop();
        return Task.CompletedTask;
    }

    public bool IsOccupiedSummoningBell()
    {
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell])
        {
            return true;
        }

        return false;
    }

    public bool? SelectNearestBell()
    {
        if (IsOccupiedSummoningBell())
        {
            return true;
        }

        if (!IsOccupied())
        {
            var x = this.GetReachableRetainerBell();
            if (x != null)
            {
                if (AllaganThrottle.ThrottleGeneric())
                {
                    Svc.Targets.Target = x;
                    pluginLog.Verbose($"Set target to {x}");
                    return true;
                }
            }
        }

        return false;
    }

    public bool? InteractWithTargetedBell()
    {
        if (Svc.ClientState.LocalPlayer == null)
        {
            throw new InvalidOperationException("Cannot interact with targeted bell while not loggged in");
        }

        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell])
        {
            return true;
        }

        var x = Svc.Targets.Target;
        var bellName = Svc.Data.GetExcelSheet<EObjName>().GetRow(2000401).Singular.ToString();
        if (x != null && (x.ObjectKind == ObjectKind.Housing || x.ObjectKind == ObjectKind.EventObj) &&
            x.Name.ToString().EqualsIgnoreCaseAny(bellName, "リテイナーベル") && !IsOccupied())
        {
            if (Vector3.Distance(x.Position, Svc.ClientState.LocalPlayer.Position) <
                this.GetValidInteractionDistance(x) && x.Struct()->GetIsTargetable())
            {
                if (AllaganThrottle.ThrottleGeneric() && EzThrottler.Throttle("InteractWithBell", 5000))
                {
                    TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)x.Address, false);
                    Svc.Log.Verbose($"Interacted with {x}");
                    return true;
                }
            }
        }

        return false;
    }

    public IGameObject? GetReachableRetainerBell()
    {
        if (Svc.ClientState.LocalPlayer == null)
        {
            return null;
        }

        var bellName = Svc.Data.GetExcelSheet<EObjName>().GetRow(2000401).Singular.ToString();
        foreach (var x in Svc.Objects)
        {
            if ((x.ObjectKind == ObjectKind.Housing || x.ObjectKind == ObjectKind.EventObj) && x.Name.ToString().EqualsIgnoreCaseAny(bellName, "リテイナーベル"))
            {
                if (Vector3.Distance(x.Position, Svc.ClientState.LocalPlayer.Position) < this.GetValidInteractionDistance(x) && x.Struct()->GetIsTargetable())
                {
                    return x;
                }
            }
        }

        return null;
    }

    private float GetValidInteractionDistance(IGameObject bell)
    {
        if (bell.ObjectKind == ObjectKind.Housing)
        {
            return 6.5f;
        }
        else if (Inns.List.Contains(Svc.ClientState.TerritoryType))
        {
            return 4.75f;
        }
        else
        {
            return 4.6f;
        }
    }

    public unsafe bool? CloseRetainerList()
    {
        if (this.retainerList != null && IsAddonReady(this.retainerList.Base))
        {
            pluginLog.Verbose("Close retainer list.");
            Callback.Fire(this.retainerList.Base, true, -1);
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 打开指定显示顺序的雇员
    public bool? ClickRetainer(int displayOrder)
    {
        if (this.retainerList != null && this.retainerList.Retainers != null)
        {
            if (displayOrder >= this.retainerList.Retainers.Length)
            {
                pluginLog.Verbose($"Invalid displayOrder: {displayOrder}");
                throw new ArgumentOutOfRangeException(nameof(displayOrder));
            }

            this.retainerList.Retainers[displayOrder].Select();
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 打开当前雇员的出售商品列表
    public bool? ClickRetainerSaleList()
    {
        var text = new string[]
        {
            Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2380).Text.ToDalamudString().GetText(),
            Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2381).Text.ToDalamudString().GetText(),
        };

        if (SelectStringHandler.TrySelectSpecificEntry(text))
        {
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 打开指定显示顺序的出售商品修改界面
    public unsafe bool? ClickRetainerSaleItem(int itemIndex)
    {
        if (this.retainerSellList != null && IsAddonReady(this.retainerSellList) && this.retainerSell == null)
        {
            var itemCount = this.retainerSellList->UldManager.NodeListCount;
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(itemIndex, itemCount);

            pluginLog.Verbose($"Clicking item {itemIndex}");
            Callback.Fire(this.retainerSellList, true, 0, itemIndex, 1);
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 该函数使用字符串检测能够修改价格，不推荐使用
    public unsafe bool? ClickAdjustPrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && IsAddonReady(addon))
        {
            var reader = new ReaderContextMenu(addon);
            if (IsItemPriceAdjustable(reader.Entries))
            {
                pluginLog.Verbose($"Clicking adjust price");
                Callback.Fire(addon, true, 0, 0, 0, 0, 0); // click adjust price
            }
            else
            {
                pluginLog.Verbose("Current item is a mannequin item and will be skipped");
                addon->Close(true);
            }

            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    public unsafe bool ClickComparingPrices()
    {
        if (this.retainerSell != null && IsAddonReady(this.retainerSell.Base))
        {
            Callback.Fire(this.retainerSell.Base, true, 4);

            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    public unsafe bool CloseComparingPrices()
    {
        if (this.itemSearchResult != null && IsAddonReady(itemSearchResult))
        {
            pluginLog.Verbose("Close comparing prices");
            Callback.Fire(this.itemSearchResult, true, -1);

            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }
        return false;
    }

    // 修改当前出售商品的价格
    public unsafe bool AdjustSaleItemPrice(int inputPrice)
    {
        if (this.retainerSell != null && IsAddonReady(this.retainerSell.Base))
        {
            if (AllaganThrottle.ThrottleGeneric())
            {
                // 获取原生指针
                var AddonRetainerSell = (AddonRetainerSell*)this.retainerSell.Base;

                // 验证UI结构
                if (AddonRetainerSell->AtkUnitBase.UldManager.NodeListCount != 23)
                {
                    throw new InvalidOperationException("Invalid UI structure.");
                }

                // 获取价格输入组件
                var priceInput = (AtkComponentNumericInput*)AddonRetainerSell->AtkUnitBase.UldManager.NodeList[15]->GetComponent();
                // 获取数量输入组件
                var quantityInput = (AtkComponentNumericInput*)AddonRetainerSell->AtkUnitBase.UldManager.NodeList[11]->GetComponent();

                // 设置价格
                priceInput->SetValue(inputPrice);

                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 确定当前出售商品的价格
    public bool ConfirmSaleItemPrice()
    {
        if (this.retainerSell != null && IsAddonReady(this.retainerSell.Base))
        {
            pluginLog.Verbose("Price adjustment confirm.");
            Callback.Fire(this.retainerSell.Base, true, 0);
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 关闭当前雇员的出售商品列表
    public bool CloseRetainerSaleList()
    {
        if (this.retainerSellList != null && IsAddonReady(this.retainerSellList))
        {
            pluginLog.Verbose("Close retainer sale list.");

            Callback.Fire(this.retainerSellList, true, -1);
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    // 关闭当前雇员界面
    public bool CloseRetainer()
    {
        var text = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2383).Text.ToDalamudString().GetText();

        if (SelectStringHandler.TrySelectSpecificEntry(text))
        {
            pluginLog.Verbose("关闭雇员界面");
            return true;
        }
        else
        {
            AllaganThrottle.RethrottleGeneric();
        }

        return false;
    }

    private void Start()
    {
        // 注册事件：当 "RetainerList" Addon 打开和关闭时触发
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", this.OnRetainerListOpened);
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", this.OnRetainerSellListOpened);
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", this.OnRetainerSellOpened);
        addonLifecycle.RegisterListener(AddonEvent.PreSetup, "ItemSearchResult", this.OnItemSearchResultOpened);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerList", this.OnRetainerListClosed);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSellList", this.OnRetainerSellListClosed);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSell", this.OnRetainerSellClosed);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "ItemSearchResult", this.OnItemSearchResultClosed);
    }

    private void Stop()
    {
        // 注销事件监听
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "RetainerList", this.OnRetainerListOpened);
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "RetainerSellList", this.OnRetainerSellListOpened);
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "RetainerSell", this.OnRetainerSellOpened);
        addonLifecycle.UnregisterListener(AddonEvent.PreSetup, "ItemSearchResult", this.OnItemSearchResultOpened);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "RetainerList", this.OnRetainerListClosed);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "RetainerSellList", this.OnRetainerSellListClosed);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "RetainerSell", this.OnRetainerSellClosed);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "ItemSearchResult", this.OnItemSearchResultClosed);

        this.retainerList = null;
        this.retainerSellList = null;
        this.retainerSell = null;
        this.itemSearchResult = null;
    }

    private void OnRetainerListOpened(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已打开，指针等于" + args.Addon);

        this.retainerList = new AddonMaster.RetainerList(args.Addon);
    }

    private unsafe void OnRetainerSellListOpened(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已打开，指针等于" + args.Addon);

        this.retainerSellList = (AtkUnitBase*)args.Addon;
    }

    private void OnRetainerSellOpened(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已打开，指针等于" + args.Addon);

        this.retainerSell = new AddonMaster.RetainerSell(args.Addon);
    }

    private void OnItemSearchResultOpened(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已打开，指针等于" + args.Addon);

        this.itemSearchResult = (AtkUnitBase*)(args.Addon);
    }
    
    private void OnRetainerListClosed(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已关闭，指针等于" + args.Addon);

        this.retainerList = null;
    }

    private void OnRetainerSellListClosed(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已关闭，指针等于" + args.Addon);

        // 当出售列表关闭时清空指针
        this.retainerSellList = null;
    }

    private void OnRetainerSellClosed(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已关闭，指针等于" + args.Addon);

        // 当价格调整窗口关闭时清空引用
        this.retainerSell = null;
    }

    private void OnItemSearchResultClosed(AddonEvent type, AddonArgs args)
    {
        pluginLog.Debug(args.AddonName + "已关闭，指针等于" + args.Addon);

        this.itemSearchResult = null;
    }

    private static readonly string[] AdjustPriceKeywords =
    {
        "adjust price",
        "修改价格",
        "preis ändern",
        "価格を変更する",
        "changer le prix",
    };

    private static bool IsItemPriceAdjustable(List<ContextMenuEntry> entries) 
        => entries.Any(e => AdjustPriceKeywords.Contains(e.Name.ToLowerInvariant()));
}
