using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CriticalCommonLib.Models;

namespace CriticalCommonLib.Enums {
    // Source: https://github.com/SapphireServer/Sapphire/blob/c5d63e2eccf483c0e785d373ab9ea9a504c734f4/src/common/Common.h#L188-L271
    // https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/master/apps/client/src/app/model/user/inventory/container-type.ts

    [AttributeUsage(AttributeTargets.Field)]
    public class InventoryTypeInfoAttribute : Attribute
    {
        public InventoryCategory Category { get; }
        public string DisplayName { get; }
        public string DetailedName { get; }

        public InventoryTypeInfoAttribute(InventoryCategory category, string displayName, string detailedName = null)
        {
            Category = category;
            DisplayName = displayName;
            DetailedName = detailedName ?? displayName;
        }
    }

    public static class InventoryTypeExtensions
    {
        private static readonly Dictionary<InventoryType, InventoryCategory> _categoryCache = new();
        private static readonly Dictionary<InventoryType, string> _displayNameCache = new();
        private static readonly Dictionary<InventoryType, string> _detailedNameCache = new();

        static InventoryTypeExtensions()
        {
            foreach (var type in Enum.GetValues<InventoryType>())
            {
                var field = typeof(InventoryType).GetField(type.ToString());
                var attr = field.GetCustomAttribute<InventoryTypeInfoAttribute>(); // 替换为反射获取属性
                _categoryCache[type] = attr?.Category ?? InventoryCategory.Other;
                _displayNameCache[type] = attr?.DisplayName ?? type.ToString();
                _detailedNameCache[type] = attr?.DetailedName ?? type.ToString();
            }
        }

        public static InventoryCategory GetCategory(this InventoryType type)
            => _categoryCache[type];

        public static string GetDisplayName(this InventoryType type)
            => _displayNameCache[type];

        public static string GetDetailedName(this InventoryType type)
            => _detailedNameCache[type];

        public static bool IsArmory(this InventoryType type)
        {
            return (uint)type >= 3200 && (uint)type <= 3500;
        }

        public static bool IsEquipped(this InventoryType type)
        {
            return (uint)type >= 1000 && (uint)type <= 1001;
        }
    }

    public enum InventoryType
    {
        // 角色背包
        [InventoryTypeInfo(InventoryCategory.CharacterBags, "主背包1", "角色背包1")]
        Bag0 = 0,
        [InventoryTypeInfo(InventoryCategory.CharacterBags, "主背包2", "角色背包2")]
        Bag1 = 1,
        [InventoryTypeInfo(InventoryCategory.CharacterBags, "主背包3", "角色背包3")]
        Bag2 = 2,
        [InventoryTypeInfo(InventoryCategory.CharacterBags, "主背包4", "角色背包4")]
        Bag3 = 3,

        // 装备套装,EquippedItems
        [InventoryTypeInfo(InventoryCategory.CharacterEquipped, "装备套装1", "角色装备1")]
        GearSet0 = 1000,
        [InventoryTypeInfo(InventoryCategory.CharacterEquipped, "装备套装2", "角色装备2")]
        GearSet1 = 1001,

        // 货币和水晶
        [InventoryTypeInfo(InventoryCategory.Currency, "货币")]
        Currency = 2000,
        [InventoryTypeInfo(InventoryCategory.Crystals, "水晶")]
        Crystal = 2001,
        [InventoryTypeInfo(InventoryCategory.Other, "邮件")]
        Mail = 2003,
        [InventoryTypeInfo(InventoryCategory.Other, "关键物品")]
        KeyItem = 2004,
        [InventoryTypeInfo(InventoryCategory.Other, "交付物品")]
        HandIn = 2005,
        [InventoryTypeInfo(InventoryCategory.Other, "损坏装备")]
        DamagedGear = 2007,
        [InventoryTypeInfo(InventoryCategory.Other, "未知")]
        UNKNOWN_2008 = 2008,
        [InventoryTypeInfo(InventoryCategory.Other, "检查")]
        Examine = 2009,
        
        // 自定义
        [InventoryTypeInfo(InventoryCategory.Armoire, "衣柜")]
        Armoire = 2500,
        [InventoryTypeInfo(InventoryCategory.GlamourChest, "幻化柜")]
        GlamourChest = 2501,
        [InventoryTypeInfo(InventoryCategory.Other, "部队货币")]
        FreeCompanyCurrency = 2502,

        // 装备兵装库
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "副手兵装库")]
        ArmoryOffHand = 3200,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "头部兵装库")]
        ArmoryHead = 3201,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "身体兵装库")]
        ArmoryBody = 3202,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "手部兵装库")]
        ArmoryHand = 3203,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "腰部兵装库")]
        ArmoryWaist = 3204,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "腿部兵装库")]
        ArmoryLegs = 3205,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "脚部兵装库")]
        ArmoryFeet = 3206,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "耳饰兵装库")]
        ArmoryEar = 3207,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "项链兵装库")]
        ArmoryNeck = 3208,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "手镯兵装库")]
        ArmoryWrist = 3209,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "戒指兵装库")]
        ArmoryRing = 3300,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "职业水晶")] // 保留原名称（无"装备"前缀）
        ArmorySoulCrystal = 3400,
        [InventoryTypeInfo(InventoryCategory.CharacterArmoryChest, "主手兵装库")]
        ArmoryMain = 3500,

        // 陆行鸟背包
        [InventoryTypeInfo(InventoryCategory.CharacterSaddleBags, "陆行鸟背包1")]
        SaddleBag0 = 4000,
        [InventoryTypeInfo(InventoryCategory.CharacterSaddleBags, "陆行鸟背包2")]
        SaddleBag1 = 4001,
        [InventoryTypeInfo(InventoryCategory.CharacterPremiumSaddleBags, "额外陆行鸟背包1", "特职陆行鸟背包1")]
        PremiumSaddleBag0 = 4100,
        [InventoryTypeInfo(InventoryCategory.CharacterPremiumSaddleBags, "额外陆行鸟背包2", "特职陆行鸟背包2")]
        PremiumSaddleBag1 = 4101,

        // 雇员相关
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏1")]
        RetainerBag0 = 10000,
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏2")]
        RetainerBag1 = 10001,
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏3")]
        RetainerBag2 = 10002,
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏4")]
        RetainerBag3 = 10003,
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏5")]
        RetainerBag4 = 10004,
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏6")]
        RetainerBag5 = 10005,
        [InventoryTypeInfo(InventoryCategory.RetainerBags, "雇员物品栏7")]
        RetainerBag6 = 10006,
        [InventoryTypeInfo(InventoryCategory.RetainerEquipped, "雇员装备")]
        RetainerEquippedGear = 11000,
        [InventoryTypeInfo(InventoryCategory.Currency, "雇员金币")]
        RetainerGil = 12000,
        [InventoryTypeInfo(InventoryCategory.Crystals, "雇员水晶")]
        RetainerCrystal = 12001,
        [InventoryTypeInfo(InventoryCategory.RetainerMarket, "雇员市场")]
        RetainerMarket = 12002,

        // 部队相关
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱1")]
        FreeCompanyBag0 = 20000,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱2")]
        FreeCompanyBag1 = 20001,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱3")]
        FreeCompanyBag2 = 20002,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱4")]
        FreeCompanyBag3 = 20003,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱5")]
        FreeCompanyBag4 = 20004,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱6")]
        FreeCompanyBag5 = 20005,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱7")]
        FreeCompanyBag6 = 20006,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱8")]
        FreeCompanyBag7 = 20007,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱9")]
        FreeCompanyBag8 = 20008,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱10")]
        FreeCompanyBag9 = 20009,
        [InventoryTypeInfo(InventoryCategory.FreeCompanyBags, "部队物品箱11")]
        FreeCompanyBag10 = 20010,
        [InventoryTypeInfo(InventoryCategory.Currency, "部队金币")]
        FreeCompanyGil = 22000,
        [InventoryTypeInfo(InventoryCategory.Crystals, "部队水晶")]
        FreeCompanyCrystal = 22001,
        
        // 房屋相关
        [InventoryTypeInfo(InventoryCategory.HousingExteriorAppearance, "房屋外观")]
        HousingExteriorAppearance = 25000,
        [InventoryTypeInfo(InventoryCategory.HousingExteriorItems, "房屋外部摆放物品")]
        HousingExteriorPlacedItems = 25001,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorAppearance, "房屋室内外观")]
        HousingInteriorAppearance = 25002,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品1")]
        HousingInteriorPlacedItems1 = 25003,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品2")]
        HousingInteriorPlacedItems2 = 25004,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品3")]
        HousingInteriorPlacedItems3 = 25005,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品4")]
        HousingInteriorPlacedItems4 = 25006,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品5")]
        HousingInteriorPlacedItems5 = 25007,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品6")]
        HousingInteriorPlacedItems6 = 25008,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品7")]
        HousingInteriorPlacedItems7 = 25009,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorItems, "房屋室内摆放物品8")]
        HousingInteriorPlacedItems8 = 25010,
        [InventoryTypeInfo(InventoryCategory.HousingExteriorStoreroom, "房屋室外储藏室")]
        HousingExteriorStoreroom = 27000,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室1")]
        HousingInteriorStoreroom1 = 27001,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室2")]
        HousingInteriorStoreroom2 = 27002,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室3")]
        HousingInteriorStoreroom3 = 27003,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室4")]
        HousingInteriorStoreroom4 = 27004,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室5")]
        HousingInteriorStoreroom5 = 27005,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室6")]
        HousingInteriorStoreroom6 = 27006,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室7")]
        HousingInteriorStoreroom7 = 27007,
        [InventoryTypeInfo(InventoryCategory.HousingInteriorStoreroom, "房屋室内储藏室8")]
        HousingInteriorStoreroom8 = 27008,
    }
}