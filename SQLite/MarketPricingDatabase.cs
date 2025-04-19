using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CriticalCommonLib.MarketBoard;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using static Dalamud.Game.Network.Structures.MarketBoardCurrentOfferings;

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
                Available INTEGER DEFAULT 0,

                UniversalisRecdPrice INTEGER DEFAULT NULL,
                UniversalisRecdNQPrice INTEGER DEFAULT NULL,
                UniversalisRecdHQPrice INTEGER DEFAULT NULL,

                MBRecdPrice INTEGER DEFAULT NULL,
                MBRecdNQPrice INTEGER DEFAULT NULL,
                MBRecdHQPrice INTEGER DEFAULT NULL,

                MBMaxHQPrice REAL DEFAULT 0,
                MBMinHQPrice REAL DEFAULT 0,
                MBAvgHQPrice REAL DEFAULT 0,
                MBMaxNQPrice REAL DEFAULT 0,
                MBMinNQPrice REAL DEFAULT 0,
                MBAvgNQPrice REAL DEFAULT 0,

                MBLastUpdate TEXT,
                UniversalisLastUpdate TEXT,

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
                Offerings TEXT,
                Listings TEXT,
                RecentHistory TEXT,
                StackSizeHistogram TEXT,
                StackSizeHistogramNQ TEXT,
                StackSizeHistogramHQ TEXT,
                -- 新增StackSize相关字段
                MBMostFrequentStackSize INTEGER DEFAULT NULL,
                MBMostFrequentStackSizeNQ INTEGER DEFAULT NULL,
                MBMostFrequentStackSizeHQ INTEGER DEFAULT NULL,
                -- 新增Universalis相关StackSize字段
                UniversalisMostFrequentStackSize INTEGER DEFAULT NULL,
                UniversalisMostFrequentStackSizeNQ INTEGER DEFAULT NULL,
                UniversalisMostFrequentStackSizeHQ INTEGER DEFAULT NULL
            );";
            command.ExecuteNonQuery();

            // 检查并添加新增字段
            AddColumnIfNotExists("MBMostFrequentStackSize", "INTEGER DEFAULT NULL");
            AddColumnIfNotExists("MBMostFrequentStackSizeNQ", "INTEGER DEFAULT NULL");
            AddColumnIfNotExists("MBMostFrequentStackSizeHQ", "INTEGER DEFAULT NULL");
            AddColumnIfNotExists("UniversalisMostFrequentStackSize", "INTEGER DEFAULT NULL");
            AddColumnIfNotExists("UniversalisMostFrequentStackSizeNQ", "INTEGER DEFAULT NULL");
            AddColumnIfNotExists("UniversalisMostFrequentStackSizeHQ", "INTEGER DEFAULT NULL");
        }

        private void AddColumnIfNotExists(string columnName, string columnDefinition)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = $"ALTER TABLE MarketPricing ADD COLUMN {columnName} {columnDefinition}";
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name"))
            {
                // 字段已存在，忽略错误
            }
        }

        /// <summary>
        /// 保存或更新市场价格数据
        /// </summary>
        private void SaveMarketPricing(MarketPricing pricing)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT OR REPLACE INTO MarketPricing (
                ItemId, WorldId, LastUploadTime, MBLastUpdate, Available,
                UniversalisRecdPrice, UniversalisRecdNQPrice, UniversalisRecdHQPrice,
                MBRecdPrice, MBRecdNQPrice, MBRecdHQPrice,
                MBMaxHQPrice, MBMinHQPrice, MBAvgHQPrice,
                MBMaxNQPrice, MBMinNQPrice, MBAvgNQPrice,
                UniversalisLastUpdate,
                CurrentAveragePrice, CurrentAveragePriceNQ, CurrentAveragePriceHQ,
                AveragePrice, AveragePriceNQ, AveragePriceHQ,
                MinPrice, MinPriceNQ, MinPriceHQ,
                MaxPrice, MaxPriceNQ, MaxPriceHQ,
                RegularSaleVelocity, NqSaleVelocity, HqSaleVelocity,
                WorldName, ListingsCount, RecentHistoryCount,
                UnitsForSale, UnitsSold, HasData,
                Offerings,
                Listings, RecentHistory,
                StackSizeHistogram, StackSizeHistogramNQ, StackSizeHistogramHQ,
                MBMostFrequentStackSize, MBMostFrequentStackSizeNQ, MBMostFrequentStackSizeHQ,
                UniversalisMostFrequentStackSize, UniversalisMostFrequentStackSizeNQ, UniversalisMostFrequentStackSizeHQ
            ) VALUES (
                @ItemId, @WorldId, @LastUploadTime, @MBLastUpdate, @Available,
                @UniversalisRecdPrice, @UniversalisRecdNQPrice, @UniversalisRecdHQPrice,
                @MBRecdPrice, @MBRecdNQPrice, @MBRecdHQPrice,
                @MBMaxHQPrice, @MBMinHQPrice, @MBAvgHQPrice,
                @MBMaxNQPrice, @MBMinNQPrice, @MBAvgNQPrice,
                @UniversalisLastUpdate,
                @CurrentAveragePrice, @CurrentAveragePriceNQ, @CurrentAveragePriceHQ,
                @AveragePrice, @AveragePriceNQ, @AveragePriceHQ,
                @MinPrice, @MinPriceNQ, @MinPriceHQ,
                @MaxPrice, @MaxPriceNQ, @MaxPriceHQ,
                @RegularSaleVelocity, @NqSaleVelocity, @HqSaleVelocity,
                @WorldName, @ListingsCount, @RecentHistoryCount,
                @UnitsForSale, @UnitsSold, @HasData,
                @Offerings,
                @Listings, @RecentHistory,
                @StackSizeHistogram, @StackSizeHistogramNQ, @StackSizeHistogramHQ,
                @MBMostFrequentStackSize, @MBMostFrequentStackSizeNQ, @MBMostFrequentStackSizeHQ,
                @UniversalisMostFrequentStackSize, @UniversalisMostFrequentStackSizeNQ, @UniversalisMostFrequentStackSizeHQ
            )";

            // 设置参数
            command.Parameters.AddWithValue("@ItemId", pricing.itemID);
            command.Parameters.AddWithValue("@WorldId", pricing.worldID);
            command.Parameters.AddWithValue("@LastUploadTime", pricing.lastUploadTime);

            command.Parameters.AddWithValue("@UniversalisLastUpdate", pricing.UniversalisLastUpdate.ToString("o"));

            command.Parameters.AddWithValue("@MBLastUpdate", pricing.MBLastUpdate.ToString("o"));

            command.Parameters.AddWithValue("@Available", pricing.Available);

            // UniversalisRecdPrice 参数
            command.Parameters.AddWithValue(
                "@UniversalisRecdPrice", 
                pricing.UniversalisRecdPrice.HasValue ? pricing.UniversalisRecdPrice.Value : DBNull.Value);
            command.Parameters.AddWithValue(
                "@UniversalisRecdNQPrice", 
                pricing.UniversalisRecdNQPrice.HasValue ? pricing.UniversalisRecdNQPrice.Value : DBNull.Value);
            command.Parameters.AddWithValue(
                "@UniversalisRecdHQPrice", 
                pricing.UniversalisRecdHQPrice.HasValue ? pricing.UniversalisRecdHQPrice.Value : DBNull.Value);
            
            // MBRecdPrice 参数
            command.Parameters.AddWithValue(
                "@MBRecdPrice", 
                pricing.MBRecdPrice.HasValue ? pricing.MBRecdPrice.Value : DBNull.Value);
            command.Parameters.AddWithValue(
                "@MBRecdNQPrice", 
                pricing.MBRecdNQPrice.HasValue ? pricing.MBRecdNQPrice.Value : DBNull.Value);
            command.Parameters.AddWithValue(
                "@MBRecdHQPrice", 
                pricing.MBRecdHQPrice.HasValue ? pricing.MBRecdHQPrice.Value : DBNull.Value);
            
            // 新增参数设置
            command.Parameters.AddWithValue("@MBMaxHQPrice", pricing.MBMaxHQPrice);
            command.Parameters.AddWithValue("@MBMinHQPrice", pricing.MBMinHQPrice);
            command.Parameters.AddWithValue("@MBAvgHQPrice", pricing.MBAvgHQPrice);
            command.Parameters.AddWithValue("@MBMaxNQPrice", pricing.MBMaxNQPrice);
            command.Parameters.AddWithValue("@MBMinNQPrice", pricing.MBMinNQPrice);
            command.Parameters.AddWithValue("@MBAvgNQPrice", pricing.MBAvgNQPrice);

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

            command.Parameters.AddWithValue("@Offerings", 
                pricing.offerings != null ? JsonConvert.SerializeObject(pricing.offerings) : "");

            command.Parameters.AddWithValue(
                "@MBMostFrequentStackSize", 
                pricing.MBMostFrequentStackSize.HasValue ? 
                    pricing.MBMostFrequentStackSize.Value : 
                    DBNull.Value);
            command.Parameters.AddWithValue(
                "@MBMostFrequentStackSizeNQ", 
                pricing.MBMostFrequentStackSizeNQ.HasValue ? 
                    pricing.MBMostFrequentStackSizeNQ.Value : 
                    DBNull.Value);
            command.Parameters.AddWithValue(
                "@MBMostFrequentStackSizeHQ", 
                pricing.MBMostFrequentStackSizeHQ.HasValue ? 
                    pricing.MBMostFrequentStackSizeHQ.Value : 
                    DBNull.Value);

            command.Parameters.AddWithValue(
                "@UniversalisMostFrequentStackSize", 
                pricing.UniversalisMostFrequentStackSize.HasValue ? 
                    pricing.UniversalisMostFrequentStackSize.Value : 
                    DBNull.Value);
            command.Parameters.AddWithValue(
                "@UniversalisMostFrequentStackSizeNQ", 
                pricing.UniversalisMostFrequentStackSizeNQ.HasValue ? 
                    pricing.UniversalisMostFrequentStackSizeNQ.Value : 
                    DBNull.Value);
            command.Parameters.AddWithValue(
                "@UniversalisMostFrequentStackSizeHQ", 
                pricing.UniversalisMostFrequentStackSizeHQ.HasValue ? 
                    pricing.UniversalisMostFrequentStackSizeHQ.Value : 
                    DBNull.Value);

            command.ExecuteNonQuery();
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
                    MBLastUpdate = DateTime.Parse(reader.GetString(reader.GetOrdinal("MBLastUpdate"))),
                    UniversalisLastUpdate = DateTime.Parse(reader.GetString(reader.GetOrdinal("UniversalisLastUpdate"))),
                    Available = reader.GetInt32(reader.GetOrdinal("Available")),

                    // UniversalisRecdPrice 字段
                    UniversalisRecdPrice = reader.IsDBNull(reader.GetOrdinal("UniversalisRecdPrice")) ? null : (uint?)reader.GetInt32(reader.GetOrdinal("UniversalisRecdPrice")),
                    UniversalisRecdNQPrice = reader.IsDBNull(reader.GetOrdinal("UniversalisRecdNQPrice")) ? null : (uint?)reader.GetInt32(reader.GetOrdinal("UniversalisRecdNQPrice")),
                    UniversalisRecdHQPrice = reader.IsDBNull(reader.GetOrdinal("UniversalisRecdHQPrice")) ? null : (uint?)reader.GetInt32(reader.GetOrdinal("UniversalisRecdHQPrice")),
                    
                    // MBRecdPrice 字段
                    MBRecdPrice = reader.IsDBNull(reader.GetOrdinal("MBRecdPrice")) ? null : (uint?)reader.GetInt32(reader.GetOrdinal("MBRecdPrice")),
                    MBRecdNQPrice = reader.IsDBNull(reader.GetOrdinal("MBRecdNQPrice")) ? null : (uint?)reader.GetInt32(reader.GetOrdinal("MBRecdNQPrice")),
                    MBRecdHQPrice = reader.IsDBNull(reader.GetOrdinal("MBRecdHQPrice")) ? null : (uint?)reader.GetInt32(reader.GetOrdinal("MBRecdHQPrice")),
                    
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

                var offeringsStr = reader.GetString(reader.GetOrdinal("Offerings"));
                if (!string.IsNullOrEmpty(offeringsStr))
                {
                    var items = JsonConvert.DeserializeObject<List<MarketBoardItemListing>>(offeringsStr);
                    pricing.offerings = items?.Cast<IMarketBoardItemListing>().ToList() ?? new List<IMarketBoardItemListing>();
                }
                else
                {
                    pricing.offerings = new List<IMarketBoardItemListing>();
                }

                // 新增StackSize字段读取 - 使用TryGetOrdinal来兼容旧版本数据库
                pricing.MBMostFrequentStackSize = TryGetNullableUInt(reader, "MBMostFrequentStackSize");
                pricing.MBMostFrequentStackSizeNQ = TryGetNullableUInt(reader, "MBMostFrequentStackSizeNQ");
                pricing.MBMostFrequentStackSizeHQ = TryGetNullableUInt(reader, "MBMostFrequentStackSizeHQ");

                // 新增Universalis相关StackSize字段
                pricing.UniversalisMostFrequentStackSize = TryGetNullableUInt(reader, "UniversalisMostFrequentStackSize");
                pricing.UniversalisMostFrequentStackSizeNQ = TryGetNullableUInt(reader, "UniversalisMostFrequentStackSizeNQ");
                pricing.UniversalisMostFrequentStackSizeHQ = TryGetNullableUInt(reader, "UniversalisMostFrequentStackSizeHQ");


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
        /// 尝试获取可为空的uint字段值，如果字段不存在则返回null
        /// </summary>
        private uint? TryGetNullableUInt(SqliteDataReader reader, string columnName)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? null : (uint?)reader.GetInt32(ordinal);
            }
            catch (IndexOutOfRangeException)
            {
                // 字段不存在于旧版本数据库中
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