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

        public static List<InventoryType> GetTypes(this InventoryCategory category)
        {
            switch (category)
            {
                case InventoryCategory.CharacterBags:
                    return new List<InventoryType>()
                        {InventoryType.Bag0, InventoryType.Bag1, InventoryType.Bag2, InventoryType.Bag3};
                case InventoryCategory.RetainerBags:
                    return new List<InventoryType>()
                        {InventoryType.RetainerBag0, InventoryType.RetainerBag1, InventoryType.RetainerBag2, InventoryType.RetainerBag3, InventoryType.RetainerBag4, InventoryType.RetainerBag5, InventoryType.RetainerBag6};
                case InventoryCategory.Armoire:
                    return new List<InventoryType>()
                        {InventoryType.Armoire};
                case InventoryCategory.Crystals:
                    return new List<InventoryType>()
                        {InventoryType.Crystal,InventoryType.RetainerCrystal, InventoryType.FreeCompanyCrystal};
                case InventoryCategory.Currency:
                    return new List<InventoryType>()
                        {InventoryType.Currency,InventoryType.FreeCompanyGil, InventoryType.RetainerGil, InventoryType.FreeCompanyCurrency};
                case InventoryCategory.CharacterEquipped:
                    return new List<InventoryType>()
                        {InventoryType.GearSet0};
                case InventoryCategory.CharacterArmoryChest:
                    return new List<InventoryType>()
                        {InventoryType.ArmoryBody, InventoryType.ArmoryEar, InventoryType.ArmoryFeet, InventoryType.ArmoryHand, InventoryType.ArmoryHead, InventoryType.ArmoryLegs, InventoryType.ArmoryMain, InventoryType.ArmoryNeck, InventoryType.ArmoryOff, InventoryType.ArmoryRing, InventoryType.ArmorySoulCrystal, InventoryType.ArmoryWaist, InventoryType.ArmoryWrist};
                case InventoryCategory.GlamourChest:
                    return new List<InventoryType>()
                        {InventoryType.GlamourChest};
                case InventoryCategory.RetainerEquipped:
                    return new List<InventoryType>()
                        {InventoryType.RetainerEquippedGear};
                case InventoryCategory.RetainerMarket:
                    return new List<InventoryType>()
                        {InventoryType.RetainerMarket};
                case InventoryCategory.CharacterSaddleBags:
                    return new List<InventoryType>()
                        {InventoryType.SaddleBag0,InventoryType.SaddleBag1};
                case InventoryCategory.CharacterPremiumSaddleBags:
                    return new List<InventoryType>()
                        {InventoryType.PremiumSaddleBag0,InventoryType.PremiumSaddleBag1};
                case InventoryCategory.FreeCompanyBags:
                    return new List<InventoryType>()
                        {InventoryType.FreeCompanyBag0,InventoryType.FreeCompanyBag1,InventoryType.FreeCompanyBag2,InventoryType.FreeCompanyBag3,InventoryType.FreeCompanyBag4,InventoryType.FreeCompanyBag5,InventoryType.FreeCompanyBag6,InventoryType.FreeCompanyBag7,InventoryType.FreeCompanyBag8,InventoryType.FreeCompanyBag9,InventoryType.FreeCompanyBag10};
                case InventoryCategory.HousingInteriorItems:
                    return new List<InventoryType>()
                        {
                            InventoryType.HousingInteriorPlacedItems1, InventoryType.HousingInteriorPlacedItems2,
                            InventoryType.HousingInteriorPlacedItems3, InventoryType.HousingInteriorPlacedItems4,
                            InventoryType.HousingInteriorPlacedItems5, InventoryType.HousingInteriorPlacedItems6,
                            InventoryType.HousingInteriorPlacedItems7, InventoryType.HousingInteriorPlacedItems8,
                        };
                case InventoryCategory.HousingInteriorStoreroom:
                    return new List<InventoryType>()
                        {
                            InventoryType.HousingInteriorStoreroom1, InventoryType.HousingInteriorStoreroom2,
                            InventoryType.HousingInteriorStoreroom3, InventoryType.HousingInteriorStoreroom4,
                            InventoryType.HousingInteriorStoreroom5, InventoryType.HousingInteriorStoreroom6,
                            InventoryType.HousingInteriorStoreroom7, InventoryType.HousingInteriorStoreroom8,
                        };
                case InventoryCategory.HousingInteriorAppearance:
                    return new List<InventoryType>()
                        {
                            InventoryType.HousingInteriorAppearance
                        };
                case InventoryCategory.HousingExteriorStoreroom:
                    return new List<InventoryType>()
                        {
                            InventoryType.HousingExteriorStoreroom
                        };
                case InventoryCategory.HousingExteriorItems:
                    return new List<InventoryType>()
                        {
                            InventoryType.HousingExteriorPlacedItems
                        };
                case InventoryCategory.HousingExteriorAppearance:
                    return new List<InventoryType>()
                        {
                            InventoryType.HousingExteriorAppearance
                        };
            }

            return new List<InventoryType>();
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
            switch (reason)
            {
                case InventoryChangeReason.Added:
                    return "新增";
                case InventoryChangeReason.Removed:
                    return "移除";
                case InventoryChangeReason.Moved:
                    return "移动";
                case InventoryChangeReason.ConditionChanged:
                    return "状态变更";
                case InventoryChangeReason.FlagsChanged:
                    return "NQ/HQ变更";
                case InventoryChangeReason.GlamourChanged:
                    return "投影变更";
                case InventoryChangeReason.MateriaChanged:
                    return "魔晶石变更";
                case InventoryChangeReason.QuantityChanged:
                    return "数量变更";
                case InventoryChangeReason.SpiritbondChanged:
                    return "灵魂绑定变更";
                case InventoryChangeReason.StainChanged:
                    return "染色变更";
                case InventoryChangeReason.ItemIdChanged:
                    return "物品变更";
                case InventoryChangeReason.Transferred:
                    return "转移";
                case InventoryChangeReason.MarketPriceChanged:
                    return "市场价格变更";
                case InventoryChangeReason.GearsetsChanged:
                    return "装备套装变更";
            }
            return "未知";
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