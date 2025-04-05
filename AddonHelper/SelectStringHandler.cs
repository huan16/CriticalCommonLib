using System;
using System.Collections.Generic;

using Dalamud.Plugin.Services;

using ECommons;
using ECommons.Logging;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace CriticalCommonLib.AddonHelper;

internal static unsafe class SelectStringHandler
{
    // 重载1：接受单个字符串
    internal static bool TrySelectSpecificEntry(string text, Func<bool> Throttler = null)
    {
        return TrySelectSpecificEntry(new[] { text }, Throttler);
    }

    // 重载2：接受字符串集合
    internal static bool TrySelectSpecificEntry(IEnumerable<string> texts, Func<bool> Throttler = null)
    {
        // 使用 StartsWithAny 扩展方法生成条件委托
        return TrySelectSpecificEntry(x => x.StartsWithAny(texts), Throttler);
    }

    // 核心方法：接受条件委托
    internal static bool TrySelectSpecificEntry(Func<string, bool> inputTextTest, Func<bool> throttler = null)
    {
        if (GenericHelpers.TryGetAddonByName<AddonSelectString>("SelectString", out var addon)
            && GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
        {
            var selectString = new AddonMaster.SelectString(addon);
            if (selectString.Entries.TryGetFirst(x => inputTextTest(x.Text), out var entry))
            {
                // 节流检查（若Throttler为null则跳过）
                if (throttler?.Invoke() ?? true)
                {
                    entry.Select();
                    PluginLog.LogDebug($"已选中条目: {entry.Text}");
                    return true;
                }
            }
        }

        return false;
    }
}
