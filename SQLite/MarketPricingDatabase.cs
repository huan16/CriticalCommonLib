using System;
using System.Collections.Generic;
using System.IO;
using CriticalCommonLib.MarketBoard;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace CriticalCommonLib.SQLite
{
    public class MarketPricingDatabase : IDisposable
    {
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly IPluginLog pluginLog;
        private readonly SqliteConnection connection;
        private bool disposed = false;

        private string DatabasePath
        {
            get
            {
                var pluginRootDirectory = Path.GetDirectoryName(pluginInterface.ConfigDirectory.FullName) ?? string.Empty;

                var sharedDir = Path.Combine(pluginRootDirectory, "AllaganMarket");

                return Path.Combine(sharedDir, "MarketPricingDatabase.db");
            }
        }

        public MarketPricingDatabase(
            IDalamudPluginInterface pluginInterface,
            IPluginLog pluginLog)
        {
            // 初始化SQLite
            SQLitePCL.Batteries.Init();
            this.pluginInterface = pluginInterface;
            this.pluginLog = pluginLog;

            // 创建数据库连接
            connection = new SqliteConnection($"Data Source={DatabasePath}");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS MarketPricing (
                ItemId INTEGER PRIMARY KEY,
                WorldId INTEGER,
                LastUploadTime INTEGER,
                LastSellDate TEXT,
                LastUpdate TEXT NOT NULL,
                Available INTEGER DEFAULT 0,
                UniversalisRecomendationPrice INTEGER DEFAULT 0,
                MarketBoardRecomendationPrice INTEGER DEFAULT 0,
                
                -- UniversalisApiResponse 基础字段
                CurrentAveragePrice REAL DEFAULT 0,
                CurrentAveragePriceNQ REAL DEFAULT 0,
                CurrentAveragePriceHQ REAL DEFAULT 0,
                AveragePrice REAL DEFAULT 0,
                AveragePriceNQ REAL DEFAULT 0,
                AveragePriceHQ REAL DEFAULT 0,
                MinPrice REAL DEFAULT 0,
                MinPriceNQ REAL DEFAULT 0,
                MinPriceHQ REAL DEFAULT 0,
                MaxPrice REAL DEFAULT 0,
                MaxPriceNQ REAL DEFAULT 0,
                MaxPriceHQ REAL DEFAULT 0,
                RegularSaleVelocity REAL DEFAULT 0,
                NqSaleVelocity REAL DEFAULT 0,
                HqSaleVelocity REAL DEFAULT 0,
                WorldName TEXT,
                ListingsCount INTEGER DEFAULT 0,
                RecentHistoryCount INTEGER DEFAULT 0,
                UnitsForSale INTEGER DEFAULT 0,
                UnitsSold INTEGER DEFAULT 0,
                HasData INTEGER DEFAULT 0,
                
                -- 序列化字段
                Listings TEXT,
                RecentHistory TEXT,
                StackSizeHistogram TEXT,
                StackSizeHistogramNQ TEXT,
                StackSizeHistogramHQ TEXT
            );";
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 保存或更新市场价格数据
        /// </summary>
        public void SaveMarketPricing(MarketPricing pricing)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT OR REPLACE INTO MarketPricing (
                    ItemId, WorldId, LastUploadTime, LastSellDate, LastUpdate, Available,
                    UniversalisRecomendationPrice, MarketBoardRecomendationPrice,
                    CurrentAveragePrice, CurrentAveragePriceNQ, CurrentAveragePriceHQ,
                    AveragePrice, AveragePriceNQ, AveragePriceHQ,
                    MinPrice, MinPriceNQ, MinPriceHQ,
                    MaxPrice, MaxPriceNQ, MaxPriceHQ,
                    RegularSaleVelocity, NqSaleVelocity, HqSaleVelocity,
                    WorldName, ListingsCount, RecentHistoryCount,
                    UnitsForSale, UnitsSold, HasData,
                    Listings, RecentHistory,
                    StackSizeHistogram, StackSizeHistogramNQ, StackSizeHistogramHQ
                ) VALUES (
                    @ItemId, @WorldId, @LastUploadTime, @LastSellDate, @LastUpdate, @Available,
                    @UniversalisRecomendationPrice, @MarketBoardRecomendationPrice,
                    @CurrentAveragePrice, @CurrentAveragePriceNQ, @CurrentAveragePriceHQ,
                    @AveragePrice, @AveragePriceNQ, @AveragePriceHQ,
                    @MinPrice, @MinPriceNQ, @MinPriceHQ,
                    @MaxPrice, @MaxPriceNQ, @MaxPriceHQ,
                    @RegularSaleVelocity, @NqSaleVelocity, @HqSaleVelocity,
                    @WorldName, @ListingsCount, @RecentHistoryCount,
                    @UnitsForSale, @UnitsSold, @HasData,
                    @Listings, @RecentHistory,
                    @StackSizeHistogram, @StackSizeHistogramNQ, @StackSizeHistogramHQ
                )";

                // 设置参数
                command.Parameters.AddWithValue("@ItemId", pricing.itemID);
                command.Parameters.AddWithValue("@WorldId", pricing.worldID);
                command.Parameters.AddWithValue("@LastUploadTime", pricing.lastUploadTime);
                command.Parameters.AddWithValue("@LastSellDate", pricing.LastSellDate?.ToString("o"));
                command.Parameters.AddWithValue("@LastUpdate", pricing.LastUpdate.ToString("o"));
                command.Parameters.AddWithValue("@Available", pricing.Available);
                command.Parameters.AddWithValue("@UniversalisRecomendationPrice", pricing.UniversalisRecomendationPrice);
                command.Parameters.AddWithValue("@MarketBoardRecomendationPrice", pricing.MarketBoardRecomendationPrice);

                // UniversalisApiResponse 参数
                command.Parameters.AddWithValue("@CurrentAveragePrice", pricing.currentAveragePrice);
                command.Parameters.AddWithValue("@CurrentAveragePriceNQ", pricing.currentAveragePriceNQ);
                command.Parameters.AddWithValue("@CurrentAveragePriceHQ", pricing.currentAveragePriceHQ);
                command.Parameters.AddWithValue("@AveragePrice", pricing.averagePrice);
                command.Parameters.AddWithValue("@AveragePriceNQ", pricing.averagePriceNQ);
                command.Parameters.AddWithValue("@AveragePriceHQ", pricing.averagePriceHQ);
                command.Parameters.AddWithValue("@MinPrice", pricing.minPrice);
                command.Parameters.AddWithValue("@MinPriceNQ", pricing.minPriceNQ);
                command.Parameters.AddWithValue("@MinPriceHQ", pricing.minPriceHQ);
                command.Parameters.AddWithValue("@MaxPrice", pricing.maxPrice);
                command.Parameters.AddWithValue("@MaxPriceNQ", pricing.maxPriceNQ);
                command.Parameters.AddWithValue("@MaxPriceHQ", pricing.maxPriceHQ);
                command.Parameters.AddWithValue("@RegularSaleVelocity", pricing.regularSaleVelocity);
                command.Parameters.AddWithValue("@NqSaleVelocity", pricing.nqSaleVelocity);
                command.Parameters.AddWithValue("@HqSaleVelocity", pricing.hqSaleVelocity);
                command.Parameters.AddWithValue("@WorldName", pricing.worldName ?? "");
                command.Parameters.AddWithValue("@ListingsCount", pricing.listingsCount);
                command.Parameters.AddWithValue("@RecentHistoryCount", pricing.recentHistoryCount);
                command.Parameters.AddWithValue("@UnitsForSale", pricing.unitsForSale);
                command.Parameters.AddWithValue("@UnitsSold", pricing.unitsSold);
                command.Parameters.AddWithValue("@HasData", pricing.hasData ? 1 : 0);

                // 序列化复杂对象
                command.Parameters.AddWithValue("@Listings", pricing.listings != null ? JsonConvert.SerializeObject(pricing.listings) : "");
                command.Parameters.AddWithValue("@RecentHistory", pricing.recentHistory != null ? JsonConvert.SerializeObject(pricing.recentHistory) : "");
                command.Parameters.AddWithValue("@StackSizeHistogram", pricing.stackSizeHistogram != null ? JsonConvert.SerializeObject(pricing.stackSizeHistogram) : "");
                command.Parameters.AddWithValue("@StackSizeHistogramNQ", pricing.stackSizeHistogramNQ != null ? JsonConvert.SerializeObject(pricing.stackSizeHistogramNQ) : "");
                command.Parameters.AddWithValue("@StackSizeHistogramHQ", pricing.stackSizeHistogramHQ != null ? JsonConvert.SerializeObject(pricing.stackSizeHistogramHQ) : "");

                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 批量保存市场价格数据
        /// </summary>
        public void SaveMarketPricings(IEnumerable<MarketPricing> pricings)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var pricing in pricings)
                {
                    SaveMarketPricing(pricing);
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 根据物品ID获取市场价格数据
        /// </summary>
        public MarketPricing? GetMarketPricing(uint itemId)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM MarketPricing WHERE ItemId = @ItemId";
            command.Parameters.AddWithValue("@ItemId", itemId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadMarketPricingFromReader(reader);
            }

            return null;
        }

        /// <summary>
        /// 获取所有市场价格数据
        /// </summary>
        public List<MarketPricing> GetAllMarketPricings()
        {
            var pricings = new List<MarketPricing>();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM MarketPricing";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var pricing = ReadMarketPricingFromReader(reader);
                if (pricing != null)
                {
                    pricings.Add(pricing);
                }
            }

            return pricings;
        }

        /// <summary>
        /// 从数据库读取器创建 MarketPricing 对象
        /// </summary>
        private MarketPricing? ReadMarketPricingFromReader(SqliteDataReader reader)
        {
            try
            {
                var pricing = new MarketPricing
                {
                    itemID = (uint)reader.GetInt32(reader.GetOrdinal("ItemId")),
                    worldID = (uint)reader.GetInt32(reader.GetOrdinal("WorldId")),
                    lastUploadTime = reader.GetInt64(reader.GetOrdinal("LastUploadTime")),
                    LastUpdate = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdate"))),
                    Available = reader.GetInt32(reader.GetOrdinal("Available")),
                    UniversalisRecomendationPrice = (uint)reader.GetInt32(reader.GetOrdinal("UniversalisRecomendationPrice")),
                    MarketBoardRecomendationPrice = (uint)reader.GetInt32(reader.GetOrdinal("MarketBoardRecomendationPrice")),
                    
                    // UniversalisApiResponse 字段
                    currentAveragePrice = (float)reader.GetDouble(reader.GetOrdinal("CurrentAveragePrice")),
                    currentAveragePriceNQ = (float)reader.GetDouble(reader.GetOrdinal("CurrentAveragePriceNQ")),
                    currentAveragePriceHQ = (float)reader.GetDouble(reader.GetOrdinal("CurrentAveragePriceHQ")),
                    averagePrice = (float)reader.GetDouble(reader.GetOrdinal("AveragePrice")),
                    averagePriceNQ = (float)reader.GetDouble(reader.GetOrdinal("AveragePriceNQ")),
                    averagePriceHQ = (float)reader.GetDouble(reader.GetOrdinal("AveragePriceHQ")),
                    minPrice = (float)reader.GetDouble(reader.GetOrdinal("MinPrice")),
                    minPriceNQ = (float)reader.GetDouble(reader.GetOrdinal("MinPriceNQ")),
                    minPriceHQ = (float)reader.GetDouble(reader.GetOrdinal("MinPriceHQ")),
                    maxPrice = (float)reader.GetDouble(reader.GetOrdinal("MaxPrice")),
                    maxPriceNQ = (float)reader.GetDouble(reader.GetOrdinal("MaxPriceNQ")),
                    maxPriceHQ = (float)reader.GetDouble(reader.GetOrdinal("MaxPriceHQ")),
                    regularSaleVelocity = (float)reader.GetDouble(reader.GetOrdinal("RegularSaleVelocity")),
                    nqSaleVelocity = (float)reader.GetDouble(reader.GetOrdinal("NqSaleVelocity")),
                    hqSaleVelocity = (float)reader.GetDouble(reader.GetOrdinal("HqSaleVelocity")),
                    worldName = reader.GetString(reader.GetOrdinal("WorldName")),
                    listingsCount = reader.GetInt32(reader.GetOrdinal("ListingsCount")),
                    recentHistoryCount = reader.GetInt32(reader.GetOrdinal("RecentHistoryCount")),
                    unitsForSale = reader.GetInt32(reader.GetOrdinal("UnitsForSale")),
                    unitsSold = reader.GetInt32(reader.GetOrdinal("UnitsSold")),
                    hasData = reader.GetInt32(reader.GetOrdinal("HasData")) == 1
                };

                // 处理可空的 LastSellDate
                var lastSellDateOrdinal = reader.GetOrdinal("LastSellDate");
                if (!reader.IsDBNull(lastSellDateOrdinal))
                {
                    pricing.LastSellDate = DateTime.Parse(reader.GetString(lastSellDateOrdinal));
                }

                // 反序列化复杂对象
                var listingsStr = reader.GetString(reader.GetOrdinal("Listings"));
                if (!string.IsNullOrEmpty(listingsStr))
                {
                    pricing.listings = JsonConvert.DeserializeObject<Listing[]>(listingsStr);
                }

                var recentHistoryStr = reader.GetString(reader.GetOrdinal("RecentHistory"));
                if (!string.IsNullOrEmpty(recentHistoryStr))
                {
                    pricing.recentHistory = JsonConvert.DeserializeObject<RecentHistory[]>(recentHistoryStr);
                }

                var stackSizeHistogramStr = reader.GetString(reader.GetOrdinal("StackSizeHistogram"));
                if (!string.IsNullOrEmpty(stackSizeHistogramStr))
                {
                    pricing.stackSizeHistogram = JsonConvert.DeserializeObject<Dictionary<string, int>>(stackSizeHistogramStr);
                }

                var stackSizeHistogramNQStr = reader.GetString(reader.GetOrdinal("StackSizeHistogramNQ"));
                if (!string.IsNullOrEmpty(stackSizeHistogramNQStr))
                {
                    pricing.stackSizeHistogramNQ = JsonConvert.DeserializeObject<Dictionary<string, int>>(stackSizeHistogramNQStr);
                }

                var stackSizeHistogramHQStr = reader.GetString(reader.GetOrdinal("StackSizeHistogramHQ"));
                if (!string.IsNullOrEmpty(stackSizeHistogramHQStr))
                {
                    pricing.stackSizeHistogramHQ = JsonConvert.DeserializeObject<Dictionary<string, int>>(stackSizeHistogramHQStr);
                }

                return pricing;
            }
            catch (Exception ex)
            {
                // 记录错误并返回null
                pluginLog.Error($"Error reading MarketPricing from database: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 删除指定物品ID的市场价格数据
        /// </summary>
        public void DeleteMarketPricing(uint itemId)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM MarketPricing WHERE ItemId = @ItemId";
            command.Parameters.AddWithValue("@ItemId", itemId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 清空所有市场价格数据
        /// </summary>
        public void ClearAllMarketPricings()
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM MarketPricing";
            command.ExecuteNonQuery();
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
                // 释放托管资源
                connection?.Close();
                connection?.Dispose();
            }

            disposed = true;
        }
    }
}