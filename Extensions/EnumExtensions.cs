using System;
using System.Collections.Generic;
using System.Linq;
using AllaganLib.GameSheets.Model;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Models;

namespace CriticalCommonLib.Extensions
{
    public static class EnumExtensions
    {
        public static IEnumerable<TEnum> GetFlags<TEnum>(this TEnum enumValue)
            where TEnum : Enum
        {
            return EnumUtil.GetFlags<TEnum>().Where(ev => enumValue.HasFlag(ev));
        }

        public static string FormattedName(this CharacterSex characterSex)
        {
            switch (characterSex)
            {
                case CharacterSex.Both:
                    return "两者";
                case CharacterSex.Either:
                    return "任意";
                case CharacterSex.Female:
                    return "女性";
                case CharacterSex.Male:
                    return "男性";
                case CharacterSex.FemaleOnly:
                    return "仅女性";
                case CharacterSex.MaleOnly:
                    return "仅男性";
                case CharacterSex.NotApplicable:
                    return "不适用";
            }

            return "未知";
        }
        public static string FormattedName(this CharacterRace characterRace)
        {
            switch (characterRace)
            {
                case CharacterRace.Any:
                    return "任意";
                case CharacterRace.Hyur:
                    return "人族";
                case CharacterRace.Elezen:
                    return "精灵族";
                case CharacterRace.Lalafell:
                    return "拉拉菲尔族";
                case CharacterRace.Miqote:
                    return "猫魅族";
                case CharacterRace.Roegadyn:
                    return "鲁加族";
                case CharacterRace.Viera:
                    return "维埃拉族";
                case CharacterRace.AuRa:
                    return "敖龙族";
                case CharacterRace.None:
                    return "无";
            }

            return "不适用";
        }

        public static bool IsRetainerCategory(this InventoryCategory category)
        {
            return category is InventoryCategory.RetainerBags or InventoryCategory.RetainerEquipped or InventoryCategory
                .RetainerMarket or InventoryCategory.Crystals or InventoryCategory.Currency;
        }

        public static bool IsFreeCompanyCategory(this InventoryCategory category)
        {
            return category is InventoryCategory.FreeCompanyBags or InventoryCategory.Crystals or InventoryCategory.Currency;
        }

        public static bool IsHousingCategory(this InventoryCategory category)
        {
            return category is InventoryCategory.HousingExteriorAppearance or InventoryCategory.HousingExteriorItems or InventoryCategory.HousingExteriorStoreroom or InventoryCategory.HousingInteriorAppearance or InventoryCategory.HousingInteriorItems or InventoryCategory.HousingInteriorStoreroom;
        }

        public static bool IsCharacterCategory(this InventoryCategory category)
        {
            return !IsRetainerCategory(category) && !IsFreeCompanyCategory(category) && !IsHousingCategory(category) || category == InventoryCategory.Crystals || category == InventoryCategory.Currency;
        }

        public static InventoryCategory ToInventoryCategory(this InventoryType type)
        {
            return type.GetCategory();
        }

        public static string FormattedName(this InventoryCategory category)
        {
            return category.GetDisplayName();
        }

        public static string FormattedDetailedName(this InventoryCategory category)
        {
            return category.GetDetailedName();
        }

        public static string FormattedName(this InventoryType type)
        {
            return type.GetDisplayName();
        }

        public static string FormattedName(this InventoryCategory? category)
        {
            if (category.HasValue)
            {
                return FormattedName(category.Value);
            }

            return "未知";
        }
        

        public static string FormattedName(this InventoryChangeReason reason)
        {
            return reason.GetDisplayName();
        }
        
        public static string FormattedName(this CharacterType characterType)
        {
            switch (characterType)
            {
                case CharacterType.Character:
                    return "角色";
                case CharacterType.Housing:
                    return "房屋";
                case CharacterType.Retainer:
                    return "雇员";
                case CharacterType.FreeCompanyChest:
                    return "部队物品箱";
            }
            return "未知";
        }

        public static bool IsApplicable(this InventoryCategory inventoryCategory, CharacterType characterType)
        {
            switch (characterType)
            {
                case CharacterType.Character:
                    return IsCharacterCategory(inventoryCategory);
                case CharacterType.Retainer:
                    return IsRetainerCategory(inventoryCategory);
                case CharacterType.FreeCompanyChest:
                    return IsFreeCompanyCategory(inventoryCategory);
                case CharacterType.Housing:
                    return IsHousingCategory(inventoryCategory);
            }

            return true;
        }
    }
}