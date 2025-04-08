using System;
using System.Collections.Generic;
using System.Linq;
using CriticalCommonLib.Enums;

namespace CriticalCommonLib.Models
{
    public static class InventoryCategoryExtensions
    {
        // 直接内联初始化字典，移除静态构造函数
        private static readonly Dictionary<InventoryCategory, List<InventoryType>> CategoryToTypesMap = 
            Enum.GetValues(typeof(InventoryType))
                .Cast<InventoryType>()
                .GroupBy(type => type.GetCategory())
                .ToDictionary(
                    group => group.Key, 
                    group => group.ToList()
                );

        private static readonly Dictionary<InventoryCategory, string> DisplayNameMap = 
            Enum.GetValues(typeof(InventoryCategory))
                .Cast<InventoryCategory>()
                .ToDictionary(
                    category => category,
                    category => GetCategoryDisplayName(category)
                );

        private static readonly Dictionary<InventoryCategory, string> DetailedNameMap = 
            Enum.GetValues(typeof(InventoryCategory))
                .Cast<InventoryCategory>()
                .ToDictionary(
                    category => category,
                    category => GetCategoryDetailedName(category)
                );

        private static string GetCategoryDisplayName(InventoryCategory category)
        {
            var types = CategoryToTypesMap.GetValueOrDefault(category);
            if (types.Count > 0)
            {
                var firstName = types[0].GetDisplayName();
                return StripNumberSuffix(firstName);
            }
            return category.ToString();
        }

        private static string GetCategoryDetailedName(InventoryCategory category)
        {
            var types = CategoryToTypesMap.GetValueOrDefault(category);
            if (types.Count > 0)
            {
                var firstName = types[0].GetDetailedName();
                return StripNumberSuffix(firstName);
            }
            return category.ToString();
        }

        private static string StripNumberSuffix(string input)
        {
            int lastDigitIndex = input.Length - 1;
            while (lastDigitIndex >= 0 && char.IsDigit(input[lastDigitIndex]))
            {
                lastDigitIndex--;
            }
            return lastDigitIndex > 0 ? input[..(lastDigitIndex + 1)] : input;
        }

        public static List<InventoryType> GetTypes(this InventoryCategory category)
        {
            return CategoryToTypesMap.TryGetValue(category, out var types) 
                ? types 
                : new List<InventoryType>();
        }

        public static string GetDisplayName(this InventoryCategory category)
        {
            return DisplayNameMap.GetValueOrDefault(category, category.ToString());
        }

        public static string GetDetailedName(this InventoryCategory category)
        {
            return DetailedNameMap.GetValueOrDefault(category, category.ToString());
        }
    }

    public enum InventoryCategory
    {
        CharacterBags = 1,
        CharacterSaddleBags = 2,
        CharacterPremiumSaddleBags = 3,
        RetainerBags = 4,
        CharacterArmoryChest = 5,
        CharacterEquipped = 6,
        RetainerEquipped = 7,
        FreeCompanyBags = 8,
        RetainerMarket = 9,
        GlamourChest = 10,
        Armoire = 11,
        Currency = 12,
        Crystals = 13,
        HousingInteriorItems = 14,
        HousingInteriorStoreroom = 15,
        HousingInteriorAppearance = 16,
        HousingExteriorItems = 17,
        HousingExteriorStoreroom = 18,
        HousingExteriorAppearance = 19,
        Other = 99
    }
}