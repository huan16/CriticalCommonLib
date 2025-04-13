using System;
using System.Collections.Generic;
using System.Globalization;
using AllaganLib.GameSheets.Sheets.Rows;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;

namespace CriticalCommonLib.MarketBoard;

public class MarketPricing : UniversalisApiResponse
{
    public DateTime? LastSellDate { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.Now;
    public int Available { get; set; } = 0;
    // 从UniversalisApiResponse推荐的价格
    public uint UniversalisRecomendationPrice { get; set; } = 0;
    // 从交易板推荐的价格
    public uint MarketBoardRecomendationPrice { get; set; } = 0;
    // 创建空的市场价格对象
    public MarketPricing() : base()
    {
    }

    // 基于 UniversalisApiResponse 创建市场价格对象
    public MarketPricing(UniversalisApiResponse source) : base(source)
    {
        Available = listings?.Length ?? 0;
    }
}