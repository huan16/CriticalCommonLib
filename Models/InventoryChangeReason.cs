using System;
using System.Collections.Generic;
using System.Reflection;

namespace CriticalCommonLib.Models
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DisplayNameAttribute : Attribute
    {
        public string DisplayName { get; }
        public DisplayNameAttribute(string displayName) => DisplayName = displayName;
    }

    public static class InventoryChangeReasonExtensions
    {
        private static readonly Dictionary<InventoryChangeReason, string> _changeReasonNames = new();

        static InventoryChangeReasonExtensions()
        {
            foreach (var reason in Enum.GetValues<InventoryChangeReason>())
            {
                var field = typeof(InventoryChangeReason).GetField(reason.ToString());
                var attr = field.GetCustomAttribute<DisplayNameAttribute>();
                _changeReasonNames[reason] = attr?.DisplayName ?? "未知";
            }
        }

        public static string GetDisplayName(this InventoryChangeReason reason)
        {
            return _changeReasonNames.GetValueOrDefault(reason, "未知");
        }
    }

    public enum InventoryChangeReason
    {
        [DisplayName("新增")]
        Added,
        [DisplayName("移除")]
        Removed,
        [DisplayName("移动")]
        Moved,
        [DisplayName("状态变更")]
        ConditionChanged,
        [DisplayName("NQ/HQ变更")]
        FlagsChanged,
        [DisplayName("投影变更")]
        GlamourChanged,
        [DisplayName("魔晶石变更")]
        MateriaChanged,
        [DisplayName("数量变更")]
        QuantityChanged,
        [DisplayName("灵魂绑定变更")]
        SpiritbondChanged,
        [DisplayName("染色变更")]
        StainChanged,
        [DisplayName("物品变更")]
        ItemIdChanged,
        [DisplayName("转移")]
        Transferred,
        [DisplayName("市场价格变更")]
        MarketPriceChanged,
        [DisplayName("装备套装变更")]
        GearsetsChanged,
    }
}