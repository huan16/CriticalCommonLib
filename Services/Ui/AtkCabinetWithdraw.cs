using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AllaganLib.GameSheets.Sheets;
using AllaganLib.GameSheets.Sheets.Rows;
using CriticalCommonLib.Agents;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;


namespace CriticalCommonLib.Services.Ui
{
    using System.Runtime.InteropServices;
    using Addons;

    public class AtkCabinetWithdraw : AtkOverlay
    {
        private Dictionary<byte, CabinetCategoryRow>? _cabinetCategories;
        private readonly CabinetCategorySheet _cabinetCategorySheet;
        public override WindowName WindowName { get; set; } = WindowName.CabinetWithdraw;
        private uint RadioButtonOffset = 12;
        private uint ListComponentNodeId = 30;

        public AtkCabinetWithdraw(CabinetCategorySheet cabinetCategorySheet, IGameGui gameGui) : base(gameGui)
        {
            _cabinetCategorySheet = cabinetCategorySheet;
        }

        private Dictionary<byte, CabinetCategoryRow> CabinetCategories
        {
            get
            {
                return _cabinetCategories ??= _cabinetCategorySheet.DistinctBy(c => c.Base.MenuOrder)
                    .ToDictionary(c => c.Base.MenuOrder, c => c);
            }
        }

        public unsafe byte SelectedTab
        {
            get
            {
                if (AtkUnitBase != null)
                {
                    var cabinetWithdrawAddon = (AddonCabinetWithdraw*)this.AtkUnitBase.AtkUnitBase;
                    if (cabinetWithdrawAddon->ArtifactArmorRadioButton != null && cabinetWithdrawAddon->ArtifactArmorRadioButton->IsSelected)
                    {
                        return 0;
                    }

                    if (cabinetWithdrawAddon->SeasonalGear1RadioButton != null && cabinetWithdrawAddon->SeasonalGear1RadioButton->IsSelected)
                    {
                        return 1;
                    }

                    if (cabinetWithdrawAddon->SeasonalGear2RadioButton != null && cabinetWithdrawAddon->SeasonalGear2RadioButton->IsSelected)
                    {
                        return 2;
                    }

                    if (cabinetWithdrawAddon->SeasonalGear3RadioButton != null && cabinetWithdrawAddon->SeasonalGear3RadioButton->IsSelected)
                    {
                        return 3;
                    }

                    if (cabinetWithdrawAddon->SeasonalGear4RadioButton != null && cabinetWithdrawAddon->SeasonalGear4RadioButton->IsSelected)
                    {
                        return 4;
                    }

                    if (cabinetWithdrawAddon->SeasonalGear5RadioButton != null && cabinetWithdrawAddon->SeasonalGear5RadioButton->IsSelected)
                    {
                        return 5;
                    }

                    if (cabinetWithdrawAddon->AchievementsRadioButton != null && cabinetWithdrawAddon->AchievementsRadioButton->IsSelected)
                    {
                        return 6;
                    }

                    if (cabinetWithdrawAddon->ExclusiveExtrasRadioButton != null && cabinetWithdrawAddon->ExclusiveExtrasRadioButton->IsSelected)
                    {
                        return 7;
                    }

                    if (cabinetWithdrawAddon->SearchRadioButton != null && cabinetWithdrawAddon->SearchRadioButton->IsSelected)
                    {
                        return 8;
                    }

                }
                return 0;
            }
        }

        public unsafe CabinetCategoryRow? CurrentTab
        {
            get
            {
                if (AtkUnitBase != null)
                {
                    var cabinetWithdrawAddon = (AddonCabinetWithdraw*)this.AtkUnitBase.AtkUnitBase;

                    return CabinetCategories.GetValueOrDefault(SelectedTab);
                }
                return null;
            }
        }

        private CabinetCategoryRow? _storedTab;

        public override void Update()
        {
            var currentTab = CurrentTab;
            if (currentTab != null && currentTab != _storedTab)
            {
                _storedTab = currentTab;
                SendUpdatedEvent();
            }
        }

        public unsafe void SetColours(Dictionary<string, Vector4?> colours)
        {
            var atkBaseWrapper = AtkUnitBase;
            if (atkBaseWrapper == null) return;
            var listComponentNode = (AtkComponentNode*) atkBaseWrapper.AtkUnitBase->GetNodeById(ListComponentNodeId);
            if (listComponentNode == null || (ushort) listComponentNode->AtkResNode.Type < 1000) return;
            var component = (AtkComponentTreeList*) listComponentNode->Component;
            var list = component->Items;
            foreach(var listItem in list.AsSpan())
            {
                var uldManager = listItem.Value->Renderer->AtkComponentButton.AtkComponentBase.UldManager;
                if (uldManager.NodeListCount < 4) continue;
                var atkResNode = uldManager.NodeList[3];
                var textNode = (AtkTextNode*) atkResNode;

                if (textNode == null) {
                    continue;
                }

                if (textNode->NodeText.IsEmpty) continue;
                var seString = textNode->NodeText.StringPtr.AsDalamudSeString();

                var priceString = string.Join(
                    " ",
                    seString.Payloads.OfType<TextPayload>().Select(c => c.Text ?? string.Empty));
                if(priceString.Length == 0) continue;
                textNode->SetText(priceString);
                if (colours.ContainsKey(priceString))
                {
                    var newColour = colours[priceString];
                    if (newColour.HasValue)
                    {
                        textNode->TextColor = Utils.ColorFromVector4(newColour.Value);
                    }
                    else
                    {
                        textNode->TextColor =  Utils.ColorFromVector4(ImGuiColors.DalamudWhite);
                    }
                }
                else
                {
                    textNode->TextColor =  Utils.ColorFromVector4(ImGuiColors.DalamudWhite);
                }
            }
        }

        public unsafe void SetTabColors(Dictionary<uint, Vector4?> indexedTabColours)
        {
            var atkBaseWrapper = AtkUnitBase;
            if (atkBaseWrapper == null) return;

            foreach (var colour in indexedTabColours)
            {
                Vector4? newColour = colour.Value;
                var tab = colour.Key;
                var nodeId = (uint) (tab + RadioButtonOffset);
                var radioButton = (AtkComponentNode*) atkBaseWrapper.AtkUnitBase->GetNodeById(nodeId);
                if (radioButton == null || (ushort) radioButton->AtkResNode.Type < 1000) return;
                var atkResNode = (AtkResNode*) radioButton;
                if (newColour.HasValue)
                {
                    atkResNode->Color.A = (byte) (newColour.Value.W * 255.0f);
                    atkResNode->AddBlue = (short) (newColour.Value.Z * 255.0f);
                    atkResNode->AddRed = (short) (newColour.Value.X * 255.0f);
                    atkResNode->AddGreen = (short) (newColour.Value.Y * 255.0f);
                }
                else
                {
                    atkResNode->Color.A = 255;
                    atkResNode->AddBlue = 0;
                    atkResNode->AddRed = 0;
                    atkResNode->AddGreen = 0;
                }
            }
        }
    }
}